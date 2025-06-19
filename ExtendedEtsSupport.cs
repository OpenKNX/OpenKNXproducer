using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Xml;
using OpenKNXproducer;

static class ExtendedEtsSupport
{
    static readonly Dictionary<DefineContent, Dictionary<string, string>> sParameterInfo = new();

    public static string ParameterInfo
    {
        get
        {
            string lResult = "\nvar uctChannelParams = {";
            foreach (var lEntry in sParameterInfo)
            {
                Dictionary<string, string> lModuleParams = lEntry.Value;
                lResult += $"\n\"{lEntry.Key.prefix}\": {{";
                foreach (var lModuleParam in lModuleParams)
                {
                    lResult += $"\n  {lModuleParam.Value},";
                }
                lResult = lResult[..^1] + "},";
            }
            lResult = lResult[..^1] + "\n};";
            return lResult;
        }
    }


    private static bool GenerateModuleSelector(ProcessInclude iInclude, int iApplicationVersion, int iApplicationNumber)
    {
        XmlNode lModuleSelector = iInclude.CreateElement("ParameterType", "Id", "%AID%_PT-ModuleSelector", "Name", "ModuleSelector");
        XmlNode lTypeRestriction = iInclude.CreateElement("TypeRestriction", "Base", "Value", "SizeInBit", "8", "UIHint", "DropDown");
        lTypeRestriction.AppendChild(iInclude.CreateElement("Enumeration", "Text", "Bitte wählen...", "Value", "255", "Id", "%ENID%"));
        XmlNode lModuleSelectorCopy = iInclude.CreateElement("ParameterType", "Id", "%AID%_PT-ModuleSelectorWithChannels", "Name", "ModuleSelectorWithChannels");
        lModuleSelector.AppendChild(lTypeRestriction);
        XmlNode lTypeRestrictionCopy = iInclude.CreateElement("TypeRestriction", "Base", "Value", "SizeInBit", "8", "UIHint", "DropDown");
        lTypeRestrictionCopy.AppendChild(iInclude.CreateElement("Enumeration", "Text", "Bitte wählen...", "Value", "255", "Id", "%ENID%"));
        lModuleSelectorCopy.AppendChild(lTypeRestrictionCopy);
        int lCount = 0;
        string lVersionInformation = $"\nvar uctVersionInformation = [0x{iApplicationNumber:X}, 0x{iApplicationVersion:X}];";
        string lModuleOrder = "\nvar uctModuleOrder = [";
        XmlNodeList lChannels = iInclude.SelectNodes("//ApplicationProgram/Dynamic/Channel");
        IEnumerator lIterator = lChannels.GetEnumerator();
        lIterator.Reset();
        var lModuleSelectorCopyCounter = 0;
        var lModuleSelectorCopyValue = 255;
        foreach (var lEntry in sParameterInfo)
        {
            string lText = lEntry.Key.prefix;
            if (lEntry.Key.ConfigTransferName != "")
                lText = lEntry.Key.ConfigTransferName;
            // if (lText == "BASE")
            //     lText = "Allgemein";
            // else
            for (int lIndex = 0; lIndex < lChannels.Count; lIndex++)
            {
                if (!lIterator.MoveNext())
                {
                    lIterator.Reset();
                    lIterator.MoveNext();
                }
                XmlNode lChannel = (XmlNode)lIterator.Current;
                if (lChannel.NodeAttr("Name").Contains(lText))
                {
                    lText = lChannel.NodeAttr("Text");
                    break;
                }
            }
            if (lEntry.Key.NumChannels > 0)
            {
                lTypeRestrictionCopy.AppendChild(iInclude.CreateElement("Enumeration", "Text", lText, "Value", lCount.ToString(), "Id", "%ENID%"));
                lModuleSelectorCopyCounter++;
                lModuleSelectorCopyValue = lCount;
            }
            lTypeRestriction.AppendChild(iInclude.CreateElement("Enumeration", "Text", lText, "Value", lCount++.ToString(), "Id", "%ENID%"));
            lModuleOrder += "\"" + lEntry.Key.prefix + "\",";
        }
        // special handling if there is just on entry in ModuleSelectorCopy dropdown
        if (lModuleSelectorCopyCounter == 1)
        {
            // we remove the "please choose" entry from dropdown
            lTypeRestrictionCopy.RemoveChild(lTypeRestrictionCopy.FirstChild);
            // and we replace the default values of according parameters to the new entry
            XmlNodeList lParameters = iInclude.SelectNodes("//ApplicationProgram/Static/Parameters//Parameter[@ParameterType='%AID%_PT-ModuleSelectorWithChannels']");
            foreach (XmlNode lParameter in lParameters)
            {
                if (lParameter.NodeAttr("Value") == "255")
                {
                    lParameter.Attributes["Value"].Value = lModuleSelectorCopyValue.ToString();
                }
            }
        }
        lModuleOrder = lModuleOrder[..^1] + "];\n";
        XmlNode lNode = iInclude.SelectSingleNode("//ApplicationProgram/Static/ParameterTypes");
        lNode?.InsertAfter(lModuleSelector, null);
        lNode?.InsertAfter(lModuleSelectorCopy, lModuleSelector);
        if (lNode != null)
        {
            lNode = iInclude.SelectSingleNode("//ApplicationProgram/Static/Script");
            if (lNode != null)
                lNode.InnerText = lVersionInformation + lModuleOrder + lNode.InnerText;
        }
        return lNode != null;
    }

