using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Xml;
using OpenKNXproducer;

class TemplateApplication
{
    readonly XmlDocument mDocument = new();
    private XmlNamespaceManager nsmgr;


    public string Generate(ProcessInclude iInclude, XmlNode iBase)
    {
        GetTemplateDocument(iBase.Value, iInclude);
        nsmgr = new XmlNamespaceManager(mDocument.NameTable);
        nsmgr.AddNamespace("op", ProcessInclude.cOwnNamespace);
        AddIncludes(iInclude);
        AddVersionAttribute(iInclude);
        AddDynamicPart(iInclude);
        mDocument.Save("TemplateApplication.generated.xml");
        return mDocument.OuterXml;
    }

    private void GetTemplateDocument(string iFileName, ProcessInclude iInclude)
    {
        string lXml;
        if (File.Exists(iFileName))
        {
            lXml = File.ReadAllText(iFileName, Encoding.UTF8);
        }
        else
        {
            var assembly = Assembly.GetEntryAssembly();
            var resourceStream = assembly.GetManifestResourceStream("OpenKNXproducer.xml.TemplateApplication.xml");
            using var reader = new StreamReader(resourceStream, Encoding.UTF8);
            lXml = reader.ReadToEnd();
        }
        lXml = ProcessInclude.ReplaceXmlns(lXml);
        lXml = ReplaceCatalogTag(lXml, iInclude);
        mDocument.LoadXml(lXml);
    }

    private static string ReplaceCatalogTag(string iXml, ProcessInclude iInclude)
    {
        // find catalog attribute
        XmlNodeList lCatalogList = iInclude.SelectNodes("//oknxp:catalog");
        Dictionary<string, string> lParams = new();
        // update parameters
        if (lCatalogList.Count > 0)
        {
            foreach (XmlAttribute lAttribute in lCatalogList[0].Attributes)
                lParams["%" + lAttribute.Name + "%"] = lAttribute.Value;
            // set defaults
            if (!lParams.ContainsKey("%CatalogName%")) lParams["%CatalogName%"] = lParams["%ApplicationName%"];
            if (!lParams.ContainsKey("%HardwareName%")) lParams["%HardwareName%"] = lParams["%CatalogName%"];
            if (!lParams.ContainsKey("%ProductName%")) lParams["%ProductName%"] = lParams["%HardwareName%"];
            if (!lParams.ContainsKey("%BuildSuffix%")) lParams["%BuildSuffix%"] = "";
            if (!lParams.ContainsKey("%BusCurrent%")) lParams["%BusCurrent%"] = "10";
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
                XmlElement lElement = mDocument.CreateElement("op:include", ProcessInclude.cOwnNamespace);
                lTemplate.ParentNode.AppendChild(lElement);
                foreach (XmlAttribute lAttribute in lTemplate.Attributes)
                {
                    string lValue = lAttribute.Value;
                    if (lValue == "%prefix%") lValue = lDefine.Value.prefix;
                    if (lValue == "%share%") lValue = lDefine.Value.share;
                    if (lValue == "%templ%") lValue = lDefine.Value.template;
                    XmlAttribute lAttributeCopy = mDocument.CreateAttribute(lAttribute.Name);
                    lAttributeCopy.Value = lValue;
                    lElement.Attributes.Append(lAttributeCopy);
                }
            }
        }
        // finally we remove all include templates
        foreach (XmlNode lTemplate in lTemplates)
            lTemplate.ParentNode.RemoveChild(lTemplate);
    }

    private void AddVersionAttribute(ProcessInclude iInclude)
    {
        // find version attribute
        XmlNodeList lVersionList = iInclude.SelectNodes("//oknxp:version");
        if (lVersionList.Count > 0)
        {
            XmlNode lVersion = mDocument.ImportNode(lVersionList[0], false);
            XmlNode lApplication = mDocument.SelectSingleNode("//ApplicationProgram", nsmgr);
            lApplication?.ParentNode.InsertBefore(lVersion, lApplication);

            // we calculate the default
            string lAppId = lVersion.NodeAttr("OpenKnxId");
            lAppId = lAppId.Replace("0x", "");
            string lAppNumber = lVersion.NodeAttr("ApplicationNumber");
            bool lDefault = int.TryParse(lAppId, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int lAppIdInt);
            int lAppNumberInt = 0;
            if (lDefault)
                lDefault = int.TryParse(lAppNumber, out lAppNumberInt);
            // we also handle a preset BaggagesRootDir
            string lRootDir = lVersion.NodeAttr("BaggagesRootDir");
            if (lRootDir == "")
                if (lDefault)
                    lRootDir = $"{lAppIdInt:X02}/{lAppNumberInt:X02}";
            if (lRootDir != "") iInclude.BaggagesBaseDir = lRootDir.Replace('/', '\\');
            string lSerialNumber = lVersion.NodeAttr("SerialNumber");
            if (lSerialNumber == "")
                if (lDefault)
                    lSerialNumber = $"0x{lAppIdInt:X02}{lAppNumberInt:X02}";
            if (lSerialNumber != "") ProcessInclude.AddConfig("SerialNumber", lSerialNumber);
        }
    }

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