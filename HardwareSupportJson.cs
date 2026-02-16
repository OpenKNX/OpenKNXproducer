using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;

namespace OpenKNXproducer
{
    static class HardwareSupportJson
    {
        static readonly JsonObject sHardwareParamLong = [];
        static readonly JsonObject sHardwareParamShort = [];

        public static void ParseHardwareParams(XmlNodeList iHardwareParams, string iPrefix, int iChannelCount)
        {
            foreach (XmlNode lHardwareParam in iHardwareParams)
            {
                ParseHardwareNode(lHardwareParam, iPrefix, iChannelCount);                
            }
        }

        public static string OutputLong()
        {
           return sHardwareParamLong.ToJsonString(new JsonSerializerOptions { WriteIndented = true }); 
        }

        public static string OutputShort()
        {
           return sHardwareParamShort.ToJsonString(new JsonSerializerOptions { WriteIndented = true }); 
        }

        static void ParseHardwareNode(XmlNode iNode, string iPrefix, int iChannelCount)
        {
            if (iNode.Name != "Parameter") return;
            XmlNode lHardwareAttr = iNode.SelectSingleNode("@oknxp:hardwareDefault", ProcessInclude.nsmgr);
            if (lHardwareAttr == null || lHardwareAttr.Value != "true") return;
            string lParameterName = iPrefix + iNode.NodeAttr("Name");
            string lDefaultName = iNode.NodeAttr("Value");
            bool lAdded = false;
            if ((lDefaultName.Contains("%C%") || lParameterName.Contains("%C%")) && iChannelCount > 0)
            {
                DefineContent lDefine = DefineContent.GetDefineContent(iPrefix.Trim('_'));
                if (lDefine != null)
                {
                    int lNumChannels = lDefine.NumChannels;
                    for (int i = 0; i < lNumChannels; i++)
                    {
                        string lNewParameterName = lParameterName.Replace("%C%", (i + 1).ToString());
                        string lNewDefaultName = lDefaultName.Replace("%C%", (i + 1).ToString());
                        AddJsonEntry(lNewParameterName, lNewDefaultName);
                        lAdded = true;
                    }
                }
            }
            if (!lAdded) AddJsonEntry(lParameterName, lDefaultName);
            iNode.Attributes.RemoveNamedItem(lHardwareAttr.Name);
        }

        private static void AddJsonEntry(string iParameterName, string iDefaultName)
        {

            string lDefaultValue;
            JsonObject lEntry;
            if (ProcessInclude.Config.TryGetValue(iDefaultName, out ProcessInclude.ConfigEntry value))
            {
                lDefaultValue = value.ConfigValue;
                if (!sHardwareParamLong.ContainsKey(iDefaultName))
                    sHardwareParamLong[iDefaultName] = new JsonObject();
                lEntry = (JsonObject)sHardwareParamLong[iDefaultName];
            }
            else
            { 
                lDefaultValue = iDefaultName;
                lEntry = sHardwareParamLong;
            }
            lEntry[iParameterName] = lDefaultValue;

            sHardwareParamShort[iParameterName] = lDefaultValue;
        }
    }
}
