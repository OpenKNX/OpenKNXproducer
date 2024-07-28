using System.Text.RegularExpressions;
using OpenKNXproducer;

namespace OpenKNX.Toolbox.Lib
{
    public static class KnxProdBuilder
    {
        /// <summary>
        /// Generates a KNXprod file.
        /// </summary>
        /// <param name="xmlFilePath">The path to the XML file the KNXprod will be generated from.</param>
        /// <param name="knxprodOutputPath">The output path of the generated KNXprod file.</param>
        /// <returns>True, if success, False otherwise.</returns>
        public static bool BuildKnxProd(string xmlFilePath, string knxprodOutputPath)
        {
            var workingDir = KnxProdHelper.GetAbsWorkingDir(xmlFilePath);
            var xml = File.ReadAllText(xmlFilePath);
            var rs = new Regex("xmlns=\"(http:\\/\\/knx\\.org\\/xml\\/project\\/[0-9]{1,2})\"");
            var match = rs.Match(xml);
            var etsPath = KnxProdHelper.FindEtsPath(match.Groups[1].Value);
            return KnxProdHelper.ExportKnxprod(etsPath, workingDir, knxprodOutputPath, xmlFilePath, Path.GetFileName(xmlFilePath).Replace(".xml", ".baggages"), null, false, false) == 0;
        }
    }
}