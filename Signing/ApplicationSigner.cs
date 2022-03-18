using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;

namespace OpenKNXproducer.Signing
{
    class ApplicationProgramHasher
    {
        public ApplicationProgramHasher(
                    FileInfo applProgFile,
                    IDictionary<string, string> mapBaggageIdToFileIntegrity,
                    string basePath,
                    int nsVersion,
                    bool patchIds = true)
        {
            //if ets6 use ApplicationProgramStoreHasher
            //with HashStore Method
            Assembly asm = Assembly.LoadFrom(Path.Combine(basePath, "Knx.Ets.XmlSigning.dll"));

            if(asm.GetName().Version.ToString().StartsWith("6.0")) { //ab ETS6
                Assembly objm = Assembly.LoadFrom(Path.Combine(basePath, "Knx.Ets.Xml.ObjectModel.dll"));
                object knxSchemaVersion = Enum.ToObject(objm.GetType("Knx.Ets.Xml.ObjectModel.KnxXmlSchemaVersion"), nsVersion);
                _instance = Activator.CreateInstance(asm.GetType("Knx.Ets.XmlSigning.ApplicationProgramHasher"), applProgFile, mapBaggageIdToFileIntegrity, patchIds, knxSchemaVersion);
                _type = asm.GetType("Knx.Ets.XmlSigning.ApplicationProgramHasher");
            } else { //für ETS5 und früher
                _instance = Activator.CreateInstance(asm.GetType("Knx.Ets.XmlSigning.ApplicationProgramHasher"), applProgFile, mapBaggageIdToFileIntegrity, patchIds);
                _type = asm.GetType("Knx.Ets.XmlSigning.ApplicationProgramHasher");
            }
        }

        public void Hash()
        {
             _type.GetMethod("HashFile", BindingFlags.Instance | BindingFlags.Public).Invoke(_instance, null);
        }

        public string OldApplProgId
        {
            get
            {
                return _type.GetProperty("OldApplProgId", BindingFlags.Public | BindingFlags.Instance).GetValue(_instance).ToString();
            }
        }

        public string NewApplProgId
        {
            get
            {
                return _type.GetProperty("NewApplProgId", BindingFlags.Public | BindingFlags.Instance).GetValue(_instance).ToString();
            }
        }

        public string GeneratedHashString
        {
            get
            {
                return _type.GetProperty("GeneratedHashString", BindingFlags.Public | BindingFlags.Instance).GetValue(_instance).ToString();
            }
        }

        private readonly object _instance;
        private readonly Type _type;
    }
}