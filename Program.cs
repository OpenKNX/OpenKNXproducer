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
// using Knx.Ets.Xml.ObjectModel;

//using Knx.Ets.Converter.ConverterEngine;

namespace OpenKNXproducer {

    struct EtsVersion {
        public EtsVersion(string iSubdir, string iEts) {
            Subdir = iSubdir;
            ETS = iEts;
        }

        public string Subdir { get; private set; }
        public string ETS { get; private set; }
    }
    class Program {
        private static Dictionary<string, EtsVersion> EtsVersions = new Dictionary<string, EtsVersion>() {
            {"http://knx.org/xml/project/11", new EtsVersion("4.0.1997.50261", "ETS 4")},
            {"http://knx.org/xml/project/12", new EtsVersion("5.0.204.12971", "ETS 5")},
            {"http://knx.org/xml/project/13", new EtsVersion("5.1.84.17602", "ETS 5.5")},
            {"http://knx.org/xml/project/14", new EtsVersion("5.6.241.33672", "ETS 5.6")},
            {"http://knx.org/xml/project/20", new EtsVersion("5.7", "ETS 5.7")},
            {"http://knx.org/xml/project/21", new EtsVersion("6.0", "ETS 6.0")}
        };

        private const string gtoolName = "KNX MT";
        private const string gtoolVersion = "5.1.255.16695";
        //installation path of a valid ETS instance (only ETS4 or ETS5 supported)
        private static List<string> gPathETS = new List<string> {
            @"C:\Program Files (x86)\ETS6",
            @"C:\Program Files (x86)\ETS5",
            AppDomain.CurrentDomain.BaseDirectory
        };

        static string FindEtsPath(string lXmlns) {
            string lResult = "";
            
            int lProjectVersion = int.Parse(lXmlns.Substring(27));

            if (EtsVersions.ContainsKey(lXmlns)) {
                string lEts = "";
                //if we found an ets6, we can generate all versions with it
                if(Directory.Exists(@"C:\Program Files (x86)\ETS6")) {
                    lResult = @"C:\Program Files (x86)\ETS6";
                    lEts = "ETS 6";
                }
                
                //if we found ets6 dlls, we can generate all versions with it
                if(Directory.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CV", "6.0"))) {
                    lResult = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CV", "6.0");
                    lEts = "ETS 6 (local)";
                }

                //else search for an older ETS or CV files
                if(string.IsNullOrEmpty(lResult)) {
                    string lSubdir = EtsVersions[lXmlns].Subdir;
                    lEts = EtsVersions[lXmlns].ETS;

                    foreach(string path in gPathETS) {
                        if(!Directory.Exists(path)) continue;
                        if(Directory.Exists(Path.Combine(path, "CV", lSubdir))) //If subdir exists everything ist fine
                        {
                            lResult = Path.Combine(path, "CV", lSubdir);
                            break;
                        }
                        else { //otherwise it might be the file in the root folder
                            if(!File.Exists(Path.Combine(path, "Knx.Ets.XmlSigning.dll"))) continue;
                            System.Diagnostics.FileVersionInfo versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(Path.Combine(path, "Knx.Ets.XmlSigning.dll"));
                            string newVersion = versionInfo.FileVersion;
                            if (lSubdir.Split('.').Length == 2) newVersion = string.Join('.', newVersion.Split('.').Take(2));
                            // if(newVersion.Split('.').Length != 4) newVersion += ".0";

                            if(lSubdir == newVersion)
                            {
                                lResult = path;
                                break;
                            }
                        }
                    }
                }
                
                if (!string.IsNullOrEmpty(lResult))
                    Console.WriteLine("Found namespace {1} in xml, will use {0} for conversion... (Path: {2})", lEts, lXmlns, lResult);
            }
            if (string.IsNullOrEmpty(lResult)) Console.WriteLine("No valid conversion engine available for xmlns {0}", lXmlns);
            
            return lResult;
        }

        public static void WriteFail(ref bool iFail, string iFormat, params object[] iParams) {
            if (!iFail) Console.WriteLine();
            Console.WriteLine("  --> " + iFormat, iParams);
            iFail = true;
        }

        // Node cache
        static Dictionary<string, XmlNode> gIds = new Dictionary<string, XmlNode>();

        static XmlNode GetNodeById(XmlNode iRootNode, string iId) {
            XmlNode lResult = null;
            if (gIds.ContainsKey(iId)) {
                lResult = gIds[iId];
            } else {
                lResult = iRootNode.SelectSingleNode(string.Format("//*[@Id='{0}']", iId));
                if (lResult != null) gIds.Add(iId, lResult);
            }
            return lResult;
        }

        private static void CreateComment(XmlDocument iTargetNode, XmlNode iNode, string iId, string iSuffix = "") {
            string lNodeId = iId.Substring(0, iId.LastIndexOf("_R"));
            string lTextId = iId;
            string lNodeName = "Id-mismatch! Name not found!";
            string lText = "Id-mismatch! Text not found!";
            if (gIds.ContainsKey(lNodeId)) lNodeName = gIds[lNodeId].NodeAttr("Name");
            if (gIds.ContainsKey(lTextId) && gIds[lTextId].NodeAttr("Text") == "") lTextId = lNodeId;
            if (gIds.ContainsKey(lTextId)) lText = gIds[lTextId].NodeAttr("Text");
            XmlComment lComment = iTargetNode.CreateComment(string.Format(" {0}{3} {1} '{2}'", iNode.Name, lNodeName, lText, iSuffix));
            iNode.ParentNode.InsertBefore(lComment, iNode);
        }

