using System.Xml;
using System.Text.RegularExpressions;
using System.Text;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Serialization;
using System.Globalization;
using System.Diagnostics;
using System.Security.Principal;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Collections;
using System.Drawing;

namespace OpenKNXproducer
{
    public partial class ProcessInclude
    {

        public const string cOwnNamespace = "http://github.com/OpenKNX/OpenKNXproducer";
        public class ConfigEntry
        {
            public string ConfigValue;
            public bool WasReplaced;
        }
        public static readonly Dictionary<string, ConfigEntry> Config = [];
        public static readonly List<XmlNode> MinVersionNodes = [];    
        public static XmlNamespaceManager nsmgr;
        private readonly XmlDocument mDocument = new();
        private bool mLoaded = false;
        readonly StringBuilder mHeaderGenerated = new();
        // public static Version MinVersion = new(0, 0, 0);
        private static readonly Dictionary<string, XmlNode> sParameterTypes = new();
        private bool mParameterTypesFetched;
        private static readonly Dictionary<string, ProcessInclude> gIncludes = new();
        private static int sMaxKoNumber = 0;
        private readonly string mXmlFileName;
        public string XmlFileName { get { return mXmlFileName; } }
        private readonly string mHeaderPrefixName;
        private bool mHeaderParameterStartGenerated;
        private bool mHeaderParameterBlockGenerated;
        private bool mHeaderKoStartGenerated;
        private bool mHeaderKoBlockGenerated;
        private int mChannelCount = 1;
        public int OriginalChannelCount = -1;
        public int ParameterBlockOffset = 0;
        public int ParameterBlockSize = -1;
        public int KoOffset = 0;
        public int KoSingleOffset = 0;
        public int ModuleType = 1;
        public bool IsScript = false;
        public string[] ReplaceKeys = { };
        public string[] ReplaceValues = { };
        private int mKoBlockSize = 0;
        public static bool Renumber = false;
        public static bool AbsoluteSingleParameters = false;
        public bool IsInnerInclude = false;
        public bool IsPart = false;

        public int ChannelCount
        {
            get { return mChannelCount; }
            set
            {
                Program.MaxNumChannels = mChannelCount;
                mChannelCount = value;
                OriginalChannelCount = value;
            }
        }

        private static bool MergeParameterTypes(XmlNode iMergeTarget, XmlNode iMergeSource)
        {
            // check merge preconditions
            if (iMergeTarget == null || iMergeSource == null) return false;
            XmlNode lSourceChild = iMergeSource.FirstChild;
            XmlNode lTargetChild = iMergeTarget;
            // we only merge restrictions, other PT are merged if they are identical
            if (lSourceChild.Name != "TypeRestriction")
                return lSourceChild.OuterXml == lTargetChild.OuterXml;
            // Precheck (speedup): if source && target are identical, there is no need to merge
            if (lSourceChild.OuterXml == lTargetChild.OuterXml) return true;
            // the remainig case are different TypeRestriction
            // Size has to be identical
            if (lSourceChild.Name != lTargetChild.Name || lSourceChild.NodeAttr("Base") != lTargetChild.NodeAttr("Base") || lSourceChild.NodeAttr("SizeInBit") != lTargetChild.NodeAttr("SizeInBit"))
            {
                // we have to check the parameter types, if they are compatible
                // if not, we cannot merge them
                Program.Message(true, "Merging ParameterType {0}: Source and Target are not compatible!", iMergeTarget.Name);
                return false;
            }

            // first step: take all enumerations from source, which are not contained in target
            List<XmlNode> lEnums = [];
            // Loop in loop, might be performance critical with huge enumerations
            foreach (XmlNode lSourceNode in lSourceChild.ChildNodes)
            {
                bool lFound = false;
                if (lSourceNode.NodeType == XmlNodeType.Comment) continue;
                // speed: we check only if values are equal
                _ = int.TryParse(lSourceNode.NodeAttr("Value"), out int lSourceValue);
                foreach (XmlNode lTargetNode in lTargetChild.ChildNodes)
                {
                    if (lTargetNode.NodeType == XmlNodeType.Comment) continue;
                    _ = int.TryParse(lTargetNode.NodeAttr("Value"), out int lTargetValue);
                    if (lSourceValue == lTargetValue)
                    {
                        lFound = true;
                        break;
                    }
                }
                if (!lFound) lEnums.Add(lSourceNode);
            }

            // now we know, which source enums are not part of the target,  we merge them into the target
            foreach (XmlNode lChild in lEnums)
            {
                XmlNode lImportNode = lTargetChild.OwnerDocument.ImportNode(lChild.Clone(), true);
                lTargetChild.AppendChild(lImportNode);
            }
            return true;
        }

        private void MergeParameterTypes()
        {
            List<XmlNode> lMergedList = new();
            Dictionary<string, XmlNode> lFoundParameterTypes = new();
            // before we start with template processing, we calculate all Parameter relevant info
            XmlNode lParameterTypes = mDocument.SelectSingleNode("//ApplicationProgram/Static/ParameterTypes");
            if (lParameterTypes != null)
            {
                foreach (XmlNode lChild in lParameterTypes.ChildNodes)
                    if (lChild.NodeType == XmlNodeType.Element)
                    {
                        string lParameterTypeId = lChild.SubId("Id", "_PT-");
                        if (lParameterTypeId == "") continue;
                        if (lFoundParameterTypes.ContainsKey(lParameterTypeId))
                        {
                            // we try to merge parameter types 
                            bool lMerged = MergeParameterTypes(lFoundParameterTypes[lParameterTypeId], lChild);
                            if (lMerged) lMergedList.Add(lChild);
                        }
                        else
                        {
                            // Speed: we don't add the full type node, just the type definition itself
                            XmlNode lTypeChild = lChild.FirstChild;
                            while (lTypeChild != null && lTypeChild.NodeType == XmlNodeType.Comment) lTypeChild = lTypeChild.NextSibling;
                            lFoundParameterTypes.Add(lParameterTypeId, lTypeChild);
                        }
                    }
                // we remove all merged parameter types from the document
                // this is necessary, because the parameter types are not allowed to be duplicated
                // has to be done after the loop, otherwise we get an exception
                if (lMergedList.Count > 0)
                    foreach (XmlNode lChild in lMergedList)
                        lChild.ParentNode.RemoveChild(lChild);
            }
            mParameterTypesFetched = true;
        }


        private void FetchParameterTypes()
        {
            if (!mParameterTypesFetched)
            {
                // before we start with template processing, we calculate all Parameter relevant info
                XmlNode lParameterTypes = mDocument.SelectSingleNode("//ApplicationProgram/Static/ParameterTypes");
                if (lParameterTypes != null)
                {
                    foreach (XmlNode lChild in lParameterTypes.ChildNodes)
                        if (lChild.NodeType == XmlNodeType.Element)
                        {
                            string lParameterTypeId = lChild.SubId("Id", "_PT-");
                            if (lParameterTypeId == "") continue;
                            if (!sParameterTypes.ContainsKey(lParameterTypeId))
                            {
                                // Speed: we don't add the full type node, just the type definition itself
                                XmlNode lTypeChild = lChild.FirstChild;
                                while (lTypeChild != null && lTypeChild.NodeType == XmlNodeType.Comment) lTypeChild = lTypeChild.NextSibling;
                                sParameterTypes.Add(lParameterTypeId, lTypeChild);
                            }
                        }
                }
                mParameterTypesFetched = true;
            }

        }

       
        public static XmlNode ParameterType(string iId, bool iError = true)
        {
            string lId = "_PT-";
            if (iId.Contains(lId)) lId = iId;
            lId = lId.Split("_PT-")[1];
            if (sParameterTypes.TryGetValue(lId, out XmlNode value))
                return value;
            else if (iError)
                Program.Message(true, "ParameterType {0} was not declared, before it was used. Usually the declaration is missing or the include order of your used modules is wrong!", iId);
            return null;
        }

        public static int GetIdOfProjectNamespace(XmlDocument iDocument)
        {
            string lProject = iDocument.DocumentElement.NodeAttr("oldxmlns");
            lProject = lProject.Replace("http://knx.org/xml/project/", "");
            int.TryParse(lProject, out int lResult);
            return lResult;
        }

        private bool mSingletonDefinesAdded = false;

        public string HeaderGenerated
        {
            get
            {
                if (!mSingletonDefinesAdded)
                {
                    mHeaderGenerated.Insert(0, ExtendedEtsSupport.GeneratedHeaderAddon);
                    mHeaderGenerated.Insert(0, @"
#define paramDelay(time) (uint32_t)( \
            (time & 0xC000) == 0xC000 ? (time & 0x3FFF) * 100 : \
            (time & 0xC000) == 0x0000 ? (time & 0x3FFF) * 1000 : \
            (time & 0xC000) == 0x4000 ? (time & 0x3FFF) * 60000 : \
            (time & 0xC000) == 0x8000 ? ((time & 0x3FFF) > 1000 ? 3600000 : \
                                         (time & 0x3FFF) * 3600000 ) : 0 )
                                             
");
                    mHeaderGenerated.Insert(0, "#pragma once\n\n");
                    mSingletonDefinesAdded = true;
                }
                // add konstant suffix
                mHeaderGenerated.AppendLine("#ifdef MAIN_FirmwareRevision");              
                mHeaderGenerated.AppendLine("#ifndef FIRMWARE_REVISION");              
                mHeaderGenerated.AppendLine("#define FIRMWARE_REVISION MAIN_FirmwareRevision");              
                mHeaderGenerated.AppendLine("#endif");              
                mHeaderGenerated.AppendLine("#endif");              
                mHeaderGenerated.AppendLine("#ifdef MAIN_FirmwareName");              
                mHeaderGenerated.AppendLine("#ifndef FIRMWARE_NAME");              
                mHeaderGenerated.AppendLine("#define FIRMWARE_NAME MAIN_FirmwareName");              
                mHeaderGenerated.AppendLine("#endif");              
                mHeaderGenerated.AppendLine("#endif");              
                return mHeaderGenerated.ToString();
            }
        }

        private static void GetMinVersions(XmlNode iTargetNode) {
            XmlNodeList lMinVersionNodes = iTargetNode.SelectNodes("//oknxp:minOpenKNXproducerVersion | //oknxp:minModuleVersion", nsmgr);
            MinVersionNodes.AddRange(lMinVersionNodes.Cast<XmlNode>());
        }

        public static ProcessInclude Factory(string iXmlFileName, string iHeaderPrefixName)
        {
            ProcessInclude lInclude = null;
            string lXmlFileName = Path.GetFileName(iXmlFileName);
            if (gIncludes.ContainsKey(lXmlFileName))
            {
                // Console.WriteLine("Reusing existing include {0}", iXmlFileName);
                lInclude = gIncludes[lXmlFileName];
            }
            else
            {
                Console.WriteLine("Processing include {0}", iXmlFileName);
                lInclude = new ProcessInclude(iXmlFileName, iHeaderPrefixName);
                gIncludes.Add(lXmlFileName, lInclude);
            }
            return lInclude;
        }

        private ProcessInclude(string iXmlFileName, string iHeaderPrefixName)
        {
            mXmlFileName = iXmlFileName;
            mBaggagesName = Path.GetFileName(iXmlFileName).Replace(".xml", ".baggages");
            if (iHeaderPrefixName != "" && !iHeaderPrefixName.EndsWith('_')) iHeaderPrefixName += "_";
            mHeaderPrefixName = iHeaderPrefixName;
        }

        static int GetHeaderParameter(string iHeaderFileContent, string iDefineName)
        {
            string lPattern = "#define.*" + iDefineName + @"\s*(\d{1,4})";
            Match m = Regex.Match(iHeaderFileContent, lPattern, RegexOptions.None);
            int lResult = -1;
            if (m.Groups.Count > 1)
            {
                int.TryParse(m.Groups[1].Value, out lResult);
            }
            return lResult;
        }

        public XmlNodeList SelectNodes(string iXPath)
        {
            return mDocument.SelectNodes(iXPath, nsmgr);
        }

        public XmlNode SelectSingleNode(string iXPath)
        {
            return mDocument.SelectSingleNode(iXPath, nsmgr);
        }


        static string CalculateId(int iApplicationNumber, int iApplicationVersion)
        {
            return string.Format("-{0:X4}-{1:X2}-0000", iApplicationNumber, iApplicationVersion);
        }

        public XmlDocument GetDocument()
        {
            return mDocument;
        }

        public void ResetXsd()
        {
            XmlNode lXmlModel = mDocument.FirstChild?.NextSibling;
            // the following if has to be very specific, because an assignment to InnerText of an XmlElement might delete all inner tags!!! 
            if (lXmlModel != null && lXmlModel.NodeType == XmlNodeType.ProcessingInstruction && lXmlModel.InnerText.Contains("-editor.xsd"))
                lXmlModel.InnerText = lXmlModel.InnerText.Replace("-editor.xsd", ".xsd");
        }

        public void DocumentDebugOutput()
        {
            mDocument.Save(Path.ChangeExtension(mXmlFileName, "out.xml"));
        }

        public void SetToolAndVersion()
        {
            mDocument.DocumentElement.RemoveAttribute("CreatedBy");
            mDocument.DocumentElement.SetAttribute("CreatedBy", typeof(Program).Assembly.GetName().Name);
            mDocument.DocumentElement.RemoveAttribute("ToolVersion");
            // alternative version in case ETS does not accept our tool versions anymore
            // System.Diagnostics.FileVersionInfo info = FileVersionInfo.GetVersionInfo(Path.Combine(iETSPath, "Knx.Ets.XmlSigning.dll"));
            // toolVersion = $"{info.FileVersion}.{info.FilePrivatePart}";
            mDocument.DocumentElement.SetAttribute("ToolVersion", typeof(Program).Assembly.GetName().Version.ToString());
        }

