using System.Xml;
using System.Text.RegularExpressions;
using System.Text;

namespace OpenKNXproducer
{
    public class ProcessInclude {

        const string cOwnNamespace = "http://github.com/OpenKNX/OpenKNXproducer";
        private class DefineContent
        {
            public string prefix;
            public int KoOffset;
            public int KoSingleOffset;
            public string[] ReplaceKeys = {};
            public string[] ReplaceValues = {};
            public int NumChannels;
            public int ModuleType;
            public string header;
            public bool IsTemplate;
            public bool IsParameter;
            private DefineContent(string iPrefix, string iHeader, int iKoOffset, int iKoSingleOffset, int iNumChannels, string iReplaceKeys, string iReplaceValues, int iModuleType, bool iIsParameter) {
                prefix = iPrefix;
                header = iHeader;
                KoOffset = iKoOffset;
                KoSingleOffset = iKoSingleOffset;
                if (iReplaceKeys.Length > 0) {
                    ReplaceKeys = iReplaceKeys.Split(" ");
                    ReplaceValues = iReplaceValues.Split(" ");
                }
                NumChannels = iNumChannels;
                ModuleType = iModuleType;
                IsParameter = iIsParameter;
            }
            public static DefineContent Factory(XmlNode iDefineNode) {
                DefineContent lResult;

                int lChannelCount = 1;
                int lKoOffset = 1;
                int lKoSingleOffset = 0;
                string lPrefix = "";
                string lHeader = "";
                string lReplaceKeys = "";
                string lReplaceValues = "";
                int lModuleType = 1;

                lPrefix = iDefineNode.NodeAttr("prefix");
                if (lPrefix == "") lPrefix = "LOG"; // backward compatibility
                if (sDefines.ContainsKey(lPrefix)) {
                    lResult = sDefines[lPrefix];
                } else {
                    lHeader = iDefineNode.NodeAttr("header");
                    lChannelCount = int.Parse(iDefineNode.NodeAttr("NumChannels"));
                    lKoOffset = int.Parse(iDefineNode.NodeAttr("KoOffset"));
                    int lValue = 0;
                    if (int.TryParse(iDefineNode.NodeAttr("KoSingleOffset"), out lValue)) lKoSingleOffset = lValue;
                    lReplaceKeys = iDefineNode.NodeAttr("ReplaceKeys");
                    lReplaceValues = iDefineNode.NodeAttr("ReplaceValues");
                    lModuleType = int.Parse(iDefineNode.NodeAttr("ModuleType"));
                    lResult = new DefineContent(lPrefix, lHeader, lKoOffset, lKoSingleOffset, lChannelCount, lReplaceKeys, lReplaceValues, lModuleType, false);
                    sDefines.Add(lPrefix, lResult);
                }
                return lResult;
            }

            static public DefineContent Empty = new DefineContent("LOG", "", 1, 0, 1, "", "", 1, true);
            public static DefineContent GetDefineContent(string iPrefix) {
                DefineContent lResult;
                if (sDefines.ContainsKey(iPrefix)) {
                    lResult = sDefines[iPrefix];
                } else {
                    lResult = Empty;
                }
                return lResult;
            }
        }

        private XmlNamespaceManager nsmgr;
        private XmlDocument mDocument = new XmlDocument();
        private bool mLoaded = false;
        StringBuilder mHeaderGenerated = new StringBuilder();

        private XmlNode mParameterTypesNode = null;
        private static Dictionary<string, ProcessInclude> gIncludes = new Dictionary<string, ProcessInclude>();
        private static Dictionary<string, DefineContent> sDefines = new Dictionary<string, DefineContent>();
        private string mXmlFileName;
        private string mHeaderFileName;
        private string mHeaderPrefixName;
        private bool mHeaderParameterStartGenerated;
        private bool mHeaderParameterBlockGenerated;
        private bool mHeaderKoStartGenerated;
        private bool mHeaderKoBlockGenerated;
        private int mChannelCount = 1;
        private int mParameterBlockOffset = 0;
        private int mParameterBlockSize = -1;
        private int mKoOffset = 0;
        private int mKoSingleOffset = 0;
        private int mModuleType = 1;
        private string[] mReplaceKeys = {};
        private string[] mReplaceValues = {};
        private int mKoBlockSize = 0;
        private static bool mRenumber = false;
        private static bool mAbsoluteSingleParameters = false;

        public static bool Renumber {
            get { return mRenumber; }
            set { mRenumber = value; }
        }

        public static bool AbsoluteSingleParameters {
            get { return mAbsoluteSingleParameters; }
            set { mAbsoluteSingleParameters = value; }
        }

        public int ParameterBlockOffset {
            get { return mParameterBlockOffset; }
            set { mParameterBlockOffset = value; }
        }

        public int ParameterBlockSize {
            get { return mParameterBlockSize; }
            set { mParameterBlockSize = value; }
        }
        public int ChannelCount {
            get { return mChannelCount; }
            set { mChannelCount = value; }
        }

        public int KoOffset {
            get { return mKoOffset; }
            set { mKoOffset = value; }
        }

        public int KoSingleOffset {
            get { return mKoSingleOffset; }
            set { mKoSingleOffset = value; }
        }

        public int ModuleType {
            get { return mModuleType; }
            set { mModuleType = value; }
        }

        public string[] ReplaceKeys {
            get { return mReplaceKeys; }
            set { mReplaceKeys = value; }
        }
        
        public string[] ReplaceValues {
            get { return mReplaceValues; }
            set { mReplaceValues = value; }
        }
        
        public int GetIdOfProjectNamespace(XmlDocument iDocument) {
            string lProject = iDocument.DocumentElement.NodeAttr("oldxmlns");
            lProject = lProject.Replace("http://knx.org/xml/project/", "");
            int lResult = 0;
            int.TryParse(lProject, out lResult);
            return lResult;
        }

        public string HeaderGenerated {
            get {
                mHeaderGenerated.Insert(0, @"
#define paramDelay(time) (uint32_t)( \
            (time & 0xC000) == 0xC000 ? (time & 0x3FFF) * 100 : \
            (time & 0xC000) == 0x0000 ? (time & 0x3FFF) * 1000 : \
            (time & 0xC000) == 0x4000 ? (time & 0x3FFF) * 60000 : \
            (time & 0xC000) == 0x8000 ? ((time & 0x3FFF) > 1000 ? 3600000 : \
                                         (time & 0x3FFF) * 3600000 ) : 0 )
                                             
");
                mHeaderGenerated.Insert(0, "#pragma once\n\n");
                return mHeaderGenerated.ToString();
            }
        }

        public static ProcessInclude Factory(string iXmlFileName, string iHeaderFileName, string iHeaderPrefixName) {
            ProcessInclude lInclude = null;
            if (gIncludes.ContainsKey(iXmlFileName)) {
                lInclude = gIncludes[iXmlFileName];
            } else {
                Console.WriteLine("Processing include {0}", iXmlFileName);
                lInclude = new ProcessInclude(iXmlFileName, iHeaderFileName, iHeaderPrefixName);
                gIncludes.Add(iXmlFileName, lInclude);
            }
            return lInclude;
        }

        private ProcessInclude(string iXmlFileName, string iHeaderFileName, string iHeaderPrefixName) {
            mXmlFileName = iXmlFileName;
            mHeaderFileName = iHeaderFileName;
            mBaggagesName = Path.GetFileName(iXmlFileName).Replace(".xml", ".baggages");
            if (iHeaderPrefixName != "" && !iHeaderPrefixName.EndsWith('_')) iHeaderPrefixName += "_";
            mHeaderPrefixName = iHeaderPrefixName;
        }

