using System.Xml;

namespace OpenKNXproducer
{
    public class DefineContent
    {
        private static readonly Dictionary<string, DefineContent> sDefines = new();
        public string prefix = "LOG";
        public int KoOffset = 1;
        public int KoSingleOffset;
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
        public static DefineContent Empty = new();
        // private DefineContent(string iPrefix, string iHeader, int iKoOffset, int iKoSingleOffset, int iNumChannels, string iReplaceKeys, string iReplaceValues, int iModuleType, bool iIsParameter, string iVerifyFile, string iVerifyRegex, int iVerifyVersion)
        // {
        //     prefix = iPrefix;
        //     header = iHeader;
        //     KoOffset = iKoOffset;
        //     KoSingleOffset = iKoSingleOffset;
        //     if (iReplaceKeys.Length > 0)
        //     {
        //         ReplaceKeys = iReplaceKeys.Split(" ");
        //         ReplaceValues = iReplaceValues.Split(" ");
        //     }
        //     NumChannels = iNumChannels;
        //     ModuleType = iModuleType;
        //     IsParameter = iIsParameter;
        //     VerifyFile = iVerifyFile;
        //     VerifyRegex = iVerifyRegex;
        //     VerifyVersion = iVerifyVersion;
        // }
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
                    int.TryParse(lVerify.NodeAttr("ModuleVersion", "-1"), out lResult.VerifyVersion);
                }
                lResult.share = iDefineNode.NodeAttr("share");
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
            // if (sDefines.ContainsKey(iPrefix))
            // {
            //     lResult = sDefines[iPrefix];
            // }
            // else
            // {
            //     lResult = Empty;
            // }
            return lResult;
        }

        public static Dictionary<string, DefineContent> Defines()
        {
            return sDefines;
        }
    }
}