        public void SetNamespace()
        {
            // we restore the original namespace, if necessary
            if (mDocument.DocumentElement.GetAttribute("xmlns") == "")
            {
                string lXmlns = mDocument.DocumentElement.GetAttribute("oldxmlns");
                if (lXmlns != "")
                {
                    mDocument.DocumentElement.SetAttribute("xmlns", lXmlns);
                    mDocument.DocumentElement.RemoveAttribute("oldxmlns");
                }
            }
        }

        public string GetNamespace()
        {
            return mDocument.DocumentElement.GetAttribute("xmlns");
        }

        public bool Expand()
        {
            // here we recursively process all includes and all channel repetitions
            LoadAdvanced(mXmlFileName);
            // we use here an empty DefineContent, just for startup
            ExportHeader(DefineContent.Empty, mHeaderPrefixName, this);
            ProcessModule.ExportHeaderParameterAll(this, mHeaderGenerated);
            // finally we do all processing necessary for the whole (resolved) document
            // mDocument.Save("TemplateApplication.expanded.xml");
            bool lWithVersions = ProcessFinish(mDocument);
            // DocumentDebugOutput();
            return lWithVersions;
        }

        private static int ChannelCalculationHelper(int iChannel, string iExpression)
        {
            char lOperator = iExpression[0];
            if (int.TryParse(iExpression[1..], out int lOperand))
                switch (lOperator)
                {
                    case '*':
                        iChannel *= lOperand;
                        break;
                    case '-':
                        iChannel -= lOperand;
                        break;
                    default:
                        iChannel += lOperand;
                        break;
                }
            return iChannel;
        }
        static string ReplaceChannelTemplate(string iValue, int iChannel)
        {
            string lResult = iValue;
            bool lReplaced = false;
            Regex lChannelNumberRegex = FastRegex.ChannelNumberPattern();
            Regex lChannelLetterRegex = FastRegex.ChannelLetterPattern();
            Match lMatch;
            // support multiple occurrences 
            if (iValue.Contains('%'))
            {
                do
                {
                    lMatch = lChannelNumberRegex.Match(lResult);
                    lReplaced = false;
                    if (lMatch.Captures.Count > 0)
                    {
                        int lLen = lMatch.Groups[1].Value.Length;
                        string lFormat = string.Format("D{0}", lLen);
                        for (int lGroup = 2; lGroup < lMatch.Groups.Count; lGroup++)
                            if (lMatch.Groups[lGroup].Success)
                                iChannel = ChannelCalculationHelper(iChannel, lMatch.Groups[lGroup].Value);
                        lResult = lResult.Replace(lMatch.Value, iChannel.ToString(lFormat));
                        lReplaced = true;
                    }
                } while (lReplaced);
                do
                {
                    lMatch = lChannelLetterRegex.Match(lResult);
                    lReplaced = false;
                    if (lMatch.Captures.Count > 0)
                    {
                        int lLen = lMatch.Groups[1].Value.Length;
                        string channelName = "";
                        int temp_Channel = iChannel - 1;
                        for (int i = 0; i < lLen; i++)
                        {
                            if (temp_Channel >= 0) channelName = Convert.ToChar((temp_Channel) % 26 + 65) + channelName;
                            temp_Channel /= 26;
                            temp_Channel--;
                        }
                        lResult = lResult.Replace(lMatch.Value, channelName);
                        lReplaced = true;
                    }
                } while (lReplaced);
            }
            return lResult;
        }

        static void ReplaceKoTemplateFinal(XmlNode iTargetNode)
        {
            Regex lRegex = FastRegex.KoTemplateFinal();
            XmlNodeList lNodes = iTargetNode.SelectNodes("//*/@*[contains(.,'%!K')]");
            foreach (XmlAttribute lNode in lNodes)
            {
                string lValue = lNode.Value;
                Match lMatch = lRegex.Match(lValue);
                if (lMatch.Captures.Count > 0)
                {
                    string lPrefix = lMatch.Groups[3].Value;
                    DefineContent lDefine = DefineContent.GetDefineContent(lPrefix);
                    if (lDefine != null)
                    {
                        int lBlockSize = lDefine.KoBlockSize;
                        // if (lBlockSize == 0) return;
                        int lOffset = lDefine.KoOffset;
                        _ = int.TryParse(lMatch.Groups[2].Value, out int lChannel);
                        if (int.TryParse(lMatch.Groups[1].Value, out int lShift))
                        {
                            int lKoNumber = (lChannel - 1) * lBlockSize + lOffset + lShift;
                            // we replace just in case it is numeric, otherwise an error message will appear during final document check                        
                            lNode.Value = lValue.Replace(lMatch.Value, lKoNumber.ToString());
                            // remember the max replaced number
                            sMaxKoNumber = (lKoNumber > sMaxKoNumber) ? lKoNumber : sMaxKoNumber;
                        }
                    }
                }

            }
        }

        static string ReplaceKoTemplate(DefineContent iDefine, string iValue, int iChannel, ProcessInclude iInclude, bool iIsName, XmlAttribute iAttr = null)
        {
            string lResult = iValue;
            int lBlockSize = 0;
            int lOffset = 0;
            if (iDefine.IsTemplate)
            {
                if (iValue.Contains("%K"))
                {
                    if (iInclude != null)
                    {
                        lBlockSize = iInclude.mKoBlockSize;
                        lOffset = iInclude.KoOffset;
                    }
                    // too slow!!!
                    // MatchCollection lMatches = Regex.Matches(iValue, @"%K(\d{1,3})%");
                    Regex lRegex = FastRegex.KoTemplate();
                    Match lMatch = lRegex.Match(iValue);
                    if (lMatch.Captures.Count > 0)
                    {
                        if (iInclude != null && !iInclude.mHeaderKoBlockGenerated)
                        {
                            lResult = iValue.Replace(lMatch.Value, string.Format("%!K{0}!C{1:D04}!{2}!%", lMatch.Groups[1].Value, iChannel, iDefine.prefix));
                        }
                        else if (int.TryParse(lMatch.Groups[1].Value, out int lShift))
                        {
                            int lKoNumber = (iChannel - 1) * lBlockSize + lOffset + lShift;
                            // we replace just in case it is numeric, otherwise an error message will appear during final document check                        
                            lResult = iValue.Replace(lMatch.Value, lKoNumber.ToString());
                            // remember the max replaced number
                            sMaxKoNumber = (lKoNumber > sMaxKoNumber) ? lKoNumber : sMaxKoNumber;
                            // we want to replace all occurrences, but a match collection is to slow, so we call recursively just if 
                            // a replacement happened 
                            lResult = ReplaceKoTemplate(iDefine, lResult, iChannel, iInclude, iIsName, iAttr);
                        }
                    }
                }
            }
            else if (iIsName)
            {
                // iChannel is in this case KoSingleOffset
                if (int.TryParse(iValue, out int lValue))
                {
                    // we replace just in case it is numeric, otherwise an error message will appear during final document check
                    lResult = (lValue + iDefine.KoSingleOffset).ToString();
                }
            }
            return lResult;
        }

        static void ProcessAttribute(DefineContent iDefine, int iChannel, XmlAttribute iAttr, XmlNode iTargetNode, ProcessInclude iInclude)
        {
            // we have to mark ParameterBlock and ParameterSeparator for renumber processing
            string lValue = iAttr.Value;
            if ((lValue.Contains("%T%") || lValue.Contains("%TT%")))
            {
                // if (!lAttr.Value.Contains("_MD-"))
                // {
                //     lValue = lValue.Replace("_PS-", "_PST-");
                //     lValue = lValue.Replace("_PB-", "_PBT-");
                // }
                string lModuleType = iInclude.ModuleType.ToString();
                if (lValue.Contains("%TT%"))
                {
                    if (lModuleType.Length == 2)
                        lValue = lValue.Replace("%TT%", lModuleType);
                    else if (lModuleType.Length == 1)
                        lValue = lValue.Replace("%TT%", lModuleType + "0");
                }
                else if (lModuleType.Length == 1)
                    lValue = lValue.Replace("%T%", lModuleType);
            }
            lValue = ReplaceChannelTemplate(lValue, iChannel);
            lValue = ReplaceKoTemplate(iDefine, lValue, iChannel, iInclude, iAttr.Name == "Number", iAttr);
            // lAttr.Value = lAttr.Value.Replace("%N%", mChannelCount.ToString());
            if (iAttr.Name == "Name" && iAttr.OwnerElement.Name != "ParameterType" && iTargetNode.Name != "Argument" && !iInclude.IsInnerInclude)
                if (!lValue.StartsWith(iInclude.mHeaderPrefixName))
                    lValue = iInclude.mHeaderPrefixName + lValue;
            iAttr.Value = lValue;
        }

        static void ProcessAttributes(DefineContent iDefine, int iChannel, XmlNode iTargetNode, ProcessInclude iInclude)
        {
            foreach (XmlAttribute lAttr in iTargetNode.Attributes)
            {
                ProcessAttribute(iDefine, iChannel, lAttr, iTargetNode, iInclude);
            }
        }

        static void ProcessHelpContext(DefineContent iDefine, XmlNode iTargetNode, ProcessInclude iInclude)
        {
            XmlNodeList lNodes = iTargetNode.SelectNodes(@"//ParameterRefRef[@HelpContext]");
            XmlNodeList lParameters = iTargetNode.SelectNodes(@"//Parameter[@Id]");
            Dictionary<string, XmlNode> lParameterLookup = new();
            // create lookup first (performance optimization)
            foreach (XmlNode lNode in lParameters)
            {
                string lId = lNode.NodeAttr("Id");
                if (!lParameterLookup.ContainsKey(lId))
                    lParameterLookup.Add(lId, lNode);
            }
            foreach (XmlNode lNode in lNodes)
            {
                XmlNode lAttribute = lNode.Attributes.GetNamedItem("HelpContext");
                if (lAttribute.Value == "%DOC%")
                {
                    string lId = lNode.NodeAttr("RefId").Split("_R-")[0];
                    if (lParameterLookup.ContainsKey(lId))
                    {
                        XmlNode lParameter = lParameterLookup[lId];
                        string lText = lParameter.NodeAttr("Text");
                        lText = ParseDocumentation.GetChapterId(lText, iDefine.prefixDoc);
                        // Console.WriteLine(lText);
                        lAttribute.Value = lText;
                    }
                }
            }
        }

        static void ProcessParameter(DefineContent iDefine, int iChannel, XmlNode iTargetNode, ProcessInclude iInclude)
        {
            // ensure property parameters are correctly replaced
            XmlNode lProperty = iTargetNode.SelectSingleNode("Property");
            if (lProperty != null)
            {
                ProcessAttributes(iDefine, iChannel, lProperty, iInclude);
            }
            //calculate new offset
            XmlNode lMemory = iTargetNode.SelectSingleNode("Memory");
            if (lMemory != null)
            {
                XmlNode lAttr = lMemory.Attributes.GetNamedItem("Offset");
                int lOffset = int.Parse(lAttr.Value);
                // if (iInclude.ChannelCount > (AbsoluteSingleParameters ? 1 : 0))
                if (iInclude.ChannelCount > (AbsoluteSingleParameters ? 1 : 0))
                    lOffset += iInclude.ParameterBlockOffset + (iChannel - 1) * iInclude.ParameterBlockSize;
                lAttr.Value = lOffset.ToString();
            }
        }

        static void ProcessUnion(DefineContent iDefine, int iChannel, XmlNode iTargetNode, ProcessInclude iInclude)
        {
            //calculate new offset
            ProcessParameter(iDefine, iChannel, iTargetNode, iInclude);
            XmlNodeList lChildren = iTargetNode.ChildNodes;
            foreach (XmlNode lChild in lChildren)
            {
                if (lChild.Name == "Parameter")
                    ProcessAttributes(iDefine, iChannel, lChild, iInclude);
            }
        }

        static void ProcessChannel(DefineContent iDefine, int iChannel, XmlNode iTargetNode, ProcessInclude iInclude)
        {
            //attributes of the node
            if (iTargetNode.Attributes != null)
            {
                ProcessAttributes(iDefine, iChannel, iTargetNode, iInclude);
            }

            if (iTargetNode.Name == "ModuleDef")
                ProcessModule.ProcessModuleFactory(iDefine, iChannel, iTargetNode, iInclude);

            if (iTargetNode.Name == "Module")
                ProcessModule.ProcessModuleInstance(iDefine, iChannel, iTargetNode, iInclude);

            //Print individual children of the node, gets only direct children of the node
            XmlNodeList lChildren = iTargetNode.ChildNodes;
            foreach (XmlNode lChild in lChildren)
            {
                ProcessChannel(iDefine, iChannel, lChild, iInclude);
            }
        }
        // static void ProcessChannel(DefineContent iDefine, int iChannel, XmlNode iTargetNode, ProcessInclude iInclude)
        // {
        //     XmlNodeList lAttrs = iTargetNode.SelectNodes("*/@*");
        //     foreach (XmlAttribute lAttr in lAttrs)
        //     {
        //         ProcessAttribute(iDefine, iChannel, lAttr, iInclude);
        //     }
        // }