        static bool ProcessSanityChecks(XmlDocument iTargetNode, bool iWithVersions) {

            Console.WriteLine();
            Console.WriteLine("Sanity checks... ");
            bool lFail = false;

            Console.Write("- Id-Uniqueness...");
            bool lFailPart = false;
            XmlNodeList lNodes = iTargetNode.SelectNodes("//*[@Id]");
            foreach (XmlNode lNode in lNodes) {
                string lId = lNode.Attributes.GetNamedItem("Id").Value;
                if (gIds.ContainsKey(lId)) {
                    WriteFail(ref lFailPart, "{0} is a duplicate Id in {1}", lId, lNode.NodeAttr("Name"));
                } else {
                    gIds.Add(lId, lNode);
                }
            }
            if (!lFailPart) Console.WriteLine(" OK");
            lFail = lFail || lFailPart;

            Console.Write("- Id-R_Suffix-Uniqueness...");
            lFailPart = false;
            Dictionary<string, bool> lParameterSuffixes = new Dictionary<string, bool>();
            Dictionary<string, bool> lComObjectSuffixes = new Dictionary<string, bool>();
            foreach (XmlNode lNode in lNodes) {
                string lId = lNode.Attributes.GetNamedItem("Id").Value;
                int lPos = lId.LastIndexOf("_R-");
                Dictionary<string, bool> lSuffixes = null;
                if (lPos > 0) {
                    if (lId.Substring(0, lPos).Contains("_P-"))
                        lSuffixes = lParameterSuffixes;
                    else if (lId.Substring(0, lPos).Contains("_O-"))
                        lSuffixes = lComObjectSuffixes;
                    if (lSuffixes != null) {
                        string lSuffix = lId.Substring(lPos + 3);
                        if (lSuffixes.ContainsKey(lSuffix)) {
                            WriteFail(ref lFailPart, "{0} is a duplicate _R-Suffix in {1}", lId, lNode.Name);
                        } else {
                            lSuffixes.Add(lSuffix, false);
                        }
                    }
                }
            }
            if (!lFailPart) Console.WriteLine(" OK");
            lFail = lFail || lFailPart;

            lFailPart = false;
            Console.Write("- RefId-Integrity...");
            lNodes = iTargetNode.SelectNodes("//*[@RefId]");
            foreach (XmlNode lNode in lNodes) {
                if (lNode.Name != "Manufacturer") {
                    string lRefId = lNode.Attributes.GetNamedItem("RefId").Value;
                    if (!gIds.ContainsKey(lRefId)) {
                        WriteFail(ref lFailPart, "{0} is referenced in {1} {2}, but not defined", lRefId, lNode.Name, lNode.NodeAttr("Name"));
                    } else if (lRefId.Contains("_R")) {
                        CreateComment(iTargetNode, lNode, lRefId);
                    }
                }
            }
            if (!lFailPart) Console.WriteLine(" OK");
            lFail = lFail || lFailPart;

            lFailPart = false;
            Console.Write("- ParamRefId-Integrity...");
            lNodes = iTargetNode.SelectNodes("//*[@ParamRefId]");
            foreach (XmlNode lNode in lNodes) {
                if (lNode.Name != "Manufacturer") {
                    string lParamRefId = lNode.Attributes.GetNamedItem("ParamRefId").Value;
                    if (!gIds.ContainsKey(lParamRefId)) {
                        WriteFail(ref lFailPart, "{0} is referenced in {1} {2}, but not defined", lParamRefId, lNode.Name, lNode.NodeAttr("Name"));
                    } else {
                        CreateComment(iTargetNode, lNode, lParamRefId);
                    }
                }
            }
            if (!lFailPart) Console.WriteLine(" OK");
            lFail = lFail || lFailPart;

            lFailPart = false;
            Console.Write("- TextParameterRefId-Integrity...");
            lNodes = iTargetNode.SelectNodes("//*[@TextParameterRefId]");
            foreach (XmlNode lNode in lNodes) {
                string lTextParamRefId = lNode.Attributes.GetNamedItem("TextParameterRefId").Value;
                if (!gIds.ContainsKey(lTextParamRefId)) {
                    WriteFail(ref lFailPart, "{0} is referenced in {1} {2}, but not defined", lTextParamRefId, lNode.Name, lNode.NodeAttr("Name"));
                } else {
                    CreateComment(iTargetNode, lNode, lTextParamRefId);
                }
            }
            if (!lFailPart) Console.WriteLine(" OK");
            lFail = lFail || lFailPart;

            lFailPart = false;
            Console.Write("- SourceParamRefRef-Integrity...");
            lNodes = iTargetNode.SelectNodes("//*[@SourceParamRefRef]");
            foreach (XmlNode lNode in lNodes) {
                string lSourceParamRefRef = lNode.Attributes.GetNamedItem("SourceParamRefRef").Value;
                if (!gIds.ContainsKey(lSourceParamRefRef)) {
                    WriteFail(ref lFailPart, "{0} is referenced in {1} {2}, but not defined", lSourceParamRefRef, lNode.Name, lNode.NodeAttr("Name"));
                } else {
                    CreateComment(iTargetNode, lNode, lSourceParamRefRef, "-Source");
                }
            }
            if (!lFailPart) Console.WriteLine(" OK");
            lFail = lFail || lFailPart;

            lFailPart = false;
            Console.Write("- TargetParamRefRef-Integrity...");
            lNodes = iTargetNode.SelectNodes("//*[@TargetParamRefRef]");
            foreach (XmlNode lNode in lNodes) {
                string lTargetParamRefRef = lNode.Attributes.GetNamedItem("TargetParamRefRef").Value;
                if (!gIds.ContainsKey(lTargetParamRefRef)) {
                    WriteFail(ref lFailPart, "{0} is referenced in {1} {2}, but not defined", lTargetParamRefRef, lNode.Name, lNode.NodeAttr("Name"));
                } else {
                    CreateComment(iTargetNode, lNode, lTargetParamRefRef, "-Target");
                }
            }
            if (!lFailPart) Console.WriteLine(" OK");
            lFail = lFail || lFailPart;

            lFailPart = false;
            Console.Write("- ParameterType-Integrity...");
            lNodes = iTargetNode.SelectNodes("//*[@ParameterType]");
            foreach (XmlNode lNode in lNodes) {
                string lParameterType = lNode.Attributes.GetNamedItem("ParameterType").Value;
                if (!gIds.ContainsKey(lParameterType)) {
                    WriteFail(ref lFailPart, "{0} is referenced in {1} {2}, but not defined", lParameterType, lNode.Name, lNode.NodeAttr("Name"));
                }
            }
            if (!lFailPart) Console.WriteLine(" OK");
            lFail = lFail || lFailPart;

            lFailPart = false;
            Console.Write("- Union-Integrity...");
            lNodes = iTargetNode.SelectNodes("//Union");
            foreach (XmlNode lNode in lNodes) {
                string lSize = lNode.NodeAttr("SizeInBit");
                if (lSize == "") {
                    WriteFail(ref lFailPart, "Union without SizeInBit-Attribute found");
                }
            }
            if (!lFailPart) Console.WriteLine(" OK");
            lFail = lFail || lFailPart;

            Console.Write("- Parameter-Name-Uniqueness...");
            lFailPart = false;
            lNodes = iTargetNode.SelectNodes("//Parameter[@Name]");
            Dictionary<string, bool> lParameterNames = new Dictionary<string, bool>();
            foreach (XmlNode lNode in lNodes) {
                string lName = lNode.Attributes.GetNamedItem("Name").Value;
                if (lParameterNames.ContainsKey(lName)) {
                    WriteFail(ref lFailPart, "{0} is a duplicate Name in Parameter '{1}'", lName, lNode.NodeAttr("Text"));
                } else {
                    lParameterNames.Add(lName, true);
                }
            }
            if (!lFailPart) Console.WriteLine(" OK");
            lFail = lFail || lFailPart;

            lFailPart = false;
            Console.Write("- Parameter-Value-Integrity...");
            lNodes = iTargetNode.SelectNodes("//Parameter");
            foreach (XmlNode lNode in lNodes) {
                // we add the node to parameter cache
                string lNodeId = lNode.NodeAttr("Id");
                string lMessage = string.Format("Parameter {0}", lNode.NodeAttr("Name"));
                string lParameterValue = lNode.NodeAttr("Value", null);
                if (lParameterValue == null) {
                    WriteFail(ref lFailPart, "{0} has no Value attribute", lMessage);
                }
                lFailPart = CheckParameterValueIntegrity(iTargetNode, lFailPart, lNode, lParameterValue, lMessage);
            }
            if (!lFailPart) Console.WriteLine(" OK");
            lFail = lFail || lFailPart;

            lFailPart = false;
            bool lSkipTest = false;
            Console.Write("- ParameterRef-Value-Integrity...");
            lNodes = iTargetNode.SelectNodes("//ParameterRef[@Value]");
            foreach (XmlNode lNode in lNodes) {
                string lParameterRefValue = lNode.NodeAttr("Value");
                // find parameter
                XmlNode lParameterNode = GetNodeById(iTargetNode, lNode.NodeAttr("RefId"));
                if (lParameterNode == null) {
                    lSkipTest = true;
                    break;
                }
                string lMessage = string.Format("ParameterRef {0}, referencing Parameter {1},", lNode.NodeAttr("Id"), lParameterNode.NodeAttr("Name"));
                lFailPart = CheckParameterValueIntegrity(iTargetNode, lFailPart, lParameterNode, lParameterRefValue, lMessage);
            }
            if (lSkipTest) {
                WriteFail(ref lFailPart, "Test not possible due to Errors in ParameterRef definitions (sove above problems first)");
            }
            if (!lFailPart) Console.WriteLine(" OK");
            lFail = lFail || lFailPart;

            Console.Write("- ComObject-Name-Uniqueness...");
            lFailPart = false;
            lNodes = iTargetNode.SelectNodes("//ComObject[@Name]");
            Dictionary<string, bool> lKoNames = new Dictionary<string, bool>();
            foreach (XmlNode lNode in lNodes) {
                string lName = lNode.Attributes.GetNamedItem("Name").Value;
                if (lKoNames.ContainsKey(lName)) {
                    WriteFail(ref lFailPart, "{0} is a duplicate Name in ComObject number {1}", lName, lNode.NodeAttr("Number"));
                } else {
                    lKoNames.Add(lName, true);
                }
            }
            if (!lFailPart) Console.WriteLine(" OK");
            lFail = lFail || lFailPart;

            Console.Write("- ComObject-Number-Uniqueness...");
            lFailPart = false;
            lNodes = iTargetNode.SelectNodes("//ComObject[@Number]");
            Dictionary<int, bool> lKoNumbers = new Dictionary<int, bool>();
            foreach (XmlNode lNode in lNodes) {
                int lNumber = 0;
                bool lIsInt = int.TryParse(lNode.Attributes.GetNamedItem("Number").Value, out lNumber);
                if (lIsInt) {
                    if (lKoNumbers.ContainsKey(lNumber)) {
                        WriteFail(ref lFailPart, "{0} is a duplicate Number in ComObject with name {1}", lNumber, lNode.NodeAttr("Name"));
                    } else {
                        lKoNumbers.Add(lNumber, true);
                    }
                } else {
                    WriteFail(ref lFailPart, "ComObject.Number is not an Integer in ComObject with name {0}", lNode.NodeAttr("Name"));
                }
            }
            if (!lFailPart) Console.WriteLine(" OK");
            lFail = lFail || lFailPart;

            Console.Write("- Id-Namespace...");
            // find refid
            lFailPart = false;
            XmlNode lApplicationProgramNode = iTargetNode.SelectSingleNode("/KNX/ManufacturerData/Manufacturer/ApplicationPrograms/ApplicationProgram");
            string lApplicationId = lApplicationProgramNode.Attributes.GetNamedItem("Id").Value;
            string lRefNs = lApplicationId; //.Replace("M-00FA_A", "");
            if(lRefNs.StartsWith("M-")) lRefNs = lRefNs.Substring(8);
            // check all nodes according to refid
            lNodes = iTargetNode.SelectNodes("//*/@*[string-length() > '13']");
            foreach (XmlNode lNode in lNodes) {
                if (lNode.Value != null) {
                    var lMatch = Regex.Match(lNode.Value, "-[0-9A-F]{4}-[0-9A-F]{2}-[0-9A-F]{4}");
                    if (lMatch.Success) {
                        if (lMatch.Value != lRefNs) {
                            XmlElement lElement = ((XmlAttribute)lNode).OwnerElement;
                            WriteFail(ref lFailPart, "{0} of node {2} {3} is in a different namespace than application namespace {1}", lMatch.Value, lRefNs, lElement.Name, lElement.NodeAttr("Name"));
                        }
                    }
                }
            }
            if (!lFailPart) Console.WriteLine(" OK");
            lFail = lFail || lFailPart;

            Console.Write("- Id-Format...");
            // An id has to fulfill a specific format
            lFailPart = false;
            string lIdPart = "";
            foreach (var lKeyValuePair in gIds) {
                string lId = lKeyValuePair.Key;
                string lIdMatch = "";
                string lIdMatchReadable = "";
                lId = lId.Replace(lApplicationId, "");
                XmlNode lElement = lKeyValuePair.Value;
                switch (lElement.Name)
                {
                    case "Parameter":
                        if (lElement.ParentNode.Name == "Union") {
                            lIdPart = "_UP-";
                        } else {
                            lIdPart = "_P-";
                        }
                        lIdMatch = lIdPart + @"[1-3]?\d\d{6}";
                        lIdMatchReadable = lIdPart + "tcccnnn";
                        break;
                    case "ComObject":
                        lIdPart = "_O-";
                        lIdMatch = lIdPart + @"[1-3]?\d\d{6}";
                        lIdMatchReadable = lIdPart + "tcccnnn";
                        break;
                    case "ParameterType":
                    case "Enumeration":
                        lIdPart = "_PT-";
                        break;
                    case "ParameterRef":
                    case "ComObjectRef":
                        lIdPart = "_R-";
                        if (lId.Contains(lIdPart)) lIdPart = "";
                        lIdMatch = @"([1-3]?\d\d{6})_R-\1\d\d";
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
                if (lIdPart != "" && !lId.StartsWith(lIdPart)) {
                    WriteFail(ref lFailPart, "{0} {1} has the Id={2}, but this Id is missing the required part {3}", lElement.Name, lElement.NodeAttr("Name"), lKeyValuePair.Key, lIdPart);
                }
                if (iWithVersions && lIdMatch != "" && !Regex.IsMatch(lId, lIdMatch)) {
                    WriteFail(ref lFailPart, "{0} {1} has the Id={2}, but this Id has not the OpenKNX-Format {3}", lElement.Name, lElement.NodeAttr("Name"), lKeyValuePair.Key, lIdMatchReadable);
                }
            }
            if (!lFailPart) Console.WriteLine(" OK");
            lFail = lFail || lFailPart;

            lFailPart = false;
            Console.Write("- Serial number...");
            lNodes = iTargetNode.SelectNodes("//*[@SerialNumber]");
            foreach (XmlNode lNode in lNodes) {
                string lSerialNumber = lNode.Attributes.GetNamedItem("SerialNumber").Value;
                if (lSerialNumber.Contains("-")) {
                    WriteFail(ref lFailPart, "Hardware.SerialNumber={0}, it contains a dash (-), this will cause problems in knxprod.", lSerialNumber);
                }
            }
            if (!lFailPart) Console.WriteLine(" OK");
            lFail = lFail || lFailPart;

            lFailPart = false;
            Console.Write("- Application data...");
            lNodes = iTargetNode.SelectNodes("//ApplicationProgram");
            foreach (XmlNode lNode in lNodes) {
                int lNumber = -1;
                bool lIsInt = int.TryParse(lNode.Attributes.GetNamedItem("ApplicationNumber").Value, out lNumber);
                if (!lIsInt || lNumber < 0) {
                    WriteFail(ref lFailPart, "Applicationprogram.ApplicationNumber is incorrect or could not be parsed");
                }
                lNumber = -1;
                lIsInt = int.TryParse(lNode.Attributes.GetNamedItem("ApplicationVersion").Value, out lNumber);
                if (!lIsInt || lNumber < 0) {
                    WriteFail(ref lFailPart, "Applicationprogram.ApplicationVersion is incorrect or could not be parsed");
                }
            }
            if (!lFailPart) Console.WriteLine(" OK");
            lFail = lFail || lFailPart;

            lFailPart = false;
            Console.Write("- Memory size...");
            lNodes = iTargetNode.SelectNodes("//*[self::RelativeSegment or self::LdCtrlRelSegment or self::LdCtrlWriteRelMem][@Size]");
            foreach (XmlNode lNode in lNodes) {
                int lNumber = -1;
                string lValue = lNode.Attributes.GetNamedItem("Size").Value;
                bool lIsInt = int.TryParse(lValue, out lNumber);
                if (!lIsInt || lNumber <= 0) {
                    WriteFail(ref lFailPart, "Size-Attribute of {0} is incorrect ({1}), are you missing %MemorySize% replacement?", lNode.Name, lValue);
                }
            }
            if (!lFailPart) Console.WriteLine(" OK");
            lFail = lFail || lFailPart;

            return !lFail;
        }

        private static bool CheckParameterValueIntegrity(XmlNode iTargetNode, bool iFailPart, XmlNode iParameterNode, string iValue, string iMessage) {
            string lParameterType = iParameterNode.NodeAttr("ParameterType");
            if (lParameterType == "") {
                WriteFail(ref iFailPart, "Parameter {0} has no ParameterType attribute", iParameterNode.NodeAttr("Name"));
            }
            if (iValue != null && lParameterType != "") {
                // find parameter type
                XmlNode lParameterTypeNode = iTargetNode.SelectSingleNode(string.Format("//ParameterType[@Id='{0}']", lParameterType));
                if (lParameterTypeNode != null) {
                    // get first child ignoring comments
                    XmlNode lChild = lParameterTypeNode.ChildNodes[0];
                    while (lChild != null && lChild.NodeType != XmlNodeType.Element) lChild = lChild.NextSibling;


                    int sizeInBit;
                    long maxSize = 0;
                    switch(lChild.Name)
                    {
                        case "TypeText":
                            if (!int.TryParse(lChild.Attributes["SizeInBit"]?.Value, out sizeInBit))
                                WriteFail(ref iFailPart, "SizeInBit of {0} cannot be converted to a number, value is '{1}'", iMessage, lChild.Attributes["SizeInBit"]?.Value ?? "empty");
                            maxSize = sizeInBit / 8;
                            break;

                        case "TypeFloat":
                            //There is no SizeInBit attribute
                            break;

                        case "TypeColor":
                            //There is no SizeInBit attribute
                            break;

                        default:
                            if (!int.TryParse(lChild.Attributes["SizeInBit"]?.Value, out sizeInBit))
                                WriteFail(ref iFailPart, "SizeInBit of {0} cannot be converted to a number, value is '{1}'", iMessage, lChild.Attributes["SizeInBit"]?.Value ?? "empty");
                            maxSize = Convert.ToInt64(Math.Pow(2, int.Parse(lChild.Attributes["SizeInBit"]?.Value ?? "0")));
                            break;
                    }

                    int min=0, max=0;
                    switch (lChild.Name) {
                        case "TypeNumber":
                            int lDummyInt;
                            bool lSuccess = int.TryParse(iValue, out lDummyInt);
                            if (!lSuccess) {
                                WriteFail(ref iFailPart, "Value of {0} cannot be converted to a number, value is '{1}'", iMessage, iValue);
                            }
                            if(!int.TryParse(lChild.Attributes["minInclusive"]?.Value, out min))
                                WriteFail(ref iFailPart, "MinInclusive of {0} cannot be converted to a number, value is '{1}'", iMessage, lChild.Attributes["minInclusive"]?.Value ?? "empty");
                            if(!int.TryParse(lChild.Attributes["maxInclusive"]?.Value, out max))
                                WriteFail(ref iFailPart, "MaxInclusive of {0} cannot be converted to a number, value is '{1}'", iMessage, lChild.Attributes["minInclusive"]?.Value ?? "empty");
                            
                            switch(lChild.Attributes["Type"]?.Value) {
                                case "unsignedInt":
                                    if(min < 0)
                                        WriteFail(ref iFailPart, "MinInclusive of {0} cannot be smaller than 0, value is '{1}'", iMessage, min);
                                    if(max >= maxSize)
                                        WriteFail(ref iFailPart, "MaxInclusive of {0} cannot be greater than {1}, value is '{2}'", iMessage, maxSize, max);
                                    break;

                                case "signedInt":
                                    if(min < ((maxSize/2)*(-1)))
                                        WriteFail(ref iFailPart, "MinInclusive of {0} cannot be smaller than {1}, value is '{2}'", iMessage, ((maxSize/2)*(-1)), min);
                                    if(max > ((maxSize/2)-1))
                                        WriteFail(ref iFailPart, "MinInclusive of {0} cannot be greater than {1}, value is '{2}'", iMessage, ((maxSize/2)-1), max);
                                    break;
                            }
                            //TODO check value
                            break;
                        case "TypeFloat":
                            float lDummyFloat;
                            lSuccess = float.TryParse(iValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out lDummyFloat);
                            if (!lSuccess || iValue.Contains(",")) {
                                WriteFail(ref iFailPart, "Value of {0} cannot be converted to a float, value is '{1}'", iMessage, iValue);
                            }
                            //TODO check value
                            break;
                        case "TypeRestriction":
                            lSuccess = false;
                            int maxEnumValue = -1;
                            foreach (XmlNode lEnumeration in lChild.ChildNodes) {
                                if (lEnumeration.Name == "Enumeration") {
                                    if (lEnumeration.NodeAttr("Value") == iValue) {
                                        lSuccess = true;
                                    }
                                    int enumValue = 0;
                                    if(!int.TryParse(lEnumeration.Attributes["Value"]?.Value, out enumValue))
                                        WriteFail(ref iFailPart, "Enum Value of {2} in {0} cannot be converted to an int, value is '{1}'", iMessage, iValue, lParameterType);
                                    else {
                                        if(enumValue > maxEnumValue)
                                            maxEnumValue = enumValue;
                                    }
                                }
                            }
                            if (!lSuccess) {
                                WriteFail(ref iFailPart, "Value of {0} is not contained in enumeration {2}, value is '{1}'", iMessage, iValue, lParameterType);
                            }
                            if(maxEnumValue >= maxSize)
                                WriteFail(ref iFailPart, "Max Enum Value of {0} can not be greater than {2}, value is '{1}'", iMessage, maxEnumValue, maxSize);
                            break;
                        case "TypeText":
                            //TODO add string length validation
                            if(iValue.Length > maxSize)
                                WriteFail(ref iFailPart, "String Length of {0} can not be greater than {2}, length is '{1}'", iMessage, maxSize, iValue.Length);
                            break;
                        default:
                            break;
                    }
                }
            }

            return iFailPart;
        }

        #region Reflection
        private static object InvokeMethod(Type type, string methodName, object[] args) {

            var mi = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
            return mi.Invoke(null, args);
        }

        private static void SetProperty(Type type, string propertyName, object value) {
            PropertyInfo prop = type.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Static);
            prop.SetValue(null, value, null);
        }
        #endregion

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

