using System.Xml;

namespace OpenKNXproducer
{
    public class DefineContent
    {
        private static Dictionary<string, DefineContent> sDefines = new Dictionary<string, DefineContent>();
        public string prefix;
        public int KoOffset;
        public int KoSingleOffset;
        public string[] ReplaceKeys = { };
        public string[] ReplaceValues = { };
        public int NumChannels;
        public int ModuleType;
        public string header;
        public bool IsTemplate;
        public bool IsParameter;
        public DefineContent(string iPrefix, string iHeader, int iKoOffset, int iKoSingleOffset, int iNumChannels, string iReplaceKeys, string iReplaceValues, int iModuleType, bool iIsParameter)
        {
            prefix = iPrefix;
            header = iHeader;
            KoOffset = iKoOffset;
            KoSingleOffset = iKoSingleOffset;
            if (iReplaceKeys.Length > 0)
            {
                ReplaceKeys = iReplaceKeys.Split(" ");
                ReplaceValues = iReplaceValues.Split(" ");
            }
            NumChannels = iNumChannels;
            ModuleType = iModuleType;
            IsParameter = iIsParameter;
        }
        public static DefineContent Factory(XmlNode iDefineNode)
        {
            DefineContent lResult;

            int lChannelCount = 0;
            int lKoOffset = 1;
            int lKoSingleOffset = 0;
            string lPrefix = "";
            string lHeader = "";
            string lReplaceKeys = "";
            string lReplaceValues = "";
            int lModuleType = 1;

            lPrefix = iDefineNode.NodeAttr("prefix");
            if (lPrefix == "") lPrefix = "LOG"; // backward compatibility
            if (sDefines.ContainsKey(lPrefix))
            {
                lResult = sDefines[lPrefix];
            }
            else
            {
                lHeader = iDefineNode.NodeAttr("header");
                if (!int.TryParse(iDefineNode.NodeAttr("NumChannels"), out lChannelCount)) lChannelCount = 0;
                if (!int.TryParse(iDefineNode.NodeAttr("KoOffset"), out lKoOffset)) lKoOffset = 1;
                if (!int.TryParse(iDefineNode.NodeAttr("KoSingleOffset"), out lKoSingleOffset)) lKoSingleOffset = 0;
                lReplaceKeys = iDefineNode.NodeAttr("ReplaceKeys");
                lReplaceValues = iDefineNode.NodeAttr("ReplaceValues");
                lModuleType = int.Parse(iDefineNode.NodeAttr("ModuleType"));
                lResult = new DefineContent(lPrefix, lHeader, lKoOffset, lKoSingleOffset, lChannelCount, lReplaceKeys, lReplaceValues, lModuleType, false);
                sDefines.Add(lPrefix, lResult);
            }
            return lResult;
        }

        static public DefineContent Empty = new DefineContent("LOG", "", 1, 0, 1, "", "", 1, true);
        public static DefineContent GetDefineContent(string iPrefix)
        {
            DefineContent lResult;
            if (sDefines.ContainsKey(iPrefix))
            {
                lResult = sDefines[iPrefix];
            }
            else
            {
                lResult = Empty;
            }
            return lResult;
        }
    }
}