        void ProcessBaggage(DefineContent iDefine, int iChannel, XmlNode iTargetNode, ProcessInclude iInclude)
        {
            // Baggage-node
            // Console.WriteLine($"Processing baggages of {iInclude.mXmlFileName}");
            XmlNode lBaggageIdAttr = iTargetNode.Attributes.GetNamedItem("Id");
            if (lBaggageIdAttr != null)
            {
                // we have a real Baggage definition, not just an extension reference
                // we copy all baggage files to our working dir
                string lBaggageId = lBaggageIdAttr.Value;
                XmlNode lFileNameAttr = iTargetNode.Attributes.GetNamedItem("Name");
                string lFileName = lFileNameAttr.Value;
                XmlNode lPathAttr = iTargetNode.Attributes.GetNamedItem("TargetPath");
                string lTargetPath = lPathAttr.Value;
                string lSourceDirName = "";
                if (lFileName.StartsWith("..\\"))
                    lSourceDirName = iInclude.mCurrentDir;
                else
                    lSourceDirName = Path.Combine(iInclude.mCurrentDir, "Baggages", lTargetPath);
                string lTargetDirRoot = Path.Combine(mCurrentDir, mBaggagesName);
                lPathAttr.Value = Path.Combine(BaggagesBaseDir, lTargetPath);
                // on MacOS/Linux we have to replace the path separator
                lPathAttr.Value = lPathAttr.Value.Replace("/", "\\");
                if (lBaggageId.StartsWith("%FILE-HELP") || lBaggageId.StartsWith("%FILE-ICONS"))
                {
                    // context sensitive help and icons have to be merged
                    // we expect a directory, even if the file notation says ".zip"
                    lFileName = lFileName.Replace(".zip", "");
                    // for ETS, we need a zip and ensure, that it is generated
                    lFileNameAttr.Value = lFileName + ".zip";
                    // the path has to go to the specific application folder of the root application
                    // lPath = DetermineBaggagePath(lPath);
                    if (mCurrentDir == iInclude.mCurrentDir)
                    {
                        // BaggagesBaseDir = lPath;
                        if (!mBaggageTargetZipDirName.ContainsKey(lBaggageId))
                        {
                            mBaggageTargetZipDirName.Add(lBaggageId, lFileName);
                        }
                    }
                    // now we copy all files to target
                    lSourceDirName = Path.Combine(lSourceDirName, lFileName);
                    if (Directory.Exists(lSourceDirName))
                    {
                        if (!mBaggageTargetZipDirName.ContainsKey(lBaggageId))
                        {
                            // this is the case where an include provides a .zip-File, but the application doesn't.
                            // i.e. logic has an Icons.zip, but vpm has not. We generate an according file name
                            mBaggageTargetZipDirName.Add(lBaggageId, lFileName);
                        }
                        var lSourceDir = new DirectoryInfo(lSourceDirName);
                        lSourceDir.DeepCopy(Path.Combine(lTargetDirRoot, BaggagesBaseDir, mBaggageTargetZipDirName[lBaggageId]));
                    }
                }
                else
                {
                    // we copy single files without any merge process
                    string lSourceFileName = Path.Combine(lSourceDirName, lFileName);
                    string lTargetDirName = Path.Combine(lTargetDirRoot, BaggagesBaseDir, lTargetPath);
                    string lTargetFileName = Path.Combine(lTargetDirName, Path.GetFileName(lFileName));
                    if (File.Exists(lSourceFileName))
                    {
                        Directory.CreateDirectory(lTargetDirName);
                        File.Copy(lSourceFileName, lTargetFileName, true);
                        File.SetLastWriteTimeUtc(lTargetFileName, DateTime.Now);
                        lFileNameAttr.Value = Path.GetFileName(lFileName);
                    }
                }
            }
        }

        private string DetermineBaggagePath(string iPath)
        {
            var lDirInfo = new DirectoryInfo(Path.Combine(mCurrentDir, "Baggages"));
            if (lDirInfo.Exists)
            {
                var lSubDirInfos = lDirInfo.EnumerateDirectories("??");
                if (lSubDirInfos != null && lSubDirInfos.Any())
                {
                    var lSubSubDirInfos = lSubDirInfos.First().EnumerateDirectories("??");
                    if (lSubSubDirInfos != null && lSubSubDirInfos.Any())
                        iPath = Path.Combine(lSubDirInfos.First().Name, lSubSubDirInfos.First().Name);
                }
            }
            return iPath;
        }

        void ProcessTemplate(DefineContent iDefine, int iChannel, XmlNode iTargetNode, ProcessInclude iInclude)
        {
            if (iTargetNode.Name == "Baggage")
            {
                ProcessBaggage(iDefine, iChannel, iTargetNode, iInclude);
            }
            else
            {
                ProcessAttributes(iDefine, iChannel, iTargetNode, iInclude);
                if (iTargetNode.Name == "Parameter")
                {
                    ProcessParameter(iDefine, iChannel, iTargetNode, iInclude);
                }
                else
                if (iTargetNode.Name == "Union")
                {
                    ProcessUnion(iDefine, iChannel, iTargetNode, iInclude);
                }
                else
                if ("ParameterType,Channel,ChannelIndependentBlock,ParameterBlock,choose,ParameterCalculations,ParameterValidations,ModuleDef".Contains(iTargetNode.Name))
                {
                    ProcessChannel(iDefine, iChannel, iTargetNode, iInclude);
                }
            }
        }

        static void ReplaceDocumentStrings(XmlNode iNode, string iSourceText, string iTargetText)
        {
            // fastest method
            XmlNodeList lAttributes = iNode.SelectNodes($"//*/@*[contains(., '{iSourceText}')]");
            foreach (XmlAttribute lAttribute in lAttributes)
            {
                XmlNode lNode = lAttribute.OwnerElement;
                XmlNode lComment = lNode.OwnerDocument.CreateComment("Replaced " + iSourceText + " by '" + iTargetText + "'");
                lNode.ParentNode.InsertBefore(lComment, lNode);
                lAttribute.Value = lAttribute.Value.Replace(iSourceText, iTargetText);
            }
        }

