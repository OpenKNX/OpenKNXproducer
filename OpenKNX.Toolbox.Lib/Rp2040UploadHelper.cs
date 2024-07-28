namespace OpenKNX.Toolbox.Lib
{
    public static class Rp2040UploadHelper
    {
        /// <summary>
        /// Get a list of drives that can be used to upload firmware to a RP2040 MCU.
        /// </summary>
        /// <returns>The list of compatible drives.</returns>
        public static List<string> GetCompatibleDrives()
        {
            var uploadDrives = new List<string>();
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType == DriveType.Removable &&
                    drive.VolumeLabel == "RPI-RP2")
                    uploadDrives.Add(drive.Name);
            }

            uploadDrives.Sort();
            return uploadDrives;
        }

        /// <summary>
        /// Start an firmware upload to a RP2040 MCU via a drive. 
        /// </summary>
        /// <param name="uploadDrive">The drive to upload to.</param>
        /// <param name="filePathUf2">The file path to the UF2 file to upload.</param>
        /// <returns>True, if success, False otherwise.</returns>
        public static bool UploadFirmware(string uploadDrive, string filePathUf2)
        {
            try
            {
                var firmwareFileName = Path.GetFileName(filePathUf2);
                var drivePath = Path.Combine(uploadDrive, firmwareFileName);
                File.Copy(filePathUf2, drivePath);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}