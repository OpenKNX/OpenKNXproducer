using System.Xml;

namespace OpenKNXproducer
{
    public class DefineContent
    {
        private static readonly Dictionary<string, DefineContent> sDefines = new();
        public static DefineContent Empty = new();
        private static bool sWithConfigTransfer = false;

        private bool mNoConfigTransfer = false;

        public string prefix = "LOG";
        public int KoOffset = 1;
        public int KoSingleOffset;
        public int KoBlockSize = 0;
        public string[] ReplaceKeys = Array.Empty<string>();
        public string[] ReplaceValues = Array.Empty<string>();
        public int NumChannels = 1;
        public int ModuleType = 1;
        public string header;
        public bool IsTemplate;
        public bool IsParameter = true;
        public string VerifyFile = "";
        public string VerifyRegex = "";
        public int VerifyVersion = -1;
        public string share;
        public string template;

        public static bool WithConfigTransfer
        {
            get { return sWithConfigTransfer; }
            private set { sWithConfigTransfer = value; }
        }

        public bool NoConfigTransfer
        {
            get { return mNoConfigTransfer; }
            private set { mNoConfigTransfer = value; }
        }

        public static DefineContent Factory(XmlNode iDefineNode)
        {
            DefineContent lResult = new();
            string lPrefix = iDefineNode.NodeAttr("prefix", "LOG");
            if (sDefines.ContainsKey(lPrefix))
            {
                lResult = sDefines[lPrefix];
            }
            else
            {
                lResult.prefix = lPrefix;
                lResult.header = iDefineNode.NodeAttr("header");

                if (!int.TryParse(iDefineNode.NodeAttr("NumChannels"), out lResult.NumChannels)) lResult.NumChannels = 0;
                if (!int.TryParse(iDefineNode.NodeAttr("KoOffset"), out lResult.KoOffset)) lResult.KoOffset = 1;
                if (!int.TryParse(iDefineNode.NodeAttr("KoSingleOffset"), out lResult.KoSingleOffset)) lResult.KoSingleOffset = 0;
                string lReplaceKeys = iDefineNode.NodeAttr("ReplaceKeys");
                string lReplaceValues = iDefineNode.NodeAttr("ReplaceValues");
                if (lReplaceKeys.Length > 0)
                {
                    lResult.ReplaceKeys = lReplaceKeys.Split(" ");
                    lResult.ReplaceValues = lReplaceValues.Split(" ");
                }
                lResult.ModuleType = int.Parse(iDefineNode.NodeAttr("ModuleType"));
                XmlNode lVerify = iDefineNode.FirstChild;
                if (lVerify != null && lVerify.Name == "op:verify")
                {
                    lResult.VerifyFile = lVerify.NodeAttr("File");
                    lResult.VerifyRegex = lVerify.NodeAttr("Regex", "\\s\"version\":\\s\"(\\d{1,2}).(\\d{1,2}).*\",");
                    lResult.VerifyVersion = TemplateApplication.ParseNumberValue("ModuleVersion", lVerify.NodeAttr("ModuleVersion", "-1"));
                    // int.TryParse(lVerify.NodeAttr("ModuleVersion", "-1"), out lResult.VerifyVersion);
                }
                lResult.share = iDefineNode.NodeAttr("share");
                lResult.NoConfigTransfer = iDefineNode.NodeAttr("noConfigTransfer") == "true";
                if (lResult.share.Contains("ConfigTransfer.share.xml"))
                {
                    lResult.NoConfigTransfer = true;
                    DefineContent.WithConfigTransfer = true;
                }
                lResult.template = iDefineNode.NodeAttr("template");
                lResult.IsParameter = false;
                lResult.IsTemplate = false;
                sDefines.Add(lPrefix, lResult);
            }
            return lResult;
        }

        public static DefineContent GetDefineContent(string iPrefix)
        {
            DefineContent lResult;
            lResult = sDefines.ContainsKey(iPrefix) ? sDefines[iPrefix] : Empty;
            return lResult;
        }

        public static bool ValidateDefines()
        {
            bool lResult = false;
            int[] lModuleTypes = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            foreach (var lDefineEntry in sDefines)
            {
                DefineContent lDefine = lDefineEntry.Value;
                int lModuleTypeLen = lDefine.ModuleType.ToString().Length;
                int lModuleTypeIndex = lDefine.ModuleType < 10 ? lDefine.ModuleType : lDefine.ModuleType / 10;
                switch (lModuleTypes[lModuleTypeIndex])
                {
                    case 1:
                        if (lModuleTypeLen == 2)
                            Program.Message(true, "Inconsistent ModuleType definitions found: {0} and {1}. Use always 2-digit ModuleTypes except you know what you are doing!", lModuleTypeIndex, lDefine.ModuleType);
                        break;
                    case 2:
                        if (lModuleTypeLen == 1)
                            Program.Message(true, "Inconsistent ModuleType definitions found: {0}x and {0}. Use always 2-digit ModuleTypes except you know what you are doing!", lModuleTypeIndex);
                        break;
                    default:
                        lModuleTypes[lModuleTypeIndex] = lModuleTypeLen;
                        break;
                }
            }
            return lResult;
        }

        public static Dictionary<string, DefineContent> Defines()
        {
            return sDefines;
        }
    }
}

