using System.Collections.Specialized;
using System.ComponentModel.Design.Serialization;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using OpenKNXproducer;
using static OpenKNXproducer.ProcessInclude;
using StringDict = System.Collections.Generic.Dictionary<string, string>;

class TemplateApplication
{
    readonly XmlDocument mDocument = new();
    private XmlNamespaceManager nsmgr;

    public string Generate(ProcessInclude iInclude, XmlNode iBase)
    {
        GetTemplateDocument(iBase.NodeAttr("base"), iInclude);
        nsmgr = new XmlNamespaceManager(mDocument.NameTable);
        nsmgr.AddNamespace("op", ProcessInclude.cOwnNamespace);
        AddIncludes(iInclude);
        // AddVersionAttribute(iInclude);
        AddDynamicPart(iInclude);
        // mDocument.Save("TemplateApplication.generated.xml");
        return mDocument.OuterXml;
    }

    private void GetTemplateDocument(string iFileName, ProcessInclude iInclude)
    {
        string lXml;
        string lFileName = Path.Combine(iInclude.CurrentDir, iFileName);
        if (File.Exists(lFileName))
        {
            Console.WriteLine($"Generate: Using template file {lFileName}");
            lXml = File.ReadAllText(lFileName, Encoding.UTF8);
        }
        else
        {
            Console.WriteLine("Generate: Using default generation procedure");
            var assembly = Assembly.GetEntryAssembly();
            var resourceStream = assembly.GetManifestResourceStream("OpenKNXproducer.xml.TemplateApplication.xml");
            using var reader = new StreamReader(resourceStream, Encoding.UTF8);
            lXml = reader.ReadToEnd();
        }
        lXml = ProcessInclude.ReplaceXmlns(lXml);
        // we have to ensure that xmlns is the same in both documents
        string lCurrentEtsVersion = iInclude.SelectNodes("//KNX")[0].NodeAttr("oldxmlns")[^2..];
        lXml = lXml.Replace(@"%etsversion%", lCurrentEtsVersion);
        lXml = ReplaceEtsTag(lXml, iInclude);
        mDocument.LoadXml(lXml);
    }

    public static int ParseNumberValue(string iName, string iValue)
    {
        int lResult = -1;
        bool lError = false;
        if (iValue.Contains('.'))
        {
            string[] lValues = iValue.Split('.');
            int lLow = 0;
            int lHigh = 0;
            if (lValues.Length == 2 && int.TryParse(lValues[0], out lHigh))
                if (int.TryParse(lValues[1], out lLow))
                    lResult = lHigh * 16 + lLow;
            lError = lResult < 0 || lResult > 255 || lLow < 0 || lLow > 15 || lHigh < 0 || lHigh > 15;
        }
        else if (!iValue.StartsWith("0x") || !int.TryParse(iValue.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture, out lResult))
            if (!int.TryParse(iValue, out lResult))
                lError = true;
        if (lError)
            Program.Message(true, "ETS Attribute {0} contains {1}, this is not a valid version number", iName, iValue);
        return lResult;
    }

    private static StringDict GetEtsParams(XmlNode iEtsNode)
    {
        StringDict lResult = new();
        foreach (XmlAttribute lAttribute in iEtsNode.Attributes)
        {
            string lValue;
            // there might be a config param overriding this attribute
            string lConfigName = $"%{lAttribute.Name}%";
            if (Config.ContainsKey(lConfigName))
            {
                ConfigEntry lEntry = Config[lConfigName];
                lValue = lEntry.ConfigValue;
                lEntry.WasReplaced = true;
            }
            else
                lValue = lAttribute.Value;
            // some Attributes might be hex numbers, we parse them
            if ("OpenKnxId,ApplicationNumber,ApplicationVersion,ApplicationRevision".Contains(lAttribute.Name))
            {
                int lNumberValue = ParseNumberValue(lAttribute.Name, lValue);
                if (lAttribute.Name == "OpenKnxId")
                    lValue = $"0x{lNumberValue:X02}";
                else
                    lValue = lNumberValue.ToString();
            }
            else if (lAttribute.Name == "ReplacesVersions")
            {
                string[] lVersions = lValue.Split(' ');
                for (int lPos = 0; lPos < lVersions.Length; lPos++)
                    lVersions[lPos] = ParseNumberValue("ReplacesVersions", lVersions[lPos]).ToString();
                lValue = string.Join(' ', lVersions);
            }
            // TODO: we have to check, if these values should also go to config (additional replacement option)?
            lResult[lConfigName] = lValue;
        }
        return lResult;
    }

    private static void InitParam(StringDict iParams, string iName, bool iIsRequired, string iDefaultName = "", string iDefaultValue = "[missing]")
    {
        string lParamName = $"%{iName}%";
        if (!iParams.ContainsKey(lParamName))
        {
            if (iIsRequired)
                Program.Message(true, "Required ETS-Attribute {0} is missing", iName);
            if (iDefaultName == "")
                iParams[lParamName] = iDefaultValue;
            else
                iParams[lParamName] = iParams[$"%{iDefaultName}%"];
        }
    }