        bool ProcessFinish(XmlNode iTargetNode)
        {
            Console.WriteLine("Processing merged file...");
            // Console.WriteLine();
            // Console.WriteLine(HardwareSupportCustom.OutputLong());
            // Console.WriteLine();
            // Console.WriteLine(HardwareSupportCustom.OutputShortCustom());
            // Console.WriteLine();
            // Console.WriteLine(HardwareSupportCustom.OutputShortC());
            ProcessConfig(iTargetNode);
            MergeParameterTypes();
            ReplaceKoTemplateFinal(iTargetNode);
            // ensure, that we found maxKoNumber
            XmlNodeList lKoNumbers = iTargetNode.SelectNodes("//ComObject/@Number");
            foreach (XmlAttribute lKoNumber in lKoNumbers)
                if (int.TryParse(lKoNumber.Value, out int lKoValue))
                    sMaxKoNumber = (lKoValue > sMaxKoNumber) ? lKoValue : sMaxKoNumber;
            bool lWithVersions = false;
            XmlNode lApplicationProgramNode = iTargetNode.SelectSingleNode("/KNX/ManufacturerData/Manufacturer/ApplicationPrograms/ApplicationProgram");
            // evaluate oknxp:version, if available
            XmlNode lMcVersionNode = iTargetNode.SelectSingleNode("//oknxp:version", nsmgr);
            string lInlineData = "";
            string lVersionMessage = "";
            StringBuilder lVersionInformation = new StringBuilder();
            if (lMcVersionNode != null)
            {
                // found oknxp:version, we apply its attributes to knxprod-xml
                int lOpenKnxId = 175;
                string lValue = lMcVersionNode.Attributes.GetNamedItem("OpenKnxId").Value;
                lValue = lValue.Replace("0x", "");
                if (!int.TryParse(lValue, System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture, out lOpenKnxId))
                    Program.Message(true, "OpenKnxId could not be parsed, given value was {0}", lMcVersionNode.Attributes.GetNamedItem("OpenKnxId").Value);
                int lAppNumber = 0;
                if (!int.TryParse(lMcVersionNode.Attributes.GetNamedItem("ApplicationNumber").Value, out lAppNumber))
                    Program.Message(true, "ApplicationNumber could not be parsed, given value was {0}", lMcVersionNode.Attributes.GetNamedItem("ApplicationNumber").Value);
                int lCalcAppNumber = lAppNumber + (lOpenKnxId << 8);
                lApplicationProgramNode.Attributes.GetNamedItem("ApplicationNumber").Value = lCalcAppNumber.ToString();
                int lAppVersion = 1;
                if (!int.TryParse(lMcVersionNode.Attributes.GetNamedItem("ApplicationVersion").Value, out lAppVersion))
                    Program.Message(true, "ApplicationVersion could not be parsed, given value was {0}", lMcVersionNode.Attributes.GetNamedItem("ApplicationVersion").Value);
                lApplicationProgramNode.Attributes.GetNamedItem("ApplicationVersion").Value = lAppVersion.ToString();
                string lReplVersions = lMcVersionNode.Attributes.GetNamedItem("ReplacesVersions").Value;
                if (lReplVersions == "") lReplVersions = "0";
                lApplicationProgramNode.Attributes.GetNamedItem("ReplacesVersions").Value = lReplVersions;
                int lAppRevision = 0;
                if (!int.TryParse(lMcVersionNode.Attributes.GetNamedItem("ApplicationRevision").Value, out lAppRevision))
                    Program.Message(true, "ApplicationRevision could not be parsed, given value was {0}", lMcVersionNode.Attributes.GetNamedItem("ApplicationRevision").Value);
                int lFirmwareRevision = -1;
                if (!int.TryParse(lMcVersionNode.NodeAttr("FirmwareRevision", "-1"), out lFirmwareRevision))
                    Program.Message(true, "FirmwareRevision could not be parsed, given value was {0}", lMcVersionNode.Attributes.GetNamedItem("FirmwareRevision").Value);
                // now we calculate according versioning verification string
                int lDerivedVersion = lAppVersion - lAppRevision;
                lInlineData = string.Format("0000{0:X4}{1:X2}00", lCalcAppNumber, lDerivedVersion);
                lVersionMessage = string.Format("{0}.{1}", (lDerivedVersion >> 4), (lDerivedVersion & 0x0F));
                // XmlNode lLdCtrlCompareProp = iTargetNode.SelectSingleNode("//LdCtrlCompareProp");
                // if (lLdCtrlCompareProp != null) {
                //     lLdCtrlCompareProp.Attributes.GetNamedItem("InlineData").Value = lInlineData;
                // }
                // we create a comment from version node
                string lVersion = " " + string.Join(" ", lMcVersionNode.OuterXml.Split().Skip(1).SkipLast(2)) + " ";
                XmlNode lVersionComment = ((XmlDocument)iTargetNode).CreateComment(lVersion);
                lMcVersionNode.ParentNode.ReplaceChild(lVersionComment, lMcVersionNode);
                lWithVersions = true; // we have to deal with versions
                // finally, we make some version info available in header file
                lVersionInformation.AppendFormat("#define MAIN_OpenKnxId 0x{0:X2}", lOpenKnxId);
                lVersionInformation.AppendLine();
                lVersionInformation.AppendFormat("#define MAIN_ApplicationNumber {0}", lAppNumber);
                lVersionInformation.AppendLine();
                lVersionInformation.AppendFormat("#define MAIN_ApplicationVersion {0}", lAppVersion - lAppRevision);
                lVersionInformation.AppendLine();
                if (lFirmwareRevision >= 0)
                {
                    lVersionInformation.AppendFormat("#define MAIN_FirmwareRevision {0}", lFirmwareRevision);
                    lVersionInformation.AppendLine();
                }
                lVersionInformation.AppendFormat("#define MAIN_ApplicationEncoding {0}", """iso-8859-15""");
                lVersionInformation.AppendLine();
            }
            // change all Id-Attributes / renumber ParameterSeparator and ParameterBlock
            string lApplicationId = lApplicationProgramNode.Attributes.GetNamedItem("Id").Value;
            int lApplicationNumber = -1;
            bool lIsInt = int.TryParse(lApplicationProgramNode.Attributes.GetNamedItem("ApplicationNumber").Value, out lApplicationNumber);
            int lApplicationVersion = -1;
            lIsInt = int.TryParse(lApplicationProgramNode.Attributes.GetNamedItem("ApplicationVersion").Value, out lApplicationVersion);
            XmlNode lReplacesVersionsAttribute = lApplicationProgramNode.Attributes.GetNamedItem("ReplacesVersions");
            string lOldId = lApplicationId;//.Replace("M-00FA_A", ""); // CalculateId(1, 1);
            if (lOldId.StartsWith("M-")) lOldId = lOldId.Substring(8);
            string lNewId = CalculateId(lApplicationNumber, lApplicationVersion);
            if (lOldId == "%AID%") lNewId = "M-00FA_A" + lNewId;

            // support ModuleCopy
            ExtendedEtsSupport.AddEtsExtensions(this, lApplicationVersion, lApplicationNumber);

            int lParameterSeparatorCount = 1;
            int lParameterBlockCount = 1;
            // Baggages handling
            PreprocessBaggages(iTargetNode);
            ReplaceBaggages(iTargetNode);
            ReplaceExtensions(iTargetNode);
            XmlNodeList lAttrs;
            lAttrs = iTargetNode.SelectNodes("//*/@*[contains(.,'%AID%')]");
            if (lAttrs.Count == 0)
                lAttrs = iTargetNode.SelectNodes("//*/@*[string-length() > '13']");
            foreach (XmlNode lAttr in lAttrs)
            {
                if (lAttr.Value != null)
                {
                    lAttr.Value = lAttr.Value.Replace(lOldId, lNewId);
                    if (ProcessInclude.Renumber)
                    {
                        // ParameterSeparator is renumbered
                        if (lAttr.Value.Contains("_PS-"))
                        {
                            lAttr.Value = string.Format("{0}-{1}", lAttr.Value.Substring(0, lAttr.Value.LastIndexOf('-')), lParameterSeparatorCount);
                            lParameterSeparatorCount += 1;
                        }
                        // ParameterBlock is renumbered
                        if (lAttr.Value.Contains("_PB-"))
                        {
                            lParameterBlockCount = RenumberParameterBlock(lParameterBlockCount, lAttr);
                        }
                    }
                    // lAttr.Value = lAttr.Value.Replace("_PST-", "_PS-");
                    // lAttr.Value = lAttr.Value.Replace("_PBT-", "_PB-");
                }
            }
            // move OAM-specific definitions to visible channel (common)
            XmlNodeList lMoveToCommon = mDocument.SelectNodes("//ParameterBlock[@oknxp:moveToCommon='true']", nsmgr);
            if (lMoveToCommon != null && lMoveToCommon.Count > 0)
            {
                XmlNode lCommonChannel = mDocument.SelectSingleNode("//Dynamic/Channel[@Number='BASE']");
                foreach (XmlNode lBlock in lMoveToCommon)
                {
                    if (lBlock.ParentNode.ChildNodes.Count == 1)
                    {
                        // just remove the whole channel, if this was the only block
                        lBlock.ParentNode.ParentNode.RemoveChild(lBlock.ParentNode);
                    }
                    lBlock.ParentNode.RemoveChild(lBlock);
                    var lAttribute = lBlock.SelectSingleNode("@oknxp:moveToCommon", nsmgr);
                    lBlock.Attributes.RemoveNamedItem(lAttribute.Name);
                    lCommonChannel.InsertAfter(lBlock, null);
                }
            }

            // remove empty elements (without attributes and without children)
            XmlNodeList lEmptyElements = iTargetNode.SelectNodes("//*[not(node()) and not(@*)]");
            foreach (XmlNode lEmptyElement in lEmptyElements)
                lEmptyElement.ParentNode.RemoveChild(lEmptyElement);
            // process Enumeration-IDs (%ENID%)
            XmlNodeList lEnumerations = iTargetNode.SelectNodes("//ParameterType//*[@Id='%ENID%']");
            foreach (XmlNode lEnumeration in lEnumerations)
            {
                XmlNode lIdNode = lEnumeration.Attributes.GetNamedItem("Id");
                XmlNode lParameterType = lEnumeration.ParentNode.ParentNode;
                lIdNode.Value = lParameterType.NodeAttr("Id") + "_EN-" + lEnumeration.NodeAttr("Value");
            }
            Console.WriteLine("- ApplicationNumber: {0:X4} ({0}), ApplicationVersion: {1}, old ID is: {3}, new (calculated) ID is: {2}", lApplicationNumber, lApplicationVersion, lNewId, lOldId);
            if (lInlineData != "") Console.WriteLine("- Calculated InlineData for Versioning: {0}", lInlineData);
            // create registration entry
            XmlNode lHardwareNode = iTargetNode.SelectSingleNode("/KNX/ManufacturerData/Manufacturer/Hardware/Hardware");
            int lHardwareVersion = 1;
            int.TryParse(lHardwareNode.Attributes.GetNamedItem("VersionNumber").Value, out lHardwareVersion);
            string lSerialNumber = lHardwareNode.Attributes.GetNamedItem("SerialNumber").Value;
            XmlNode lRegistrationNumber = iTargetNode.SelectSingleNode("/KNX/ManufacturerData/Manufacturer/Hardware/Hardware/Hardware2Programs/Hardware2Program/RegistrationInfo/@RegistrationNumber");
            if (lRegistrationNumber == null)
            {
                Console.WriteLine("- Missing 'RegistrationVersion', no updates via 'ReplacesVersion' in ETS possible!");
            }
            else
            {
                lRegistrationNumber.Value = string.Format("0001/{0}{1}", lHardwareVersion, lApplicationVersion);
                Console.WriteLine("- RegistrationVersion is: {0}", lRegistrationNumber.Value);
            }
            // Add ReplacesVersions 
            if (lReplacesVersionsAttribute != null)
            {
                string lReplacesVersions = lReplacesVersionsAttribute.Value;
                Console.WriteLine("- ReplacesVersions entry is: {0}", lReplacesVersions);
                // string lOldVersion = string.Format(" {0}", lApplicationVersion - 1);
                // if (!lReplacesVersions.Contains(lOldVersion) && lReplacesVersions != (lApplicationVersion - 1).ToString()) lReplacesVersionsAttribute.Value += lOldVersion;
            }
            // set the right Size attributes
            // XmlNodeList lNodes = iTargetNode.SelectNodes("(//RelativeSegment | //LdCtrlRelSegment | //LdCtrlWriteRelMem)[@Size]");
            string lSize = ParameterBlockSize.ToString();
            // foreach (XmlNode lNode in lNodes) {
            //     lNode.Attributes.GetNamedItem("Size").Value = lSize;
            // }
            // Console.WriteLine("- Final parameter size is {0}", lSize);
            string lPartId = CalculateId(lApplicationNumber, lApplicationVersion);
            string lHardwareId = "M-00FA_H-%SerialNumberEncoded%-%HardwareVersionEncoded%";
            string lHardware2ProgramId = lHardwareId + "_HP" + lPartId;
            string lCatalogItemId = lHardware2ProgramId + "_CI-%OrderNumberEncoded%-1";
            string lProductId = lHardwareId + "_P-%OrderNumberEncoded%";

            string lHardwareVersionEncoded = Program.GetEncoded(lHardwareVersion.ToString());
            string lSerialNumberEncoded = Program.GetEncoded(lSerialNumber);
            XmlNode lOrderNumberAttribute = iTargetNode.SelectSingleNode("/KNX/ManufacturerData/Manufacturer/Hardware/Hardware/Products/Product/@OrderNumber");
            string lOrderNumberEncoded = Program.GetEncoded(lOrderNumberAttribute.Value);
            if (lMcVersionNode != null)
            {
                lVersionInformation.AppendFormat("#define MAIN_ParameterSize {0}", lSize);
                lVersionInformation.AppendLine();
                lVersionInformation.AppendFormat("#define MAIN_MaxKoNumber {0}", sMaxKoNumber);
                lVersionInformation.AppendLine();
                lVersionInformation.AppendFormat("#define MAIN_OrderNumber \"{0}\"", lOrderNumberAttribute.Value);
                lVersionInformation.AppendLine();
                mHeaderGenerated.Insert(0, lVersionInformation);

                // finally we add also FIRMWARE_NAME
                XmlNode lCatalogItemNode = iTargetNode.SelectSingleNode("/KNX/ManufacturerData/Manufacturer/Catalog/CatalogSection/CatalogItem");
                string lCatalogItemName = "";
                if (lCatalogItemNode != null)
                {
                    lCatalogItemName = lCatalogItemNode.NodeAttr("Name");
                }
                if (lCatalogItemName != "")
                {
                    lCatalogItemName = lCatalogItemName.Replace("OpenKNX: ", "");
                    lCatalogItemName = NormalizeString(lCatalogItemName);
                    StringBuilder lFirmwareName = new();
                    lFirmwareName.AppendFormat("#define MAIN_FirmwareName \"{0}\"", lCatalogItemName);
                    lFirmwareName.AppendLine();
                    mHeaderGenerated.Insert(0, lFirmwareName);
                }

            }
            // XmlNodeList lCatalog = iTargetNode.SelectNodes("/KNX/ManufacturerData/Manufacturer/Catalog/descendant::*/@*");
            // XmlNodeList lHardware = iTargetNode.SelectNodes("/KNX/ManufacturerData/Manufacturer/Hardware/descendant::*/@*");
            // XmlNodeList lStatic = iTargetNode.SelectNodes("/KNX/ManufacturerData/Manufacturer/ApplicationPrograms/ApplicationProgram/Static/descendant::*/@*");
            // var lNodes = lCatalog.Cast<XmlNode>().Concat(lHardware.Cast<XmlNode>().Concat<XmlNode>(lStatic.Cast<XmlNode>())).ToList();
            var lNodes = iTargetNode.SelectNodes("/KNX/ManufacturerData/Manufacturer/*[self::Catalog or self::Hardware or self::Languages or self::ApplicationPrograms/ApplicationProgram/Static]/descendant::*/@*[contains(.,'%')]");
            foreach (XmlNode lNode in lNodes)
            {
                if (lNode.Value != null)
                {
                    string lValue = lNode.Value;
                    // these need to be replaced first
                    lValue = lValue.Replace("%CatalogItemId%", lCatalogItemId);
                    lValue = lValue.Replace("%ProductId%", lProductId);
                    lValue = lValue.Replace("%HardwareId%", lHardwareId);
                    lValue = lValue.Replace("%Hardware2ProgramId%", lHardware2ProgramId);
                    lValue = lValue.Replace("%MemorySize%", lSize);
                    // now we replace encoded values
                    lValue = lValue.Replace("%HardwareVersionEncoded%", lHardwareVersionEncoded);
                    lValue = lValue.Replace("%OrderNumberEncoded%", lOrderNumberEncoded);
                    lValue = lValue.Replace("%SerialNumberEncoded%", lSerialNumberEncoded);
                    lValue = lValue.Replace("%VersionCheck%", lInlineData);
                    lValue = lValue.Replace("%VersionMessage%", lVersionMessage);
                    // TODO: solve this in a more generic way
                    lValue = lValue.Replace("%MaxKoNumber%", sMaxKoNumber.ToString());
                    lValue = lValue.Replace("-%MaxKoNumber%", (-sMaxKoNumber).ToString());
                    lValue = lValue.Replace("%MaxKoNumber-1%", (sMaxKoNumber - 1).ToString());
                    // lValue = lValue.Replace("-%MaxKoNumber-1%", (-sMaxKoNumber + 1).ToString());
                    lNode.Value = lValue;
                }
            }
            return lWithVersions;
        }

        static readonly Dictionary<string, string> sNormalizeChars = new() { { "Ä", "Ae" }, { "ä", "ae" }, { "Ö", "Oe" }, { "ö", "oe" }, { "Ü", "Ue" }, { "ü", "ue" }, { "ß", "ss" } };


        public static string NormalizeString(string iSource, string iForbiddenCharReplace = "-")
        {
            string lResult = iSource;

            // replace special characters
            foreach (var lEntry in sNormalizeChars)
                lResult = lResult.Replace(lEntry.Key, lEntry.Value);

            // get rid of forbidden characters
            foreach (char lChar in iSource)
                if (lChar < 32 || lChar > 126)
                    lResult = lResult.Replace(lChar.ToString(), iForbiddenCharReplace);

            // replace multiple dashes with single dashes
            lResult = FastRegex.DocChapterWhitespaces().Replace(lResult, "-");

            // get rid of whitespaces at start and end
            lResult = lResult.Trim(' ', '-');
            return lResult;
        }


        Dictionary<string, string> mBaggageId = new Dictionary<string, string>();
        private void PreprocessBaggages(XmlNode iTargetNode)
        {
            XmlNode lBaggages = iTargetNode.SelectSingleNode(@"//Baggages", nsmgr);
            bool lWithHelp = false;
            bool lWithIcons = false;
            string lPath = "";
            string lFileName = "";
            XmlNode lIdNode = null;

            bool HandleZipFile(string iZipPattern, ref bool eZipType)
            {
                if (lIdNode.Value.StartsWith(iZipPattern))
                {
                    if (eZipType)
                    {
                        // mark for removal
                        lIdNode.Value = "!!DELETE!!";
                        return true;
                    }
                    // the following happens just once
                    eZipType = true;
                    string lSourceDirName = Path.Combine(mCurrentDir, mBaggagesName, lPath, lFileName.Replace(".zip", ""));
                    string lTargetName = Path.Combine(mCurrentDir, mBaggagesName, lPath, lFileName);
                    // TODO: check if skip is ok
                    if (Directory.Exists(lSourceDirName))
                    {
                        System.IO.Compression.ZipFile.CreateFromDirectory(lSourceDirName, lTargetName);
                        // before we delete the underlying directories, we store some information
                        HashSet<string> lHashId = new();
                        var lFiles = Directory.EnumerateFiles(lSourceDirName);
                        foreach (var lFile in lFiles)
                        {
                            lHashId.Add(Path.GetFileNameWithoutExtension(lFile));
                        }
                        if (iZipPattern == "%FILE-HELP")
                        {
                            mBaggageHelpFileName = Path.Combine(lPath, lFileName);
                            mBaggageHelpId = lHashId;
                            ParseHelpFiles(lFiles);
                        }
                        else if (iZipPattern == "%FILE-ICONS")
                        {
                            mBaggageIconFileName = Path.Combine(lPath, lFileName);
                            mBaggageIconId = lHashId;
                        }
                        Directory.Delete(lSourceDirName, true);
                    }
                }
                return false;
            }

            if (lBaggages != null)
            {
                foreach (XmlNode lBaggage in lBaggages.ChildNodes)
                {
                    if (lBaggage.NodeType != XmlNodeType.Comment)
                    {
                        // We need to create according Id from Baggage filename
                        lPath = lBaggage.NodeAttr("TargetPath");
                        if (!System.OperatingSystem.IsWindows() && lPath.Contains("\\"))
                        {
                            // convert xml path back to OS specific path
                            lPath = lPath.Replace("\\", Path.DirectorySeparatorChar.ToString());
                            lPath = lPath.Replace(Path.AltDirectorySeparatorChar.ToString(), Path.DirectorySeparatorChar.ToString());
                        }
                        lFileName = lBaggage.NodeAttr("Name");
                        lIdNode = lBaggage.Attributes.GetNamedItem("Id");
                        if (HandleZipFile("%FILE-HELP", ref lWithHelp)) continue;
                        if (HandleZipFile("%FILE-ICONS", ref lWithIcons)) continue;

                        string lBaggageId = string.Format("M-00FA_BG-{0}-{1}", Program.GetEncoded(lPath), Program.GetEncoded(lFileName));
                        if (!mBaggageId.ContainsKey(lIdNode.Value))
                            mBaggageId.Add(lIdNode.Value, lBaggageId);
                        lIdNode.Value = lBaggageId;
                        DateTime lFileLastWrite = File.GetLastWriteTimeUtc(Path.Combine(mCurrentDir, mBaggagesName, lPath, lFileName));
                        string lIsoDateTime = lFileLastWrite.ToString("o", CultureInfo.InvariantCulture);
                        XmlNode lTimeInfo = lBaggage.SelectSingleNode("FileInfo/@TimeInfo", nsmgr);
                        if (lTimeInfo != null && lTimeInfo.Value == "%DATETIME%")
                            lTimeInfo.Value = lIsoDateTime;
                    }
                }
                // duplicate zip-baggages are deleted
                XmlNodeList lDeletes = lBaggages.SelectNodes(@"Baggage[@Id ='!!DELETE!!']", nsmgr);
                foreach (XmlNode lDelete in lDeletes)
                {
                    lDelete.ParentNode.RemoveChild(lDelete);
                }
            }
        }

