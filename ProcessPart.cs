using System.ComponentModel.DataAnnotations;
using System.ComponentModel.Design.Serialization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml;

namespace OpenKNXproducer
{
    public class ParamEntry
    {
        private string mValue;
        private bool mIsNumeric;
        private int mValueInt;
        public string Value
        {
            get { return mValue; }
            set
            {
                mValue = value;
                mIsNumeric = int.TryParse(value, out mValueInt);
            }
        }

        public string Name;
        public bool WasReplaced;
        public bool IsNumeric { get { return mIsNumeric; } }
        public int ValueInt { get { return mValueInt; } }
        public int Digits;
        public int Increment = 1;
        public bool AsLetter = false;
        private readonly bool IsList = false;
        private readonly string[] mValues;
        private int mValuesPosition;

        public ParamEntry(string iName, XmlNode iNode)
        {
            Name = iName;
            Value = iNode.NodeAttr("value");
            if (!int.TryParse(iNode.NodeAttr("digits", "0"), out Digits))
                Digits = 0;
            if (!int.TryParse(iNode.NodeAttr("increment", "1"), out Increment))
                Increment = 1;
            AsLetter = iNode.NodeAttr("asLetter") == "true";
            string lList = iNode.NodeAttr("list", " ");
            string lValues = iNode.NodeAttr("values");
            if (lValues != "")
            {
                mValues = lValues.Split(lList);
                IsList = true;
                mValuesPosition = 0;
                Value = mValues[0];
            }
        }

        public ParamEntry(string iName)
        {
            Name = iName;
            Digits = 0;
            Increment = 1;
            AsLetter = false;
            IsList = false;
            mValues = null;
            mValuesPosition = 0;
            mIsNumeric = true;
            Value = "0";
        }

        public string FormattedValue(int iOffset)
        {
            string lResult = mValue;
            if (IsNumeric)
            {
                int lValue = mValueInt + iOffset;
                if (AsLetter)
                {
                    // be careful: A is 0!!!
                    lResult = "";
                    lValue++;
                    while (--lValue >= 0)
                    {
                        lResult = (char)('A' + lValue % 26) + lResult;
                        lValue /= 26;
                    }
                    lResult = lResult.PadLeft(Digits, 'A');
                }
                else
                {
                    lResult = lValue.ToString(Digits == 0 ? "D" : $"D{Digits}");
                }
            }
            return lResult;
        }

        public void Next()
        {
            if (IsList)
            {
                mValuesPosition = (mValuesPosition + Increment) % mValues.Length;
                Value = mValues[mValuesPosition];
            }
            else if (IsNumeric)
            {
                mValueInt += Increment;
            }
        }
    }

    public class ProcessPart
    {
        private readonly static Dictionary<string, ProcessPart> sParts = new();
        private readonly Dictionary<string, ParamEntry> mParams = new();

        readonly XmlNode mPartNode;
        readonly XmlDocument[] mDocuments;

        private ProcessInclude mInclude;

        public string IncludeName { get { return mInclude.XmlFileName; } }

        readonly string mName;
        public readonly int Instances;


        public bool AddParam(XmlNode iNode)
        {
            bool lResult = false;
            string lName = iNode.NodeAttr("name");
            lName = lName.Trim('%');
            lName = lName.Trim();
            lName = $"%{lName}%";
            if (lName != "" && !mParams.ContainsKey(lName))
            {
                mParams[lName] = new(lName, iNode);
                lResult = true;
            }
            return lResult;
        }

        public void ParseParams(XmlNodeList iParamNodes)
        {
            // config consists of a list of name-value pairs to be replaced in document
            foreach (XmlNode lNode in iParamNodes)
            {
                if (lNode.NodeType == XmlNodeType.Comment) continue;
                AddParam(lNode);
            }
        }



        protected ProcessPart(XmlNode iIncludeNode, string iName, int iInstances)
        {
            mPartNode = iIncludeNode;
            mName = iName;
            Instances = iInstances;
            mDocuments = new XmlDocument[Instances];
            sParts.Add(iName, this);
        }

        public static bool ParseInstance(XmlNode iNode, out string eName, out int eInstance)
        {
            eName = iNode.NodeAttr("name");
            bool lResult = int.TryParse(iNode.NodeAttr("instance", "1"), out eInstance);
            return lResult && eName != "";
        }

        public static ProcessPart GetPart(XmlNode iPartNode)
        {
            string lName = iPartNode.NodeAttr("name");
            return GetPart(lName);
        }

        public static ProcessPart GetPart(string iName)
        {
            ProcessPart lPart = null;
            if (sParts.ContainsKey(iName))
                lPart = sParts[iName];
            return lPart;
        }

        public static ProcessPart Factory(XmlNode iPartNode)
        {
            ProcessPart lPart;
            string lName = iPartNode.NodeAttr("name");
            if (sParts.ContainsKey(lName))
                lPart = sParts[lName];
            else
            {
                _ = int.TryParse(iPartNode.NodeAttr("instances", "1"), out int lInstances);
                lPart = new(iPartNode, lName, lInstances);
            }
            return lPart;
        }

