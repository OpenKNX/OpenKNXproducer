using System.Xml;
using System.Xml.Linq;
using System.IO;

namespace OpenKNXproducer
{
    public static class ExtensionMethods
    {

        public static string NodeAttr(this XmlNode iNode, string iAttributeName, string iDefault = "")
        {
            string lResult = iDefault;
            XmlNode lAttribute = iNode.Attributes.GetNamedItem(iAttributeName);
            if (lAttribute != null) lResult = lAttribute.Value.ToString();
            return lResult;
        }

        public static string SubId(this XmlNode iNode, string iAttributeName, string iIdSeparator)
        {
            string lValue = iNode.NodeAttr(iAttributeName, iIdSeparator).Split(iIdSeparator)[1];
            return lValue;
        }

        public static XmlDocument ToXmlDocument(this XDocument xDocument)
        {
            var xmlDocument = new XmlDocument();
            using (var xmlReader = xDocument.CreateReader())
            {
                xmlDocument.Load(xmlReader);
            }
            return xmlDocument;
        }

        public static XDocument ToXDocument(this XmlDocument xmlDocument)
        {
            using (var nodeReader = new XmlNodeReader(xmlDocument))
            {
                // nodeReader.MoveToContent();
                return XDocument.Load(nodeReader);
            }
        }

        public static void DeepCopy(this DirectoryInfo directory, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);
            foreach (string dir in Directory.GetDirectories(directory.FullName, "*", SearchOption.AllDirectories))
            {
                string dirToCreate = dir.Replace(directory.FullName, destinationDir);
                Directory.CreateDirectory(dirToCreate);
            }

            foreach (string newPath in Directory.GetFiles(directory.FullName, "*.*", SearchOption.AllDirectories))
            {
                string lTargetPath = newPath.Replace(directory.FullName, destinationDir);
                if (lTargetPath.EndsWith(".md"))
                    lTargetPath = lTargetPath[..^3] + ".txt";
                File.Copy(newPath, lTargetPath, true);
            }
        }
    }
}