        /// <summary>
        /// Check if any of provided help files contains foreign links, this is forbidden in ETS
        /// </summary>
        /// <param name="lFiles">List of files to check</param>
        private static void ParseHelpFiles(IEnumerable<string> lFiles)
        {
            if (lFiles != null)
            {
                Regex lLinkPattern = FastRegex.LinkPattern();
                foreach (var lFileName in lFiles)
                {
                    using var lFile = File.OpenText(lFileName);
                    string lContent = lFile.ReadToEnd();
                    Match lMatch = lLinkPattern.Match(lContent);
                    if (lMatch.Success)
                    {
                        string lName = Path.GetFileNameWithoutExtension(lFileName);
                        if (lMatch.Value.StartsWith("!"))
                            Program.Message(true, "Baggage file {0} contains a file link '{1}', this ist not allowed in ETS!", lName, lMatch.Value);
                        else
                            Program.Message(false, "Baggage file {0} contains a link '{1}', this will not work in ETS!", lName, lMatch.Value);
                    }
                }
            }
        }

        private void ReplaceBaggages(XmlNode iTargetNode)
        {
            XmlNodeList lRefIds = iTargetNode.SelectNodes(@"//./@*[starts-with(.,'%FILE-')]", nsmgr);
            int lProjectNamespace = GetIdOfProjectNamespace((XmlDocument)iTargetNode);
            if (lRefIds != null)
            {
                foreach (XmlNode lRefId in lRefIds)
                {
                    if (mBaggageId.ContainsKey(lRefId.Value))
                    {
                        lRefId.Value = mBaggageId[lRefId.Value];
                    }
                    if (lProjectNamespace == 14)
                    {
                        if (lRefId.Name == "ContextHelpFile")
                        {
                            lRefId.Value = mBaggageHelpFileName;
                        }
                        else
                        if (lRefId.Name == "IconFile")
                        {
                            lRefId.Value = mBaggageIconFileName;
                        }
                    }
                }
            }
        }

        private void ReplaceExtensions(XmlNode iTargetNode)
        {
            XmlNode lExtensions = iTargetNode.SelectSingleNode(@"//Extension", nsmgr);
            if (lExtensions != null)
            {
                lExtensions.RemoveAll();
                foreach (var lBaggageId in mBaggageId)
                {
                    XmlNode lBaggage = ((XmlDocument)iTargetNode).CreateElement("Baggage");
                    lExtensions.AppendChild(lBaggage);
                    XmlAttribute lRefId = ((XmlDocument)iTargetNode).CreateAttribute("RefId");
                    lBaggage.Attributes.Append(lRefId);
                    lRefId.Value = lBaggageId.Value;
                }
            }
        }

        Dictionary<string, string> mParameterBlockMap = new Dictionary<string, string>();
        string mLastParameterBlockId = "";
        private int RenumberParameterBlock(int lParameterBlockCount, XmlNode lAttr)
        {
            // for inline parameter blocks like grid and table we need
            // the same PB number in all subblocks.
            // we assume, that the iterator first provides the PB and later on the subPB, 
            // before the next PB is offered.
            int lPos = lAttr.Value.IndexOf("_PB-");
            string lValue = "";
            // if (lAttr.Value.Substring(lPos + 4).Contains('_'))
            if (lAttr.Value[(lPos + 4)..].Contains('_'))
            {
                // this is a subblock, we assume, that its main block was already renumbered
                // lValue = lAttr.Value.Substring(0, lAttr.Value.LastIndexOf("_"));
                lValue = lAttr.Value[..lAttr.Value.LastIndexOf("_")];
                // lAttr.Value = lAttr.Value.Replace(lValue, mParameterBlockMap[lValue]);
                lAttr.Value = lAttr.Value.Replace(lValue, mLastParameterBlockId);
            }
            else
            {
                // it is a main block, renumber it and store the result
                lValue = string.Format("{0}-{1}", lAttr.Value.Substring(0, lAttr.Value.LastIndexOf('-')), lParameterBlockCount);
                // if (mParameterBlockMap.ContainsKey(lAttr.Value)) {
                //     // number collision? Further checks should find this out
                // } else {
                //     mParameterBlockMap.Add(lAttr.Value, lValue);
                // }
                mLastParameterBlockId = lValue;
                lAttr.Value = lValue;
                lParameterBlockCount += 1;
            }
            return lParameterBlockCount;
        }

        public static int CalcParamSize(XmlNode iParameter, bool iIsInUnion = false)
        {
            int lResult = 0;
            if (sParameterTypes.Count > 0)
            {
                // we calculate the size only, if the parameter uses some memory in the device storage
                XmlNode lMemory = iParameter.SelectSingleNode("Memory");
                if (iIsInUnion || lMemory != null)
                {
                    XmlNode lSizeNode = null;
                    XmlNode lSizeInBitAttribute = null;
                    if (iParameter.Name == "Union")
                    {
                        lSizeNode = iParameter;
                        lSizeInBitAttribute = lSizeNode.Attributes.GetNamedItem("SizeInBit");
                    }
                    else
                    {
                        string lParameterTypeId = iParameter.NodeAttr("ParameterType");
                        lSizeNode = ParameterType(lParameterTypeId);
                        // if (lSizeNode != null) lSizeInBitAttribute = lSizeNode.SelectSingleNode("*/@SizeInBit");
                        if (lSizeNode != null) lSizeInBitAttribute = lSizeNode.Attributes.GetNamedItem("SizeInBit");
                    }
                    if (lSizeNode != null)
                    {
                        if (lSizeInBitAttribute != null)
                        {
                            lResult = 8;
                            bool lIsInt = int.TryParse(lSizeInBitAttribute.Value, out lResult);
                            lResult = (lResult - 1) / 8 + 1;
                            if (!lIsInt)
                            {
                                Program.Message(true, "Wrong SizeInBit value in ", lSizeNode.NodeAttr("Name"));
                            }
                        }
                        else
                        {
                            if (lSizeNode.Name == "ParameterType")
                                lSizeNode = lSizeNode.SelectSingleNode("*[not(comment())]");
                            if (lSizeNode.Name == "TypeFloat")
                                lResult = 4;
                            else if (lSizeNode.Name == "TypeIPAddress")
                                lResult = 4;
                            else if (lSizeNode.Name == "TypeColor")
                                lResult = 3;
                            else if (lSizeNode.Name == "TypeDate")
                                lResult = 3;
                        }
                    }
                }
            }
            return lResult;
        }

        public int CalcParamSize(XmlNodeList iParameterList)
        {
            int lResult = 0;
            foreach (XmlNode lNode in iParameterList)
            {
                int lSize = CalcParamSize(lNode);
                if (lSize > 0)
                {
                    // at this point we know there is a memory reference, we look at the offset
                    XmlNode lOffset = lNode.SelectSingleNode("*/@Offset");
                    lResult = Math.Max(lResult, int.Parse(lOffset.Value) + lSize);
                }
            }
            return lResult;
        }

        private static string ReplaceChannelName(string iName)
        {
            string lResult = iName;
            // if (iName.Contains("%C%")) lResult = iName.Remove(0, iName.IndexOf("%C%") + 3);
            lResult = iName.Replace("%C%", "");
            lResult = lResult.Replace(" ", "_");
            return lResult;
        }

        private void ExportHeaderKoStart(DefineContent iDefine, StringBuilder cOut, string iHeaderPrefixName)
        {
            if (!mHeaderKoStartGenerated)
            {
                StringBuilder lOut = new StringBuilder();
                mHeaderKoStartGenerated = ExportHeaderKo(iDefine, lOut, iHeaderPrefixName);
                if (mHeaderKoStartGenerated && iDefine.IsParameter)
                {
                    cOut.AppendLine("// Communication objects with single occurrence");
                    cOut.Append(lOut);
                }
            }
        }

        private static int MaxKoNumber(XmlNodeList iComObjects)
        {
            int lResult = 0;
            foreach (XmlNode lKo in iComObjects)
            {
                string lNumberString = lKo.NodeAttr("Number");
                lNumberString = lNumberString.Replace("%", "");
                lNumberString = lNumberString.Replace("K", "");
                if (int.TryParse(lNumberString, out int lValue))
                    lResult = Math.Max(lResult, lValue);
            }
            if (lResult < iComObjects.Count)
                return iComObjects.Count;
            else if (iComObjects.Count == 0)
                return 0;
            else
                return ++lResult;
        }

        private void ExportHeaderKoBlock(DefineContent iDefine, StringBuilder cOut, string iHeaderPrefixName)
        {
            if (!mHeaderKoBlockGenerated)
            {
                XmlNodeList lComObjects = mDocument.SelectNodes("//ApplicationProgram/Static/ComObjectTable/ComObject");
                mKoBlockSize = MaxKoNumber(lComObjects);
                if (iDefine.IsTemplate && iDefine.KoBlockSize > 0 && iDefine.KoBlockSize != mKoBlockSize) throw new Exception("Different KoBlockSize!");
                if (iDefine.IsTemplate) iDefine.KoBlockSize = mKoBlockSize;
                StringBuilder lOut = new();
                mHeaderKoBlockGenerated = ExportHeaderKo(iDefine, lOut, iHeaderPrefixName);
                if (mHeaderKoBlockGenerated)
                {
                    if (iDefine.IsTemplate)
                    {
                        cOut.AppendLine("// deprecated");
                        cOut.AppendFormat("#define {0}KoOffset {1}", iHeaderPrefixName, KoOffset);
                        cOut.AppendLine();
                        cOut.AppendLine();
                        cOut.AppendLine("// Communication objects per channel (multiple occurrence)");
                        cOut.AppendFormat("#define {0}KoBlockOffset {1}", iHeaderPrefixName, KoOffset);
                        cOut.AppendLine();
                        cOut.AppendFormat("#define {0}KoBlockSize {1}", iHeaderPrefixName, mKoBlockSize);
                        cOut.AppendLine();
                        cOut.AppendLine();
                        cOut.AppendFormat("#define {0}KoCalcNumber(index) (index + {0}KoBlockOffset + _channelIndex * {0}KoBlockSize)", iHeaderPrefixName);
                        cOut.AppendLine();
                        cOut.AppendFormat("#define {0}KoCalcIndex(number) ((number >= {0}KoCalcNumber(0) && number < {0}KoCalcNumber({0}KoBlockSize)) ? (number - {0}KoBlockOffset) % {0}KoBlockSize : -1)", iHeaderPrefixName);
                        cOut.AppendLine();
                        cOut.AppendFormat("#define {0}KoCalcChannel(number) ((number >= {0}KoBlockOffset && number < {0}KoBlockOffset + {0}ChannelCount * {0}KoBlockSize) ? (number - {0}KoBlockOffset) / {0}KoBlockSize : -1)", iHeaderPrefixName);
                        cOut.AppendLine();
                        cOut.AppendLine();
                    }
                    cOut.Append(lOut);
                }
            }
        }

