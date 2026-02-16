using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace OpenKNXproducer
{
    static class HardwareSupportYaml
    {
        // root may contain either a string value or a nested Dictionary<string,string>
        static readonly Dictionary<string, object> sHardwareParamLong = new Dictionary<string, object>();
        static readonly Dictionary<string, string> sHardwareParamShort = new Dictionary<string, string>();

        public static void ParseHardwareParams(XmlNodeList iHardwareParams, string iPrefix, int iChannelCount)
        {
            foreach (XmlNode lHardwareParam in iHardwareParams)
            {
                ParseHardwareNode(lHardwareParam, iPrefix, iChannelCount);
            }
        }

        public static string OutputLong()
        {
            var sb = new StringBuilder();
            BuildYamlForLong(sb, sHardwareParamLong, 0);
            return sb.ToString();
        }

        public static string OutputShort()
        {
            var sb = new StringBuilder();
            foreach (var kv in sHardwareParamShort)
            {
                sb.Append(new string(' ', 0));
                sb.Append(kv.Key);
                sb.Append(": ");
                sb.Append(kv.Value);
                sb.AppendLine();
            }
            return sb.ToString();
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
                        AddYamlEntry(lNewParameterName, lNewDefaultName);
                        lAdded = true;
                    }
                }
            }
            if (!lAdded) AddYamlEntry(lParameterName, lDefaultName);
            iNode.Attributes.RemoveNamedItem(lHardwareAttr.Name);
        }

        private static void AddYamlEntry(string iParameterName, string iDefaultName)
        {
            string lDefaultValue;
            if (ProcessInclude.Config.TryGetValue(iDefaultName, out ProcessInclude.ConfigEntry value))
            {
                lDefaultValue = value.ConfigValue;
                if (!sHardwareParamLong.TryGetValue(iDefaultName, out var obj))
                {
                    var dic = new Dictionary<string, string>();
                    sHardwareParamLong[iDefaultName] = dic;
                    obj = dic;
                }
                var dict = obj as Dictionary<string, string>;
                dict[iParameterName] = lDefaultValue;
            }
            else
            {
                lDefaultValue = iDefaultName;
                sHardwareParamLong[iParameterName] = lDefaultValue;
            }
            sHardwareParamShort[iParameterName] = lDefaultValue;
        }

        static void BuildYamlForLong(StringBuilder sb, Dictionary<string, object> root, int indent)
        {
            string ind = new string(' ', indent);
            foreach (var kv in root)
            {
                if (kv.Value is string s)
                {
                    sb.Append(ind);
                    sb.Append(kv.Key);
                    sb.Append(": ");
                    sb.Append(s);
                    sb.AppendLine();
                }
                else if (kv.Value is Dictionary<string, string> dic)
                {
                    sb.Append(ind);
                    sb.Append(kv.Key);
                    sb.AppendLine(":");
                    foreach (var inner in dic)
                    {
                        sb.Append(ind);
                        sb.Append("  ");
                        sb.Append(inner.Key);
                        sb.Append(": ");
                        sb.Append(inner.Value);
                        sb.AppendLine();
                    }
                }
            }
        }

        static string QuoteIfNeeded(string s)
        {
            return s ?? string.Empty;
        }
    }
}
