using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;

namespace OpenKNXproducer.Signing
{
    class HardwareSigner
    {
        public HardwareSigner(
                FileInfo hardwareFile,
                IDictionary<string, string> applProgIdMappings,
                IDictionary<string, string> applProgHashes,
                string basePath,
                int nsVersion,
                bool patchIds)
        {
            Assembly asm = Assembly.LoadFrom(Path.Combine(basePath, "Knx.Ets.XmlSigning.dll"));
            Assembly objm = Assembly.LoadFrom(Path.Combine(basePath, "Knx.Ets.Xml.ObjectModel.dll"));

            Type RegistrationKeyEnum = objm.GetType("Knx.Ets.Xml.ObjectModel.RegistrationKey");
            object registrationKey = Enum.Parse(RegistrationKeyEnum, "knxconv");

            string asmVersion = asm.GetName().Version.ToString();
            if(asmVersion.StartsWith("6.")) { //ab ETS6
                // registrationKey= Knx.Ets.Xml.ObjectModel.RegistrationKey.knxconv (is an enum)
                object knxSchemaVersion = Enum.ToObject(objm.GetType("Knx.Ets.Xml.ObjectModel.KnxXmlSchemaVersion"), nsVersion);
                _type = asm.GetType("Knx.Ets.XmlSigning.Signer.HardwareSigner");
                if (asmVersion.StartsWith("6.0"))
                    _type = asm.GetType("Knx.Ets.XmlSigning.HardwareSigner");
                _instance = Activator.CreateInstance(_type, hardwareFile, applProgIdMappings, applProgHashes, patchIds, registrationKey, knxSchemaVersion);
            } else {
                // registrationKey= Knx.Ets.Xml.ObjectModel.RegistrationKey.knxconv (is an enum)
                _type = asm.GetType("Knx.Ets.XmlSigning.HardwareSigner");
                _instance = Activator.CreateInstance(_type, hardwareFile, applProgIdMappings, applProgHashes, patchIds, registrationKey);
            }
        }

        public void SignFile()
        {
            _type.GetMethod("SignFile", BindingFlags.Instance | BindingFlags.Public).Invoke(_instance, null);
        }

        private readonly object _instance;
        private readonly Type _type;

        public IDictionary<string, string> OldNewIdMappings
        {
            get
            {
                return (IDictionary<string, string>)_type.GetProperty("OldNewIdMappings", BindingFlags.Public | BindingFlags.Instance).GetValue(_instance);
            }
        }
    }
}