        private bool ExportHeaderKo(DefineContent iDefine, StringBuilder cOut, string iHeaderPrefixName)
        {
            XmlNodeList lNodes = mDocument.SelectNodes("//ApplicationProgram/Static/ComObjectTable/ComObject");
            StringBuilder lOut = new();

            bool lResult = false;
            foreach (XmlNode lNode in lNodes)
            {
                string lComment = "// " + lNode.Attributes.GetNamedItem("Text").Value;
                string lNumber = ReplaceKoTemplate(iDefine, lNode.NodeAttr("Number"), 1, null, true);
                string lName = ReplaceChannelName(lNode.NodeAttr("Name"));
                cOut.AppendFormat("#define {0}Ko{1} {2}", iHeaderPrefixName, lName, lNumber);
                cOut.AppendLine();
                lOut.AppendLine(RemoveControlChars(lComment));
                if (iDefine.IsTemplate)
                    lOut.AppendFormat("#define Ko{0}{3,-35} (knx.getGroupObject({0}KoCalcNumber({0}Ko{1})))", iHeaderPrefixName, lName, lNumber, lName);
                else
                    lOut.AppendFormat("#define Ko{0}{3,-35} (knx.getGroupObject({0}Ko{1}))", iHeaderPrefixName, lName, lNumber, lName);
                lOut.AppendLine();
                // lOut.AppendFormat("#define Ko{0}{1,-35} Ko{0}{3}", iHeaderPrefixName, lName, lNumber, lName + "(0)");
                // lOut.AppendLine();
                lResult = true;
            }
            if (lResult)
            {
                cOut.AppendLine();
                cOut.Append(lOut);
                cOut.AppendLine();
            }
            return lResult;
        }

        private void ExportHeaderParameterStart(XmlNodeList iNodes, DefineContent iDefine, StringBuilder cOut, string iHeaderPrefixName)
        {
            if (!mHeaderParameterStartGenerated && iDefine.IsParameter)
            {
                cOut.AppendLine("// Parameter with single occurrence");
                ExportHeaderParameter(iNodes, iDefine, cOut, iHeaderPrefixName, iDefine.IsParameter);
                mHeaderParameterStartGenerated = true;
            }
        }

        private void ExportHeaderParameterBlock(XmlNodeList iNodes, DefineContent iDefine, StringBuilder cOut, string iHeaderPrefixName)
        {
            if (!mHeaderParameterBlockGenerated)
            {
                if (iDefine.IsTemplate)
                {
                    cOut.AppendFormat("#define {0}ChannelCount {1}", iHeaderPrefixName, ChannelCount);
                    cOut.AppendLine();
                    cOut.AppendLine();
                    cOut.AppendLine("// Parameter per channel");
                    cOut.AppendFormat("#define {0}ParamBlockOffset {1}", iHeaderPrefixName, ParameterBlockOffset);
                    cOut.AppendLine();
                    cOut.AppendFormat("#define {0}ParamBlockSize {1}", iHeaderPrefixName, ParameterBlockSize);
                    cOut.AppendLine();
                    cOut.AppendFormat("#define {0}ParamCalcIndex(index) (index + {0}ParamBlockOffset + _channelIndex * {0}ParamBlockSize)", iHeaderPrefixName);
                    cOut.AppendLine();
                    cOut.AppendLine();
                }
                int lSize = ExportHeaderParameter(iNodes, iDefine, cOut, iHeaderPrefixName, iDefine.IsParameter);
                // if (lSize != mParameterBlockSize) throw new ArgumentException(string.Format("ParameterBlockSize {0} calculation differs from header file calculated ParameterBlockSize {1}", mParameterBlockSize, lSize));
                mHeaderParameterBlockGenerated = true;
            }
        }

        static string RemoveControlChars(string iText)
        {
            return iText.Replace("\n", "");
            // return new string(iText.Where(c => !char.IsControl(c)).ToArray());
        }

        public int ExportHeaderParameter(XmlNodeList iNodes, DefineContent iDefine, StringBuilder cOut, string iHeaderPrefixName, bool iWithAbsoluteOffset, string iChannelCalculation = "", string iChannelArgs = "")
        {
            int lMaxSize = 0;
            StringBuilder lOut = new StringBuilder();
            // XmlNodeList lNodes = mDocument.SelectNodes("//ApplicationProgram/Static/Parameters/Parameter|//ApplicationProgram/Static/Parameters/Union/Parameter");
            foreach (XmlNode lNode in iNodes)
            {
                XmlNode lMemoryNode;
                string lName = lNode.Attributes.GetNamedItem("Name").Value;
                lName = ReplaceChannelName(lName);
                lMemoryNode = lNode.ParentNode;
                if (lMemoryNode != null && lMemoryNode.Name != "Union")
                {
                    lMemoryNode = lNode;
                }
                XmlNode lMemory = lMemoryNode.FirstChild;
                while (lMemory != null && lMemory.NodeType == XmlNodeType.Comment) lMemory = lMemory.NextSibling;
                if (lMemory != null && sParameterTypes.Count > 0)
                {
                    // parse parameter type to fill additional information
                    string lParameterTypeId = lNode.NodeAttr("ParameterType");
                    XmlNode lParameterType = ParameterType(lParameterTypeId);
                    int lBits = 0;
                    int lBitBaseSize = 0;
                    string lType = "";
                    string lKnxAccessMethod = "";
                    bool lDirectType = false;
                    if (lParameterType != null)
                    {
                        XmlNode lBitsAttribute = lParameterType.Attributes.GetNamedItem("SizeInBit");
                        if (lBitsAttribute != null) lBits = int.Parse(lBitsAttribute.Value);
                        XmlNode lTypeAttribute = lParameterType.Attributes.GetNamedItem("Type");
                        if (lParameterType.Name == "TypeNumber" || lParameterType.Name == "TypeRestriction")
                        {
                            if (lTypeAttribute != null) lType = lTypeAttribute.Value;
                            if (lBits <= 8)
                            {
                                lBitBaseSize = 8;
                                lKnxAccessMethod = "knx.paramByte({0})";
                            }
                            else if (lBits <= 16)
                            {
                                lBitBaseSize = 16;
                                lKnxAccessMethod = "knx.paramWord({0})";
                            }
                            else if (lBits <= 32)
                            {
                                lBitBaseSize = 32;
                                lKnxAccessMethod = "knx.paramInt({0})";
                            }
                            if (lType == "signedInt")
                            {
                                lType = "int";
                                lKnxAccessMethod = string.Format("({0}{1}_t){2}", lType, lBitBaseSize, lKnxAccessMethod);
                            }
                            else if (lType == "unsignedInt")
                            {
                                lType = "uint";
                            }
                            else
                            {
                                lType = "enum";
                            }
                        }
                        else if (lParameterType.Name == "TypeText")
                        {
                            lType = string.Format("char*, {0} Byte", lBits / 8);
                            lKnxAccessMethod = "knx.paramData({0})";
                            lDirectType = true;
                        }
                        else if (lParameterType.Name == "TypeFloat")
                        {
                            lType = "float";
                            lBits = 32;
                            lBitBaseSize = 32;
                            lKnxAccessMethod = "knx.paramFloat({0}, Float_Enc_IEEE754Single)";
                            lDirectType = true;
                        }
                        else if (lParameterType.Name == "TypeColor")
                        {
                            lType = "color, uint, 3 Byte";
                            lBits = 24;
                            lBitBaseSize = 32;
                            lKnxAccessMethod = "knx.paramInt({0})";
                            lDirectType = true;
                        }
                        else if (lParameterType.Name == "TypeDate")
                        {
                            lType = "date, uint, 3 Byte";
                            lBits = 24;
                            lBitBaseSize = 32;
                            lKnxAccessMethod = "knx.paramInt({0})";
                            lDirectType = true;
                        }
                        else if (lParameterType.Name == "TypeIPAddress")
                        {
                            lType = "IP address, 4 Byte";
                            lBits = 32;
                            lBitBaseSize = 32;
                            lKnxAccessMethod = "knx.paramInt({0})";
                            lDirectType = true;
                        }
                    }
                    int.TryParse(lMemory.Attributes.GetNamedItem("Offset").Value, out int lOffset);
                    int.TryParse(lMemory.Attributes.GetNamedItem("BitOffset").Value, out int lBitOffset);
                    // Offset and BitOffset might be also defined in Parameter
                    XmlNode lParamOffsetNode = lNode.Attributes.GetNamedItem("Offset");
                    if (lParamOffsetNode != null) lOffset += int.Parse(lParamOffsetNode.Value);
                    if (iWithAbsoluteOffset && !AbsoluteSingleParameters) lOffset += ParameterBlockOffset;
                    XmlNode lParamBitOffsetNode = lNode.Attributes.GetNamedItem("BitOffset");
                    if (lParamBitOffsetNode != null) lBitOffset += int.Parse(lParamBitOffsetNode.Value);
                    lMaxSize = Math.Max(lMaxSize, lOffset + (lBits - 1) / 8 + 1);
                    string lChannelCalculation = "{3}{0}";
                    if (iDefine.IsTemplate) lChannelCalculation = (iChannelCalculation == "") ? "{3}ParamCalcIndex({3}{0})" : iChannelCalculation;
                    string lKnxArgument = string.Format(lKnxAccessMethod, lChannelCalculation);
                    bool lIsOut = false;
                    string lOutput = "";
                    string lTimeOutput = "";
                    string lComment = "// " + lNode.Attributes.GetNamedItem("Text").Value;
                    if (lBits < lBitBaseSize || lType == "enum")
                    {
                        //output for bit based parameters 
                        int lShift = (lBitBaseSize - lBits - lBitOffset);
                        string lSubType = string.Format("{0} Bit{1}, Bit {2}", lBits, (lBits == 1) ? "" : "s", (lBitBaseSize - 1 - lBitOffset));
                        if (lBits > 1) lSubType = string.Format("{0}-{1}", lSubType, lShift);
                        // new time base handling
                        if (lParameterTypeId.Contains("_PT-DelayTime"))
                        {
                            lTimeOutput = string.Format("#define Param{3}{4,-35} (paramDelay(" + lKnxArgument + "))", lName, lOffset, lSubType, iHeaderPrefixName, lName + "MS" + iChannelArgs);
                        }
                        cOut.AppendFormat("#define {3}{0,-35} {1,2}      // {2}", lName, lOffset, lSubType, iHeaderPrefixName);
                        if (lBits < lBitBaseSize && lShift >= 0)
                        {
                            cOut.AppendLine();
                            int lMask = ((int)Math.Pow(2, lBits) - 1) << lShift;
                            cOut.AppendFormat("#define     {0}{1}Mask 0x{2:X2}", iHeaderPrefixName, lName, lMask);
                            cOut.AppendLine();
                            cOut.AppendFormat("#define     {0}{1}Shift {2}", iHeaderPrefixName, lName, lShift);
                            if (lBits == 1)
                                lOutput = string.Format("#define Param{3}{4,-35} ((bool)(" + lKnxArgument + " & {3}{0}Mask))", lName, lOffset, lSubType, iHeaderPrefixName, lName + iChannelArgs);
                            else if (lShift == 0)
                                lOutput = string.Format("#define Param{3}{4,-35} (" + lKnxArgument + " & {3}{0}Mask)", lName, lOffset, lSubType, iHeaderPrefixName, lName + iChannelArgs);
                            else
                                lOutput = string.Format("#define Param{3}{4,-35} ((" + lKnxArgument + " & {3}{0}Mask) >> {3}{0}Shift)", lName, lOffset, lSubType, iHeaderPrefixName, lName + iChannelArgs);
                            lIsOut = true;
                        }
                        else if (lType == "enum")
                        {
                            lOutput = string.Format("#define Param{3}{4,-35} (" + lKnxArgument + ")", lName, lOffset, lSubType, iHeaderPrefixName, lName + iChannelArgs);
                            lIsOut = true;
                        }
                    }
                    else if (lDirectType)
                    {
                        cOut.AppendFormat("#define {3}{0,-35} {1,2}      // {2}", lName, lOffset, lType, iHeaderPrefixName);
                        lOutput = string.Format("#define Param{3}{4,-35} (" + lKnxArgument + ")", lName, lOffset, lType, iHeaderPrefixName, lName + iChannelArgs);
                        if (lType.StartsWith("char*"))
                        {
                            cOut.AppendLine();
                            cOut.AppendFormat("#define     {0}{1}Length {2}", iHeaderPrefixName, lName, lBits / 8);
                            lKnxArgument = lKnxArgument[..^1] + ", {3}{0}Length)";
                            lKnxArgument = lKnxArgument.Replace("paramData", "paramString");
                            lOutput += string.Format("\n#define Param{3}{5,-35} (" + lKnxArgument + ")", lName, lOffset, lType, iHeaderPrefixName, lName + iChannelArgs, lName + iChannelArgs + "Str");
                        }
                        lIsOut = true;
                    }
                    else
                    {
                        cOut.AppendFormat("#define {3}{0,-35} {1,2}      // {4}{2}_t", lName, lOffset, lBits, iHeaderPrefixName, lType);
                        lOutput = string.Format("#define Param{3}{4,-35} (" + lKnxArgument + ")", lName, lOffset, lBitBaseSize, iHeaderPrefixName, lName + iChannelArgs);
                        lIsOut = true;
                    }
                    cOut.AppendLine();
                    if (lIsOut)
                    {
                        lOut.AppendLine(RemoveControlChars(lComment));
                        lOut.AppendLine(lOutput);
                        if (lTimeOutput != "")
                        {
                            lOut.AppendLine(RemoveControlChars(lComment) + " (in Millisekunden)");
                            lOut.AppendLine(lTimeOutput);
                        }
                    }
                }
            }
            cOut.AppendLine();
            cOut.Append(lOut);
            cOut.AppendLine();

            return lMaxSize;
        }

