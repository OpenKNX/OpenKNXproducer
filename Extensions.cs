using System.Xml;
using System.IO;

namespace OpenKNXproducer {
    public static class ExtensionMethods {
        public static string GetNamedItemValueOrEmpty(this XmlAttributeCollection iAttibutes, string iName) {
            string lResult = "";
            XmlNode lAttribute = iAttibutes.GetNamedItem(iName);
            if (lAttribute != null) lResult = lAttribute.Value;
            return lResult;
        }

        public static string NodeAttr(this XmlNode iNode, string iAttributeName, string iDefault = "") {
            string lResult = iDefault;
            XmlNode lAttribute = iNode.Attributes.GetNamedItem(iAttributeName);
            if (lAttribute != null) lResult = lAttribute.Value.ToString();
            return lResult;
        }
    }
}