        private static int ExportKnxprod(string iPathETS, string iKnxprodFileName, string lTempXmlFileName, bool iIsDebug) {
            if (iPathETS == "") return 1;
            try {

                XDocument xdoc = XDocument.Load(lTempXmlFileName, LoadOptions.SetLineInfo);

                string xsdFile = @"C:\Users\u6\Documents\repos\Kaenx.Creator\Kaenx.Creator\bin\Debug\net6.0-windows\Data\knx_project_14.xsd";

                string ns = xdoc.Root.Name.NamespaceName;

                xdoc.Root.Attribute("oldxmlns")?.Remove();

                if(!string.IsNullOrEmpty(xsdFile))
                {
                    XmlSchemaSet schemas = new XmlSchemaSet();
                    schemas.Add(null, xsdFile);

                    xdoc.Validate(schemas, (o, e) => {
                        Console.WriteLine($"{e.Severity} XML Line {e.Exception.LineNumber}:{e.Exception.LinePosition} -> {e.Message}");
                    });
                }

                XElement xmanu = xdoc.Root.Element(XName.Get("ManufacturerData", ns)).Element(XName.Get("Manufacturer", ns));

                string manuId = xmanu.Attribute("RefId").Value;
                string localPath = AppDomain.CurrentDomain.BaseDirectory;
                if(Directory.Exists(Path.Combine(localPath, "Temp")))
                    Directory.Delete(Path.Combine(localPath, "Temp"), true);
                
                Directory.CreateDirectory(Path.Combine(localPath, "Temp"));
                Directory.CreateDirectory(Path.Combine(localPath, "Temp", manuId)); //Get real Manu


                XElement xcata = xmanu.Element(XName.Get("Catalog", ns));
                XElement xhard = xmanu.Element(XName.Get("Hardware", ns));
                XElement xappl = xmanu.Element(XName.Get("ApplicationPrograms", ns));

                List<XElement> xcataL = new List<XElement>();
                List<XElement> xhardL = new List<XElement>();
                List<XElement> xapplL = new List<XElement>();
                XElement xlangs = xmanu.Element(XName.Get("Languages", ns));

                if(xlangs != null)
                {
                    xlangs.Remove();
                    foreach(XElement xlang in xlangs.Descendants(XName.Get("Language", ns)).ToList())
                    {
                        XComment xcomm = xlang.FirstNode as XComment;

                        if(xcomm.NodeType != XmlNodeType.Comment)
                        {
                            Console.WriteLine("Language has no assignment Comment and will be skipped");
                            continue;
                        }

                        switch(xcomm.Value)
                        {
                            case "Catalog":
                                xcataL.Add(xlang);
                                break;

                            case "Application":
                                xapplL.Add(xlang);
                                break;

                            case "Hardware":
                                xhardL.Add(xlang);
                                break;

                            default:
                                throw new Exception("Unknown Translation Type: " + xcomm.Value);
                        }

                        xcomm.Remove();
                    }
                }

                //Save Catalog
                xhard.Remove();
                xappl.Remove();
                if(xcataL.Count > 0)
                {
                    xlangs.Elements().Remove();
                    foreach(XElement xlang in xcataL)
                        xlangs.Add(xlang);
                    xmanu.Add(xlangs);
                }
                xdoc.Save(Path.Combine(localPath, "Temp", manuId, "Catalog.xml"));
                if(xcataL.Count > 0) xlangs.Remove();

                xcata.Remove();
                xmanu.Add(xhard);
                if(xhardL.Count > 0)
                {
                    xlangs.Elements().Remove();
                    foreach(XElement xlang in xhardL)
                        xlangs.Add(xlang);
                    xmanu.Add(xlangs);
                }
                xdoc.Save(Path.Combine(localPath, "Temp", manuId, "Hardware.xml"));
                if(xhardL.Count > 0) xlangs.Remove();

                xhard.Remove();
                xmanu.Add(xappl);
                if(xapplL.Count > 0)
                {
                    xlangs.Elements().Remove();
                    foreach(XElement xlang in xapplL)
                        xlangs.Add(xlang);
                    xmanu.Add(xlangs);
                }
                string appId = xappl.Elements(XName.Get("ApplicationProgram", ns)).First().Attribute("Id").Value;
                xdoc.Save(Path.Combine(localPath, "Temp", manuId, $"{appId}.xml"));
                if(xapplL.Count > 0) xlangs.Remove();



                IDictionary<string, string> applProgIdMappings = new Dictionary<string, string>();
                IDictionary<string, string> applProgHashes = new Dictionary<string, string>();
                IDictionary<string, string> mapBaggageIdToFileIntegrity = new Dictionary<string, string>(50);

                FileInfo hwFileInfo = new FileInfo(Path.Combine(localPath, "Temp", manuId, "Hardware.xml"));
                FileInfo catalogFileInfo = new FileInfo(Path.Combine(localPath, "Temp", manuId, "Catalog.xml"));
                FileInfo appInfo = new FileInfo(Path.Combine(localPath, "Temp", manuId, $"{appId}.xml"));

                int nsVersion = int.Parse(ns.Substring(ns.LastIndexOf('/')+1));
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
                ns = ns.Substring(ns.LastIndexOf("/")+1);
                Console.WriteLine("localPath is {0}", localPath);
                if(!File.Exists(Path.Combine(localPath, "Masters", $"project-{ns}.xml"))) {
                    // var client = new System.Net.WebClient();
                    // client.DownloadFile($"https://update.knx.org/data/XML/project-{ns}/knx_master.xml", Path.Combine(localPath, "Masters", $"project-{ns}.xml"));
                    HttpClient client = new HttpClient();
                    var task = client.GetStringAsync($"https://update.knx.org/data/XML/project-{ns}/knx_master.xml");
                    while(!task.IsCompleted) { }
                    File.WriteAllText(Path.Combine(localPath, "Masters", $"project-{ns}.xml"), task.Result.ToString());
                }

                File.Copy(Path.Combine(localPath, "Masters", $"project-{ns}.xml"), Path.Combine(localPath, "Temp", $"knx_master.xml"));
                if(File.Exists(iKnxprodFileName)) File.Delete(iKnxprodFileName);
                System.IO.Compression.ZipFile.CreateFromDirectory(Path.Combine(localPath, "Temp"), iKnxprodFileName);


                if(!iIsDebug)
                    System.IO.Directory.Delete(Path.Combine(localPath, "Temp"), true);

                Console.WriteLine("Output of {0} successful", iKnxprodFileName);
                return 0;
            }
            catch (Exception ex) {
                Console.WriteLine("ETS-Error during knxprod creation:");
                Console.WriteLine(ex.ToString());
                return 1;
            }
        }