        int GetHeaderParameter(string iHeaderFileContent, string iDefineName) {
            string lPattern = "#define.*" + iDefineName + @"\s*(\d{1,4})";
            Match m = Regex.Match(iHeaderFileContent, lPattern, RegexOptions.None);
            int lResult = -1;
            if (m.Groups.Count > 1) {
                int.TryParse(m.Groups[1].Value, out lResult);
            }
            return lResult;
        }

        // bool ParseHeaderFile(string iHeaderFileName) {
        //     if (File.Exists(iHeaderFileName)) {
        //         StreamReader lHeaderFile = File.OpenText(iHeaderFileName);
        //         string lHeaderFileContent = lHeaderFile.ReadToEnd();
        //         lHeaderFile.Close();
        //         mChannelCount = GetHeaderParameter(lHeaderFileContent, mHeaderPrefixName + "Channels");
        //         mKoOffset = GetHeaderParameter(lHeaderFileContent, mHeaderPrefixName + "KoOffset");
        //     } else {
        //         mChannelCount = 1;
        //         mKoOffset = 1;
        //     }
        //     // mKoBlockSize = GetHeaderParameter(lHeaderFileContent, mHeaderPrefixName + "KoBlockSize");
        //     return (mChannelCount >= 0) && (mKoOffset > 0);
        // }

        public XmlNodeList SelectNodes(string iXPath) {
            return mDocument.SelectNodes(iXPath);
        }


        static string CalculateId(int iApplicationNumber, int iApplicationVersion) {
            return string.Format("-{0:X4}-{1:X2}-0000", iApplicationNumber, iApplicationVersion);
        }

        public XmlDocument GetDocument() {
            return mDocument;
        }

        public void ResetXsd() {
            XmlNode lXmlModel = mDocument.FirstChild?.NextSibling;
            // the following if has to be very specific, because an assignment to InnerText of an XmlElement might delete all inner tags!!! 
            if (lXmlModel != null && lXmlModel.NodeType == XmlNodeType.ProcessingInstruction && lXmlModel.InnerText.Contains("-editor.xsd"))
                lXmlModel.InnerText = lXmlModel.InnerText.Replace("-editor.xsd", ".xsd");
        }

        public void DocumentDebugOutput() {
            mDocument.Save(Path.ChangeExtension(mXmlFileName, "out.xml"));
        }

        public void SetToolAndVersion() {
            mDocument.DocumentElement.RemoveAttribute("CreatedBy");
            mDocument.DocumentElement.SetAttribute("CreatedBy", typeof(Program).Assembly.GetName().Name);
            mDocument.DocumentElement.RemoveAttribute("ToolVersion");
            mDocument.DocumentElement.SetAttribute("ToolVersion", typeof(Program).Assembly.GetName().Version.ToString());
        }

        public void SetNamespace() {
            // we restore the original namespace, if necessary
            if (mDocument.DocumentElement.GetAttribute("xmlns") == "") {
                string lXmlns = mDocument.DocumentElement.GetAttribute("oldxmlns");
                if (lXmlns != "") {
                    mDocument.DocumentElement.SetAttribute("xmlns", lXmlns);
                    mDocument.DocumentElement.RemoveAttribute("oldxmlns");
                }
            }
        }

        public string GetNamespace() {
            return mDocument.DocumentElement.GetAttribute("xmlns");
        }

        public bool Expand() {
            // here we recursively process all includes and all channel repetitions
            LoadAdvanced(mXmlFileName);
            // we use here an empty DefineContent, just for startup
            ExportHeader(DefineContent.Empty, mHeaderFileName, mHeaderPrefixName, this);
            // finally we do all processing necessary for the whole (resolved) document
            bool lWithVersions = ProcessFinish(mDocument);
            // DocumentDebugOutput();
            return lWithVersions;
        }

        string ReplaceChannelTemplate(string iValue, int iChannel) {
            string lResult = iValue;
            Match lMatch = Regex.Match(iValue, @"%(C{1,3})%");
            if (lMatch.Captures.Count > 0) {
                int lLen = lMatch.Groups[1].Value.Length;
                string lFormat = string.Format("D{0}", lLen);
                lResult = iValue.Replace(lMatch.Value, iChannel.ToString(lFormat));
            }

            lMatch = Regex.Match(lResult, @"%(Z{1,3})%");
            if (lMatch.Captures.Count > 0)
            {
                int lLen = lMatch.Groups[1].Value.Length;
                string channelName = "";
                int temp_Channel = iChannel-1;
                for (int i = 0; i < lLen; i++) {
                    if(temp_Channel >= 0) channelName = Convert.ToChar((temp_Channel) % 26 + 65) + channelName;
                    temp_Channel /= 26;
                    temp_Channel--;
                }
                lResult = iValue.Replace(lMatch.Value, channelName);
            }

            return lResult;
        }

        string ReplaceKoTemplate(DefineContent iDefine, string iValue, int iChannel, ProcessInclude iInclude, bool iIsName) {
            string lResult = iValue;
            int lBlockSize = 0;
            int lOffset = 0;
            if (iDefine.IsTemplate)
            {
                if (iInclude != null) {
                    lBlockSize = iInclude.mKoBlockSize;
                    lOffset = iInclude.KoOffset;
                }
                // too slow!!!
                // MatchCollection lMatches = Regex.Matches(iValue, @"%K(\d{1,3})%");
                Match lMatch = Regex.Match(iValue, @"%K(\d{1,3})%");
                if (lMatch.Captures.Count > 0) {
                    int lShift = int.Parse(lMatch.Groups[1].Value);
                    lResult = iValue.Replace(lMatch.Value, ((iChannel - 1) * lBlockSize + lOffset + lShift).ToString());
                    // we want to replace all occurrences, but a match collection is to slow, so we call recursively just if 
                    // a replacement happened 
                    lResult = ReplaceKoTemplate(iDefine, lResult, iChannel, iInclude, iIsName);
                }
            } else if (iIsName) {
                // iChannel is in this case KoSingleOffset
                int lValue = int.Parse(iValue);
                lResult = (lValue + iDefine.KoSingleOffset).ToString();
            }
            return lResult;
        }

        void ProcessAttributes(DefineContent iDefine, int iChannel, XmlNode iTargetNode, ProcessInclude iInclude) {
            foreach (XmlAttribute lAttr in iTargetNode.Attributes) {
                // we have to mark ParameterBlock and ParameterSeparator for renumber processing
                if (lAttr.Value.Contains("%T%")) {
                    lAttr.Value = lAttr.Value.Replace("_PS-", "_PST-");
                    lAttr.Value = lAttr.Value.Replace("_PB-", "_PBT-");
                }
                lAttr.Value = lAttr.Value.Replace("%T%", iInclude.ModuleType.ToString());
                lAttr.Value = ReplaceChannelTemplate(lAttr.Value, iChannel);
                lAttr.Value = ReplaceKoTemplate(iDefine, lAttr.Value, iChannel, iInclude, lAttr.Name == "Number");
                // lAttr.Value = lAttr.Value.Replace("%N%", mChannelCount.ToString());
                if (lAttr.Name == "Name" && iTargetNode.Name != "ParameterType")
                    lAttr.Value = iInclude.mHeaderPrefixName + lAttr.Value;
            }
        }

        void ProcessParameter(int iChannel, XmlNode iTargetNode, ProcessInclude iInclude) {
            //calculate new offset
            XmlNode lMemory = iTargetNode.SelectSingleNode("Memory");
            if (lMemory != null) {
                XmlNode lAttr = lMemory.Attributes.GetNamedItem("Offset");
                int lOffset = int.Parse(lAttr.Value);
                if (iInclude.ChannelCount > (AbsoluteSingleParameters ? 1 : 0))
                    lOffset += iInclude.ParameterBlockOffset + (iChannel - 1) * iInclude.ParameterBlockSize;
                lAttr.Value = lOffset.ToString();
            }
        }

