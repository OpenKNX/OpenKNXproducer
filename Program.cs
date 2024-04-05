using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using CommandLine;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using OpenKNXproducer.Signing;
using System.Globalization;

namespace OpenKNXproducer
{

    struct EtsVersion
    {
        public EtsVersion(string iSubdir, string iEts)
        {
            Subdir = iSubdir;
            ETS = iEts;
        }

        public string Subdir { get; private set; }
        public string ETS { get; private set; }
    }
    class Program
    {
        public static string WorkingDir = "";
        private static Dictionary<string, EtsVersion> EtsVersions = new Dictionary<string, EtsVersion>() {
            {"http://knx.org/xml/project/11", new EtsVersion("4.0.1997.50261", "ETS 4")},
            {"http://knx.org/xml/project/12", new EtsVersion("5.0.204.12971", "ETS 5")},
            {"http://knx.org/xml/project/13", new EtsVersion("5.1.84.17602", "ETS 5.5")},
            {"http://knx.org/xml/project/14", new EtsVersion("5.6.241.33672", "ETS 5.6")},
            {"http://knx.org/xml/project/20", new EtsVersion("5.7", "ETS 5.7")},
            {"http://knx.org/xml/project/21", new EtsVersion("6.0", "ETS 6.0")},
            {"http://knx.org/xml/project/22", new EtsVersion("6.1", "ETS 6.1")},
            {"http://knx.org/xml/project/23", new EtsVersion("6.2", "ETS 6.2")}
        };

        //installation path of a valid ETS instance (only ETS5 or ETS6 supported)
        private static List<string> gPathETS = new List<string> {
            @"C:\Program Files (x86)\ETS6",
            @"C:\Program Files (x86)\ETS5",
            @"C:\Program Files\ETS6",
            @"C:\Program Files\ETS5",
            AppDomain.CurrentDomain.BaseDirectory
        };