        public static void Init(XmlNode iPartNode, ProcessInclude iInclude)
        {
            ProcessPart lPart = Factory(iPartNode);
            lPart.mInclude = iInclude;
            for (int lInstance = 0; lInstance < lPart.Instances; lInstance++)
                lPart.mDocuments[lInstance] = (XmlDocument)iInclude.GetDocument().CloneNode(true);
            lPart.Process();
        }

        public static XmlDocument GetDocument(XmlNode iIncludeNode)
        {
            // instances in xml are 1-based
            bool lSuccess = ParseInstance(iIncludeNode, out string lName, out int lInstance);
            XmlDocument lDocument = null;
            if (sParts.ContainsKey(lName))
            {
                ProcessPart lPart = sParts[lName];
                if (lInstance > 0 && lInstance <= lPart.Instances)
                    lDocument = lPart.mDocuments[lInstance - 1];
            }
            return lDocument;
        }

        public static void ReplaceAttribute(XmlAttribute iAttr, ParamEntry iParam)
        {
            string lAttr = iAttr.Value.Replace(iParam.Name, iParam.FormattedValue(0));
            if (iParam.IsNumeric)
            {
                bool lContinue;
                Regex lParamRegex = new(iParam.Name[..^1] + @"([+-]\d{1,3})%");
                do
                {
                    Match lMatch = lParamRegex.Match(lAttr);
                    lContinue = lMatch.Success;
                    if (lContinue)
                    {
                        int lOffset = int.Parse(lMatch.Groups[1].Value);
                        lAttr = lAttr.Replace(lMatch.Captures[0].Value, iParam.FormattedValue(lOffset));
                    }
                } while (lContinue);
            }
            iAttr.Value = lAttr;
        }

        public void Process()
        {
            XmlNamespaceManager nsmgr = new(mPartNode.OwnerDocument.NameTable);
            nsmgr.AddNamespace("op", ProcessInclude.cOwnNamespace);
            // first parse all params
            XmlNodeList lParamNodes = mPartNode.SelectNodes("//op:param", nsmgr);
            ParseParams(lParamNodes);
            // now replace them in part include
            for (int lInstance = 0; lInstance < Instances; lInstance++)
            {
                foreach (var lEntry in mParams)
                {
                    ParamEntry lParam = lEntry.Value;
                    XmlNodeList lAttrs = mDocuments[lInstance].SelectNodes($"//*/@*[contains(.,'{lParam.Name[..^1]}')]");
                    foreach (XmlAttribute lAttr in lAttrs)
                        ReplaceAttribute(lAttr, lParam);
                    lParam.Next();
                }
            }
        }

        public static List<XmlNode> Preprocess(XmlNodeList iNodes, XmlDocument iDocument)
        {
            List<XmlNode> lIncludeNodes = new();
            foreach (XmlNode lNode in iNodes)
            {
                ProcessInclude.ProcessConfig(lNode);
                lIncludeNodes.Add(lNode);
                if (lNode.LocalName == "part")
                {
                    // we found a part declaration
                    // at this point there is no include available, but we need the instances information
                    ProcessPart.Factory(lNode);
                }
                else if (lNode.LocalName == "usePart")
                {
                    string lName = lNode.NodeAttr("name");
                    string lInstance = lNode.NodeAttr("instance");
                    if (lName != "" && lInstance == "")
                    {
                        // We have a usepart include without a specific instance
                        ProcessPart lPart = ProcessPart.GetPart(lName);
                        if (lPart == null)
                        {
                            Program.Message(true, "Part '{0}' not found! ", lName);
                            continue;
                        }
                        int lInstances = lPart.Instances;
                        _ = int.TryParse(lNode.NodeAttr("instanceFrom", "1"), out int lFrom);
                        _ = int.TryParse(lNode.NodeAttr("instanceTo", lInstances.ToString()), out int lTo);
                        // we change the current include node to a node including the first instance
                        XmlAttribute lAttribute = iDocument.CreateAttribute("instance");
                        lAttribute.Value = lFrom.ToString();
                        lNode.Attributes.Append(lAttribute);
                        XmlNode lLastInsert = lNode;
                        // now we add all remaining instances as includes
                        for (int lCount = lFrom + 1; lCount <= lTo; lCount++)
                        {
                            XmlNode lNewInclude = lNode.CloneNode(true);
                            // XmlNode lNewInclude = lNode.OwnerDocument.ImportNode(lNode, true);
                            lNode.ParentNode.InsertAfter(lNewInclude, lLastInsert);
                            lNewInclude.Attributes["instance"].Value = lCount.ToString();
                            lIncludeNodes.Add(lNewInclude);
                            lLastInsert = lNewInclude;
                        }
                    }
                }
            }
            return lIncludeNodes;
        }
    }
}