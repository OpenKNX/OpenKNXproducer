using System.Text.RegularExpressions;
using OpenKNX.Toolbox.Lib.Data;

namespace OpenKNX.Toolbox.Lib
{
    public static class ReleaseContentHelper
    {
        /// <summary>
        /// Reads the "content.xml" file from a release.
        /// </summary>
        /// <param name="releaseDirectory">The release directory containing a "data\content.xml" file.</param>
        /// <returns>Returns a "ReleaseContent" object.</returns>
        public static ReleaseContent GetReleaseContent(string releaseDirectory)
        {
            var contentXmlPath = Path.Combine(releaseDirectory, "data", "content.xml");
            var contentXml = File.ReadAllText(contentXmlPath);

            var rs = new Regex("<ETSapp Name=\"(.*)\" XmlFile=\"(.*)\" \\/>");
            var match = rs.Match(contentXml);
            var appName = match.Groups[1].Value;
            var appXmlFileName = Path.Combine(Path.GetDirectoryName(contentXmlPath), match.Groups[2].Value);

            var appXmlFilePath = Path.Combine(releaseDirectory, "data", appXmlFileName);
            var releaseContent = new ReleaseContent(appName, appXmlFilePath);
            
            rs = new Regex("<Product Name=\"(.*)\" Firmware=\"(.*)\" Processor=\"(.*)\" \\/>");
            foreach (Match rsMatch in rs.Matches(contentXml))
            {
                if (rsMatch.Groups[3].Value != "RP2040")
                    continue;

                var firmwareName = rsMatch.Groups[1].Value;
                var filePathUf2 = Path.Combine(releaseDirectory, "data", rsMatch.Groups[2].Value);
                var firmware = new ReleaseContentFirmware(firmwareName, filePathUf2);
                releaseContent.Firmwares.Add(firmware);
            }

            return releaseContent;
        }
    }
}