        string mCurrentDir = "";
        public string BaggagesBaseDir = "";
        readonly Dictionary<string, string> mBaggageTargetZipDirName = new();
        readonly string mBaggagesName = "";
        string mBaggageHelpFileName = "";
        string mBaggageIconFileName = "";
        HashSet<string> mBaggageHelpId = new();
        HashSet<string> mBaggageIconId = new();

        public bool IsHelpContextId(string iId)
        {
            if (iId.Contains("{{")) return true;
            return mBaggageHelpId.Contains(iId);
        }

        public bool IsIconId(string iId)
        {
            return mBaggageIconId.Contains(iId);
        }

        public string BaggagesName { get { return mBaggagesName; } }
        public string CurrentDir { get { return mCurrentDir; } }
        

        private bool VerifyModuleDependency(DefineContent iDefine, string iCurrentDir)
        {
            bool lResult = true;
            if (iDefine.VerifyFile != "")
            {
                string lFileName = PathHelper.BuildFullPath(iCurrentDir, iDefine.VerifyFile);
                string lVersion = "-1";
                int lVersionInt = -1;
                try
                {
                    using (var lVersionFile = File.OpenText(lFileName))
                    {
                        Regex lKeyword = new Regex(iDefine.VerifyRegex);
                        string lLine;
                        lResult = false;
                        while ((lLine = lVersionFile.ReadLine()) != null)
                        {
                            Match lMatch = lKeyword.Match(lLine);
                            if (lMatch.Success)
                            {
                                if (lMatch.Groups.Count == 2)
                                {
                                    lVersion = lMatch.Groups[1].Value;
                                    mHeaderGenerated.AppendFormat(@"#define {0}_ModuleVersion {1}", iDefine.prefix, lVersion);
                                    mHeaderGenerated.AppendLine();
                                    lResult = int.TryParse(lVersion, out lVersionInt);
                                    break;
                                }
                                else if (lMatch.Groups.Count == 3)
                                {
                                    lVersionInt = int.Parse(lMatch.Groups[1].Value) * 16 + int.Parse(lMatch.Groups[2].Value);
                                    mHeaderGenerated.AppendFormat(@"#define {0}_ModuleVersion {1}", iDefine.prefix, lVersionInt);
                                    mHeaderGenerated.AppendLine();
                                    lResult = (lVersionInt >= 0);
                                    break;
                                }
                            }
                        }
                    }
                    if (lResult && iDefine.VerifyVersion >= 0)
                    {
                        lResult = (iDefine.VerifyVersion == lVersionInt);
                        if (!lResult)
                            Program.Message(true, "You need to >>> INCREASE YOUR <<< ETS ApplicationVersion and manually synchronize op:verify of the {0} Module to ModuleVersion {1}.{2}, see https://github.com/OpenKNX/OpenKNX/wiki/Versionierung-von-Modulen-(OFM)", iDefine.prefix, lVersionInt / 16, lVersionInt % 16);
                    }
                    else
                    {
                        // add warning
                        Program.Message(false, "Verify for module {0} was not specified or could not be parsed. You should enable this for consistent ETS Applications, see https://github.com/OpenKNX/OpenKNX/wiki/Versionierung-von-Modulen-(OFM)", iDefine.prefix);
                    }
                }
                catch (System.IO.FileNotFoundException)
                {
                    Program.Message(true, "Version file {0} not found, please check name and path.", iDefine.VerifyFile);
                    lResult = false;
                }
            }
            return lResult;
        }

        /// <summary>
        /// Load xml document from file resolving includes recursively
        /// </summary>
        public bool LoadAdvanced(string iFileName)
        {
            bool lIsNew = false;
            if (!mLoaded)
            {
                string labsoluteFilePath = PathHelper.BuildFullPath(Directory.GetCurrentDirectory(), iFileName);;
                string lCurrentDir = Path.GetDirectoryName(labsoluteFilePath);
                mCurrentDir = lCurrentDir;
                string lFileData = File.ReadAllText(labsoluteFilePath);
                lFileData = ReplaceXmlns(lFileData);
                mLoaded = true;
                lIsNew = true;
                if (IsScript)
                {
                    // mDocument.CreateXmlDeclaration("1.0", "UTF-8", "no");
                    XmlElement lElement = mDocument.CreateElement("Script");
                    mDocument.AppendChild(lElement);
                    lElement.AppendChild(mDocument.CreateTextNode(lFileData));
                }
                else
                {
                    using StringReader sr = new(lFileData);
                    mDocument.Load(sr);
                    // ResolveNaming();
                    ResolveIncludes(lCurrentDir);
                }
            }
            return lIsNew;
        }

        private readonly Dictionary<string, string> mParameterNames = new();
        private readonly Dictionary<string, string> mComObjectNames = new();

        const char cNameMarker = '#';

        public void ResolveNaming()
        {
            XmlNodeList lParameters = mDocument.SelectNodes("//ApplicationProgram/Static/Parameters//Parameter");
            foreach (XmlNode lParameter in lParameters)
            {
                string lName = cNameMarker + lParameter.NodeAttr("Name") + cNameMarker;
                string lId = lParameter.NodeAttr("Id");
                mParameterNames.Add(lName, lId);
            }
            XmlNodeList lIdAttributes = mDocument.SelectNodes($"*//@*[contains(.,'{cNameMarker}')]");
            foreach (XmlNode lIdAttribute in lIdAttributes)
            {
                string lValue = lIdAttribute.Value;
                string lRefIdSuffix = "";
                if (lValue.Length < 5 || !lValue.StartsWith(cNameMarker) || (!lValue.EndsWith(cNameMarker) && lValue[^3] != cNameMarker))
                    continue;
                if (!lValue.EndsWith(cNameMarker))
                {
                    lRefIdSuffix = lValue[^2..];
                    lValue = lValue[..^2];
                }
                if (mParameterNames.ContainsKey(lValue))
                {
                    lValue = mParameterNames[lValue];
                    if (lRefIdSuffix != "")
                    {
                        lValue = lValue + "_R" + lValue[lValue.IndexOf("-%T")..] + lRefIdSuffix;
                    }
                    lIdAttribute.Value = lValue;
                }
                else
                {
                    Program.Message(true, "Attribute {0} with Value {1} could not be replaced, use original key for this object", lIdAttribute.Name, lIdAttribute.Value);
                }
            }

        }

        public static string ReplaceXmlns(string iXmlString)
        {
            if (iXmlString.Contains("oldxmlns"))
            {
                // we get rid of default namespace, we already have an original (this file was already processed by our processor)
                int lStart = iXmlString.IndexOf(" xmlns=\"");
                if (lStart < 0)
                {
                    iXmlString = iXmlString.Replace("oldxmlns", "xmlns");
                }
                else
                {
                    // int lEnd = lFileData.IndexOf("\"", lStart + 8) + 1;
                    iXmlString = iXmlString.Remove(lStart, 38);
                    // lFileData = lFileData.Substring(0, lStart) + lFileData.Substring(lEnd);
                }
            }
            else
            {
                // we get rid of default namespace, but remember the original
                iXmlString = iXmlString.Replace(" xmlns=\"", " oldxmlns=\"");
            }

            return iXmlString;
        }

        public static void LoadConfig(string iConfigFileName, string iCurrentDir)
        {
            XmlDocument lConfig = new();
            string lConfigFileName = Path.Combine(iCurrentDir, iConfigFileName);
            lConfig.Load(lConfigFileName);
            XmlNamespaceManager nsmgr = new(lConfig.NameTable);
            nsmgr.AddNamespace("oknxp", ProcessInclude.cOwnNamespace);
            // process config
            XmlNodeList lNodes = lConfig.SelectNodes("//oknxp:config", nsmgr);
            if (lNodes != null && lNodes.Count > 0)
                ParseConfig(lNodes, iCurrentDir, null);
            // we also allow nowarn in config files
            lNodes = lConfig.SelectNodes("//oknxp:nowarn", nsmgr);
            if (lNodes != null && lNodes.Count > 0)
                ParseNoWarn(lNodes);
        }

        public static bool AddConfig(string iName, string iValue)
        {
            bool lResult = false;
            if (!iName.StartsWith('%')) iName = '%' + iName;
            if (!iName.EndsWith('%')) iName += '%';
            if (iName != "%%" && !Config.ContainsKey(iName))
            {
                Config[iName] = new() { ConfigValue = iValue };
                lResult = true;
            }
            return lResult;
        }

        public static void ParseConfig(XmlNodeList iConfigNodes, string iCurrentDir, ProcessInclude iInclude)
        {
            // config consists of a list of name-value pairs to be replaced in document
            foreach (XmlNode lNode in iConfigNodes)
            {
                if (lNode.NodeType == XmlNodeType.Comment) continue;
                string lHref = lNode.NodeAttr("href");
                if (lHref == "")
                {
                    bool lAdded = false;
                    string lName = lNode.NodeAttr("name");
                    string lValue = lNode.NodeAttr("value");
                    lNode.ParentNode.RemoveChild(lNode);
                    if (iInclude != null && lName.Contains("%C%") && iInclude.ChannelCount > 0) 
                    {
                        DefineContent lDefine = DefineContent.GetDefineContent(iInclude.mHeaderPrefixName.Trim('_'));
                        if (lDefine != null)
                        {
                            int lNumChannels = lDefine.NumChannels;
                            for (int i = 0; i < lNumChannels; i++)
                            {
                                string lChannelName = lName.Replace("%C%", (i+1).ToString());
                                AddConfig(lChannelName, lValue);
                                lAdded = true;
                            }
                        }
                    }
                    if (!lAdded) AddConfig(lName, lValue);
                }
                else
                {
                    LoadConfig(lHref, iCurrentDir);
                }
            }
        }

        public static void ParseNoWarn(XmlNodeList iNodes)
        {
            foreach (XmlNode lNode in iNodes)
            {
                if (lNode.NodeType == XmlNodeType.Comment) continue;
                lNode.ParentNode.RemoveChild(lNode);
                if (!uint.TryParse(lNode.NodeAttr("id"), out uint lId)) continue;
                CheckHelper.AddNoWarn(lId, lNode.NodeAttr("regex"));
            }
        }

        public static void ProcessConfig(XmlNode iNode)
        {
            if (iNode.NodeType != XmlNodeType.Comment)
            {
                // we check for all attributes of current node and all child nodes in the subtree
                XmlNodeList lAttributes = iNode.SelectNodes("@*[contains(.,'%')]");
                ProcessConfig(lAttributes);
                lAttributes = iNode.SelectNodes("*//@*[contains(.,'%')]");
                ProcessConfig(lAttributes);
            }
        }

        private static void ProcessConfig(XmlNodeList iNodes)
        {
            foreach (XmlAttribute lAttr in iNodes)
                if (lAttr.Value.Contains('%'))
                    foreach (var lConfig in Config)
                    {
                        string lNewValue = lAttr.Value.Replace(lConfig.Key, lConfig.Value.ConfigValue);
                        if (!lConfig.Value.WasReplaced && lNewValue != lAttr.Value) lConfig.Value.WasReplaced = true;
                        lAttr.Value = lNewValue;
                    }
        }

        private void InitNamespaceManager()
        {
            nsmgr = new XmlNamespaceManager(mDocument.NameTable);
            nsmgr.AddNamespace("oknxp", cOwnNamespace);
        }