        void ProcessUnion(DefineContent iDefine, int iChannel, XmlNode iTargetNode, ProcessInclude iInclude) {
            //calculate new offset
            ProcessParameter(iChannel, iTargetNode, iInclude);
            XmlNodeList lChildren = iTargetNode.ChildNodes;
            foreach (XmlNode lChild in lChildren) {
                if (lChild.Name=="Parameter")
                    ProcessAttributes(iDefine, iChannel, lChild, iInclude);
            }
        }

        void ProcessChannel(DefineContent iDefine, int iChannel, XmlNode iTargetNode, ProcessInclude iInclude) {
            //attributes of the node
            if (iTargetNode.Attributes != null) {
                ProcessAttributes(iDefine, iChannel, iTargetNode, iInclude);
            }

            //Print individual children of the node, gets only direct children of the node
            XmlNodeList lChildren = iTargetNode.ChildNodes;
            foreach (XmlNode lChild in lChildren) {
                ProcessChannel(iDefine, iChannel, lChild, iInclude);
            }
        }

        void ProcessBaggage(DefineContent iDefine, int iChannel, XmlNode iTargetNode, ProcessInclude iInclude) {
            // Baggage-node
            XmlNode lBaggageIdAttr = iTargetNode.Attributes.GetNamedItem("Id");
            if (lBaggageIdAttr != null) {
                // we have a real Baggage definition, not just an extension reference
                // we copy all baggage files to our working dir
                string lBaggageId = lBaggageIdAttr.Value;
                XmlNode lFileNameAttr = iTargetNode.Attributes.GetNamedItem("Name");
                string lFileName = lFileNameAttr.Value;
                XmlNode lPathAttr = iTargetNode.Attributes.GetNamedItem("TargetPath");
                string lPath = lPathAttr.Value;
                lPath = lPath.Replace("/", "\\");
                string lSourceDirName = "";
                if (lFileName.StartsWith("..\\"))
                    lSourceDirName = iInclude.mCurrentDir;
                else
                    lSourceDirName = Path.Combine(iInclude.mCurrentDir,  "Baggages", lPath);
                string lTargetDirRoot = Path.Combine(mCurrentDir, mBaggagesName);
                if (lBaggageId.StartsWith("%FILE-HELP") || lBaggageId.StartsWith("%FILE-ICONS"))
                {
                    // context sensitive help and icons have to be merged
                    // we expect a directory, even if the file notation says ".zip"
                    lFileName = lFileName.Replace(".zip", "");
                    // for ETS, we need a zip and ensure, that it is generated
                    lFileNameAttr.Value = lFileName + ".zip";
                    // the path has to go to the specific application folder of the root application
                    lPath = DetermineBaggagePath(lPath);
                    if (mCurrentDir == iInclude.mCurrentDir) {
                        mBaggageBaseDir = lPath;
                    }
                    lPathAttr.Value = mBaggageBaseDir;
                    // now we copy all files to target
                    lSourceDirName = Path.Combine(lSourceDirName, lFileName);
                    if (Directory.Exists(lSourceDirName))
                    {
                        var lSourceDir = new DirectoryInfo(lSourceDirName);
                        lSourceDir.DeepCopy(Path.Combine(lTargetDirRoot, lPath, lFileName));
                    }
                }
                else {
                    // we copy single files without any merge process
                    string lSourceFileName = Path.Combine(lSourceDirName, lFileName);
                    string lTargetFileName = Path.Combine(lTargetDirRoot, lPath, Path.GetFileName(lFileName));
                    if (File.Exists(lSourceFileName)) {
                        Directory.CreateDirectory(Path.Combine(lTargetDirRoot, lPath));
                        File.Copy(lSourceFileName, lTargetFileName, true);
                        lFileNameAttr.Value = Path.GetFileName(lFileName);
                    } 
                }
            }
        }

        private string DetermineBaggagePath(string iPath)
        {
            var lDirInfo = new DirectoryInfo(Path.Combine(mCurrentDir, "Baggages"));
            var lSubDirInfos = lDirInfo.EnumerateDirectories("A?");
            if (lSubDirInfos != null) {
                var lSubSubDirInfos = lSubDirInfos.First().EnumerateDirectories("??");
                if (lSubSubDirInfos != null)
                    iPath = Path.Combine(lSubDirInfos.First().Name, lSubSubDirInfos.First().Name);
            } 
            return iPath;
        }

        void ProcessTemplate(DefineContent iDefine, int iChannel, XmlNode iTargetNode, ProcessInclude iInclude) {
            if (iTargetNode.Name == "Baggage") {
                ProcessBaggage(iDefine, iChannel, iTargetNode, iInclude);
            } else {
                ProcessAttributes(iDefine, iChannel, iTargetNode, iInclude);
                if (iTargetNode.Name == "Parameter") {
                    ProcessParameter(iChannel, iTargetNode, iInclude);
                } else
                if (iTargetNode.Name == "Union") {
                    ProcessUnion(iDefine, iChannel, iTargetNode, iInclude);
                } else
                if (iTargetNode.Name == "Channel" || iTargetNode.Name == "ParameterBlock" || iTargetNode.Name == "choose") {
                    ProcessChannel(iDefine, iChannel, iTargetNode, iInclude);
                }
            }
        }

        void ProcessIncludeFinish(XmlNode iTargetNode) {
            // set number of Channels
            XmlNodeList lNodes = iTargetNode.SelectNodes("//*[@Value='%N%']");
            foreach (XmlNode lNode in lNodes) {
                lNode.Attributes.GetNamedItem("Value").Value = mChannelCount.ToString();
            }
            lNodes = iTargetNode.SelectNodes("//*[@maxInclusive='%N%']");
            foreach (XmlNode lNode in lNodes) {
                lNode.Attributes.GetNamedItem("maxInclusive").Value = mChannelCount.ToString();
            }
            // // set the max channel value
            // ReplaceDocumentStrings(mDocument, "%N%", mChannelCount.ToString());
        }

        void ReplaceDocumentStrings(XmlNodeList iNodeList, string iSourceText, string iTargetText) {
            foreach (XmlNode lNode in iNodeList) {
                if (lNode.Attributes != null) {
                    foreach (XmlNode lAttribute in lNode.Attributes) {
                        lAttribute.Value = lAttribute.Value.ToString().Replace(iSourceText, iTargetText);
                    }
                }
                ReplaceDocumentStrings(lNode, iSourceText, iTargetText);
            }
        }
        void ReplaceDocumentStrings(XmlNode iNode, string iSourceText, string iTargetText) {
            ReplaceDocumentStrings(iNode.ChildNodes, iSourceText, iTargetText);
        }

        void ReplaceDocumentStrings(string iSourceText, string iTargetText) {
            ReplaceDocumentStrings(mDocument.ChildNodes, iSourceText, iTargetText);
        }