    private static string ReplaceEtsTag(string iXml, ProcessInclude iInclude)
    {
        // find catalog attribute
        XmlNodeList lEtsList = iInclude.SelectNodes("//oknxp:ETS");
        // update parameters
        if (lEtsList.Count > 0)
        {
            StringDict lParams = GetEtsParams(lEtsList[0]);
            // set defaults
            InitParam(lParams, "OpenKnxId", true);
            InitParam(lParams, "ApplicationNumber", true);
            InitParam(lParams, "ApplicationVersion", true);
            InitParam(lParams, "ReplacesVersions", true);
            InitParam(lParams, "ApplicationRevision", true);
            InitParam(lParams, "ProductName", true);
            InitParam(lParams, "CatalogName", false, "ProductName");
            InitParam(lParams, "HardwareName", false, "ProductName");
            InitParam(lParams, "ApplicationName", false, "ProductName");
            InitParam(lParams, "BuildSuffix", false, "", "");
            InitParam(lParams, "BuildSuffixText", false, "BuildSuffix");
            InitParam(lParams, "BusCurrent", false, "", "10");
            InitParam(lParams, "IsRailMounted", false, "", "false");
            InitParam(lParams, "IsIPEnabled", false, "", "false");
            int.TryParse(lParams["%ApplicationNumber%"], out int lApplicationNumberInt);
            string lOpenKnxId = lParams["%OpenKnxId%"].Replace("0x", "");
            InitParam(lParams, "SerialNumber", false, "", string.Format("0x{0}{1:X02}", lOpenKnxId, lApplicationNumberInt));
            InitParam(lParams, "OrderNumber", false, "SerialNumber");
            InitParam(lParams, "BaggagesRootDir", false, "", string.Format("{0}/{1:X02}", lOpenKnxId, lApplicationNumberInt));
            iInclude.BaggagesBaseDir = lParams["%BaggagesRootDir%"].Replace('/', '\\');
            // finally its a simple string replace
            foreach (var lParam in lParams)
                iXml = iXml.Replace(lParam.Key, lParam.Value);
        }
        return iXml;
    }

    private void AddIncludes(ProcessInclude iInclude)
    {
        XmlNodeList lTemplates = mDocument.SelectNodes("//op:includetemplate", nsmgr);
        foreach (var lDefine in DefineContent.Defines())
        {
            foreach (XmlNode lTemplate in lTemplates)
            {
                bool lAddElement = true;
                XmlElement lElement = mDocument.CreateElement("op:include", ProcessInclude.cOwnNamespace);
                foreach (XmlAttribute lAttribute in lTemplate.Attributes)
                {
                    string lValue = lAttribute.Value;
                    if (lValue == "%prefix%") lValue = lDefine.Value.prefix;
                    if (lValue == "%share%") lValue = lDefine.Value.share;
                    if (lValue == "%templ%") lValue = lDefine.Value.template;
                    if (lValue == "") lAddElement = false;
                    XmlAttribute lAttributeCopy = mDocument.CreateAttribute(lAttribute.Name);
                    lAttributeCopy.Value = lValue;
                    lElement.Attributes.Append(lAttributeCopy);
                }
                if (lAddElement)
                    lTemplate.ParentNode.AppendChild(lElement);
            }
        }
        // finally we remove all include templates
        foreach (XmlNode lTemplate in lTemplates)
            lTemplate.ParentNode.RemoveChild(lTemplate);
    }

    // private void AddVersionAttribute(ProcessInclude iInclude)
    // {
    //     // find version attribute
    //     XmlNodeList lVersionList = iInclude.SelectNodes("//oknxp:version");
    //     if (lVersionList.Count > 0)
    //     {
    //         XmlNode lVersion = mDocument.ImportNode(lVersionList[0], false);
    //         XmlNode lApplication = mDocument.SelectSingleNode("//ApplicationProgram", nsmgr);
    //         lApplication?.ParentNode.InsertBefore(lVersion, lApplication);

    //         // we calculate the default
    //         string lAppId = lVersion.NodeAttr("OpenKnxId");
    //         lAppId = lAppId.Replace("0x", "");
    //         string lAppNumber = lVersion.NodeAttr("ApplicationNumber");
    //         bool lDefault = int.TryParse(lAppId, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int lAppIdInt);
    //         int lAppNumberInt = 0;
    //         if (lDefault)
    //             lDefault = int.TryParse(lAppNumber, out lAppNumberInt);
    //         // we also handle a preset BaggagesRootDir
    //         string lRootDir = lVersion.NodeAttr("BaggagesRootDir");
    //         if (lRootDir == "")
    //             if (lDefault)
    //                 lRootDir = $"{lAppIdInt:X02}/{lAppNumberInt:X02}";
    //         if (lRootDir != "") iInclude.BaggagesBaseDir = lRootDir.Replace('/', '\\');
    //         string lSerialNumber = lVersion.NodeAttr("SerialNumber");
    //         if (lSerialNumber == "")
    //             if (lDefault)
    //                 lSerialNumber = $"0x{lAppIdInt:X02}{lAppNumberInt:X02}";
    //         if (lSerialNumber != "") ProcessInclude.AddConfig("SerialNumber", lSerialNumber);
    //     }
    // }

    private void AddDynamicPart(ProcessInclude iInclude)
    {
        // find dynamic Element
        XmlNodeList lDynamicList = iInclude.SelectNodes("//Dynamic");
        if (lDynamicList.Count > 0)
        {
            XmlNode lSourceDynamic = mDocument.ImportNode(lDynamicList[0], true);
            XmlNode lTargetDynamic = mDocument.SelectSingleNode("//Dynamic", nsmgr);
            lTargetDynamic.ParentNode.AppendChild(lSourceDynamic);
            lTargetDynamic.ParentNode.RemoveChild(lTargetDynamic);
        }
    }
}