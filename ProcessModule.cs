using System.Text;
using System.Xml;

namespace OpenKNXproducer
{
    public class ProcessModule {

        private static Dictionary<string, ProcessModule> sModule = new Dictionary<string, ProcessModule>();

        private string mName;
        private DefineContent mDefine; 
        private int mChannel;
        private XmlNode mRootNode;
        private ProcessInclude mInclude;
        private int mModuleCount = 0;
        private int mModuleParamSize = 0;
        private ProcessModule mParentModule = null;
        private List<ProcessModule> mSubmodules = null;
        private Dictionary<XmlNode, int> mModuleInstance = new Dictionary<XmlNode, int>();

        public int ModuleCount { 
            get { return mModuleCount;} 
        }

        public bool IsSubmodule {
            get {
                return (mParentModule != null);
            }
        }
        protected ProcessModule(DefineContent iDefine, int iChannel, XmlNode iRootNode, ProcessInclude iInclude)
        {
            mDefine = iDefine;
            mChannel = iChannel;
            mRootNode = iRootNode;
            mInclude = iInclude;
        }

        // Adds a ModuleDef to the collection
        public static ProcessModule ProcessModuleFactory(DefineContent iDefine, int iChannel, XmlNode iRootNode, ProcessInclude iInclude) {
            ProcessModule lModule = new ProcessModule(iDefine, iChannel, iRootNode, iInclude);
            string lId = iRootNode.NodeAttr("Id");
            lModule.mName = iRootNode.NodeAttr("Name");
            sModule.Add(lId, lModule);
            return lModule;
        }

        public static ProcessModule ModuleById(string iId) { 
            if (sModule.ContainsKey(iId))
                return sModule[iId]; 
            else
                return null;
        }

        // adds a Module-Reference as UsageCounter
        public static void ProcessModuleInstance(DefineContent iDefine, int iChannel, XmlNode iRootNode, ProcessInclude iInclude) {
            // check for Repeat
            int lCount = 0;
            if (iRootNode.ParentNode.Name == "Repeat") {
                lCount = int.Parse(iRootNode.ParentNode.NodeAttr("Count", "0"));
            }
            ProcessModule lModule = ModuleById(iRootNode.NodeAttr("RefId"));
            if (lModule != null) {
                // for module count lCount == 0 is interpreted as 1
                lModule.mModuleCount += (lCount == 0) ? 1 : lCount;
                // we store all instances for postprocessing of start-values
                // lCount == 0 indicates an unrepeated node, lCount > 0 a repeated one
                lModule.mModuleInstance.Add(iRootNode, lCount);
            }
        }

        private void GetSubmoduleList() {
            if (mSubmodules == null) {
                mSubmodules = new List<ProcessModule>();
                XmlNodeList lSubmodules = mRootNode.SelectNodes("./SubModuleDefs/ModuleDef");
                if (lSubmodules != null) {
                    foreach (XmlNode lSubmodule in lSubmodules)
                    {
                        ProcessModule lModule = ProcessModule.ModuleById(lSubmodule.NodeAttr("Id"));
                        lModule.mParentModule = this;
                        mSubmodules.Add(lModule);
                    }
                }
            }
        }

        public int SubmodulesParamSize {
            get {
                int lResult = 0;
                GetSubmoduleList();
                foreach (ProcessModule lModule in mSubmodules)
                {
                    lResult += lModule.ModuleParamSize * lModule.ModuleCount;
                }
                return lResult;
            }
        }

        public int FullParamSize {
            get {
                return ModuleParamSize + SubmodulesParamSize;
            }
        }

        public int ModuleParamSize {
            get {
                if (mModuleParamSize == 0) {
                    // we check if we have a predefined size
                    XmlNode lArgParams = mRootNode.SelectSingleNode("./Arguments/Argument[@Name='argParam']");
                    XmlNode lAllocates = null;
                    if (lArgParams != null) {
                        lAllocates = lArgParams.Attributes.GetNamedItem("Allocates");
                    }
                    if (lAllocates == null) {
                        lAllocates = lArgParams.OwnerDocument.CreateAttribute("Allocates");
                        lAllocates.Value = "%ModuleSize%";
                        lArgParams.Attributes.SetNamedItem(lAllocates);
                    }
                    if (lAllocates.Value == "%ModuleSize%") {
                        // we have to calculate module parameter size
                        XmlNodeList lParams = mRootNode.SelectNodes("./Static/Parameters/Parameter|./Static/Parameters/Union");
                        // mModuleParamSize = CalcSubmodulesSize();
                        mModuleParamSize =  mInclude.CalcParamSize(lParams, ProcessInclude.PatameterTypesNode);
                        lAllocates.Value = (mModuleParamSize + SubmodulesParamSize).ToString();
                    } else {
                        // we have to take the predefined size
                        mModuleParamSize = int.Parse(lAllocates.Value);
                    }
                }
                return mModuleParamSize;
            }
        }