        /// <summary>
        /// Resolves Includes inside xml document
        /// </summary>
        /// <param name="iCurrentDir">Directory to use for relative href expressions</param>
        public void ResolveIncludes(string iCurrentDir)
        {
            bool lIsApplicationInclude = false;
            InitNamespaceManager();
            // retrieve min version
            GetMinVersions(mDocument);
            // process config
            XmlNodeList lConfigNodes = mDocument.SelectNodes("//oknxp:config", nsmgr);
            if (lConfigNodes != null && lConfigNodes.Count > 0)
                ParseConfig(lConfigNodes, iCurrentDir, this);

            XmlNodeList lNoWarnNodes = mDocument.SelectNodes("//oknxp:nowarn", nsmgr);
            if (lNoWarnNodes != null && lNoWarnNodes.Count > 0)
                ParseNoWarn(lNoWarnNodes);

            // process define node
            XmlNodeList lDefineNodes = mDocument.SelectNodes("//oknxp:define", nsmgr);
            if (lDefineNodes != null && lDefineNodes.Count > 0)
            {
                lIsApplicationInclude = true;
                // we first process config for op:ETS
                XmlNodeList lEtsNodes = mDocument.SelectNodes("//oknxp:ETS", nsmgr);
                if (lEtsNodes != null && lEtsNodes.Count == 1)
                    ProcessConfig(lEtsNodes[0]);

                foreach (XmlNode lDefineNode in lDefineNodes)
                {
                    // allow config in defines (Cornelius' idea)
                    ProcessConfig(lDefineNode);
                    DefineContent lDefine = DefineContent.Factory(lDefineNode);
                    VerifyModuleDependency(lDefine, iCurrentDir);
                }
                DefineContent.ValidateDefines();
            }

            // parse hardware support
            XmlNodeList lHardwareParams = mDocument.SelectNodes("//Parameter[@oknxp:hardwareDefault]", nsmgr);
            if (lHardwareParams != null && lHardwareParams.Count > 0)
                HardwareSupportCustom.ParseHardwareParams(lHardwareParams, mHeaderPrefixName, ChannelCount);

            FetchParameterTypes();

            // process generate
            XmlNodeList lGenerate = mDocument.SelectNodes("//generate");
            if (lGenerate.Count > 0)
            {
                // generate application facade
                TemplateApplication lTemplateApplication = new();
                var lDocument = lTemplateApplication.Generate(this, lGenerate[0]);
                // lDocument = ReplaceXmlns(lDocument);
                mDocument.LoadXml(lDocument);
                InitNamespaceManager();
            }

            if (lIsApplicationInclude)
            {
                // we evaluate correct MemoryId
                XmlNode lMemoryNode = mDocument.SelectSingleNode("//ApplicationProgram/Static/Code/RelativeSegment|//ApplicationProgram/Static/Code/AbsoluteSegment");
                if (lMemoryNode != null)
                {
                    string lMemoryId = lMemoryNode.NodeAttr("Id");
                    if (lMemoryId != "") AddConfig("%MID%", lMemoryId);
                }
            }

            //find all XIncludes in a copy of the document
            XmlNodeList lNodes = mDocument.SelectNodes("//oknxp:include|//oknxp:part|//oknxp:usePart", nsmgr); // get all <include> nodes
            // part preprocessing: we add instance nodes
            List<XmlNode> lIncludeNodes = ProcessPart.Preprocess(lNodes, mDocument);

            foreach (XmlNode lIncludeNode in lIncludeNodes)
            // try
            {
                //Load document...
                string lIncludeName = lIncludeNode.NodeAttr("href");
                string lHeaderPrefixName = lIncludeNode.NodeAttr("prefix");
                bool lIsInner = lIncludeNode.NodeAttr("IsInner") == "true";
                bool lIsPartDeclaration = lIncludeNode.LocalName == "part";
                bool lIsPart = lIncludeNode.LocalName == "usePart";
                // we handle parts as special includes
                if (lIsPart || lIsPartDeclaration)
                    lIsInner = true;
                DefineContent lDefine = DefineContent.GetDefineContent(lHeaderPrefixName);
                lDefine.IsTemplate = (lIncludeNode.NodeAttr("type") == "template");
                lDefine.IsParameter = (lIncludeNode.NodeAttr("type") == "parameter");
                if (lIsPart)
                {
                    ProcessPart lPart = ProcessPart.GetPart(lIncludeNode);
                    if (lPart == null)
                    {
                        Program.Message(true, "Part '{0}' not found!", lIncludeNode.NodeAttr("name"));
                        continue;
                    }
                    lIncludeName = lPart.IncludeName;                    
                }
                ProcessInclude lInclude = ProcessInclude.Factory(lIncludeName, lHeaderPrefixName);
                lInclude.IsInnerInclude = lIsInner;
                DateTime lStartTime = DateTime.Now;
                string lTargetPath = Path.Combine(iCurrentDir, lIncludeName);
                lInclude.IsScript = (lIncludeNode.NodeAttr("type") == "script");
                lInclude.IsPart = lIsPart;
                bool lIsNew = lInclude.LoadAdvanced(lTargetPath);
                if (lIsPartDeclaration)
                    ProcessPart.Init(lIncludeNode, lInclude);
                lHeaderPrefixName = lInclude.mHeaderPrefixName;
                lDefine = DefineContent.GetDefineContent(lHeaderPrefixName.Trim('_'));
                if (lIsNew)
                    ReplacePrefix(lDefine, lInclude);
                //...find include in real document...
                XmlNode lParent = lIncludeNode.ParentNode;
                string lXPath = lIncludeNode.NodeAttr("xpath", lIsPart ? "/KNY" : "//*");
                XmlDocument lDocument = lInclude.GetDocument();
                if (lIsPart)
                {
                    lDocument = ProcessPart.GetDocument(lIncludeNode);
                    if (lDocument == null)
                    {
                        Program.Message(true, "Part {0} not found!", lIncludeNode.OuterXml);
                        continue;
                    }
                }
                if (lDefine.prefix=="BASE")
                    ExtendedEtsSupport.GenerateModuleListPreprocess(lInclude, lDocument);
                XmlNodeList lChildren = lDocument.SelectNodes(lXPath);
                // we replace config params before we multiply all channels (faster)
                foreach (XmlNode lNode in lChildren)
                    ProcessConfig(lNode);
                // we process parameter names for ets after any static defaults are replaced
                if (lDefine.IsParameter || lXPath.Contains("Parameters/Parameter") || lXPath.Contains("Parameters/Union") || lXPath.Contains("Parameters//Parameter"))
                    ExtendedEtsSupport.GenerateScriptContent(lInclude, lDefine);
                lInclude.ModuleType = lDefine.ModuleType;
                bool lIsDynamicPart = true;
                if (lChildren.Count == 0 || "ParameterRef | ComObjectRef | ModuleDef | Baggage | #text".Contains(lChildren[0].LocalName))
                    lIsDynamicPart = false;
                if (lChildren.Count == 0 && lIsPart)
                    Program.Message(true, "usePart {1} without existing xpath {0} found!", lXPath, lIncludeNode.NodeAttr("name"));
                if (lChildren.Count > 0 && ("ParameterType | Parameter | Union | ComObject | BusInterface | ParameterCalculation | ParameterValidation | SNIPPET".Contains(lChildren[0].LocalName) || lInclude.IsInnerInclude))
                {
                    lIsDynamicPart = false;
                    if ("ParameterType" == lChildren[0].LocalName)
                        lInclude.OriginalChannelCount = lDefine.NumChannels;
                    else if (lDefine.IsTemplate)
                    {
                        // at this point we are including a template file
                        // ChannelCount and KoOffset are taken from correct prefix
                        lInclude.ChannelCount = lDefine.NumChannels;
                        lInclude.KoOffset = lDefine.KoOffset;
                        lInclude.KoSingleOffset = lDefine.KoSingleOffset;
                        lInclude.ReplaceKeys = lDefine.ReplaceKeys;
                        lInclude.ReplaceValues = lDefine.ReplaceValues;
                        if (!lInclude.IsInnerInclude)
                            ExportHeader(lDefine, lHeaderPrefixName, lInclude, lChildren);
                    }
                    else if (lDefine.IsParameter || "ComObject".Contains(lChildren[0].LocalName))
                    {
                        lInclude.OriginalChannelCount = lDefine.NumChannels;
                        if (!lInclude.IsInnerInclude) ExportHeader(lDefine, lHeaderPrefixName, lInclude, lChildren);
                    }
                    // debug
                    // string lDebugHeader = mHeaderGenerated.ToString();
                    // File.WriteAllText(Path.Combine("debug", Path.ChangeExtension(Path.GetFileName(lIncludeName), ".h")), lDebugHeader);
                }

                DateTime lStart = DateTime.Now;
                if (!lInclude.IsInnerInclude && !lInclude.IsScript)
                {
                    ReplaceDocumentStrings(lInclude.mDocument, "%ModuleVersion%", lDefine.VerifyVersionString);
                    if (lInclude.OriginalChannelCount > 0)
                    {
                        // TODO: Improve this in a more generic way
                        ReplaceDocumentStrings(lInclude.mDocument, "%N%", lInclude.OriginalChannelCount.ToString());
                        ReplaceDocumentStrings(lInclude.mDocument, "%N-1%", (lInclude.OriginalChannelCount - 1).ToString());
                        // ReplaceDocumentStrings(lInclude.mDocument, "-%N-1%", (-lInclude.OriginalChannelCount + 1).ToString());
                    }
                }
                // here we do template processing and repeat the template as many times as
                // the Channels parameter in header file
                XmlNode lInsertNode = lIncludeNode;
                for (int lChannel = 1; lChannel <= lInclude.ChannelCount && !lIsPartDeclaration; lChannel++)
                {
                    foreach (XmlNode lChild in lChildren)
                    {
                        if (lChild.LocalName != "SNIPPET")
                        {
                            //necessary for move between XmlDocument contexts
                            XmlNode lImportNode = lParent.OwnerDocument.ImportNode(lChild, true);
                            // for any Parameter node we do offset recalculation
                            // if there is no prefix name, we do no template replacement
                            if (lInclude.IsScript)
                                lImportNode = lImportNode.ChildNodes[0];
                            else if (!lInclude.IsPart)
                                if (lHeaderPrefixName != "" && lChild.NodeType != XmlNodeType.Text) ProcessTemplate(lDefine, lChannel, lImportNode, lInclude);
                            lParent.InsertAfter(lImportNode, lInsertNode);
                            // for merge processing of ParameterTypes we have to update global ParameterTypes chache
                            if (lChild.LocalName == "ParameterType")
                            {
                                var lChildName = lChild.NodeAttr("Name", "#unknown");
                                if (sParameterTypes.ContainsKey(lChildName))
                                {
                                    var lTypeChild = lImportNode.FirstChild;
                                    while (lTypeChild != null && lTypeChild.NodeType == XmlNodeType.Comment) lTypeChild = lTypeChild.NextSibling;
                                    sParameterTypes[lChildName] = lTypeChild;
                                }
                            }
                            lInsertNode = lImportNode;
                        }
                    }
                }
                lParent.RemoveChild(lIncludeNode);
                TimeSpan lDiff = DateTime.Now - lStart;
                if (lDiff.Seconds > 0 && lDefine.IsTemplate)
                    Console.WriteLine("Multiplying {2} channels of {1} took {0:0.##} seconds", lDiff.TotalSeconds, lInclude.mHeaderPrefixName.Trim('_'), lInclude.ChannelCount);
                // we replace also all additional replace key value pairs
                for (int lCount = 0; lCount < lInclude.ReplaceKeys.Length; lCount++)
                    ReplaceDocumentStrings(mDocument, lInclude.ReplaceKeys[lCount], lInclude.ReplaceValues[lCount]);

                // we replace all HelpContext Ids 
                // string lFileName = Path.GetFileNameWithoutExtension(mXmlFileName) + ".dbg.xml";
                // mDocument.Save("xml/" + lFileName);
                if (lHeaderPrefixName != "" && !lInclude.IsScript)
                {
                    // if (lInclude.mHeaderParameterBlockGenerated)
                    //     ReplaceKoTemplateFinal(lDefine, lInclude, mDocument);
                    if (lIsDynamicPart && !lInclude.IsInnerInclude)
                        ProcessHelpContext(lDefine, mDocument, lInclude);
                }
                // if (lHeaderPrefixName != "") ProcessIncludeFinish(lChildren);
                //if this fails, something is wrong
            }
            if (lDefineNodes != null && lDefineNodes.Count > 0)
                foreach (XmlNode lDefineNode in lDefineNodes)
                    lDefineNode.ParentNode.RemoveChild(lDefineNode);
            // catch { }
        }

        private static void ReplacePrefix(DefineContent iDefine, ProcessInclude iInclude)
        {
            XmlNodeList lNodes = iInclude.SelectNodes("//*/@*[contains(.,'%PREFIX%')]");
            foreach (XmlNode lNode in lNodes)
            {
                lNode.Value = lNode.Value.Replace("%PREFIX%", iDefine.prefix);
            }
        }

        public XmlNode CreateElement(string iName, params string[] iAttr)
        {
            XmlNode lNode = mDocument.CreateElement(iName);
            for (int i = 0; i < iAttr.Length; i += 2)
            {
                XmlAttribute lAttr = mDocument.CreateAttribute(iAttr[i]);
                lAttr.Value = iAttr[i + 1];
                lNode.Attributes.Append(lAttr);
            }
            return lNode;
        }

        private void ExportHeader(DefineContent iDefine, string iHeaderPrefixName, ProcessInclude iInclude, XmlNodeList iChildren = null)
        {
            // iInclude.ParseHeaderFile(iHeaderFileName);
            // if (iInclude.IsInnerInclude)
            //     return;
            XmlNodeList lParameterNodes;
            if (sParameterTypes.Count > 0)
            {
                // the main document contains necessary ParameterTypes definitions
                // lParameterNodes = mDocument.SelectNodes("//ApplicationProgram/Static/Parameters/Parameter|//ApplicationProgram/Static/Parameters/Union");
                // there are new parameters in include, we have to calculate a new parameter offset
                lParameterNodes = mDocument.SelectNodes("//ApplicationProgram/Static/Parameters/Parameter|//ApplicationProgram/Static/Parameters/Union");
                if (lParameterNodes != null)
                {
                    ParameterBlockSize = CalcParamSize(lParameterNodes);
                }
                if (iChildren != null)
                {
                    // ... and we do parameter processing, so we calculate ParamBlockSize for this include
                    int lBlockSize = iInclude.CalcParamSize(iChildren);
                    if (lBlockSize > 0)
                    {
                        iInclude.ParameterBlockSize = lBlockSize;
                        // we calculate also ParamOffset
                        iInclude.ParameterBlockOffset = ParameterBlockSize;
                    }
                }
            }
            // Header file generation is only possible before we resolve includes
            // First we serialize local parameters of this instance
            lParameterNodes = mDocument.SelectNodes("//ApplicationProgram/Static/Parameters//Parameter");
            ExportHeaderParameterStart(lParameterNodes, iDefine, mHeaderGenerated, iHeaderPrefixName);
            // followed by template parameters of the include
            if (iInclude != this)
            {
                lParameterNodes = iInclude.mDocument.SelectNodes("//ApplicationProgram/Static/Parameters//Parameter");
                iInclude.ExportHeaderParameterBlock(lParameterNodes, iDefine, mHeaderGenerated, iHeaderPrefixName);
            }

            ExportHeaderKoStart(iDefine, mHeaderGenerated, iHeaderPrefixName);
            if (iInclude != this) iInclude.ExportHeaderKoBlock(iDefine, mHeaderGenerated, iHeaderPrefixName);
        }
    }
}