        class EtsOptions {
            private string mXmlFileName;
            [Value(0, MetaName = "xml file name", Required = true, HelpText = "Xml file name", MetaValue = "FILE")]
            public string XmlFileName {
                get { return mXmlFileName; }
                set { mXmlFileName = Path.ChangeExtension(value, "xml"); }
            }
        }

        [Verb("new", HelpText = "Create new xml file with a fully commented and working mini exaple")]
        class NewOptions : CreateOptions {
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

            public string MediumTypes {
                get {
                    string lResult = "MT-0";
                    if (MediumType == "IP") {
                        lResult = "MT-5";
                    } else if (MediumType == "both") {
                        lResult = "MT-0 MT-5";
                    }
                    return lResult;
                }
            }
            public string MaskVersion {
                get {
                    string lResult = "MV-07B0";
                    if (MediumType == "IP") lResult = "MV-57B0";
                    return lResult;
                }
            }
        }

        [Verb("knxprod", HelpText = "Create knxprod file from given xml file")]
        class KnxprodOptions : EtsOptions {
            [Option('o', "Output", Required = false, HelpText = "Output file name", MetaValue = "FILE")]
            public string OutputFile { get; set; } = "";
        }

        [Verb("create", HelpText = "Process given xml file with all includes and create knxprod")]
        class CreateOptions : KnxprodOptions {
            [Option('h', "HeaderFileName", Required = false, HelpText = "Header file name", MetaValue = "FILE")]
            public string HeaderFileName { get; set; } = "";
            [Option('p', "Prefix", Required = false, HelpText = "Prefix for generated contant names in header file", MetaValue = "STRING")]
            public string Prefix { get; set; } = "";
            [Option('d', "Debug", Required = false, HelpText = "Additional output of <xmlfile>.debug.xml, this file is the input file for knxprod converter")]
            public bool Debug { get; set; } = false;
        }

