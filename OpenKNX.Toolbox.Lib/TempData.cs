using System.IO.Compression;

namespace OpenKNX.Toolbox.Lib
{
    public class TempData
    {
        private static TempData? instance;
        public static TempData Instance => instance ??= new TempData();

        private string? tempPath;

        private TempData()
        {
        }

        /// <summary>
        /// Allows to set a custom temporary path.
        /// </summary>
        /// <remarks>Needs to be called before "GetTempPath" to avoid generating an automated temporary path.</remarks>
        public void OverrideTempPath(string overrideTempPath)
        {
            tempPath = overrideTempPath;
        }

        /// <summary>
        /// Gets a temporary path.
        /// </summary>
        /// <returns>Returns the temporary path.</returns>
        public string GetTempPath()
        {
            if (tempPath == null)
                tempPath = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(Path.GetRandomFileName()));

            if (!Directory.Exists(tempPath))
                Directory.CreateDirectory(tempPath);

            return tempPath;
        }

        /// <summary>
        /// Remove any temporary data.
        /// </summary>
        /// <remarks>Should be called on program close.</remarks>
        public void CleanUpTempData()
        {
            if (tempPath != null)
                Directory.Delete(tempPath, true);
        }

        /// <summary>
        /// Extracts a ZIP file into a temporary directly.
        /// </summary>
        /// <param name="zipFilePath">The ZIP file to extract.</param>
        /// <returns>The path the ZIP file was extracted to.</returns>
        public string ExtractZipFile(string zipFilePath)
        {
            var zipFileName = Path.GetFileName(zipFilePath);
            var extractPath = Path.Combine(GetTempPath(), Path.GetFileNameWithoutExtension(zipFileName));
            if (Directory.Exists(extractPath))
                return extractPath;

            ZipFile.ExtractToDirectory(zipFilePath, extractPath);
            return extractPath;
        }
    }
}