        bool ProcessFinish(XmlNode iTargetNode) {
            Console.WriteLine("Processing merged file...");
            bool lWithVersions = false;
            XmlNode lApplicationProgramNode = iTargetNode.SelectSingleNode("/KNX/ManufacturerData/Manufacturer/ApplicationPrograms/ApplicationProgram");
            // evaluate oknxp:version, if available
            XmlNode lMcVersionNode = iTargetNode.SelectSingleNode("//oknxp:version", nsmgr);
            string lInlineData = "";
            string lVersionMessage = "";
            if (lMcVersionNode != null) {
                // found oknxp:version, we apply its attributes to knxprod-xml
                int lOpenKnxId = Convert.ToInt32(lMcVersionNode.Attributes.GetNamedItem("OpenKnxId").Value, 16);
                int lAppNumber = Convert.ToInt32(lMcVersionNode.Attributes.GetNamedItem("ApplicationNumber").Value, 10);
                int lCalcAppNumber = (lAppNumber + (lOpenKnxId << 8));
                lApplicationProgramNode.Attributes.GetNamedItem("ApplicationNumber").Value = lCalcAppNumber.ToString();
                int lAppVersion = Convert.ToInt32(lMcVersionNode.Attributes.GetNamedItem("ApplicationVersion").Value, 10);
                lApplicationProgramNode.Attributes.GetNamedItem("ApplicationVersion").Value = lAppVersion.ToString();
                string lReplVersions = lMcVersionNode.Attributes.GetNamedItem("ReplacesVersions").Value;
                if (lReplVersions == "") lReplVersions = "0";
                lApplicationProgramNode.Attributes.GetNamedItem("ReplacesVersions").Value = lReplVersions;
                int lAppRevision = Convert.ToInt32(lMcVersionNode.Attributes.GetNamedItem("ApplicationRevision").Value, 10);
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
                mHeaderGenerated.AppendFormat("#define MAIN_OpenKnxId 0x{0:X2}", lOpenKnxId);
                mHeaderGenerated.AppendLine();
                mHeaderGenerated.AppendFormat("#define MAIN_ApplicationNumber {0}", lAppNumber);
                mHeaderGenerated.AppendLine();
                mHeaderGenerated.AppendFormat("#define MAIN_ApplicationVersion {0}", lAppVersion-lAppRevision);
                mHeaderGenerated.AppendLine();
            }

            // change all Id-Attributes / renumber ParameterSeparator and ParameterBlock
            string lApplicationId = lApplicationProgramNode.Attributes.GetNamedItem("Id").Value;
            int lApplicationNumber = -1;
            bool lIsInt = int.TryParse(lApplicationProgramNode.Attributes.GetNamedItem("ApplicationNumber").Value, out lApplicationNumber);
            int lApplicationVersion = -1;
            lIsInt = int.TryParse(lApplicationProgramNode.Attributes.GetNamedItem("ApplicationVersion").Value, out lApplicationVersion);
            XmlNode lReplacesVersionsAttribute = lApplicationProgramNode.Attributes.GetNamedItem("ReplacesVersions");
            string lOldId = lApplicationId;//.Replace("M-00FA_A", ""); // CalculateId(1, 1);
            if(lOldId.StartsWith("M-")) lOldId = lOldId.Substring(8);
            string lNewId = CalculateId(lApplicationNumber, lApplicationVersion);
            if (lOldId == "%AID%") lNewId = "M-00FA_A" + lNewId;
            int lParameterSeparatorCount = 1;
            int lParameterBlockCount = 1;
            // Baggages handling
            PreprocessBaggages(iTargetNode);
            ReplaceBaggages(iTargetNode);
            ReplaceExtensions(iTargetNode);
            XmlNodeList lAttrs;
            lAttrs = iTargetNode.SelectNodes("//*/@*[starts-with(.,'%AID%')]");
            if (lAttrs.Count == 0) 
                lAttrs = iTargetNode.SelectNodes("//*/@*[string-length() > '13']");
            foreach (XmlNode lAttr in lAttrs) {
                if (lAttr.Value != null) {
                    lAttr.Value = lAttr.Value.Replace(lOldId, lNewId);
                    if (ProcessInclude.Renumber) {
                        // ParameterSeparator is renumbered
                        if (lAttr.Value.Contains("_PS-")) {
                            lAttr.Value = string.Format("{0}-{1}", lAttr.Value.Substring(0, lAttr.Value.LastIndexOf('-')), lParameterSeparatorCount);
                            lParameterSeparatorCount += 1;
                        }
                        // ParameterBlock is renumbered
                        if (lAttr.Value.Contains("_PB-")) {
                            lParameterBlockCount = RenumberParameterBlock(lParameterBlockCount, lAttr);
                        }
                    }
                    lAttr.Value = lAttr.Value.Replace("_PST-", "_PS-");
                    lAttr.Value = lAttr.Value.Replace("_PBT-", "_PB-");
                }
            }
            // process Enumeration-IDs (%ENID%)
            XmlNodeList lEnumerations = iTargetNode.SelectNodes("//ParameterType//*[@Id='%ENID%']");
            foreach (XmlNode lEnumeration in lEnumerations)
            {
                XmlNode lIdNode = lEnumeration.Attributes.GetNamedItem("Id");
                XmlNode lParameterType = lEnumeration.ParentNode.ParentNode;
                lIdNode.Value = lParameterType.NodeAttr("Id") + "_EN-" + lEnumeration.NodeAttr("Value"); 
            }
            Console.WriteLine("- ApplicationNumber: {0}, ApplicationVersion: {1}, old ID is: {3}, new (calculated) ID is: {2}", lApplicationNumber, lApplicationVersion, lNewId, lOldId);
            if (lInlineData != "") Console.WriteLine("- Calculated InlineData for Versioning: {0}", lInlineData);

            // create registration entry
            XmlNode lHardwareNode = iTargetNode.SelectSingleNode("/KNX/ManufacturerData/Manufacturer/Hardware/Hardware");
            int lHardwareVersion = 1;
            int.TryParse(lHardwareNode.Attributes.GetNamedItem("VersionNumber").Value, out lHardwareVersion);
            string lSerialNumber = lHardwareNode.Attributes.GetNamedItem("SerialNumber").Value;
            XmlNode lRegistrationNumber = iTargetNode.SelectSingleNode("/KNX/ManufacturerData/Manufacturer/Hardware/Hardware/Hardware2Programs/Hardware2Program/RegistrationInfo/@RegistrationNumber");
            if (lRegistrationNumber == null) {
                Console.WriteLine("- Missing 'RegistrationVersion', no updates via 'ReplacesVersion' in ETS possible!");
            } else {
                lRegistrationNumber.Value = string.Format("0001/{0}{1}", lHardwareVersion, lApplicationVersion);
                Console.WriteLine("- RegistrationVersion is: {0}", lRegistrationNumber.Value);
            }
            // Add ReplacesVersions 
            if (lReplacesVersionsAttribute != null) {
                string lReplacesVersions = lReplacesVersionsAttribute.Value;
                Console.WriteLine("- ReplacesVersions entry is: {0}", lReplacesVersions);
                // string lOldVersion = string.Format(" {0}", lApplicationVersion - 1);
                // if (!lReplacesVersions.Contains(lOldVersion) && lReplacesVersions != (lApplicationVersion - 1).ToString()) lReplacesVersionsAttribute.Value += lOldVersion;
            }
            // set the right Size attributes
            // XmlNodeList lNodes = iTargetNode.SelectNodes("(//RelativeSegment | //LdCtrlRelSegment | //LdCtrlWriteRelMem)[@Size]");
            string lSize = mParameterBlockSize.ToString();
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
            if (lMcVersionNode != null) {
                mHeaderGenerated.AppendFormat("#define MAIN_OrderNumber \"{0}\"", lOrderNumberAttribute.Value);
                mHeaderGenerated.AppendLine();
            }
            // XmlNodeList lCatalog = iTargetNode.SelectNodes("/KNX/ManufacturerData/Manufacturer/Catalog/descendant::*/@*");
            // XmlNodeList lHardware = iTargetNode.SelectNodes("/KNX/ManufacturerData/Manufacturer/Hardware/descendant::*/@*");
            // XmlNodeList lStatic = iTargetNode.SelectNodes("/KNX/ManufacturerData/Manufacturer/ApplicationPrograms/ApplicationProgram/Static/descendant::*/@*");
            // var lNodes = lCatalog.Cast<XmlNode>().Concat(lHardware.Cast<XmlNode>().Concat<XmlNode>(lStatic.Cast<XmlNode>())).ToList();
            var lNodes = iTargetNode.SelectNodes("/KNX/ManufacturerData/Manufacturer/*[self::Catalog or self::Hardware or self::Languages or self::ApplicationPrograms/ApplicationProgram/Static]/descendant::*/@*");
            foreach (XmlNode lNode in lNodes)
            {
                if (lNode.Value != null) {
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
                    lNode.Value = lValue;
                }
            }
            return lWithVersions;
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

            bool HandleZipFile(string iZipPattern, ref bool eZipType) {
                if (lIdNode.Value.StartsWith(iZipPattern)) {
                    if (eZipType) {
                        // mark for removal
                        lIdNode.Value = "!!DELETE!!";
                        return true;
                    }
                    // the following happens just once
                    eZipType = true;
                    string lSourceDirName = Path.Combine(mCurrentDir, mBaggagesName, lPath, lFileName.Replace(".zip", ""));
                    string lTargetName = Path.Combine(mCurrentDir, mBaggagesName, lPath, lFileName);
                    System.IO.Compression.ZipFile.CreateFromDirectory(lSourceDirName, lTargetName);
                    // before we delete the underlying directories, we store some information
                    HashSet<string> lHashId = new HashSet<string>();
                    var lFiles = Directory.EnumerateFiles(lSourceDirName);
                    foreach (var lFile in lFiles)
                    {
                        lHashId.Add(Path.GetFileNameWithoutExtension(lFile));
                    }
                    if (iZipPattern == "%FILE-HELP") {
                        mBaggageHelpFileName = Path.Combine(lPath, lFileName);
                        mBaggageHelpId = lHashId;
                    }
                    else if (iZipPattern == "%FILE-ICONS") {
                        mBaggageIconFileName = Path.Combine(lPath, lFileName);
                        mBaggageIconId = lHashId;
                    }
                    Directory.Delete(lSourceDirName, true);
                }
                return false;
            }

            if (lBaggages != null) {
                foreach (XmlNode lBaggage in lBaggages.ChildNodes)
                {
                    // We need to create according Id from Baggage filename
                    lPath = lBaggage.Attributes.GetNamedItem("TargetPath").Value;
                    lFileName = lBaggage.Attributes.GetNamedItem("Name").Value;
                    lIdNode = lBaggage.Attributes.GetNamedItem("Id");
                    if (HandleZipFile("%FILE-HELP", ref lWithHelp)) continue;
                    if (HandleZipFile("%FILE-ICONS", ref lWithIcons)) continue;

                    string lBaggageId = string.Format("M-00FA_BG-{0}-{1}", Program.GetEncoded(lPath), Program.GetEncoded(lFileName));
                    if (!mBaggageId.ContainsKey(lIdNode.Value))
                        mBaggageId.Add(lIdNode.Value, lBaggageId);
                    lIdNode.Value = lBaggageId;
                    DateTime lFileCreation = File.GetCreationTimeUtc(Path.Combine(mCurrentDir, mBaggagesName, lPath, lFileName));
                    string lIsoDateTime = lFileCreation.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
                    XmlNode lTimeInfo = lBaggage.SelectSingleNode("FileInfo/@TimeInfo", nsmgr);
                    if (lTimeInfo != null && lTimeInfo.Value == "%DATETIME%")
                        lTimeInfo.Value = lIsoDateTime;
                } 
                // duplicate zip-baggages are deleted
                XmlNodeList lDeletes = lBaggages.SelectNodes(@"Baggage[@Id ='!!DELETE!!']", nsmgr);
                foreach (XmlNode lDelete in lDeletes)
                {
                    lDelete.ParentNode.RemoveChild(lDelete);
                }
            }
        }