        static string FindEtsPath(string lXmlns)
        {
            string lResult = "";

            int lProjectVersion = int.Parse(lXmlns.Substring(27));

            if (EtsVersions.ContainsKey(lXmlns))
            {
                string lEts = "";
                string lPath = "";

                if (Environment.Is64BitOperatingSystem)
                    lPath = @"C:\Program Files (x86)\ETS6";
                else
                    lPath = @"C:\Program Files\ETS6";
                //if we found an ets6, we can generate all versions with it
                if (Directory.Exists(lPath))
                {
                    lResult = lPath;
                    lEts = "ETS 6.x";
                }

                //if we found ets6 dlls, we can generate all versions with it
                if (Directory.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CV", "6.0")))
                {
                    lResult = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CV", "6.0");
                    lEts = "ETS 6 (local)";
                }

                //else search for an older ETS or CV files
                if (string.IsNullOrEmpty(lResult))
                {
                    string lSubdir = EtsVersions[lXmlns].Subdir;
                    lEts = EtsVersions[lXmlns].ETS;

                    foreach (string path in gPathETS)
                    {
                        if (!Directory.Exists(path)) continue;
                        if (Directory.Exists(Path.Combine(path, "CV", lSubdir))) //If subdir exists everything ist fine
                        {
                            lResult = Path.Combine(path, "CV", lSubdir);
                            break;
                        }
                        else
                        { //otherwise it might be the file in the root folder
                            if (!File.Exists(Path.Combine(path, "Knx.Ets.XmlSigning.dll"))) continue;
                            System.Diagnostics.FileVersionInfo versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(Path.Combine(path, "Knx.Ets.XmlSigning.dll"));
                            string newVersion = versionInfo.FileVersion;
                            if (lSubdir.Split('.').Length == 2) newVersion = string.Join('.', newVersion.Split('.').Take(2));
                            // if(newVersion.Split('.').Length != 4) newVersion += ".0";

                            if (lSubdir == newVersion)
                            {
                                lResult = path;
                                break;
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(lResult))
                    Console.WriteLine("Found namespace {1} in xml, using {0} for conversion... (Path: {2})", lEts, lXmlns, lResult);
            }
            if (string.IsNullOrEmpty(lResult)) Console.WriteLine("No valid conversion engine available for xmlns {0}", lXmlns);

            return lResult;
        }

        // Node cache
        static Dictionary<string, XmlNode> gIds = new Dictionary<string, XmlNode>();

        static XmlNode GetNodeById(XmlNode iRootNode, string iId)
        {
            XmlNode lResult = null;
            if (gIds.ContainsKey(iId))
            {
                lResult = gIds[iId];
            }
            else
            {
                lResult = iRootNode.SelectSingleNode(string.Format("//*[@Id='{0}']", iId));
                if (lResult != null) gIds.Add(iId, lResult);
            }
            return lResult;
        }

        private static string CommentDebug;
        private static int mMaxNumChannels;
        public static int MaxNumChannels
        {
            get
            {
                return mMaxNumChannels;
            }
            set
            {
                if (value > mMaxNumChannels)
                    mMaxNumChannels = value;
            }
        }
        private static void CreateComment(XmlDocument iTargetNode, XmlNode iNode, string iId, string iSuffix = "")
        {
            if (CommentDebug == "no") return;
            if (CommentDebug == "auto" && MaxNumChannels > 40) return;

            int lLength = iId.LastIndexOf("_R");
            if (lLength >= 0)
            {
                string lNodeId = iId[..lLength];
                string lTextId = iId;
                string lNodeName = "Id-mismatch! Name not found!";
                string lText = "Id-mismatch! Text not found!";
                if (gIds.ContainsKey(lNodeId)) lNodeName = gIds[lNodeId].NodeAttr("Name");
                if (gIds.ContainsKey(lTextId) && gIds[lTextId].NodeAttr("Text") == "") lTextId = lNodeId;
                if (gIds.ContainsKey(lTextId))
                {
                    lText = gIds[lTextId].NodeAttr("Text");
                    XmlComment lComment = iTargetNode.CreateComment(string.Format(" {0}{3} {1} '{2}'", iNode.Name, lNodeName, lText, iSuffix));
                    // this is very slow!!! But there is no alternative in the current implementation
                    iNode.ParentNode.InsertBefore(lComment, iNode);
                    // very fast, but wrong output
                    // iNode.ParentNode.InsertAfter(lComment, iNode);
                    // alternative is even slower
                    // XmlNode lParentNode = iNode.ParentNode; //necessary, in next line parent gets lost
                    // lParentNode.ReplaceChild(lComment, iNode);
                    // lParentNode.InsertAfter(iNode, lComment);
                }
            }
        }

        public static readonly Dictionary<string, bool> additionalMessages = new();

#nullable enable
        public static void Message(bool iIsError, string iFormat, params object?[] args)
        {
            string lMessage = string.Format(iFormat, args);
            if (!additionalMessages.ContainsKey(lMessage))
                additionalMessages.Add(lMessage, iIsError);
        }
#nullable disable

        private static bool EnsureEtsNamingConventionForKO(ProcessInclude iInclude)
        {
            bool lSuccess = true;
            Console.Write("Converting KO-Ids to Ets naming convention... ");
            // OpenKNX naming convention for KO: %AID%_O-%TT%%CC%nnn, where nnn is a user defined number
            // ETS naming convention for KO: %AID%_O-n, where n is the KO-Number (the Number-Attribute of ComObject-Tag)
            // This coding converts OpenKNX naming convention to ETS naming convention
            XmlNodeList lComObjects = iInclude.SelectNodes("//ApplicationProgram/Static/ComObjectTable/ComObject");
            Dictionary<string, string> lComObjectsMap = new();
            // first we replace all ComObject-Ids and create a mapping table
            foreach (XmlNode lComObject in lComObjects)
            {
                string lId = lComObject.NodeAttr("Id");
                string lNumber = lComObject.NodeAttr("Number");
                string lNewId = lId[..(lId.LastIndexOf("_O-") + 3)] + lNumber;
                lComObject.Attributes.GetNamedItem("Id").Value = lNewId;
                lComObjectsMap.Add(lId, lNewId);
            }
            XmlNodeList lComObjectRefs = iInclude.SelectNodes("//ApplicationProgram/Static/ComObjectRefs/ComObjectRef");
            Dictionary<string, string> lComObjectRefsMap = new();
            // now we replace all ComObjectRefs
            foreach (XmlNode lComObjectRef in lComObjectRefs)
            {
                string lId = lComObjectRef.NodeAttr("Id");
                string lRefId = lComObjectRef.NodeAttr("RefId");
                string lNewRefId = lComObjectsMap[lRefId];
                string lNewId = lId.Replace(lRefId, lNewRefId);
                lComObjectRef.Attributes.GetNamedItem("Id").Value = lNewId;
                lComObjectRef.Attributes.GetNamedItem("RefId").Value = lNewRefId;
                lComObjectRefsMap.Add(lId, lNewId);
            }
            XmlNodeList lComObjectRefRefs = iInclude.SelectNodes("//ApplicationProgram/Dynamic//ComObjectRefRef");
            // now we replace all ComObjectRefRefs
            foreach (XmlNode lComObjectRefRef in lComObjectRefRefs)
            {
                string lRefId = lComObjectRefRef.NodeAttr("RefId");
                string lNewRefId = lComObjectRefsMap[lRefId];
                lComObjectRefRef.Attributes.GetNamedItem("RefId").Value = lNewRefId;
            }
            Console.WriteLine("OK");
            return lSuccess;
        }

        static bool ProcessSanityChecks(ProcessInclude iInclude, bool iWithVersions)
        {

            Console.WriteLine();
            Console.WriteLine("Sanity checks... ");
            CheckHelper lCheck = new();
            XmlDocument lXml = iInclude.GetDocument();

            lCheck.Start("- Id-Homogeneity...");
            XmlNodeList lNodes = lXml.SelectNodes("//*[@Id]");
            foreach (XmlNode lNode in lNodes)
            {
                string lId = lNode.Attributes.GetNamedItem("Id").Value;
                if (lId.Contains("%AID%"))
                {
                    lCheck.WriteFail("There are includes with new '%AID%' and with old 'M-00FA_A-0001-01-0000' notation, this cannot be mixed. No further checks possible until this is solved!");
                    return false; // fail
                }
            }
            lCheck.Finish();

            lCheck.Start("- Id-Uniqueness...");
            foreach (XmlNode lNode in lNodes)
            {
                string lId = lNode.Attributes.GetNamedItem("Id").Value;
                if (gIds.ContainsKey(lId))
                    lCheck.WriteFail("{0} is a duplicate Id in {1}", lId, lNode.NodeAttr("Name"));
                else
                    gIds.Add(lId, lNode);
            }
            lCheck.Finish();

            lCheck.Start("- ParameterType-Id-Syntax...");
            foreach (XmlNode lNode in lNodes)
            {
                string lId = lNode.Attributes.GetNamedItem("Id").Value;
                int lPos = lId.IndexOf("_PT-");
                if (lPos > 0)
                {
                    string lPartId = lId[(lPos + 4)..];
                    lPos = lPartId.IndexOf("_EN-");
                    if (lPos < 0)
                        foreach (char lChar in "-_")
                            if (lPartId.Contains(lChar))
                                lCheck.WriteFail("{0} contains not allowed character {1} in {2} {3}", lId, lChar, lNode.Name, lNode.NodeAttr("Name"));
                }
            }
            lCheck.Finish();

            lCheck.Start("- Id-R_Suffix-Uniqueness...");
            Dictionary<string, bool> lParameterSuffixes = new();
            Dictionary<string, bool> lComObjectSuffixes = new();
            foreach (XmlNode lNode in lNodes)
            {
                string lId = lNode.Attributes.GetNamedItem("Id").Value;
                int lPos = lId.LastIndexOf("_R-");
                if (lId.Contains("_MD-")) lPos = 0;
                Dictionary<string, bool> lSuffixes = null;
                if (lPos > 0)
                {
                    if (lId[..lPos].Contains("_P-"))
                        lSuffixes = lParameterSuffixes;
                    else if (lId[..lPos].Contains("_O-"))
                        lSuffixes = lComObjectSuffixes;
                    if (lSuffixes != null)
                    {
                        string lSuffix = lId.Substring(lPos + 3);
                        if (lSuffixes.ContainsKey(lSuffix))
                            lCheck.WriteFail("{0} is a duplicate _R-Suffix in {1}", lId, lNode.Name);
                        else
                            lSuffixes.Add(lSuffix, false);
                    }
                }
            }
            lCheck.Finish();

            lCheck.Start("- RefId-Integrity...");
            lNodes = lXml.SelectNodes("//*[@RefId]");
            foreach (XmlNode lNode in lNodes)
            {
                if (lNode.Name != "Manufacturer")
                {
                    string lRefId = lNode.Attributes.GetNamedItem("RefId").Value;
                    if (!gIds.ContainsKey(lRefId))
                        lCheck.WriteFail("{0} is referenced in {1} {2}, but not defined", lRefId, lNode.Name, lNode.NodeAttr("Name"));
                    else if (lRefId.Contains("_R"))
                        CreateComment(lXml, lNode, lRefId);
                }
            }
            lCheck.Finish();

            lCheck.Start("- ParamRefId-Integrity...");
            lNodes = lXml.SelectNodes("//*[@ParamRefId]");
            foreach (XmlNode lNode in lNodes)
            {
                if (lNode.Name != "Manufacturer")
                {
                    string lParamRefId = lNode.Attributes.GetNamedItem("ParamRefId").Value;
                    if (!gIds.ContainsKey(lParamRefId))
                        lCheck.WriteFail("{0} is referenced in {1} {2}, but not defined", lParamRefId, lNode.Name, lNode.NodeAttr("Name"));
                    else
                        CreateComment(lXml, lNode, lParamRefId);
                }
            }
            lCheck.Finish();

            lCheck.Start("- AllocatorRefId-Integrity...");
            lNodes = lXml.SelectNodes("//*[@AllocatorRefId]");
            foreach (XmlNode lNode in lNodes)
            {
                string lAllocatorRefId = lNode.Attributes.GetNamedItem("AllocatorRefId").Value;
                if (!gIds.ContainsKey(lAllocatorRefId))
                    lCheck.WriteFail("{0} is referenced in {1} {2}, but not defined", lAllocatorRefId, lNode.Name, lNode.NodeAttr("Name"));
                else
                    CreateComment(lXml, lNode, lAllocatorRefId);
            }
            lCheck.Finish();

            lCheck.Start("- BaseValue-Integrity...");
            lNodes = lXml.SelectNodes("//*[@BaseValue]");
            foreach (XmlNode lNode in lNodes)
            {
                string lBaseValue = lNode.Attributes.GetNamedItem("BaseValue").Value;
                if (!gIds.ContainsKey(lBaseValue))
                    lCheck.WriteFail("{0} is referenced in {1} {2}, but not defined", lBaseValue, lNode.Name, lNode.NodeAttr("Name"));
                else
                    CreateComment(lXml, lNode, lBaseValue);
            }
            lCheck.Finish();

            lCheck.Start("- BaseOffset-Integrity...");
            lNodes = lXml.SelectNodes("//*[@BaseOffset]");
            foreach (XmlNode lNode in lNodes)
            {
                string lBaseOffset = lNode.Attributes.GetNamedItem("BaseOffset").Value;
                if (!gIds.ContainsKey(lBaseOffset))
                    lCheck.WriteFail("{0} is referenced in {1} {2}, but not defined", lBaseOffset, lNode.Name, lNode.NodeAttr("Name"));
                else
                    CreateComment(lXml, lNode, lBaseOffset);
            }
            lCheck.Finish();

            lCheck.Start("- TextParameterRefId-Integrity...");
            // DateTime lStart = DateTime.Now;
            lNodes = lXml.SelectNodes("//*[@TextParameterRefId]");
            foreach (XmlNode lNode in lNodes)
            {
                string lTextParamRefId = lNode.Attributes.GetNamedItem("TextParameterRefId").Value;
                if (!gIds.ContainsKey(lTextParamRefId))
                    lCheck.WriteFail("{0} is referenced in {1} {2}, but not defined", lTextParamRefId, lNode.Name, lNode.NodeAttr("Name"));
                else
                    CreateComment(lXml, lNode, lTextParamRefId);
            }
            // Console.WriteLine($"Time spend: {DateTime.Now - lStart}");
            lCheck.Finish();

            lCheck.Start("- SourceParamRefRef-Integrity...");
            lNodes = lXml.SelectNodes("//*[@SourceParamRefRef]");
            foreach (XmlNode lNode in lNodes)
            {
                string lSourceParamRefRef = lNode.Attributes.GetNamedItem("SourceParamRefRef").Value;
                if (!gIds.ContainsKey(lSourceParamRefRef))
                    lCheck.WriteFail("{0} is referenced in {1} {2}, but not defined", lSourceParamRefRef, lNode.Name, lNode.NodeAttr("Name"));
                else
                    CreateComment(lXml, lNode, lSourceParamRefRef, "-Source");
            }
            lCheck.Finish();

            lCheck.Start("- TargetParamRefRef-Integrity...");
            lNodes = lXml.SelectNodes("//*[@TargetParamRefRef]");
            foreach (XmlNode lNode in lNodes)
            {
                string lTargetParamRefRef = lNode.Attributes.GetNamedItem("TargetParamRefRef").Value;
                if (!gIds.ContainsKey(lTargetParamRefRef))
                    lCheck.WriteFail("{0} is referenced in {1} {2}, but not defined", lTargetParamRefRef, lNode.Name, lNode.NodeAttr("Name"));
                else
                    CreateComment(lXml, lNode, lTargetParamRefRef, "-Target");
            }
            lCheck.Finish();

            lCheck.Start("- ParameterType-Integrity...");
            lNodes = lXml.SelectNodes("//*[@ParameterType]");
            foreach (XmlNode lNode in lNodes)
            {
                string lParameterType = lNode.Attributes.GetNamedItem("ParameterType").Value;
                if (!gIds.ContainsKey(lParameterType))
                    lCheck.WriteFail("{0} is referenced in {1} {2}, but not defined", lParameterType, lNode.Name, lNode.NodeAttr("Name"));
            }
            lCheck.Finish();

            lCheck.Start("- Union-Integrity...");
            lNodes = lXml.SelectNodes("//Union");
            foreach (XmlNode lNode in lNodes)
            {
                string lSize = lNode.NodeAttr("SizeInBit");
                if (lSize == "")
                    lCheck.WriteFail("Union without SizeInBit-Attribute found");
            }
            lCheck.Finish();

            lCheck.Start("- Parameter-Name-Uniqueness...");
            lNodes = lXml.SelectNodes("//Parameter[@Name]");
            Dictionary<string, bool> lParameterNames = new Dictionary<string, bool>();
            foreach (XmlNode lNode in lNodes)
            {
                string lName = lNode.Attributes.GetNamedItem("Name").Value;
                if (lParameterNames.ContainsKey(lName))
                    lCheck.WriteFail("{0} is a duplicate Name in Parameter '{1}'", lName, lNode.NodeAttr("Text"));
                else
                    lParameterNames.Add(lName, true);
            }
            lCheck.Finish();

            lCheck.Start("- Parameter-Value-Integrity...");
            lNodes = lXml.SelectNodes("//Parameter");
            foreach (XmlNode lNode in lNodes)
            {
                // we add the node to parameter cache
                string lNodeId = lNode.NodeAttr("Id");
                string lMessage = string.Format("Parameter {0}", lNode.NodeAttr("Name"));
                XmlNode lParameterAttr = lNode.Attributes.GetNamedItem("Value");
                if (lParameterAttr == null)
                {
                    lCheck.WriteFail("{0} has no Value attribute", lMessage);
                }
                else
                {
                    lParameterAttr.Value = CheckParameterValueIntegrity(lXml, lCheck, lNode, lParameterAttr.Value, lMessage);
                }
            }
            lCheck.Finish();

            bool lSkipTest = false;
            lCheck.Start("- ParameterRef-Value-Integrity...");
            lNodes = lXml.SelectNodes("//ParameterRef[@Value]");
            foreach (XmlNode lNode in lNodes)
            {
                // find parameter
                XmlNode lParameterNode = GetNodeById(lXml, lNode.NodeAttr("RefId"));
                if (lParameterNode == null)
                {
                    lSkipTest = true;
                    break;
                }
                string lMessage = string.Format("ParameterRef {0}, referencing Parameter {1},", lNode.NodeAttr("Id"), lParameterNode.NodeAttr("Name"));
                XmlNode lParameterRefValueAttr = lNode.Attributes.GetNamedItem("Value");
                lParameterRefValueAttr.Value = CheckParameterValueIntegrity(lXml, lCheck, lParameterNode, lParameterRefValueAttr.Value, lMessage);
            }
            if (lSkipTest)
                lCheck.WriteFail("Test not possible due to Errors in ParameterRef definitions (sove above problems first)");
            lCheck.Finish();

            lCheck.Start("- ComObject-Name-Uniqueness...");
            lNodes = lXml.SelectNodes("//ComObject[@Name]");
            Dictionary<string, bool> lKoNames = new Dictionary<string, bool>();
            foreach (XmlNode lNode in lNodes)
            {
                string lName = lNode.Attributes.GetNamedItem("Name").Value;
                if (lKoNames.ContainsKey(lName))
                    lCheck.WriteFail("{0} is a duplicate Name in ComObject number {1}", lName, lNode.NodeAttr("Number"));
                else
                    lKoNames.Add(lName, true);
            }
            lCheck.Finish();

            lCheck.Start("- ComObject-Number-Uniqueness...");
            lNodes = lXml.SelectNodes("//ComObject[@Number]");
            Dictionary<int, bool> lKoNumbers = new Dictionary<int, bool>();
            foreach (XmlNode lNode in lNodes)
            {
                int lNumber = 0;
                bool lIsInt = int.TryParse(lNode.Attributes.GetNamedItem("Number").Value, out lNumber);
                if (lIsInt)
                {
                    if (lNumber == 0)
                        lCheck.WriteFail("ComObject Number 0 is not allowed in ComObject with name {1}", lNumber, lNode.NodeAttr("Name"));
                    else if (lKoNumbers.ContainsKey(lNumber))
                        lCheck.WriteFail("{0} is a duplicate Number in ComObject with name {1}", lNumber, lNode.NodeAttr("Name"));
                    else
                        lKoNumbers.Add(lNumber, true);
                }
                else
                    lCheck.WriteFail("ComObject.Number is not an Integer in ComObject with name {0}", lNode.NodeAttr("Name"));
            }
            lCheck.Finish();

            lCheck.Start("- RefId-Id-Comparison...");
            lNodes = lXml.SelectNodes("//ParameterRef|//ComObjectRef");
            Regex regex = new("(_O-|_UP-|_P-|_R-)");
            foreach (XmlNode lNode in lNodes)
            {
                string lId = lNode.Attributes.GetNamedItem("Id").Value;
                string[] lIds = regex.Split(lId);
                string lRefId = lNode.Attributes.GetNamedItem("RefId").Value;
                string[] lRefIds = regex.Split(lRefId);
                if (lIds[2].Length == 7 && lIds[4].Length == 9 && lRefIds[2].Length == 7)
                {
                    // seems to be OpenKNX naming convention
                    if (lIds[2] != lRefIds[2])
                        lCheck.WriteFail("{0} {1}: The first Id-Part {2}{3} should fit to the RefId-Part {2}{4} (OpenKNX naming convention)", lNode.Name, lId, lIds[1], lIds[2], lRefIds[2]);
                    // if (!lIds[4].StartsWith(lIds[2])) {
                    //     lCheck.WriteFail("{0} {1}: The first Id-Part {2} should fit to the second Id-Part {3} (OpenKNX naming convention)", lNode.Name, lId, lIds[2], lIds[4]);
                    // }    
                }
            }
            lCheck.Finish();

            lCheck.Start("- RefId-RefRef-Comparison...");
            lNodes = lXml.SelectNodes("//ParameterRefRef|//ComObjectRefRef");
            Regex regex1 = new Regex("(_UP-|_P-)");
            foreach (XmlNode lNode in lNodes)
            {
                string lId = lNode.NodeAttr("RefId");
                if (lNode.Name == "ParameterRefRef")
                {
                    if (!regex1.IsMatch(lId))
                        lCheck.WriteFail("{0} {1}: Referenced Id is not a Parameter", lNode.Name, lId);
                }
                else if (lNode.Name == "ComObjectRefRef")
                {
                    if (!lId.Contains("_O-"))
                        lCheck.WriteFail("{0} {1}: Referenced Id is not a ComObject", lNode.Name, lId);
                }
            }
            lCheck.Finish();

            lCheck.Start("- Id-Namespace...");
            // find refid
            XmlNode lApplicationProgramNode = lXml.SelectSingleNode("/KNX/ManufacturerData/Manufacturer/ApplicationPrograms/ApplicationProgram");
            string lApplicationId = lApplicationProgramNode.Attributes.GetNamedItem("Id").Value;
            string lRefNs = lApplicationId; //.Replace("M-00FA_A", "");
            if (lRefNs.StartsWith("M-")) lRefNs = lRefNs.Substring(8);
            // check all nodes according to refid
            lNodes = lXml.SelectNodes("//*/@*[string-length() > '13']");
            foreach (XmlNode lNode in lNodes)
            {
                if (lNode.Value != null)
                {
                    var lMatch = Regex.Match(lNode.Value, "-[0-9A-F]{4}-[0-9A-F]{2}-[0-9A-F]{4}");
                    if (lMatch.Success)
                    {
                        if (lMatch.Value != lRefNs)
                        {
                            XmlElement lElement = ((XmlAttribute)lNode).OwnerElement;
                            lCheck.WriteFail("{0} of node {2} {3} is in a different namespace than application namespace {1}", lMatch.Value, lRefNs, lElement.Name, lElement.NodeAttr("Name"));
                        }
                    }
                }
            }
            lCheck.Finish();

            lCheck.Start("- Id-Format...");
            // An id has to fulfill a specific format
            string lIdPart = "";
            foreach (var lKeyValuePair in gIds)
            {
                string lId = lKeyValuePair.Key;
                string lIdMatch = "";
                string lIdMatchReadable = "";
                lId = lId.Replace(lApplicationId, "");
                // remove module prefixes
                if (lId.StartsWith("_MD-"))
                {
                    var lUnderscoreIndex = lId.IndexOf("_", 3);
                    if (lUnderscoreIndex > 0)
                        lId = lId.Substring(lUnderscoreIndex);
                }
                XmlNode lElement = lKeyValuePair.Value;
                switch (lElement.Name)
                {
                    case "Parameter":
                        if (lElement.ParentNode.Name == "Union")
                        {
                            lIdPart = "_UP-";
                        }
                        else
                        {
                            lIdPart = "_P-";
                        }
                        lIdMatch = lIdPart + @"[1-3]?\d\d{6}";
                        lIdMatchReadable = lIdPart + "tcccnnn$";
                        break;
                    case "ComObject":
                        lIdPart = "_O-";
                        lIdMatch = lIdPart + @"[1-3]?\d\d{6}";
                        lIdMatchReadable = lIdPart + "tcccnnn$";
                        break;
                    case "ParameterType":
                        lIdPart = "_PT-";
                        break;
                    case "Enumeration":
                        lIdPart = "_PT-";
                        string pt_id = lElement.ParentNode.ParentNode.Attributes["Id"].Value;
                        lIdPart = pt_id.Substring(pt_id.IndexOf("_PT-")) + "_EN-"; // + lElement.Attributes["Value"].Value; don't check the value, ETS accepts anyway.
                        break;
                    case "ParameterRef":
                    case "ComObjectRef":
                        lIdPart = "_R-";
                        if (lId.Contains(lIdPart)) lIdPart = "";
                        lIdMatch = @"([1-3]?\d\d{6})_R-\1\d\d$";
                        lIdMatchReadable = "tcccnnn_R-tcccnnnrr";
                        break;
                    case "ParameterBlock":
                        lIdPart = "_PB-";
                        break;
                    case "ParameterSeparator":
                        lIdPart = "_PS-";
                        break;
                    case "Channel":
                        lIdPart = "_CH-";
                        break;
                    case "Row":
                        lIdPart = "_R-";
                        if (lId.Contains(lIdPart)) lIdPart = "_PB-";
                        break;
                    case "Column":
                        lIdPart = "_C-";
                        if (lId.Contains(lIdPart)) lIdPart = "_PB-";
                        break;
                    default:
                        lIdPart = "";
                        break;
                }
                if (lIdPart != "" && !lId.StartsWith(lIdPart))
                    lCheck.WriteFail("{0} {1} has the Id={2}, but this Id is missing the required part {3}", lElement.Name, lElement.NodeAttr("Name"), lKeyValuePair.Key, lIdPart);
                if (iWithVersions && lIdMatch != "" && !Regex.IsMatch(lId, lIdMatch))
                    lCheck.WriteFail("{0} {1} has the Id={2}, but this Id has not the OpenKNX-Format {3}", lElement.Name, lElement.NodeAttr("Name"), lKeyValuePair.Key, lIdMatchReadable);
            }
            lCheck.Finish();

            lCheck.Start("- Serial number...");
            lNodes = lXml.SelectNodes("//*[@SerialNumber]");
            foreach (XmlNode lNode in lNodes)
            {
                string lSerialNumber = lNode.Attributes.GetNamedItem("SerialNumber").Value;
                if (lSerialNumber.Contains('-'))
                    lCheck.WriteFail("Hardware.SerialNumber={0}, it contains a dash (-), this will cause problems in knxprod.", lSerialNumber);
            }
            lCheck.Finish();

            lCheck.Start("- HelpContext-Ids...");
            lNodes = lXml.SelectNodes("//*/@HelpContext");
            foreach (XmlNode lNode in lNodes)
            {
                if (!iInclude.IsHelpContextId(lNode.Value))
                    lCheck.WriteFail("HelpContext {0} not found in HelpContext baggage", lNode.Value);
            }
            lCheck.Finish();

            lCheck.Start("- Icon-Ids...");
            lNodes = lXml.SelectNodes("//*/@Icon");
            foreach (XmlNode lNode in lNodes)
            {
                if (!iInclude.IsIconId(lNode.Value))
                    lCheck.WriteFail("Icon {0} not found in Icon baggage", lNode.Value);
            }
            lCheck.Finish();

            lCheck.Start("- Baggage-File-Existence...");
            lNodes = lXml.SelectNodes("//Baggage[@TargetPath]");
            foreach (XmlNode lNode in lNodes)
            {
                string lFileName = Path.Combine(iInclude.BaggagesName, lNode.NodeAttr("TargetPath"), lNode.NodeAttr("Name"));
                if (!File.Exists(Path.Combine(iInclude.CurrentDir, lFileName)))
                    lCheck.WriteFail("File {0} not found in baggage dir", lFileName);
            }
            lCheck.Finish();

            lCheck.Start("- Application data...");
            lNodes = lXml.SelectNodes("//ApplicationProgram");
            foreach (XmlNode lNode in lNodes)
            {
                int lNumber = -1;
                bool lIsInt = int.TryParse(lNode.Attributes.GetNamedItem("ApplicationNumber").Value, out lNumber);
                if (!lIsInt || lNumber < 0)
                    lCheck.WriteFail("ApplicationProgram.ApplicationNumber is incorrect or could not be parsed");
                lNumber = -1;
                lIsInt = int.TryParse(lNode.Attributes.GetNamedItem("ApplicationVersion").Value, out lNumber);
                if (!lIsInt || lNumber < 0)
                    lCheck.WriteFail("ApplicationProgram.ApplicationVersion is incorrect or could not be parsed");
            }
            lCheck.Finish();

            lCheck.Start("- Memory size...");
            lNodes = lXml.SelectNodes("//*[self::RelativeSegment or self::LdCtrlRelSegment or self::LdCtrlWriteRelMem][@Size]");
            foreach (XmlNode lNode in lNodes)
            {
                int lNumber = -1;
                string lValue = lNode.Attributes.GetNamedItem("Size").Value;
                bool lIsInt = int.TryParse(lValue, out lNumber);
                if (!lIsInt || lNumber <= 0)
                    lCheck.WriteFail("Size-Attribute of {0} is incorrect ({1}), are you missing %MemorySize% replacement?", lNode.Name, lValue);
            }
            lCheck.Finish();

            lCheck.Start("- Unused config entries...");
            foreach (var lConfig in ProcessInclude.Config)
            {
                if (!lConfig.Value.WasReplaced)
                    lCheck.WriteWarn(1, "Config name {0} with value {1} was never replaced", lConfig.Key, lConfig.Value.ConfigValue);
            }
            lCheck.Finish();

            lCheck.Start("- Not replaced config entries...");
            lNodes = lXml.SelectNodes("//@*[contains(.,'%')]");
            Regex lConfigName = new(@"%[A-Za-z0-9\-_]*%");
            foreach (XmlAttribute lAttr in lNodes)
            {
                // if (lAttr.Value.Contains('%'))
                // {
                Match lMatch = lConfigName.Match(lAttr.Value);
                    if (lMatch.Success)
                        lCheck.WriteWarn(2, "The value {0} of attribute {1} in node {2} might be an unreplaced config entry", lAttr.Value, lAttr.Name, lAttr.OwnerElement.Name);
                // }
            }
            lCheck.Finish();

            lCheck.Start("- Integrity of script methods...");
            XmlNode lScript = lXml.SelectSingleNode("//Script");
            if (lScript != null)
            {
                Regex lFindFunctions = new(@"function\s*([A-Za-z0-9_]*)\s*\(");
                MatchCollection lFunctions = lFindFunctions.Matches(lScript.InnerText);
                XmlNodeList lAttributes = lXml.SelectNodes("//Button/@EventHandler|//ParameterCalculation/@LRTransformationFunc|//ParameterCalculation/@RLTransformationFunc");
                // speedup: transfer function call into a Hashtable
                Dictionary<string, bool> lFunctionCalls = new();
                foreach (XmlNode lAttribute in lAttributes)
                    if (!lFunctionCalls.ContainsKey(lAttribute.Value))
                        lFunctionCalls[lAttribute.Value] = false;
                // check unused javascript functions
                foreach (Match lFunction in lFunctions.Cast<Match>())
                {
                    string lFunctionName = lFunction.Groups[1].Value;
                    if (lFunctionCalls.ContainsKey(lFunctionName))
                        lFunctionCalls[lFunctionName] = true;
                    else
                        lCheck.WriteWarn(3, "Function with name {0} was never called form xml", lFunctionName);
                }
                foreach (var lFunctionCall in lFunctionCalls)
                    if (!lFunctionCall.Value)
                        lCheck.WriteFail("Function with name {0} is missing in <Script> definitions", lFunctionCall.Key);
            }
            lCheck.Finish();

            lCheck.Start("- Further messages during document processing...");
            foreach (var lMessage in additionalMessages)
            {
                if (lMessage.Value)
                    lCheck.WriteFail(lMessage.Key, "");
                else
                    lCheck.WriteWarn(0, lMessage.Key, "");
            }
            lCheck.Finish("NONE");

            Console.WriteLine();
            return !lCheck.IsFail;
        }

        static Dictionary<string, XmlNode> sParameterTypes = null;

        private static string CheckParameterValueIntegrity(XmlNode iTargetNode, CheckHelper iCheck, XmlNode iParameterNode, string iValue, string iMessage)
        {
            string lValue = iValue;
            if (sParameterTypes == null)
            {
                // init global parameter types cache
                XmlNodeList lParameterTypeNodes = iTargetNode.SelectNodes("//ParameterType[@Id]");
                sParameterTypes = new();
                foreach (XmlNode lNode in lParameterTypeNodes)
                    sParameterTypes.Add(lNode.NodeAttr("Id"), lNode);
            }
            string lParameterType = iParameterNode.NodeAttr("ParameterType");
            if (lParameterType == "")
            {
                iCheck.WriteFail("Parameter {0} has no ParameterType attribute", iParameterNode.NodeAttr("Name"));
            }
            if (iValue != null && lParameterType != "")
            {
                if (sParameterTypes.ContainsKey(lParameterType))
                {
                    // get first child ignoring comments
                    XmlNode lChild = sParameterTypes[lParameterType].ChildNodes[0];
                    while (lChild != null && lChild.NodeType != XmlNodeType.Element) lChild = lChild.NextSibling;

                    int sizeInBit;
                    long maxSize = 0;
                    switch (lChild.Name)
                    {
                        case "TypeText":
                            if (!int.TryParse(lChild.Attributes["SizeInBit"]?.Value, out sizeInBit))
                                iCheck.WriteFail("SizeInBit of {0} cannot be converted to a number, value is '{1}'", iMessage, lChild.Attributes["SizeInBit"]?.Value ?? "empty");
                            else
                                maxSize = sizeInBit / 8;
                            break;

                        case "TypeFloat":
                            //There is no SizeInBit attribute
                            break;

                        case "TypePicture":
                            //There is no SizeInBit attribute
                            break;

                        case "TypeColor":
                            //There is no SizeInBit attribute
                            break;

                        case "TypeRawData":
                            //There is no SizeInBit attribute
                            break;

                        case "TypeIPAddress":
                            //There is no SizeInBit attribute
                            break;

                        default:
                            if (!int.TryParse(lChild.Attributes["SizeInBit"]?.Value, out sizeInBit))
                                iCheck.WriteFail("SizeInBit of {0} cannot be converted to a number, value is '{1}'", iMessage, lChild.Attributes["SizeInBit"]?.Value ?? "empty");
                            else
                                maxSize = Convert.ToInt64(Math.Pow(2, int.Parse(lChild.Attributes["SizeInBit"]?.Value ?? "0")));
                            break;
                    }

                    long min = 0, max = 0;
                    switch (lChild.Name)
                    {
                        case "TypeNumber":
                            long lDummyLong;
                            bool lSuccess = long.TryParse(iValue, out lDummyLong);
                            if (!lSuccess)
                            {
                                iCheck.WriteFail("Value of {0} cannot be converted to a number, value is '{1}'", iMessage, iValue);
                            }
                            if (!long.TryParse(lChild.Attributes["minInclusive"]?.Value, out min))
                                iCheck.WriteFail("MinInclusive of {0} cannot be converted to a number, value is '{1}'", iMessage, lChild.Attributes["minInclusive"]?.Value ?? "empty");
                            if (!long.TryParse(lChild.Attributes["maxInclusive"]?.Value, out max))
                                iCheck.WriteFail("MaxInclusive of {0} cannot be converted to a number, value is '{1}'", iMessage, lChild.Attributes["minInclusive"]?.Value ?? "empty");

                            switch (lChild.Attributes["Type"]?.Value)
                            {
                                case "unsignedInt":
                                    if (min < 0)
                                        iCheck.WriteFail("MinInclusive of {0} cannot be smaller than 0, value is '{1}'", iMessage, min);
                                    if (max > maxSize)
                                        iCheck.WriteFail("MaxInclusive of {0} cannot be greater than {1}, value is '{2}'", iMessage, maxSize - 1, max);
                                    break;

                                case "signedInt":
                                    if (min < ((maxSize / 2) * (-1)))
                                        iCheck.WriteFail("MinInclusive of {0} cannot be smaller than {1}, value is '{2}'", iMessage, ((maxSize / 2) * (-1)), min);
                                    if (max > ((maxSize / 2)))
                                        iCheck.WriteFail("MaxInclusive of {0} cannot be greater than {1}, value is '{2}'", iMessage, ((maxSize / 2) - 1), max);
                                    break;
                            }
                            if (lDummyLong < min)
                                iCheck.WriteWarn(4, "Value of {0} cannot be smaller than MinInclusive {1}, value is '{2}'", iMessage, min, iValue);
                            if (lDummyLong > max)
                                iCheck.WriteWarn(5, "Value of {0} cannot be greater than MaxInclusive {1}, value is '{2}'", iMessage, max, iValue);
                            break;
                        case "TypeFloat":
                            lSuccess = double.TryParse(iValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double lDummyFloat);
                            if (!lSuccess || iValue.Contains(','))
                                iCheck.WriteFail("Value of {0} cannot be converted to a float, value is '{1}'", iMessage, iValue);
                            else
                                lValue = lDummyFloat.ToString("E15", CultureInfo.InvariantCulture);
                            //TODO check value
                            break;
                        case "TypeColor":
                            if (!iValue.StartsWith('#') && iValue.Length == 6)
                            {
                                iCheck.WriteWarn(6, "Value of {0} should be preceded by a '#' character, this is corrected by OpenKNXproducer in knxprod", iMessage, iValue);
                                lValue = '#' + iValue;
                            }
                            lSuccess = int.TryParse(lValue[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int lDummyInt);
                            if (!lSuccess || lValue.Length != 7)
                                iCheck.WriteFail("Value of {0} should be a color in hex format #RRGGBB, but is {1}", iMessage, iValue);
                            break;
                        case "TypeRestriction":
                            lSuccess = false;
                            int maxEnumValue = -1;
                            foreach (XmlNode lEnumeration in lChild.ChildNodes)
                            {
                                if (lEnumeration.Name == "Enumeration")
                                {
                                    if (lEnumeration.NodeAttr("Value") == iValue)
                                    {
                                        lSuccess = true;
                                    }
                                    int enumValue = 0;
                                    if (!int.TryParse(lEnumeration.Attributes["Value"]?.Value, out enumValue))
                                        iCheck.WriteFail("Enum Value of {2} in {0} cannot be converted to an int, value is '{1}'", iMessage, iValue, lParameterType);
                                    else
                                    {
                                        if (enumValue > maxEnumValue)
                                            maxEnumValue = enumValue;
                                    }
                                }
                            }
                            if (!lSuccess)
                            {
                                iCheck.WriteFail("Value of {0} is not contained in enumeration {2}, value is '{1}'", iMessage, iValue, lParameterType);
                            }
                            if (maxEnumValue >= maxSize)
                                iCheck.WriteFail("Max Enum Value of {0} can not be greater than {2}, value is '{1}'", iMessage, maxEnumValue, maxSize);
                            break;
                        case "TypeText":
                            //TODO add string length validation
                            if (iValue.Length > maxSize)
                                iCheck.WriteFail("String Length of {0} can not be greater than {2}, length is '{1}'", iMessage, maxSize, iValue.Length);
                            break;
                        case "TypeIPAddress":
                            Regex lRegex = new(@"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}");
                            if (!lRegex.IsMatch(iValue))
                                iCheck.WriteFail($"Value {iValue} for IPAddress is not in the expected format xxx.xxx.xxx.xxx");
                            break;
                        default:
                            break;
                    }
                }
            }
            return lValue;
        }

        #region Reflection
        private static object InvokeMethod(Type type, string methodName, object[] args)
        {

            var mi = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
            return mi.Invoke(null, args);
        }

        private static void SetProperty(Type type, string propertyName, object value)
        {
            PropertyInfo prop = type.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Static);
            prop.SetValue(null, value, null);
        }
        #endregion

        private static string GetAbsWorkingDir(string iFilename)
        {
            string lResult = Path.GetFullPath(iFilename);
            lResult = Path.GetDirectoryName(lResult);
            return lResult;
        }

        // private static void ExportXsd(string iXsdFileName) {
        //     using (var fileStream = new FileStream("knx.xsd", FileMode.Create))
        //     using (var stream = DocumentSet.GetXmlSchemaDocumentAsStream(KnxXmlSchemaVersion.Version14)) {
        //         while (true) {
        //             var buffer = new byte[4096];
        //             var count = stream.Read(buffer, 0, 4096);
        //             if (count == 0)
        //                 break;

        //             fileStream.Write(buffer, 0, count);
        //         }
        //     }
        // }

        private enum DocumentCategory
        {
            None,
            Catalog,
            Hardware,
            Application
        }

        private static DocumentCategory GetDocumentCategory(XElement iTranslationUnit)
        {
            DocumentCategory lCategory = DocumentCategory.None;
            string lId = iTranslationUnit.Attribute("RefId").Value;

            lId = lId.Substring(6);
            if (lId.StartsWith("_A-"))
                lCategory = DocumentCategory.Application;
            else if (lId.StartsWith("_CS-"))
                lCategory = DocumentCategory.Catalog;
            else if (lId.StartsWith("_H-") && lId.Contains("_CI-"))
                lCategory = DocumentCategory.Catalog;
            else if (lId.StartsWith("_H-") && lId.Contains("_P-"))
                lCategory = DocumentCategory.Hardware;
            else if (lId.StartsWith("_H-"))
                lCategory = DocumentCategory.Hardware;

            return lCategory;
        }

        private static void AddTranslationUnit(XElement iTranslationUnit, List<XElement> iLanguageList, string iNamespaceName)
        {
            // we assume, that here are adding just few TranslationUnits
            // get parent element (Language)
            XElement lSourceLanguage = iTranslationUnit.Parent;
            string lSourceLanguageId = lSourceLanguage.Attribute("Identifier").Value;
            XElement lTargetLanguage = iLanguageList.Elements("Child").FirstOrDefault(child => child.Attribute("Name").Value == lSourceLanguageId);
            if (lTargetLanguage == null)
            {
                // we create language element
                lTargetLanguage = new XElement(XName.Get("Language", iNamespaceName), new XAttribute("Identifier", lSourceLanguageId));
                iLanguageList.Add(lTargetLanguage);
            }
            iTranslationUnit.Remove();
            lTargetLanguage.Add(iTranslationUnit);
        }

        private static bool ValidateXsd(string iWorkingDir, string iTempFileName, string iXmlFileName, string iXsdFileName, bool iAutoXsd)
        {
            // iDocument.Save("validationTest.xml");
            string lContent = File.ReadAllText(iTempFileName);
            XDocument lDocument = XDocument.Parse(lContent, LoadOptions.SetLineInfo);
            lDocument.Root.Attribute("oldxmlns")?.Remove();

            string lXmlFileName = Path.GetFullPath(iXmlFileName);
            bool lError = false;
            if (string.IsNullOrEmpty(iXsdFileName) && iAutoXsd)
            {
                // we try to find a schema in the xml document
                Regex lRegex = new Regex("<\\?xml-model.* href=\"(.*.xsd)\" ");
                Match lMatch = lRegex.Match(lContent);
                iXsdFileName = lMatch.Groups[1].Value;
                // in case of an -editor.xsd, we use the original xsd
                iXsdFileName = iXsdFileName.Replace("-editor.xsd", ".xsd");
                if (!Path.IsPathFullyQualified(iXsdFileName))
                {
                    iXsdFileName = Path.Combine(iWorkingDir, iXsdFileName);
                    iXsdFileName = Path.GetRelativePath(Directory.GetCurrentDirectory(), iXsdFileName);
                }
            }
            if (!string.IsNullOrEmpty(iXsdFileName))
            {
                Console.Write("Validation against {0}... ", iXsdFileName);
                if (File.Exists(iXsdFileName))
                {
                    XmlSchemaSet schemas = new XmlSchemaSet();
                    schemas.Add(null, iXsdFileName);

                    lDocument.Validate(schemas, (o, e) =>
                    {
                        if (!lError) Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write($"XML {e.Severity} in {lXmlFileName}({e.Exception.LineNumber},{e.Exception.LinePosition}): ");
                        Console.ResetColor();
                        Console.WriteLine($"{e.Message}");
                        lError = true;
                    });
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(lError ? "" : "OK");
                    Console.ResetColor();
                }
                else
                {
                    Console.WriteLine("Xsd-File not found!");
                }
            }
            return lError;
        }

        private static int ExportKnxprod(string iPathETS, string iWorkingDir, string iKnxprodFileName, string lTempXmlFileName, string iBaggageName, string iXsdFileName, bool iIsDebug, bool iAutoXsd)
        {
            if (iPathETS == "")
            {
                Console.WriteLine("No ETS found, skipped knxprod creation!");
                return 0;
            }
            try
            {
                if (ValidateXsd(iWorkingDir, lTempXmlFileName, lTempXmlFileName, iXsdFileName, iAutoXsd)) return 1;

                Console.WriteLine("Generating knxprod file...");

                XDocument xdoc = null;
                string xmlContent = File.ReadAllText(lTempXmlFileName);
                xdoc = XDocument.Parse(xmlContent, LoadOptions.SetLineInfo);

                XNode lXmlModel = xdoc.FirstNode;
                if (lXmlModel.NodeType == XmlNodeType.ProcessingInstruction)
                    lXmlModel.Remove();

                string ns = xdoc.Root.Name.NamespaceName;
                XElement xmanu = xdoc.Root.Element(XName.Get("ManufacturerData", ns)).Element(XName.Get("Manufacturer", ns));

                string manuId = xmanu.Attribute("RefId").Value;
                string localPath = AppDomain.CurrentDomain.BaseDirectory;
                if (Directory.Exists(Path.Combine(localPath, "Temp")))
                    Directory.Delete(Path.Combine(localPath, "Temp"), true);

                Directory.CreateDirectory(Path.Combine(localPath, "Temp"));
                Directory.CreateDirectory(Path.Combine(localPath, "Temp", manuId)); //Get real Manu


                XElement xcata = xmanu.Element(XName.Get("Catalog", ns));
                XElement xhard = xmanu.Element(XName.Get("Hardware", ns));
                XElement xappl = xmanu.Element(XName.Get("ApplicationPrograms", ns));
                XElement xbagg = xmanu.Element(XName.Get("Baggages", ns));

                List<XElement> xcataL = new List<XElement>();
                List<XElement> xhardL = new List<XElement>();
                List<XElement> xapplL = new List<XElement>();
                List<XElement> xbaggL = new List<XElement>();
                XElement xlangs = xmanu.Element(XName.Get("Languages", ns));

                if (xlangs != null)
                {
                    xlangs.Remove();
                    foreach (XElement xTrans in xlangs.Descendants(XName.Get("TranslationUnit", ns)).ToList())
                    {
                        DocumentCategory lCategory = GetDocumentCategory(xTrans);
                        switch (lCategory)
                        {
                            case DocumentCategory.Catalog:
                                AddTranslationUnit(xTrans, xcataL, ns);
                                break;
                            case DocumentCategory.Hardware:
                                AddTranslationUnit(xTrans, xhardL, ns);
                                break;
                            case DocumentCategory.Application:
                                AddTranslationUnit(xTrans, xapplL, ns);
                                break;
                            default:
                                throw new Exception("Unknown Translation Type: " + lCategory.ToString());
                        }

                    }
                }
                xhard.Remove();
                if (xbagg != null) xbagg.Remove();

                //Save Catalog
                xappl.Remove();
                if (xcataL.Count > 0)
                {
                    xlangs.Elements().Remove();
                    foreach (XElement xlang in xcataL)
                        xlangs.Add(xlang);
                    xmanu.Add(xlangs);
                }
                xdoc.Save(Path.Combine(localPath, "Temp", manuId, "Catalog.xml"));
                if (xcataL.Count > 0) xlangs.Remove();
                xcata.Remove();

                // Save Hardware
                xmanu.Add(xhard);
                if (xhardL.Count > 0)
                {
                    xlangs.Elements().Remove();
                    foreach (XElement xlang in xhardL)
                        xlangs.Add(xlang);
                    xmanu.Add(xlangs);
                }
                xdoc.Save(Path.Combine(localPath, "Temp", manuId, "Hardware.xml"));
                if (xhardL.Count > 0) xlangs.Remove();
                xhard.Remove();

                if (xbagg != null)
                {
                    // Save Baggages
                    xmanu.Add(xbagg);
                    if (xbaggL.Count > 0)
                    {
                        xlangs.Elements().Remove();
                        foreach (XElement xlang in xbaggL)
                            xlangs.Add(xlang);
                        xmanu.Add(xlangs);
                    }
                    xdoc.Save(Path.Combine(localPath, "Temp", manuId, "Baggages.xml"));
                    if (xbaggL.Count > 0) xlangs.Remove();
                    xbagg.Remove();
                }

                xmanu.Add(xappl);
                if (xapplL.Count > 0)
                {
                    xlangs.Elements().Remove();
                    foreach (XElement xlang in xapplL)
                        xlangs.Add(xlang);
                    xmanu.Add(xlangs);
                }
                string appId = xappl.Elements(XName.Get("ApplicationProgram", ns)).First().Attribute("Id").Value;
                xdoc.Save(Path.Combine(localPath, "Temp", manuId, $"{appId}.xml"));
                if (xapplL.Count > 0) xlangs.Remove();

                // Copy baggages to output dir
                string lSourceBaggageName = Path.Combine(iWorkingDir, iBaggageName);
                var lSourceBaggageDir = new DirectoryInfo(lSourceBaggageName);
                if (lSourceBaggageDir.Exists)
                    lSourceBaggageDir.DeepCopy(Path.Combine(localPath, "Temp", manuId, "Baggages"));

                IDictionary<string, string> applProgIdMappings = new Dictionary<string, string>();
                IDictionary<string, string> applProgHashes = new Dictionary<string, string>();
                IDictionary<string, string> mapBaggageIdToFileIntegrity = new Dictionary<string, string>(50);

                FileInfo hwFileInfo = new FileInfo(Path.Combine(localPath, "Temp", manuId, "Hardware.xml"));
                FileInfo catalogFileInfo = new FileInfo(Path.Combine(localPath, "Temp", manuId, "Catalog.xml"));
                FileInfo appInfo = new FileInfo(Path.Combine(localPath, "Temp", manuId, $"{appId}.xml"));

                int nsVersion = int.Parse(ns.Substring(ns.LastIndexOf('/') + 1));
                ApplicationProgramHasher aph = new ApplicationProgramHasher(appInfo, mapBaggageIdToFileIntegrity, iPathETS, nsVersion, true);
                aph.Hash();

                applProgIdMappings.Add(aph.OldApplProgId, aph.NewApplProgId);
                if (!applProgHashes.ContainsKey(aph.NewApplProgId))
                    applProgHashes.Add(aph.NewApplProgId, aph.GeneratedHashString);

                HardwareSigner hws = new HardwareSigner(hwFileInfo, applProgIdMappings, applProgHashes, iPathETS, nsVersion, true);
                hws.SignFile();
                IDictionary<string, string> hardware2ProgramIdMapping = hws.OldNewIdMappings;

                CatalogIdPatcher cip = new CatalogIdPatcher(catalogFileInfo, hardware2ProgramIdMapping, iPathETS, nsVersion);
                cip.Patch();

                XmlSigning.SignDirectory(Path.Combine(localPath, "Temp", manuId), iPathETS);

                Directory.CreateDirectory(Path.Combine(localPath, "Masters"));
                ns = ns.Substring(ns.LastIndexOf("/") + 1);
                // Console.WriteLine("localPath is {0}", localPath);
                if (!File.Exists(Path.Combine(localPath, "Masters", $"project-{ns}.xml")))
                {
                    // var client = new System.Net.WebClient();
                    // client.DownloadFile($"https://update.knx.org/data/XML/project-{ns}/knx_master.xml", Path.Combine(localPath, "Masters", $"project-{ns}.xml"));
                    HttpClient client = new HttpClient();
                    var task = client.GetStringAsync($"https://update.knx.org/data/XML/project-{ns}/knx_master.xml");
                    while (!task.IsCompleted) { }
                    File.WriteAllText(Path.Combine(localPath, "Masters", $"project-{ns}.xml"), task.Result.ToString());
                }

                File.Copy(Path.Combine(localPath, "Masters", $"project-{ns}.xml"), Path.Combine(localPath, "Temp", $"knx_master.xml"));
                if (File.Exists(iKnxprodFileName)) File.Delete(iKnxprodFileName);
                System.IO.Compression.ZipFile.CreateFromDirectory(Path.Combine(localPath, "Temp"), iKnxprodFileName);


                if (!iIsDebug)
                    System.IO.Directory.Delete(Path.Combine(localPath, "Temp"), true);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Output of {0} successful", iKnxprodFileName);
                Console.ResetColor();
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ETS-Error during knxprod creation:");
                Console.WriteLine(ex.ToString());
                return 1;
            }
        }

        class EtsOptions
        {
            private string mXmlFileName;
            [Value(0, MetaName = "xml file name", Required = true, HelpText = "Xml file name", MetaValue = "FILE")]
            public string XmlFileName
            {
                get { return mXmlFileName; }
                set { mXmlFileName = Path.ChangeExtension(value, "xml"); }
            }
            [Option('V', "Xsd", Required = false, HelpText = "Validation file name (xsd)", MetaValue = "FILE")]
            public string XsdFileName { get; set; } = "";
            [Option('N', "NoXsd", Required = false, HelpText = "Prevent automatic search for validation file (xsd) ")]
            public bool NoXsd { get; set; } = false;
        }

        [Verb("new", HelpText = "Create new xml file with a fully commented and working mini exaple")]
        class NewOptions : CreateOptions
        {
            [Option('x', "ProductName", Required = true, HelpText = "Product name - appears in catalog and in property dialog", MetaValue = "STRING")]
            public string ProductName { get; set; }
            [Option('n', "AppName", Required = false, HelpText = "(Default: Product name) Application name - appears in catalog and necessary for application upgrades", MetaValue = "STRING")]
            public string ApplicationName { get; set; } = "";
            [Option('a', "AppNumber", Required = true, HelpText = "Application number - has to be unique per manufacturer", MetaValue = "INT")]
            public int? ApplicationNumber { get; set; }
            [Option('y', "AppVersion", Required = false, Default = 1, HelpText = "Application version - necessary for application upgrades", MetaValue = "INT")]
            public int? ApplicationVersion { get; set; } = 1;
            [Option('w', "HardwareName", Required = false, HelpText = "(Default: Product name) Hardware name - not visible in ETS", MetaValue = "STRING")]
            public string HardwareName { get; set; } = "";
            [Option('v', "HardwareVersion", Required = false, Default = 1, HelpText = "Hardware version - not visible in ETS, required for registration", MetaValue = "INT")]
            public int? HardwareVersion { get; set; } = 1;
            [Option('s', "SerialNumber", Required = false, HelpText = "(Default: Application number) Hardware serial number - not visible in ETS, requered for hardware-id", MetaValue = "STRING")]
            public string SerialNumber { get; set; } = "";
            [Option('m', "MediumType", Required = false, Default = "TP", HelpText = "Medium type", MetaValue = "TP,IP,both")]
            public string MediumType { get; set; } = "TP";
            [Option('#', "OrderNumber", Required = false, HelpText = "(Default: Application number) Order number - appears in catalog and in property info tab", MetaValue = "STRING")]
            public string OrderNumber { get; set; } = "";

            public string MediumTypes
            {
                get
                {
                    string lResult = "MT-0";
                    if (MediumType == "IP")
                    {
                        lResult = "MT-5";
                    }
                    else if (MediumType == "both")
                    {
                        lResult = "MT-0 MT-5";
                    }
                    return lResult;
                }
            }
            public string MaskVersion
            {
                get
                {
                    string lResult = "MV-07B0";
                    if (MediumType == "IP") lResult = "MV-57B0";
                    return lResult;
                }
            }
        }

        [Verb("knxprod", HelpText = "Create knxprod file from given xml file")]
        class KnxprodOptions : EtsOptions
        {
            [Option('o', "Output", Required = false, HelpText = "Output file name", MetaValue = "FILE")]
            public string OutputFile { get; set; } = "";
        }

        [Verb("baggages", HelpText = "Create baggages files from given documentation file")]
        class BaggagesOptions
        {
            [Option('b', "BaggagesDir", Required = true, HelpText = "Baggages directory", MetaValue = "DIR")]
            public string BaggagesDir { get; set; } = "";
            [Option('d', "DocFileName", Required = true, HelpText = "Documentation file name", MetaValue = "FILE")]
            public string DocFileName { get; set; } = "";
            [Option('p', "Prefix", Required = false, HelpText = "Module Prefix", MetaValue = "STRING")]
            public string Prefix { get; set; } = "";
        }

        [Verb("create", HelpText = "Process given xml file with all includes and create knxprod")]
        class CreateOptions : KnxprodOptions
        {
            [Option('h', "HeaderFileName", Required = false, HelpText = "Header file name", MetaValue = "FILE")]
            public string HeaderFileName { get; set; } = "";
            [Option('p', "Prefix", Required = false, HelpText = "Prefix for generated constant names in header file", MetaValue = "STRING")]
            public string Prefix { get; set; } = "";
            [Option('d', "Debug", Required = false, HelpText = "Additional output of <xmlfile>.debug.xml, this file is the input file for knxprod converter")]
            public bool Debug { get; set; } = false;
            [Option('C', "CommentDebug", Required = false, Default = "auto", MetaValue = "auto,no,yes", HelpText = "Add comments to additional output of <xmlfile>.debug.xml")]
            public string CommentDebug { get; set; } = "auto";
            [Option('R', "NoRenumber", Required = false, HelpText = "Don't renumber ParameterSeparator- and ParameterBlock-Id's")]
            public bool NoRenumber { get; set; } = false;
            [Option('A', "AbsoluteSingleParameters", Required = false, HelpText = "Compatibility with 1.5.x: Parameters with single occurrence have an absolute address in xml")]
            public bool AbsoluteSingleParameters { get; set; } = false;
            [Option('c', "ConfigFileName", Required = false, HelpText = "File containing additional configuration", MetaValue = "FILE")]
            public string ConfigFileName { get; set; } = "";
        }

        [Verb("check", HelpText = "execute sanity checks on given xml file")]
        class CheckOptions : EtsOptions
        {
            [Option('d', "Debug", Required = false, HelpText = "Additional output of <xmlfile>.debug.xml, this file is the input file for knxprod converter")]
            public bool Debug { get; set; } = false;
            [Option('C', "CommentDebug", Required = false, Default = "auto", MetaValue = "auto,no,yes", HelpText = "Add comments to additional output of <xmlfile>.debug.xml")]
            public string CommentDebug { get; set; } = "auto";
        }

        static int Main(string[] args)
        {
            return new CommandLine.Parser(settings => settings.HelpWriter = Console.Out)
              .ParseArguments<CreateOptions, CheckOptions, KnxprodOptions, NewOptions, BaggagesOptions>(args)
              .MapResult(
                (NewOptions opts) => VerbNew(opts),
                (CreateOptions opts) => VerbCreate(opts),
                (KnxprodOptions opts) => VerbKnxprod(opts),
                (CheckOptions opts) => VerbCheck(opts),
                (BaggagesOptions opts) => VerbBaggages(opts),
                errs => 1);
        }

        static private void WriteVersion()
        {
            Console.WriteLine("{0} {1}", typeof(Program).Assembly.GetName().Name, typeof(Program).Assembly.GetName().Version);
        }

        static public string GetEncoded(string iInput)
        {
            char[] lExtraChars = new char[] { '.', '%', ' ', '!', '\"', '#', '$', '&', '(', ')', '+', '-', '/', ':', ';', '<', '>', '=', '?', '@', '[', '\\', ']', '^', '_', '{', '|', '}' };

            foreach (char lChar in lExtraChars)
            {
                iInput = iInput.Replace(lChar.ToString(), string.Format(".{0:X2}", (byte)lChar));
            }
            return iInput;
        }


        static private int VerbNew(NewOptions opts)
        {
            WriteVersion();
            // Handle defaults
            if (opts.ApplicationName == "") opts.ApplicationName = opts.ProductName;
            if (opts.HardwareName == "") opts.HardwareName = opts.ProductName;
            if (opts.SerialNumber == "") opts.SerialNumber = opts.ApplicationNumber.ToString();
            if (opts.OrderNumber == "") opts.OrderNumber = opts.ApplicationNumber.ToString();

            // checks
            bool lFail = false;
            if (opts.ApplicationNumber > 65535)
            {
                Console.WriteLine("ApplicationNumber has to be less than 65536!");
                lFail = true;
            }
            if (opts.ApplicationNumber >= 0xA000 && opts.ApplicationNumber < 0xB000)
            {
                Console.WriteLine("ApplicationNumber {0} is reserved for OpenKNX applications!", opts.ApplicationNumber);
                lFail = true;
            }
            if (lFail) return 1;

            // create initial xml file
            string lXmlFile = "";
            var assembly = Assembly.GetEntryAssembly();
            var resourceStream = assembly.GetManifestResourceStream("OpenKNXproducer.xml.NewDevice.xml");
            using (var reader = new StreamReader(resourceStream, Encoding.UTF8))
            {
                lXmlFile = reader.ReadToEnd();
            }
            lXmlFile = lXmlFile.Replace("%ApplicationName%", opts.ApplicationName.Trim());
            lXmlFile = lXmlFile.Replace("%ApplicationNumber%", opts.ApplicationNumber.ToString().Trim());
            lXmlFile = lXmlFile.Replace("%ApplicationVersion%", opts.ApplicationVersion.ToString().Trim());
            lXmlFile = lXmlFile.Replace("%HardwareName%", opts.HardwareName.Trim());
            lXmlFile = lXmlFile.Replace("%HardwareVersion%", opts.HardwareVersion.ToString().Trim());
            // lXmlFile = lXmlFile.Replace("%HardwareVersionEncoded%", GetEncoded(opts.HardwareVersion.ToString()));
            lXmlFile = lXmlFile.Replace("%SerialNumber%", opts.SerialNumber.Trim());
            // lXmlFile = lXmlFile.Replace("%SerialNumberEncoded%", GetEncoded(opts.SerialNumber));
            lXmlFile = lXmlFile.Replace("%OrderNumber%", opts.OrderNumber.Trim());
            // lXmlFile = lXmlFile.Replace("%OrderNumberEncoded%", GetEncoded(opts.OrderNumber));
            lXmlFile = lXmlFile.Replace("%ProductName%", opts.ProductName.Trim());
            lXmlFile = lXmlFile.Replace("%MaskVersion%", opts.MaskVersion.Trim());
            lXmlFile = lXmlFile.Replace("%MediumTypes%", opts.MediumTypes.Trim());
            Console.WriteLine("Creating xml file {0}", opts.XmlFileName);
            File.WriteAllText(opts.XmlFileName, lXmlFile);
            return VerbCreate(opts);
        }

        static private int VerbCreate(CreateOptions opts)
        {
            int lResult = 0;
            WriteVersion();
            WorkingDir = GetAbsWorkingDir(opts.XmlFileName);
            string lHeaderFileName = Path.ChangeExtension(opts.XmlFileName, "h");
            if (opts.HeaderFileName != "") lHeaderFileName = opts.HeaderFileName;
            Console.WriteLine("Processing xml file {0}", opts.XmlFileName);
            ProcessInclude lInclude = ProcessInclude.Factory(opts.XmlFileName, opts.Prefix);
            ProcessInclude.Renumber = !opts.NoRenumber;
            ProcessInclude.AbsoluteSingleParameters = opts.AbsoluteSingleParameters;
            // additional configuration
            if (opts.ConfigFileName != "") ProcessInclude.LoadConfig(opts.ConfigFileName, WorkingDir);
            CommentDebug = opts.CommentDebug;
            string lBaggageDirName = Path.Combine(WorkingDir, lInclude.BaggagesName);
            if (Directory.Exists(lBaggageDirName)) Directory.Delete(lBaggageDirName, true);
            if (opts.Debug) TemplateApplication.IsDebugMode = true;
            // Directory.CreateDirectory(lBaggageDirName);
            bool lWithVersions = lInclude.Expand();
            // We restore the original namespace in File
            lInclude.SetNamespace();
            lInclude.ResetXsd();
            lInclude.SetToolAndVersion();
            // check here for cmdline parameter supporting module copy (based on an included module)
            // if (true) lInclude.AddEtsExtensions();
            XmlDocument lXml = lInclude.GetDocument();
            bool lSuccess = ProcessSanityChecks(lInclude, lWithVersions);
            if (lSuccess) lSuccess = EnsureEtsNamingConventionForKO(lInclude);
            string lTempXmlFileName = Path.GetTempFileName();
            File.Delete(lTempXmlFileName);
            if (opts.Debug) lTempXmlFileName = opts.XmlFileName;
            lTempXmlFileName = Path.ChangeExtension(lTempXmlFileName, "debug.xml");
            if (opts.Debug) Console.WriteLine("Writing debug file to {0}", lTempXmlFileName);
            lXml.Save(lTempXmlFileName);
            // File.WriteAllText(Path.ChangeExtension(lTempXmlFileName, ".params.js"), ProcessInclude.ParameterInfo);
            // we check against old file content
            bool lIsEqual = false;
            string lHeaderGenerated = lInclude.HeaderGenerated;
            if (File.Exists(lHeaderFileName))
            {
                string lOldHeaderGenerated = File.ReadAllText(lHeaderFileName);
                lIsEqual = (lOldHeaderGenerated == lHeaderGenerated);
            }
            if (lIsEqual)
            {
                Console.WriteLine("Unchanged header file {0}, not written to disk", lHeaderFileName);
            }
            else
            {
                Console.WriteLine("Writing header file to {0}", lHeaderFileName);
                File.WriteAllText(lHeaderFileName, lHeaderGenerated);
            }
            string lOutputFileName = Path.ChangeExtension(opts.OutputFile, "knxprod");
            if (opts.OutputFile == "") lOutputFileName = Path.ChangeExtension(opts.XmlFileName, "knxprod");
            if (lSuccess)
            {
                string lEtsPath = FindEtsPath(lInclude.GetNamespace());
                lResult = ExportKnxprod(lEtsPath, WorkingDir, lOutputFileName, lTempXmlFileName, lInclude.BaggagesName, opts.XsdFileName, opts.Debug, !opts.NoXsd);
            }
            else
                lResult = 1;
            if (lResult > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("--> Skipping creation of {0} due to check errors! <--", lOutputFileName);
                Console.ResetColor();
            }
            return lResult;
        }

        static private int VerbCheck(CheckOptions opts)
        {
            WriteVersion();
            CommentDebug = opts.CommentDebug;
            string lWorkingDir = GetAbsWorkingDir(opts.XmlFileName);
            string lFileName = Path.ChangeExtension(opts.XmlFileName, "xml");
            Console.WriteLine("Reading and resolving xml file {0}", lFileName);
            ProcessInclude lInclude = ProcessInclude.Factory(opts.XmlFileName, "");
            // ProcessInclude.Renumber = !opts.NoRenumber;
            lInclude.LoadAdvanced(lFileName);
            lInclude.SetNamespace();
            XmlDocument lXml = lInclude.GetDocument();
            bool lSuccess = ProcessSanityChecks(lInclude, false);
            string lTempXmlFileName = Path.GetTempFileName();
            File.Delete(lTempXmlFileName);
            if (opts.Debug) lTempXmlFileName = opts.XmlFileName;
            lTempXmlFileName = Path.ChangeExtension(lTempXmlFileName, "debug.xml");
            if (opts.Debug) Console.WriteLine("Writing debug file to {0}", lTempXmlFileName);
            lXml.Save(lTempXmlFileName);
            lSuccess = ValidateXsd(lWorkingDir, lTempXmlFileName, opts.XmlFileName, opts.XsdFileName, !opts.NoXsd) || lSuccess;
            return lSuccess ? 1 : 0;
        }

        static private int VerbKnxprod(KnxprodOptions opts)
        {
            WriteVersion();
            string lOutputFileName = Path.ChangeExtension(opts.OutputFile, "knxprod");
            if (opts.OutputFile == "") lOutputFileName = Path.ChangeExtension(opts.XmlFileName, "knxprod");
            Console.WriteLine("Reading xml file {0} writing to {1}", opts.XmlFileName, lOutputFileName);

            string lWorkingDir = GetAbsWorkingDir(opts.XmlFileName);
            string xml = File.ReadAllText(opts.XmlFileName);
            System.Text.RegularExpressions.Regex rs = new System.Text.RegularExpressions.Regex("xmlns=\"(http:\\/\\/knx\\.org\\/xml\\/project\\/[0-9]{1,2})\"");
            System.Text.RegularExpressions.Match match = rs.Match(xml);
            string lEtsPath = FindEtsPath(match.Groups[1].Value);
            return ExportKnxprod(lEtsPath, lWorkingDir, lOutputFileName, opts.XmlFileName, Path.GetFileName(opts.XmlFileName).Replace(".xml", ".baggages"), opts.XsdFileName, false, !opts.NoXsd);
        }

        static private int VerbBaggages(BaggagesOptions opts)
        {
            WriteVersion();
            string lBaggagesDir = opts.BaggagesDir;
            string lWorkingDir = GetAbsWorkingDir(opts.BaggagesDir);
            string lPrefix = opts.Prefix;
            return ParseDocumentation.ExportBaggages(lWorkingDir, lBaggagesDir, opts.DocFileName, lPrefix);
        }
    }
}
