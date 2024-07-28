namespace OpenKNX.Toolbox.Lib.Data
{
    public class ReleaseContent
    {
        public string AppName { get; set; }
        public string AppXmlFilePath { get; set; }
        public List<ReleaseContentFirmware> Firmwares { get; set; }

        public ReleaseContent(string appName, string appXmlFilePath)
        {
            AppName = appName;
            AppXmlFilePath = appXmlFilePath;
            Firmwares = [];
        }
    }
}