        [Verb("check", HelpText = "execute sanity checks on given xml file")]
        class CheckOptions : EtsOptions {
        }

        static int Main(string[] args) {
            return new CommandLine.Parser(settings => settings.HelpWriter = Console.Out)
              .ParseArguments<CreateOptions, CheckOptions, KnxprodOptions, NewOptions>(args)
              .MapResult(
                (NewOptions opts) => VerbNew(opts),
                (CreateOptions opts) => VerbCreate(opts),
                (KnxprodOptions opts) => VerbKnxprod(opts),
                (CheckOptions opts) => VerbCheck(opts),
                errs => 1);
        }

        static private void WriteVersion() {
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


        static private int VerbNew(NewOptions opts) {
            WriteVersion();
            // Handle defaults
            if (opts.ApplicationName == "") opts.ApplicationName = opts.ProductName;
            if (opts.HardwareName == "") opts.HardwareName = opts.ProductName;
            if (opts.SerialNumber == "") opts.SerialNumber = opts.ApplicationNumber.ToString();
            if (opts.OrderNumber == "") opts.OrderNumber = opts.ApplicationNumber.ToString();

            // checks
            bool lFail = false;
            if (opts.ApplicationNumber > 65535) {
                Console.WriteLine("ApplicationNumber has to be less than 65536!");
                lFail = true;
            }
            if (lFail) return 1;

            // create initial xml file
            string lXmlFile = "";
            var assembly = Assembly.GetEntryAssembly();
            var resourceStream = assembly.GetManifestResourceStream("OpenKNXproducer.NewDevice.xml");
            using (var reader = new StreamReader(resourceStream, Encoding.UTF8)) {
                lXmlFile = reader.ReadToEnd();
            }
            lXmlFile = lXmlFile.Replace("%ApplicationName%", opts.ApplicationName);
            lXmlFile = lXmlFile.Replace("%ApplicationNumber%", opts.ApplicationNumber.ToString());
            lXmlFile = lXmlFile.Replace("%ApplicationVersion%", opts.ApplicationVersion.ToString());
            lXmlFile = lXmlFile.Replace("%HardwareName%", opts.HardwareName);
            lXmlFile = lXmlFile.Replace("%HardwareVersion%", opts.HardwareVersion.ToString());
            // lXmlFile = lXmlFile.Replace("%HardwareVersionEncoded%", GetEncoded(opts.HardwareVersion.ToString()));
            lXmlFile = lXmlFile.Replace("%SerialNumber%", opts.SerialNumber);
            // lXmlFile = lXmlFile.Replace("%SerialNumberEncoded%", GetEncoded(opts.SerialNumber));
            lXmlFile = lXmlFile.Replace("%OrderNumber%", opts.OrderNumber);
            // lXmlFile = lXmlFile.Replace("%OrderNumberEncoded%", GetEncoded(opts.OrderNumber));
            lXmlFile = lXmlFile.Replace("%ProductName%", opts.ProductName);
            lXmlFile = lXmlFile.Replace("%MaskVersion%", opts.MaskVersion);
            lXmlFile = lXmlFile.Replace("%MediumTypes%", opts.MediumTypes);
            Console.WriteLine("Creating xml file {0}", opts.XmlFileName);
            File.WriteAllText(opts.XmlFileName, lXmlFile);
            return VerbCreate(opts);
        }

        static private int VerbCreate(CreateOptions opts) {
            WriteVersion();
            string lHeaderFileName = Path.ChangeExtension(opts.XmlFileName, "h");
            if (opts.HeaderFileName != "") lHeaderFileName = opts.HeaderFileName;
            Console.WriteLine("Processing xml file {0}", opts.XmlFileName);
            ProcessInclude lResult = ProcessInclude.Factory(opts.XmlFileName, lHeaderFileName, opts.Prefix);
            bool lWithVersions = lResult.Expand();
            // We restore the original namespace in File
            lResult.SetNamespace();
            XmlDocument lXml = lResult.GetDocument();
            bool lSuccess = ProcessSanityChecks(lXml, lWithVersions);
            string lTempXmlFileName = Path.GetTempFileName();
            File.Delete(lTempXmlFileName);
            if (opts.Debug) lTempXmlFileName = opts.XmlFileName;
            lTempXmlFileName = Path.ChangeExtension(lTempXmlFileName, "debug.xml");
            if (opts.Debug) Console.WriteLine("Writing debug file to {0}", lTempXmlFileName);
            lXml.Save(lTempXmlFileName);
            Console.WriteLine("Writing header file to {0}", lHeaderFileName);
            File.WriteAllText(lHeaderFileName, lResult.HeaderGenerated);
            string lOutputFileName = Path.ChangeExtension(opts.OutputFile, "knxprod");
            if (opts.OutputFile == "") lOutputFileName = Path.ChangeExtension(opts.XmlFileName, "knxprod");
            if (lSuccess) {
                string lEtsPath = FindEtsPath(lResult.GetNamespace());
                return ExportKnxprod(lEtsPath, lOutputFileName, lTempXmlFileName, opts.Debug);
            } else {
                Console.WriteLine("--> Skipping creation of {0} due to check errors! <--", lOutputFileName);
                return 1;
            }
        }

        static private int VerbCheck(CheckOptions opts) {
            WriteVersion();
            string lFileName = Path.ChangeExtension(opts.XmlFileName, "xml");
            Console.WriteLine("Reading and resolving xml file {0}", lFileName);
            ProcessInclude lResult = ProcessInclude.Factory(opts.XmlFileName, "", "");
            lResult.LoadAdvanced(lFileName);
            return ProcessSanityChecks(lResult.GetDocument(), false) ? 0 : 1;
        }

        static private int VerbKnxprod(KnxprodOptions opts) {
            WriteVersion();
            string lOutputFileName = Path.ChangeExtension(opts.OutputFile, "knxprod");
            if (opts.OutputFile == "") lOutputFileName = Path.ChangeExtension(opts.XmlFileName, "knxprod");
            Console.WriteLine("Reading xml file {0} writing to {1}", opts.XmlFileName, lOutputFileName);

            string xml = File.ReadAllText(opts.XmlFileName);
            System.Text.RegularExpressions.Regex rs = new System.Text.RegularExpressions.Regex("xmlns=\"(http:\\/\\/knx\\.org\\/xml\\/project\\/[0-9]{1,2})\"");
            System.Text.RegularExpressions.Match match = rs.Match(xml);
            string lEtsPath = FindEtsPath(match.Groups[1].Value);
            return ExportKnxprod(lEtsPath, lOutputFileName, opts.XmlFileName, false);
        }
    }
}