        private void UpdateModuleInstnces(int iParamOffset) {
            foreach (var lItem in mModuleInstance)
            {
                XmlNode lInstance = lItem.Key;
                // argParam has to be the first Arg of a module
                XmlNode lArg = lInstance.SelectSingleNode("./NumericArg");
                int lRepeatIndicator = lItem.Value;
                if (lRepeatIndicator > 0) {
                    // we have a repeated instance, we need to modify the allocator start value
                    string lId = lArg.NodeAttr("AllocatorRefId");
                    // find allocator
                    XmlNode lAllocator = lInstance.OwnerDocument.SelectSingleNode(string.Format("//Allocator[@Id='{0}']", lId));
                    XmlNode lStart = lAllocator.Attributes.GetNamedItem("Start");
                    if (lStart.Value == "%ModuleStart%") {
                        lStart.Value = iParamOffset.ToString();
                    }
                    iParamOffset += lRepeatIndicator * FullParamSize;
                    XmlNode lEnd = lAllocator.Attributes.GetNamedItem("maxInclusive");
                    if (lEnd.Value == "%ModuleEnd%") {
                        lEnd.Value = (iParamOffset - 1).ToString();
                    }
                } else {
                    // we have a single module call
                    XmlNode lValue = lArg.Attributes.GetNamedItem("Value");
                    if (lValue.Value == "%ModuleStart%") {
                        lValue.Value = iParamOffset.ToString();
                    }
                    iParamOffset += FullParamSize;
                }
            }
        }

        private void ExportHeaderParameter(int iParamOffset, int iModuleOffset, StringBuilder cOut) {
            string lCalcIndexName = "";
            string lCalcIndexArgs = "";
            cOut.AppendLine();
            cOut.AppendLine();
            cOut.AppendFormat("// Header generation for Module '{0}'", mName);
            cOut.AppendLine();
            cOut.AppendLine();
            cOut.AppendFormat("#define {0}Count {1}", mName, mModuleCount);
            cOut.AppendLine();
            cOut.AppendFormat("#define {0}ModuleParamSize {1}", mName, ModuleParamSize);
            cOut.AppendLine();
            cOut.AppendFormat("#define {0}SubmodulesParamSize {1}", mName, SubmodulesParamSize);
            cOut.AppendLine();
            cOut.AppendFormat("#define {0}ParamSize {1}", mName, FullParamSize);
            cOut.AppendLine();
            if (IsSubmodule) {
                cOut.AppendFormat("#define {0}CalcIndex(index, m1, m2) ({1}CalcIndex(index, m1) + {1}ModuleParamSize + m2 * {0}ParamSize)", mName, mParentModule.mName);    
                lCalcIndexName = "{3}" + $"{mName}CalcIndex" + "({3}{0}, m1, m2)"; 
                lCalcIndexArgs = "(m1, m2)";
            } else {
                cOut.AppendFormat("#define {0}ParamOffset {1}", mName, iParamOffset);
                cOut.AppendLine();
                cOut.AppendFormat("#define {0}CalcIndex(index, m1) (index + {0}ParamOffset + _channelIndex * {0}Count * {0}ParamSize + m1 * {0}ParamSize)", mName);    
                lCalcIndexName = "{3}" + $"{mName}CalcIndex" + "({3}{0}, m1)"; 
                lCalcIndexArgs = "(m1)";
            }
            cOut.AppendLine();
            cOut.AppendLine();

            XmlNodeList lNodes = mRootNode.SelectNodes("./Static/Parameters//Parameter");
            mInclude.ExportHeaderParameter(lNodes, mDefine, cOut, ProcessInclude.PatameterTypesNode, "", false, lCalcIndexName, lCalcIndexArgs);
            GetSubmoduleList();
            UpdateModuleInstnces(iModuleOffset);
            iParamOffset += ModuleParamSize;
            foreach (ProcessModule lModule in mSubmodules)
            {
                lModule.ExportHeaderParameter(iParamOffset, ModuleParamSize, cOut);
                iParamOffset += lModule.ModuleCount * lModule.ModuleParamSize;
            }
        }

        static public void ExportHeaderParameterAll(ProcessInclude iInclude, StringBuilder cOut) {
            // find correct module with this include
            foreach (var lItem in sModule)
            {
                ProcessModule lModule = lItem.Value;
                if (!lModule.IsSubmodule) {
                    lModule.ExportHeaderParameter(iInclude.ParameterBlockSize, iInclude.ParameterBlockSize, cOut);
                    iInclude.ParameterBlockSize += lModule.ModuleCount * lModule.FullParamSize;
                }
            }
        }
    }
}
 