    public static bool AddEtsExtensions(ProcessInclude iInclude, int iApplicationVersion, int iApplicationNumber)
    {
        if (!DefineContent.WithConfigTransfer)
            return false;
        XmlNode lScript = iInclude.SelectSingleNode("//ApplicationProgram/Static/Script");
        if (lScript == null)
            return false;
        lScript.InnerText = ParameterInfo + "\n\n" + lScript.InnerText;
        // return true;
        return GenerateModuleSelector(iInclude, iApplicationVersion, iApplicationNumber);
    }

    /// <summary>
    /// Generate an array of field names for ETS extensions like channel copy
    /// </summary>
    public static void GenerateScriptContent(ProcessInclude iInclude, DefineContent iDefine)
    {
        if (iDefine.NoConfigTransfer)
            return;
        if (string.IsNullOrEmpty(iDefine.template) && string.IsNullOrEmpty(iDefine.share))
            return; // pre-v1 
        Dictionary<string, string> lDict;
        XmlNodeList lParameters = iInclude.SelectNodes("//ApplicationProgram/Static/Parameters//Parameter");
        string lSuffix = "share";
        string lDelimiter = "";
        StringBuilder lParameterNames = new();
        StringBuilder lParameterDefaults = new();

        if (sParameterInfo.ContainsKey(iDefine))
            lDict = sParameterInfo[iDefine];
        else
        {
            lDict = new();
            sParameterInfo.Add(iDefine, lDict);
            // add version information, if available
            if (iDefine.NumChannels > 0)
                lDict.Add("channels", $"\"channels\": {iDefine.NumChannels}");
            if (iDefine.VerifyVersion > 0)
                lDict.Add("version", $"\"version\": 0x{iDefine.VerifyVersion:X}");

        }
        if (iDefine.template.EndsWith(Path.GetFileName(iInclude.XmlFileName)))
            lSuffix = "templ";
        lParameterNames.Append('[');
        lParameterDefaults.Append('[');
        foreach (XmlNode lNode in lParameters)
        {
            string lAccess = lNode.NodeAttr("Access");
            if (lAccess != "None" && lAccess != "Read")
            {
                XmlNode lType = ProcessInclude.ParameterType(lNode.NodeAttr("ParameterType"), false);
                string lTypeName = "";
                if (lType != null) lTypeName = lType.Name;
                // determine name
                XmlNodeList lParameterRefs = iInclude.SelectNodes($"//ApplicationProgram/Static/ParameterRefs/ParameterRef[@RefId='{lNode.NodeAttr("Id")}']");
                foreach (XmlNode lParameterRef in lParameterRefs)
                {
                    string lName = lNode.NodeAttr("Name");
                    if (lName.Contains("%CC%") || lName.Contains("%CCC%"))
                    {
                        Program.Message(true, "Name {0} has wrong format, just %C% allowed", lName);
                        lName = "";
                    }
                    if (lName.Contains('~'))
                    {
                        Program.Message(true, "Name {0} contains not allowed character '~'", lName);
                        lName = "";
                    }
                    lName = lName.Replace("%C%", "~");
                    string lDefault = lNode.NodeAttr("Value");
                    lDefault = lParameterRef.NodeAttr("Value", lDefault); // ParameterRef default has priority
                    _ = int.TryParse(lParameterRef.NodeAttr("Id")[^2..], out int lRefSuffix);
                    if (lParameterRefs.Count > 1 || (lParameterRefs.Count == 1 && lRefSuffix != 1))
                        lName = $"{lName}:{lRefSuffix}";
                    // determine default
                    if (lDefault.StartsWith('%') && lDefault.EndsWith('%'))
                        lDefault = null;
                    else if ("TypeNumber,TypeRestriction".Contains(lTypeName))
                    {
                        _ = Int64.TryParse(lDefault, out long lDefaultInt);
                        lDefault = lDefaultInt.ToString();
                    }
                    else if (lTypeName == "TypeFloat")
                    {
                        _ = double.TryParse(lDefault, NumberStyles.Float, CultureInfo.InvariantCulture, out double lDefaultFloat);
                        lDefault = lDefaultFloat.ToString(CultureInfo.InvariantCulture);
                    }
                    else if (lTypeName == "TypeColor")
                    {
                        if (lDefault.Length == 7 && lDefault.StartsWith('#')) lDefault = lDefault[1..];
                        if (lDefault.Length == 6) lDefault = "FF" + lDefault;
                        _ = int.TryParse(lDefault, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int lDefaultHex);
                        lDefault = lDefaultHex.ToString();
                    }
                    else if (lTypeName == "TypePicture")
                        lDefault = "";
                    else
                        lDefault = $"\"{lDefault}\"";
                    if (lName != "" && lDefault != "")
                    {
                        lParameterNames.Append($"{lDelimiter}\"{lName}\"");
                        lParameterDefaults.Append($"{lDelimiter}{lDefault}");
                        lDelimiter = ",";
                    }
                }
            }
        }
        lParameterNames.Append(']');
        lParameterDefaults.Append(']');
        string lOutput = $"\"{lSuffix}\": {{\n    \"names\": {lParameterNames.ToString()},\n    \"defaults\": {lParameterDefaults.ToString()}\n    }}";
        lDict.Add(lSuffix, lOutput);
        // Console.WriteLine("{2}: {0}, {1} Bytes", lOutput, lOutput.Length, iDefine.prefix);
    }

}