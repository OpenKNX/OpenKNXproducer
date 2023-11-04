using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Xml;
using OpenKNXproducer;

class TemplateApplication
{
    readonly XmlDocument mDocument = new();
    private XmlNamespaceManager nsmgr;


    public XmlDocument Generate(ProcessInclude iInclude, XmlNode iBase)
    {
        GetTemplateDocument(iBase.Value);
        nsmgr = new XmlNamespaceManager(mDocument.NameTable);
        nsmgr.AddNamespace("op", ProcessInclude.cOwnNamespace);
        AddIncludes(iInclude);
        mDocument.Save("TemplateApplication.generated.xml");
        return mDocument;
    }

    private void GetTemplateDocument(string iFileName)
    {
        if (File.Exists(iFileName))
        {
            mDocument.Load(iFileName);
        }
        else
        {
            var assembly = Assembly.GetEntryAssembly();
            var resourceStream = assembly.GetManifestResourceStream("OpenKNXproducer.TemplateApplication.xml");
            using var reader = new StreamReader(resourceStream, Encoding.UTF8);
            mDocument.Load(reader);
        }
    }

    private void AddIncludes(ProcessInclude iInclude)
    {
        XmlNodeList lTemplates = mDocument.SelectNodes("//op:includetemplate", nsmgr);
        foreach (var lDefine in DefineContent.Defines())
        {
            foreach (XmlNode lTemplate in lTemplates)
            {
                XmlElement lElement = mDocument.CreateElement("op:include", ProcessInclude.cOwnNamespace);
                lTemplate.ParentNode.AppendChild(lElement);
                foreach (XmlAttribute lAttribute in lTemplate.Attributes)
                {
                    string lValue = lAttribute.Value;
                    if (lValue == "%prefix%") lValue = lDefine.Value.prefix;
                    if (lValue == "%share%") lValue = lDefine.Value.share;
                    if (lValue == "%templ%") lValue = lDefine.Value.template;
                    XmlAttribute lAttributeCopy = mDocument.CreateAttribute(lAttribute.Name);
                    lAttributeCopy.Value = lValue;
                    lElement.Attributes.Append(lAttributeCopy);
                }
            }
        }
        // finally we remove all include templates
        foreach (XmlNode lTemplate in lTemplates)
            lTemplate.ParentNode.RemoveChild(lTemplate);
    }
}