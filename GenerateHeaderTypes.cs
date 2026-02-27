
using System.Diagnostics;
using System.Text;
using System.Xml;

namespace OpenKNXproducer
{
    public static class GenerateHeaderTypes
    {
        static XmlNodeList sEnumerationTypes;
        static string sHeaderExportAttributeName = "unknownMagicNotExistingAttribute";
        static string sHeaderNameAttributeName = "unknownMagicNotExistingAttribute";
        static string sEnumberationElementName = "unknownMagicNotExistingAttribute";
        static string sProducerNamespacePrefix = "";

        public static void DeriveProducerNamespacePrefix(string iElementName) {
            if (iElementName.Contains(':'))
            {
                string[] parts = iElementName.Split(':');
                sProducerNamespacePrefix = parts[0] + ":";
                // performance: we find a representative headerName attribute to avoid namspace resolution for each enumeration type and value
                sHeaderExportAttributeName = sProducerNamespacePrefix + "headerExport";
                sHeaderNameAttributeName = sProducerNamespacePrefix + "headerName";
                sEnumberationElementName = sProducerNamespacePrefix + "Enumeration";
            }
        }

        public static bool CheckExport(XmlNode iNode)
        {
            string lExportType = iNode.NodeAttr(sHeaderExportAttributeName);
            return lExportType == "enum" || lExportType == "base";
        }

        public static string GetHeaderTypes(XmlNode iXml)
        {
            Console.WriteLine("Processing enumeration types...");
            sEnumerationTypes = iXml.SelectNodes("//ParameterType[TypeRestriction and @oknxp:headerExport]", ProcessInclude.nsmgr);
            if (sEnumerationTypes.Count == 0) return ""; 

            StringBuilder lHeader = new();
            foreach (XmlNode lEnumType in sEnumerationTypes)
            {
                string lTypeName = "PT_" + GetNormalizedName(lEnumType, "Name", true);
                string lExportType = lEnumType.NodeAttr(sHeaderExportAttributeName);
                lEnumType.Attributes.RemoveNamedItem(sHeaderExportAttributeName);
                if (lExportType == "none" || lExportType == "base") continue; // skip types explicitly marked as not to be exported
                Dictionary<string, int> lValues = [];
                // choose Enumeration elements that are direct children of a TypeRestriction
                XmlNodeList lEnumValues = lEnumType.SelectNodes("./TypeRestriction/Enumeration | ./TypeRestriction/oknxp:Enumeration", ProcessInclude.nsmgr);
                foreach (XmlNode lEnumValue in lEnumValues)
                {
                    string lKey = GetNormalizedName(lEnumValue, "Text", true);
                    if (!int.TryParse(lEnumValue.NodeAttr("Value"), out int lValue))
                    {
                        Program.Message("3.13.0", "ParameterType {0} contains non-numeric value {1} for Text {2}", lTypeName, lEnumValue.NodeAttr("Value"), lKey);
                        continue;
                    }
                    lValues.Add(lKey, lValue);
                    if (lEnumValue.Name == sEnumberationElementName)
                    {
                        // remove the Enumeration element to avoid confusion with the Enumeration elements that are used for other purposes (e.g. in DatapointType definitions)
                        lEnumValue.ParentNode.RemoveChild(lEnumValue);
                    }
                }
                if (lExportType == "define")
                    OutputDefine(lHeader, lTypeName, lValues);
                else if (lExportType == "enum")
                    OutputEnum(lHeader, lTypeName, lValues);
                lHeader.AppendLine();
            }
            return lHeader.ToString();
        }

        public static string GetNormalizedName(XmlNode iElement, string iAttributeName, bool iRemove = false)
        {
            string lName = iElement.NodeAttr(iAttributeName);
            string lHeaderName = iElement.NodeAttr(sHeaderNameAttributeName);
            if (!string.IsNullOrEmpty(lHeaderName))
            {
                lName = lHeaderName;
                if (iRemove) iElement.Attributes.RemoveNamedItem(sHeaderNameAttributeName);
            }
            lName = ParseDocumentation.GetChapterId(lName, "");
            lName = lName.Replace("-", "_");
            return lName;
        }

        private static void OutputEnum(StringBuilder iHeader, string iTypeName, Dictionary<string, int> iValues)
        {
            iHeader.AppendLine($"enum class {iTypeName}");
            iHeader.AppendLine("{");
            var list = iValues.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                var lValue = list[i];
                string comma = i < list.Count - 1 ? "," : "";
                iHeader.AppendLine($"    {lValue.Key} = {lValue.Value}{comma}");
            }
            iHeader.AppendLine("};");
        }

        private static void OutputDefine(StringBuilder iHeader, string iTypeName, Dictionary<string, int> iValues)
        {
            // iHeader.AppendLine($"#define {iTypeName}_COUNT {iValues.Count}");
            foreach (var lValue in iValues)
            {
                iHeader.AppendLine($"#define {iTypeName}_{lValue.Key} {lValue.Value}");
            }
        }
    }
}