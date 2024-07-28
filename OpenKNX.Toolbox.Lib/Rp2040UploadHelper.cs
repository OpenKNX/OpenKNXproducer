using System.Threading;
using System;

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
        /// <param name="progress">An optional progress report.</param>
        /// <param name="cancellationToken">An optional cancellation token.</param>
        /// <returns>True, if success, False otherwise.</returns>
        public static async Task<bool> UploadFirmware(string uploadDrive, string filePathUf2, IProgress<KeyValuePair<long, long>>? progress = null, CancellationToken? cancellationToken = null)
        {
            try
            {
                var firmwareFileName = Path.GetFileName(filePathUf2);
                var drivePath = Path.Combine(uploadDrive, firmwareFileName);
                var sourceFileInfo = new FileInfo(filePathUf2);

                var bytesRead = -1;
                var totalBytesCopied = 0L;
                var buffer = new byte[16384];
                var sourceLength = sourceFileInfo.Length;
                using (var outStream = new FileStream(drivePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    using (var inStream = new FileStream(filePathUf2, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        while (bytesRead != 0 && (cancellationToken == null || !cancellationToken.Value.IsCancellationRequested))
                        {
                            bytesRead = await inStream.ReadAsync(buffer, 0, buffer.Length);
                            if (bytesRead == 0 || (cancellationToken != null && cancellationToken.Value.IsCancellationRequested))
                                break;

                            await outStream.WriteAsync(buffer, 0, buffer.Length);
                            totalBytesCopied += bytesRead;

                            progress?.Report(new KeyValuePair<long, long>(totalBytesCopied, sourceLength));
                        }
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}