        private void ReplaceBaggages(XmlNode iTargetNode)
        {
            XmlNodeList lRefIds = iTargetNode.SelectNodes(@"//./@*[starts-with(.,'%FILE-')]", nsmgr);
            int lProjectNamespace = GetIdOfProjectNamespace((XmlDocument)iTargetNode);
            if (lRefIds != null) {
                foreach (XmlNode lRefId in lRefIds) {
                    if (mBaggageId.ContainsKey(lRefId.Value)) {
                        lRefId.Value = mBaggageId[lRefId.Value];
                    }
                    if (lProjectNamespace == 14) {
                        if (lRefId.Name == "ContextHelpFile") {
                            lRefId.Value = mBaggageHelpFileName;
                        } else
                        if (lRefId.Name == "IconFile") {
                            lRefId.Value = mBaggageIconFileName;
                        } 
                    }
                }
            }
        }

        private void ReplaceExtensions(XmlNode iTargetNode)
        {
            XmlNode lExtensions = iTargetNode.SelectSingleNode(@"//Extension", nsmgr);
            if (lExtensions != null) {
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
        private int RenumberParameterBlock(int lParameterBlockCount, XmlNode lAttr) {
            // for inline parameter blocks like grid and table we need
            // the same PB number in all subblocks.
            // we assume, that the iterator first provides the PB and later on the subPB, 
            // before the next PB is offered.
            int lPos = lAttr.Value.IndexOf("_PB-");
            string lValue = "";
            if (lAttr.Value.Substring(lPos+4).Contains("_")) {
                // this is a subblock, we assume, that its main block was already renumbered
                lValue = lAttr.Value.Substring(0, lAttr.Value.LastIndexOf("_"));
                // lAttr.Value = lAttr.Value.Replace(lValue, mParameterBlockMap[lValue]);
                lAttr.Value = lAttr.Value.Replace(lValue, mLastParameterBlockId);
            } else {
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

        public int CalcParamSize(XmlNode iParameter, XmlNode iParameterTypesNode) {
            int lResult = 0;
            if (iParameterTypesNode != null) {
                // we calculate the size only, if the parameter uses some memory in the device storage
                XmlNode lMemory = iParameter.SelectSingleNode("Memory");
                if (lMemory != null) {
                    XmlNode lSizeNode = null;
                    XmlNode lSizeInBitAttribute = null;
                    if (iParameter.Name == "Union") {
                        lSizeNode = iParameter;
                        lSizeInBitAttribute = lSizeNode.Attributes.GetNamedItem("SizeInBit");
                    } else {
                        string lParameterTypeId = iParameter.NodeAttr("ParameterType");
                        lSizeNode = iParameterTypesNode.SelectSingleNode(string.Format("ParameterType[@Id='{0}']", lParameterTypeId));
                        if (lSizeNode != null) lSizeInBitAttribute = lSizeNode.SelectSingleNode("*/@SizeInBit");
                    }
                    if (lSizeNode != null) {
                        if (lSizeInBitAttribute != null) {
                            lResult = 8;
                            bool lIsInt = int.TryParse(lSizeInBitAttribute.Value, out lResult);
                            lResult = (lResult - 1) / 8 + 1;
                            if (!lIsInt) {
                                Console.WriteLine("Parse error in include {0} in line {1}", mXmlFileName, lSizeNode.InnerXml);
                            }
                        } else if (lSizeNode.SelectSingleNode("TypeFloat") != null) {
                            lResult = 2;
                        } else if (lSizeNode.SelectSingleNode("TypeColor") != null) {
                            lResult = 3;
                        }
                    }
                }
            }
            return lResult;
        }

        public int CalcParamSize(XmlNodeList iParameterList, XmlNode iParameterTypesNode) {
            int lResult = 0;
            foreach (XmlNode lNode in iParameterList) {
                int lSize = CalcParamSize(lNode, iParameterTypesNode);
                if (lSize > 0) {
                    // at this point we know there is a memory reference, we look at the offset
                    XmlNode lOffset = lNode.SelectSingleNode("*/@Offset");
                    lResult = Math.Max(lResult, int.Parse(lOffset.Value) + lSize);
                }
            }
            return lResult;
        }

        private string ReplaceChannelName(string iName) {
            string lResult = iName;
            // if (iName.Contains("%C%")) lResult = iName.Remove(0, iName.IndexOf("%C%") + 3);
            lResult = iName.Replace("%C%", "");
            lResult = lResult.Replace(" ", "_");
            return lResult;
        }

        private void ExportHeaderKoStart(DefineContent iDefine, StringBuilder cOut, string iHeaderPrefixName) {
            if (!mHeaderKoStartGenerated) {
                StringBuilder lOut = new StringBuilder();
                mHeaderKoStartGenerated = ExportHeaderKo(iDefine, lOut, iHeaderPrefixName);
                if (mHeaderKoStartGenerated && iDefine.IsParameter) {
                    cOut.AppendLine("// Communication objects with single occurrence");
                    cOut.Append(lOut);
                }
            }
        }

        private void ExportHeaderKoBlock(DefineContent iDefine, StringBuilder cOut, string iHeaderPrefixName) {
            if (!mHeaderKoBlockGenerated) {
                XmlNodeList lComObjects = mDocument.SelectNodes("//ComObjectTable/ComObject");
                mKoBlockSize = lComObjects.Count;

                StringBuilder lOut = new StringBuilder();
                mHeaderKoBlockGenerated = ExportHeaderKo(iDefine, lOut, iHeaderPrefixName);
                if (mHeaderKoBlockGenerated) {
                    if (iDefine.IsTemplate) {
                        cOut.AppendLine("// deprecated");
                        cOut.AppendFormat("#define {0}KoOffset {1}", iHeaderPrefixName, mKoOffset);
                        cOut.AppendLine();
                        cOut.AppendLine();
                        cOut.AppendLine("// Communication objects per channel (multiple occurrence)");
                        cOut.AppendFormat("#define {0}KoBlockOffset {1}", iHeaderPrefixName, mKoOffset);
                        cOut.AppendLine();
                        cOut.AppendFormat("#define {0}KoBlockSize {1}", iHeaderPrefixName, mKoBlockSize);
                        cOut.AppendLine();
                        cOut.AppendLine();
                        cOut.AppendFormat("#define {0}KoCalcNumber(index) (index + {0}KoBlockOffset + _channelIndex * {0}KoBlockSize)", iHeaderPrefixName);
                        cOut.AppendLine();
                        cOut.AppendFormat("#define {0}KoCalcIndex(number) ((number >= {0}KoCalcNumber(0) && number < {0}KoCalcNumber({0}KoBlockSize)) ? (number - {0}KoOffset) % {0}KoBlockSize : -1)", iHeaderPrefixName);
                        cOut.AppendLine();
                        cOut.AppendLine();
                    }
                    cOut.Append(lOut);
                }
            }
        }

        private bool ExportHeaderKo(DefineContent iDefine, StringBuilder cOut, string iHeaderPrefixName) {
            XmlNodeList lNodes = mDocument.SelectNodes("//ComObject");
            StringBuilder lOut = new StringBuilder();
            
            bool lResult = false;
            foreach (XmlNode lNode in lNodes) {
                string lComment = "// " + lNode.Attributes.GetNamedItem("Text").Value;
                string lNumber = ReplaceKoTemplate(iDefine, lNode.NodeAttr("Number"), 1, null, true);
                cOut.AppendFormat("#define {0}Ko{1} {2}", iHeaderPrefixName, ReplaceChannelName(lNode.NodeAttr("Name")), lNumber);
                cOut.AppendLine();
                lOut.AppendLine(RemoveControlChars(lComment));
                string lName = ReplaceChannelName(lNode.NodeAttr("Name"));
                if (iDefine.IsTemplate)
                    lOut.AppendFormat("#define Ko{0}{3,-25} (knx.getGroupObject({0}KoCalcNumber({0}Ko{1})))", iHeaderPrefixName, lName, lNumber, lName);
                else
                    lOut.AppendFormat("#define Ko{0}{3,-25} (knx.getGroupObject({0}Ko{1}))", iHeaderPrefixName, lName, lNumber, lName);
                lOut.AppendLine();
                // lOut.AppendFormat("#define Ko{0}{1,-25} Ko{0}{3}", iHeaderPrefixName, lName, lNumber, lName + "(0)");
                // lOut.AppendLine();
                lResult = true;
            }
            if (lResult) {
              cOut.AppendLine();
              cOut.Append(lOut);
              cOut.AppendLine();
            }
            return lResult;
        }

        private void ExportHeaderParameterStart(DefineContent iDefine, StringBuilder cOut, XmlNode iParameterTypesNode, string iHeaderPrefixName) {
            if (!mHeaderParameterStartGenerated && iDefine.IsParameter) {
                cOut.AppendLine("// Parameter with single occurrence");
                ExportHeaderParameter(iDefine, cOut, iParameterTypesNode, iHeaderPrefixName, iDefine.IsParameter);
                mHeaderParameterStartGenerated = true;
            }
        }

        private void ExportHeaderParameterBlock(DefineContent iDefine, StringBuilder cOut, XmlNode iParameterTypesNode, string iHeaderPrefixName) {
            if (!mHeaderParameterBlockGenerated) {
                if (iDefine.IsTemplate) {
                    cOut.AppendFormat("#define {0}ChannelCount {1}", iHeaderPrefixName, mChannelCount);
                    cOut.AppendLine();
                    cOut.AppendLine();
                    cOut.AppendLine("// Parameter per channel");
                    cOut.AppendFormat("#define {0}ParamBlockOffset {1}", iHeaderPrefixName, mParameterBlockOffset);
                    cOut.AppendLine();
                    cOut.AppendFormat("#define {0}ParamBlockSize {1}", iHeaderPrefixName, mParameterBlockSize);
                    cOut.AppendLine();
                    cOut.AppendFormat("#define {0}ParamCalcIndex(index) (index + {0}ParamBlockOffset + _channelIndex * {0}ParamBlockSize)", iHeaderPrefixName);
                    cOut.AppendLine();
                    cOut.AppendLine();
                }
                int lSize = ExportHeaderParameter(iDefine, cOut, iParameterTypesNode, iHeaderPrefixName, iDefine.IsParameter);
                // if (lSize != mParameterBlockSize) throw new ArgumentException(string.Format("ParameterBlockSize {0} calculation differs from header file calculated ParameterBlockSize {1}", mParameterBlockSize, lSize));
                mHeaderParameterBlockGenerated = true;
            }
        }

        string RemoveControlChars(string iText) {
            return iText.Replace("\n", "");
            // return new string(iText.Where(c => !char.IsControl(c)).ToArray());
        }

        private int ExportHeaderParameter(DefineContent iDefine, StringBuilder cOut, XmlNode iParameterTypesNode, string iHeaderPrefixName, bool iWithAbsoluteOffset) {
            int lMaxSize = 0;
            StringBuilder lOut = new StringBuilder();
            XmlNodeList lNodes = mDocument.SelectNodes("//Parameter");
            foreach (XmlNode lNode in lNodes) {
                XmlNode lMemoryNode;
                string lName = lNode.Attributes.GetNamedItem("Name").Value;
                lName = ReplaceChannelName(lName);
                lMemoryNode = lNode.ParentNode;
                if (lMemoryNode != null && lMemoryNode.Name!="Union") {
                    lMemoryNode = lNode;
                }
                XmlNode lMemory = lMemoryNode.FirstChild;
                while (lMemory != null && lMemory.NodeType == XmlNodeType.Comment) lMemory = lMemory.NextSibling;
                if (lMemory != null && iParameterTypesNode != null) {
                    // parse parameter type to fill additional information
                    string lParameterTypeId = lNode.NodeAttr("ParameterType");
                    XmlNode lParameterType = iParameterTypesNode.SelectSingleNode(string.Format("//ParameterType[@Id='{0}']", lParameterTypeId));
                    XmlNode lTypeNumber = null;
                    if (lParameterType != null) lTypeNumber = lParameterType.FirstChild;
                    while (lTypeNumber != null && lTypeNumber.NodeType == XmlNodeType.Comment) lTypeNumber = lTypeNumber.NextSibling;
                    int lBits = 0;
                    int lBitBaseSize = 0;
                    string lType = "";
                    string lKnxAccessMethod = "";
                    bool lDirectType = false;
                    if (lTypeNumber != null) {
                        XmlNode lBitsAttribute = lTypeNumber.Attributes.GetNamedItem("SizeInBit");
                        if (lBitsAttribute != null) lBits = int.Parse(lBitsAttribute.Value);
                        XmlNode lTypeAttribute = lTypeNumber.Attributes.GetNamedItem("Type");
                        if (lTypeNumber.Name == "TypeNumber" || lTypeNumber.Name == "TypeRestriction") {
                            if (lTypeAttribute != null) lType = lTypeAttribute.Value;
                            if (lBits <= 8) {
                                lBitBaseSize = 8;
                                lKnxAccessMethod = "knx.paramByte({0})";
                            } else if (lBits <= 16) {
                                lBitBaseSize = 16;
                                lKnxAccessMethod = "knx.paramWord({0})";
                            } else if (lBits <= 32) {
                                lBitBaseSize = 32;
                                lKnxAccessMethod = "knx.paramInt({0})";
                            }
                            if (lType == "signedInt") {
                                lType = "int";
                                lKnxAccessMethod = string.Format("({0}{1}_t){2}", lType, lBitBaseSize, lKnxAccessMethod);
                            } else if (lType == "unsignedInt") {
                                lType = "uint";
                            } else {
                                lType = "enum";
                            }
                        } else if (lTypeNumber.Name == "TypeText") {
                            lType = string.Format("char*, {0} Byte", lBits / 8);
                            lKnxAccessMethod = "knx.paramData({0})";
                            lDirectType = true;
                        } else if (lTypeNumber.Name == "TypeFloat") {
                            lType = "float";
                            lBits = 16;
                            lBitBaseSize = 16;
                            lKnxAccessMethod = "knx.paramFloat({0}, Float_Enc_IEEE754Single)";
                            lDirectType = true;
                        } else if (lTypeNumber.Name == "TypeColor") {
                            lType = "color, uint, 3 Byte";
                            lBits = 24;
                            lBitBaseSize = 32;
                            lKnxAccessMethod = "knx.paramInt({0})";
                            lDirectType = true;
                        }
                    }
                    int lOffset = int.Parse(lMemory.Attributes.GetNamedItem("Offset").Value);
                    int lBitOffset = int.Parse(lMemory.Attributes.GetNamedItem("BitOffset").Value);
                    // Offset and BitOffset might be also defined in Parameter
                    XmlNode lParamOffsetNode = lNode.Attributes.GetNamedItem("Offset");
                    if (lParamOffsetNode != null) lOffset += int.Parse(lParamOffsetNode.Value);
                    if (iWithAbsoluteOffset && !AbsoluteSingleParameters) lOffset += mParameterBlockOffset;
                    XmlNode lParamBitOffsetNode = lNode.Attributes.GetNamedItem("BitOffset");
                    if (lParamBitOffsetNode != null) lBitOffset += int.Parse(lParamBitOffsetNode.Value);
                    lMaxSize = Math.Max(lMaxSize, lOffset + (lBits - 1) / 8 + 1);
                    string lChannelCalculation = "{3}{0}";
                    if (iDefine.IsTemplate) lChannelCalculation = "{3}ParamCalcIndex({3}{0})"; 
                    string lKnxArgument = string.Format(lKnxAccessMethod, lChannelCalculation);
                    bool lIsOut = false;
                    string lOutput = "";
                    string lTimeOutput = "";
                    string lComment = "// " + lNode.Attributes.GetNamedItem("Text").Value;
                    if (lBits < lBitBaseSize || lType == "enum") {
                        //output for bit based parameters 
                        int lShift = (lBitBaseSize - lBits - lBitOffset);
                        string lSubType = string.Format("{0} Bit{1}, Bit {2}", lBits, (lBits == 1) ? "" : "s", (lBitBaseSize - 1 - lBitOffset));
                        if (lBits > 1) lSubType = string.Format("{0}-{1}", lSubType, lShift);
                        // new time base handling
                        if (lParameterTypeId.Contains("_PT-DelayTime")) {
                            lTimeOutput = string.Format("#define Param{3}{4,-25} (paramDelay(" + lKnxArgument + "))", lName, lOffset, lSubType, iHeaderPrefixName, lName + "MS");
                        }
                        cOut.AppendFormat("#define {3}{0,-25} {1,2}      // {2}", lName, lOffset, lSubType, iHeaderPrefixName);
                        if (lBits < lBitBaseSize && lShift >= 0) {
                            cOut.AppendLine();
                            int lMask = ((int)Math.Pow(2, lBits) - 1) << lShift;
                            cOut.AppendFormat("#define     {0}{1}Mask 0x{2:X2}", iHeaderPrefixName, lName, lMask);
                            cOut.AppendLine();
                            cOut.AppendFormat("#define     {0}{1}Shift {2}", iHeaderPrefixName, lName, lShift);
                            if (lBits == 1)
                                lOutput = string.Format("#define Param{3}{4,-25} ((bool)(" + lKnxArgument + " & {3}{0}Mask))", lName, lOffset, lSubType, iHeaderPrefixName, lName);
                            else if (lShift == 0)
                                lOutput = string.Format("#define Param{3}{4,-25} (" + lKnxArgument + " & {3}{0}Mask)", lName, lOffset, lSubType, iHeaderPrefixName, lName);
                            else
                                lOutput = string.Format("#define Param{3}{4,-25} ((" + lKnxArgument + " & {3}{0}Mask) >> {3}{0}Shift)", lName, lOffset, lSubType, iHeaderPrefixName, lName);
                            lIsOut = true;
                        } else if (lType == "enum") {
                            lOutput = string.Format("#define Param{3}{4,-25} (" + lKnxArgument + ")", lName, lOffset, lSubType, iHeaderPrefixName, lName);
                            lIsOut = true;
                        }
                    } else if (lDirectType) {
                        cOut.AppendFormat("#define {3}{0,-25} {1,2}      // {2}", lName, lOffset, lType, iHeaderPrefixName);
                        lOutput = string.Format("#define Param{3}{4,-25} (" + lKnxArgument + ")", lName, lOffset, lType, iHeaderPrefixName, lName);
                        lIsOut = true;
                    } else {
                        cOut.AppendFormat("#define {3}{0,-25} {1,2}      // {4}{2}_t", lName, lOffset, lBits, iHeaderPrefixName, lType);
                        lOutput = string.Format("#define Param{3}{4,-25} (" + lKnxArgument + ")", lName, lOffset, lBitBaseSize, iHeaderPrefixName, lName);
                        lIsOut = true;
                    }
                    cOut.AppendLine();
                    if (lIsOut) {
                        lOut.AppendLine(RemoveControlChars(lComment));
                        lOut.AppendLine(lOutput);
                        if (lTimeOutput != "") {
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
        string mBaggageBaseDir = "";
        string mBaggagesName = "";
        string mBaggageHelpFileName = "";
        string mBaggageIconFileName = "";
        HashSet<string> mBaggageHelpId = new HashSet<string>();
        HashSet<string> mBaggageIconId = new HashSet<string>();

        public bool IsHelpContextId(string iId) {
            return mBaggageHelpId.Contains(iId);
        }

        public bool IsIconId(string iId) {
            return mBaggageIconId.Contains(iId);
        }

        public string BaggagesName { get {return mBaggagesName;} }
        public string CurrentDir { get {return mCurrentDir;} }

        /// <summary>
        /// Load xml document from file resolving includes recursively
        /// </summary>
        public void LoadAdvanced(string iFileName) {
            if (!mLoaded) {
                string lCurrentDir = Path.GetDirectoryName(Path.GetFullPath(iFileName));
                mCurrentDir = lCurrentDir;
                string lFileData = File.ReadAllText(iFileName);
                if (lFileData.Contains("oldxmlns")) {
                    // we get rid of default namespace, we already have an original (this file was already processed by our processor)
                    int lStart = lFileData.IndexOf(" xmlns=\"");
                    if (lStart < 0) {
                        lFileData = lFileData.Replace("oldxmlns", "xmlns");
                    } else {
                        // int lEnd = lFileData.IndexOf("\"", lStart + 8) + 1;
                        lFileData = lFileData.Remove(lStart, 38);
                        // lFileData = lFileData.Substring(0, lStart) + lFileData.Substring(lEnd);
                    }
                } else {
                    // we get rid of default namespace, but remember the original
                    lFileData = lFileData.Replace(" xmlns=\"", " oldxmlns=\"");
                }
                using (StringReader sr = new StringReader(lFileData)) {
                    mDocument.Load(sr);
                    mLoaded = true;
                    ResolveIncludes(lCurrentDir);
                }
            }
        }

        /// <summary>
        /// Resolves Includes inside xml document
        /// </summary>
        /// <param name="iCurrentDir">Directory to use for relative href expressions</param>
        public void ResolveIncludes(string iCurrentDir) {
            nsmgr = new XmlNamespaceManager(mDocument.NameTable);
            nsmgr.AddNamespace("oknxp", cOwnNamespace);
            // process define node
            XmlNodeList lDefineNodes = mDocument.SelectNodes("//oknxp:define", nsmgr);
            if (lDefineNodes != null && lDefineNodes.Count > 0) {
                foreach (XmlNode lDefineNode in lDefineNodes)
                    DefineContent.Factory(lDefineNode);
            }

            //find all XIncludes in a copy of the document
            XmlNodeList lIncludeNodes = mDocument.SelectNodes("//oknxp:include", nsmgr); // get all <include> nodes

            foreach (XmlNode lIncludeNode in lIncludeNodes)
            // try
            {
                //Load document...
                string lIncludeName = lIncludeNode.NodeAttr("href");
                string lHeaderPrefixName = lIncludeNode.NodeAttr("prefix");
                DefineContent lDefine = DefineContent.GetDefineContent(lHeaderPrefixName);
                lDefine.IsTemplate = (lIncludeNode.NodeAttr("type") == "template");
                lDefine.IsParameter = (lIncludeNode.NodeAttr("type") == "parameter");
                ProcessInclude lInclude = ProcessInclude.Factory(lIncludeName, lDefine.header, lHeaderPrefixName);
                string lTargetPath = Path.Combine(iCurrentDir, lIncludeName);
                lInclude.LoadAdvanced(lTargetPath);

                lHeaderPrefixName = lInclude.mHeaderPrefixName;
                lDefine = DefineContent.GetDefineContent(lHeaderPrefixName.Trim('_'));
                //...find include in real document...
                XmlNode lParent = lIncludeNode.ParentNode;
                string lXPath = lIncludeNode.NodeAttr("xpath");
                XmlNodeList lChildren = lInclude.SelectNodes(lXPath);
                string lHeaderFileName = Path.Combine(iCurrentDir, lDefine.header);
                lInclude.ModuleType = lDefine.ModuleType;
                if (lChildren.Count > 0 && "Parameter | Union | ComObject | SNIPPET".Contains(lChildren[0].LocalName)) {
                    if (lDefine.IsTemplate) {
                        // at this point we are including a template file
                        // ChannelCount and KoOffset are taken from correct prefix
                        lInclude.ChannelCount = lDefine.NumChannels;
                        lInclude.KoOffset = lDefine.KoOffset;
                        lInclude.KoSingleOffset = lDefine.KoSingleOffset;
                        lInclude.ReplaceKeys = lDefine.ReplaceKeys;
                        lInclude.ReplaceValues = lDefine.ReplaceValues;
                        ExportHeader(lDefine, lHeaderFileName, lHeaderPrefixName, lInclude, lChildren);
                    }
                    else if (lDefine.IsParameter) {
                        ExportHeader(lDefine, lHeaderFileName, lHeaderPrefixName, lInclude, lChildren);
                    }
                }
                // here we do template processing and repeat the template as many times as
                // the Channels parameter in header file
                for (int lChannel = 1; lChannel <= lInclude.ChannelCount; lChannel++) {
                    foreach (XmlNode lChild in lChildren) {
                        if (lChild.LocalName != "SNIPPET") {
                            //necessary for move between XmlDocument contexts
                            XmlNode lImportNode = lParent.OwnerDocument.ImportNode(lChild, true);
                            // for any Parameter node we do offset recalculation
                            // if there is no prefix name, we do no template replacement
                            if (lHeaderPrefixName != "") ProcessTemplate(lDefine, lChannel, lImportNode, lInclude);
                            lParent.InsertBefore(lImportNode, lIncludeNode);
                        }
                    }
                }
                lParent.RemoveChild(lIncludeNode);
                if (lInclude.ChannelCount > 1) ReplaceDocumentStrings("%N%", lInclude.ChannelCount.ToString());
                // we replace also all additional replace key value pairs
                for (int lCount = 0; lCount < lInclude.ReplaceKeys.Length; lCount++)
                {
                    ReplaceDocumentStrings(mDocument, lInclude.ReplaceKeys[lCount], lInclude.ReplaceValues[lCount]);
                }
                // if (lHeaderPrefixName != "") ProcessIncludeFinish(lChildren);
                //if this fails, something is wrong
            }
            if (lDefineNodes != null && lDefineNodes.Count > 0) {
                foreach (XmlNode lDefineNode in lDefineNodes)
                {
                    lDefineNode.ParentNode.RemoveChild(lDefineNode);
                }
            } 
            // catch { }
        }

        private void ExportHeader(DefineContent iDefine, string iHeaderFileName, string iHeaderPrefixName, ProcessInclude iInclude, XmlNodeList iChildren = null) {
            // iInclude.ParseHeaderFile(iHeaderFileName);

            if (mParameterTypesNode == null) {
                // before we start with template processing, we calculate all Parameter relevant info
                mParameterTypesNode = mDocument.SelectSingleNode("//ParameterTypes");
            }

            if (mParameterTypesNode != null) {
                // the main document contains necessary ParameterTypes definitions
                // there are new parameters in include, we have to calculate a new parameter offset
                XmlNodeList lParameterNodes = mDocument.SelectNodes("//Parameters/Parameter|//Parameters/Union");
                if (lParameterNodes != null) {
                    mParameterBlockSize = CalcParamSize(lParameterNodes, mParameterTypesNode);
                }
                if (iChildren != null) {
                    // ... and we do parameter processing, so we calculate ParamBlockSize for this include
                    int lBlockSize = iInclude.CalcParamSize(iChildren, mParameterTypesNode);
                    if (lBlockSize > 0) {
                        iInclude.ParameterBlockSize = lBlockSize;
                        // we calculate also ParamOffset
                        iInclude.ParameterBlockOffset = mParameterBlockSize;
                    }
                }
            }
            // Header file generation is only possible before we resolve includes
            // First we serialize local parameters of this instance
            ExportHeaderParameterStart(iDefine, mHeaderGenerated, mParameterTypesNode, iHeaderPrefixName);
            // followed by template parameters of the include
            if (iInclude != this) iInclude.ExportHeaderParameterBlock(iDefine, mHeaderGenerated, mParameterTypesNode, iHeaderPrefixName);

            ExportHeaderKoStart(iDefine, mHeaderGenerated, iHeaderPrefixName);
            if (iInclude != this) iInclude.ExportHeaderKoBlock(iDefine, mHeaderGenerated, iHeaderPrefixName);
        }
    }
}

