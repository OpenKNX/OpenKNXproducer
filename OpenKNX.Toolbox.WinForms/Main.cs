using System;
using System.Diagnostics;
using Microsoft.VisualBasic.Devices;
using OpenKNX.Toolbox.Lib;
using OpenKNX.Toolbox.Lib.Data;
using OpenKNX.Toolbox.WinForms.Properties;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace OpenKNX.Toolbox.WinForms
{
    public partial class Main : Form
    {
        private OpenKnxData? openKnxData;

        public Main()
        {
            InitializeComponent();
        }

        private void Main_Load(object sender, EventArgs e)
        {
            Icon = Resources.openknx;
            RefreshFirmwareTargets();

            try
            {
                var task = Task.Run(() => GitHubAccess.GetOpenKnxData(Application.StartupPath));
                task.Wait();
                openKnxData = task.Result;

                if (openKnxData == null)
                {
                    MessageBox.Show("OpenKNX-Projekte konnten nicht von GitHub geladen werden.", "Laden von GitHub fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Close();
                    return;
                }

                inReleaseGitHubProject.Items.AddRange(openKnxData.Projects.ToArray());
            }
            catch (AggregateException ex)
            {
                MessageBox.Show(
                    "OpenKNX-Projekte konnten nicht von GitHub geladen werden:" + Environment.NewLine +
                    "GitHub API Rate Limit erreicht!", "Laden von GitHub fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void inRelease_CheckedChanged(object sender, EventArgs e)
        {
            grpReleaseGitHub.Enabled = inReleaseGitHub.Checked;
            grpReleaseZip.Enabled = inReleaseZip.Checked;

            grpFirmware.Enabled = false;
            grpKnxprod.Enabled = false;
        }

        private void inReleaseGitHubProject_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (inReleaseGitHubProject.SelectedItem == null)
                return;

            var openKnxProject = (OpenKnxProject)inReleaseGitHubProject.SelectedItem;

            inReleaseGitHubRelease.Items.Clear();
            inReleaseGitHubRelease.Items.AddRange(openKnxProject.Releases.ToArray());

            inReleaseGitHubFile.Items.Clear();
            CheckReleaseGitHubSelected();
        }

        private void inReleaseGitHubRelease_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (inReleaseGitHubRelease.SelectedItem == null)
                return;

            var openKnxRelease = (OpenKnxRelease)inReleaseGitHubRelease.SelectedItem;

            inReleaseGitHubFile.Items.Clear();
            inReleaseGitHubFile.Items.AddRange(openKnxRelease.Files.ToArray());

            CheckReleaseGitHubSelected();
        }

        private void inReleaseGitHubFile_SelectedIndexChanged(object sender, EventArgs e)
        {
            CheckReleaseGitHubSelected();
        }

        private void CheckReleaseGitHubSelected()
        {
            inReleaseGitHubFileDownloadExtract.Enabled = inReleaseGitHubFile.SelectedItem != null;
            grpFirmware.Enabled = false;
            grpKnxprod.Enabled = false;
        }

        private void inReleaseGitHubFileDownloadExtract_Click(object sender, EventArgs e)
        {
            if (inReleaseGitHubFile.SelectedItem == null)
                return;

            var openKnxReleaseFile = (OpenKnxReleaseFile)inReleaseGitHubFile.SelectedItem;
            var task = Task.Run(() => GitHubAccess.DownloadReleaseFile(openKnxReleaseFile));
            task.Wait();

            ExtractReleaseAndLoadFirmwareVariants(task.Result);
        }

        private void inReleaseZipFileSelect_Click(object sender, EventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Title = "Release ZIP-Datei auswählen";
            openFileDialog.Filter = "*.zip|*.zip";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
                inReleaseZipFile.Text = openFileDialog.FileName;
        }

        private void inReleaseZipFile_TextChanged(object sender, EventArgs e)
        {
            inReleaseZipFileExtract.Enabled = inReleaseZipFile.Text.ToLower().EndsWith(".zip");
            grpFirmware.Enabled = false;
            grpKnxprod.Enabled = false;
        }

        private void inReleaseZipFileExtract_Click(object sender, EventArgs e)
        {
            ExtractReleaseAndLoadFirmwareVariants(inReleaseZipFile.Text);
        }

        private void ExtractReleaseAndLoadFirmwareVariants(string releaseZipFilePath)
        {
            if (releaseZipFilePath == null)
                return;

            var releaseDirectory = TempData.Instance.ExtractZipFile(releaseZipFilePath);
            var releaseContent = ReleaseContentHelper.GetReleaseContent(releaseDirectory);

            inFirmwareVariant.Items.Clear();
            inFirmwareVariant.Items.AddRange(releaseContent.Firmwares.ToArray());

            var appXmlFileName = Path.GetFileName(releaseContent.AppXmlFilePath);
            outKnxprodApp.Text = $"{releaseContent.AppName} ({appXmlFileName})";
            outKnxprodApp.Tag = releaseContent.AppXmlFilePath;
            inKnxprodPath.Text = Path.Combine(Application.StartupPath, Path.ChangeExtension(appXmlFileName, "knxprod"));

            grpFirmware.Enabled = true;
            grpKnxprod.Enabled = true;
        }

        private void inKnxprodPathSelect_Click(object sender, EventArgs e)
        {
            var saveFileDialog = new SaveFileDialog();
            saveFileDialog.Title = "Zielpfad der KNXprod auswählen";
            saveFileDialog.Filter = "*.knxprod|*.knxprod";

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
                inReleaseZipFile.Text = saveFileDialog.FileName;
        }

        private void inFirmwareTargetRefresh_Click(object sender, EventArgs e)
        {
            RefreshFirmwareTargets();
        }

        private void inFirmwareVariant_SelectedIndexChanged(object sender, EventArgs e)
        {
            CheckFirmwareUploadReady();
        }

        private void inFirmwareTarget_SelectedIndexChanged(object sender, EventArgs e)
        {
            CheckFirmwareUploadReady();
        }

        private void RefreshFirmwareTargets()
        {
            inFirmwareTarget.Items.Clear();
            inFirmwareTarget.Items.AddRange(Rp2040UploadHelper.GetCompatibleDrives().ToArray());

            CheckFirmwareUploadReady();
        }

        private void CheckFirmwareUploadReady()
        {
            inFirmwareUpload.Enabled =
                inFirmwareVariant.SelectedItem != null &&
                inFirmwareTarget.SelectedItem != null;
        }

        private void inFirmwareUpload_Click(object sender, EventArgs e)
        {
            if (inFirmwareVariant.SelectedItem == null ||
                inFirmwareTarget.SelectedItem == null)
                return;

            var firmware = (ReleaseContentFirmware)inFirmwareVariant.SelectedItem;
            var uploadDrive = (string)inFirmwareTarget.SelectedItem;

            var progress = new Progress<KeyValuePair<long, long>>();
            progress.ProgressChanged += FirmwareUploadProgress_ProgressChanged;

            var task = Task.Run(() => Rp2040UploadHelper.UploadFirmware(uploadDrive, firmware.FilePathUf2, progress));
            while (!task.IsCompleted)
            {
                task.Wait(100);
                Application.DoEvents();
            }

            if (task.Result)
                MessageBox.Show("Die Firmware wurde erfolgreich hochgeladen.", "Firmware-Upload erfolgreich", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                MessageBox.Show("Die Firmware konnte nicht hochgeladen werden.", "Firmware-Upload fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Error);

            RefreshFirmwareTargets();
        }

        private void FirmwareUploadProgress_ProgressChanged(object? sender, KeyValuePair<long, long> e)
        {
            outFirmwareUploadProgress.Invoke(() => outFirmwareUploadProgress.Value = (int)(e.Key / (double)e.Value * 100));
        }

        private void inKnxprodPath_TextChanged(object sender, EventArgs e)
        {
            inKnxprodBuild.Enabled = inKnxprodPath.Text.ToLower().EndsWith(".knxprod");
        }

        private void inKnxprodBuild_Click(object sender, EventArgs e)
        {
            if (outKnxprodApp.Tag == null)
                return;

            var xmlFilePath = (string)outKnxprodApp.Tag;
            if (KnxProdBuilder.BuildKnxProd(xmlFilePath, inKnxprodPath.Text))
                MessageBox.Show("Die KNXprod wurde erfolgreich gebaut.", "KNXprod-Bau erfolgreich", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                MessageBox.Show("Die KNXprod konnte nicht gebaut werden.", "KNXprod-Bau fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void Main_FormClosed(object sender, FormClosedEventArgs e)
        {
            TempData.Instance.CleanUpTempData();
        }
    }
}