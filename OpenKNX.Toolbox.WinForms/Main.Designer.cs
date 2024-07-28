namespace OpenKNX.Toolbox.WinForms
{
    partial class Main
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            inReleaseGitHubProject = new ComboBox();
            grpReleaseZip = new GroupBox();
            inReleaseZipFileExtract = new Button();
            inReleaseZipFileSelect = new Button();
            inReleaseZipFile = new TextBox();
            inReleaseZip = new RadioButton();
            inReleaseGitHub = new RadioButton();
            grpReleaseGitHub = new GroupBox();
            inReleaseGitHubFileDownloadExtract = new Button();
            lblReleaseGitHubFile = new Label();
            inReleaseGitHubFile = new ComboBox();
            lblReleaseGitHubRelease = new Label();
            inReleaseGitHubRelease = new ComboBox();
            lblReleaseGitHubProject = new Label();
            inFirmwareUpload = new Button();
            grpRelease = new GroupBox();
            grpFirmware = new GroupBox();
            lblFirmwareVariant = new Label();
            inFirmwareVariant = new ComboBox();
            inFirmwareTargetRefresh = new Button();
            lblFirmwareTarget = new Label();
            inFirmwareTarget = new ComboBox();
            grpKnxprod = new GroupBox();
            outKnxprodApp = new Label();
            lblKnxprodApp = new Label();
            lblKnxprodPath = new Label();
            inKnxprodPathSelect = new Button();
            inKnxprodBuild = new Button();
            inKnxprodPath = new TextBox();
            outFirmwareUploadProgress = new ProgressBar();
            grpReleaseZip.SuspendLayout();
            grpReleaseGitHub.SuspendLayout();
            grpRelease.SuspendLayout();
            grpFirmware.SuspendLayout();
            grpKnxprod.SuspendLayout();
            SuspendLayout();
            // 
            // inReleaseGitHubProject
            // 
            inReleaseGitHubProject.DropDownStyle = ComboBoxStyle.DropDownList;
            inReleaseGitHubProject.FormattingEnabled = true;
            inReleaseGitHubProject.Location = new Point(16, 32);
            inReleaseGitHubProject.Name = "inReleaseGitHubProject";
            inReleaseGitHubProject.Size = new Size(272, 23);
            inReleaseGitHubProject.TabIndex = 0;
            inReleaseGitHubProject.SelectedIndexChanged += inReleaseGitHubProject_SelectedIndexChanged;
            // 
            // grpReleaseZip
            // 
            grpReleaseZip.Controls.Add(inReleaseZipFileExtract);
            grpReleaseZip.Controls.Add(inReleaseZipFileSelect);
            grpReleaseZip.Controls.Add(inReleaseZipFile);
            grpReleaseZip.Enabled = false;
            grpReleaseZip.Location = new Point(32, 248);
            grpReleaseZip.Name = "grpReleaseZip";
            grpReleaseZip.Size = new Size(304, 88);
            grpReleaseZip.TabIndex = 1;
            grpReleaseZip.TabStop = false;
            // 
            // inReleaseZipFileExtract
            // 
            inReleaseZipFileExtract.Enabled = false;
            inReleaseZipFileExtract.Location = new Point(16, 48);
            inReleaseZipFileExtract.Name = "inReleaseZipFileExtract";
            inReleaseZipFileExtract.Size = new Size(272, 23);
            inReleaseZipFileExtract.TabIndex = 7;
            inReleaseZipFileExtract.Text = "Release-Datei jetzt entpacken";
            inReleaseZipFileExtract.UseVisualStyleBackColor = true;
            inReleaseZipFileExtract.Click += inReleaseZipFileExtract_Click;
            // 
            // inReleaseZipFileSelect
            // 
            inReleaseZipFileSelect.Location = new Point(216, 24);
            inReleaseZipFileSelect.Name = "inReleaseZipFileSelect";
            inReleaseZipFileSelect.Size = new Size(75, 23);
            inReleaseZipFileSelect.TabIndex = 1;
            inReleaseZipFileSelect.Text = "Wählen";
            inReleaseZipFileSelect.UseVisualStyleBackColor = true;
            inReleaseZipFileSelect.Click += inReleaseZipFileSelect_Click;
            // 
            // inReleaseZipFile
            // 
            inReleaseZipFile.Location = new Point(16, 24);
            inReleaseZipFile.Name = "inReleaseZipFile";
            inReleaseZipFile.Size = new Size(192, 23);
            inReleaseZipFile.TabIndex = 0;
            inReleaseZipFile.TextChanged += inReleaseZipFile_TextChanged;
            // 
            // inReleaseZip
            // 
            inReleaseZip.AutoSize = true;
            inReleaseZip.Location = new Point(16, 232);
            inReleaseZip.Name = "inReleaseZip";
            inReleaseZip.Size = new Size(171, 19);
            inReleaseZip.TabIndex = 0;
            inReleaseZip.Text = "Release von ZIP-Datei laden";
            inReleaseZip.UseVisualStyleBackColor = true;
            inReleaseZip.CheckedChanged += inRelease_CheckedChanged;
            // 
            // inReleaseGitHub
            // 
            inReleaseGitHub.AutoSize = true;
            inReleaseGitHub.Checked = true;
            inReleaseGitHub.Location = new Point(16, 16);
            inReleaseGitHub.Name = "inReleaseGitHub";
            inReleaseGitHub.Size = new Size(160, 19);
            inReleaseGitHub.TabIndex = 2;
            inReleaseGitHub.TabStop = true;
            inReleaseGitHub.Text = "Release von GitHub laden";
            inReleaseGitHub.UseVisualStyleBackColor = true;
            inReleaseGitHub.CheckedChanged += inRelease_CheckedChanged;
            // 
            // grpReleaseGitHub
            // 
            grpReleaseGitHub.Controls.Add(inReleaseGitHubFileDownloadExtract);
            grpReleaseGitHub.Controls.Add(lblReleaseGitHubFile);
            grpReleaseGitHub.Controls.Add(inReleaseGitHubFile);
            grpReleaseGitHub.Controls.Add(lblReleaseGitHubRelease);
            grpReleaseGitHub.Controls.Add(inReleaseGitHubRelease);
            grpReleaseGitHub.Controls.Add(lblReleaseGitHubProject);
            grpReleaseGitHub.Controls.Add(inReleaseGitHubProject);
            grpReleaseGitHub.Location = new Point(32, 32);
            grpReleaseGitHub.Name = "grpReleaseGitHub";
            grpReleaseGitHub.Size = new Size(304, 192);
            grpReleaseGitHub.TabIndex = 3;
            grpReleaseGitHub.TabStop = false;
            // 
            // inReleaseGitHubFileDownloadExtract
            // 
            inReleaseGitHubFileDownloadExtract.Enabled = false;
            inReleaseGitHubFileDownloadExtract.Location = new Point(16, 152);
            inReleaseGitHubFileDownloadExtract.Name = "inReleaseGitHubFileDownloadExtract";
            inReleaseGitHubFileDownloadExtract.Size = new Size(272, 23);
            inReleaseGitHubFileDownloadExtract.TabIndex = 6;
            inReleaseGitHubFileDownloadExtract.Text = "Release-Datei jetzt herunterladen und entpacken";
            inReleaseGitHubFileDownloadExtract.UseVisualStyleBackColor = true;
            inReleaseGitHubFileDownloadExtract.Click += inReleaseGitHubFileDownloadExtract_Click;
            // 
            // lblReleaseGitHubFile
            // 
            lblReleaseGitHubFile.AutoSize = true;
            lblReleaseGitHubFile.Location = new Point(16, 112);
            lblReleaseGitHubFile.Name = "lblReleaseGitHubFile";
            lblReleaseGitHubFile.Size = new Size(78, 15);
            lblReleaseGitHubFile.TabIndex = 5;
            lblReleaseGitHubFile.Text = "Datei wählen:";
            // 
            // inReleaseGitHubFile
            // 
            inReleaseGitHubFile.DropDownStyle = ComboBoxStyle.DropDownList;
            inReleaseGitHubFile.FormattingEnabled = true;
            inReleaseGitHubFile.Location = new Point(16, 128);
            inReleaseGitHubFile.Name = "inReleaseGitHubFile";
            inReleaseGitHubFile.Size = new Size(272, 23);
            inReleaseGitHubFile.TabIndex = 4;
            inReleaseGitHubFile.SelectedIndexChanged += inReleaseGitHubFile_SelectedIndexChanged;
            // 
            // lblReleaseGitHubRelease
            // 
            lblReleaseGitHubRelease.AutoSize = true;
            lblReleaseGitHubRelease.Location = new Point(16, 64);
            lblReleaseGitHubRelease.Name = "lblReleaseGitHubRelease";
            lblReleaseGitHubRelease.Size = new Size(90, 15);
            lblReleaseGitHubRelease.TabIndex = 3;
            lblReleaseGitHubRelease.Text = "Release wählen:";
            // 
            // inReleaseGitHubRelease
            // 
            inReleaseGitHubRelease.DropDownStyle = ComboBoxStyle.DropDownList;
            inReleaseGitHubRelease.FormattingEnabled = true;
            inReleaseGitHubRelease.Location = new Point(16, 80);
            inReleaseGitHubRelease.Name = "inReleaseGitHubRelease";
            inReleaseGitHubRelease.Size = new Size(272, 23);
            inReleaseGitHubRelease.TabIndex = 2;
            inReleaseGitHubRelease.SelectedIndexChanged += inReleaseGitHubRelease_SelectedIndexChanged;
            // 
            // lblReleaseGitHubProject
            // 
            lblReleaseGitHubProject.AutoSize = true;
            lblReleaseGitHubProject.Location = new Point(16, 16);
            lblReleaseGitHubProject.Name = "lblReleaseGitHubProject";
            lblReleaseGitHubProject.Size = new Size(143, 15);
            lblReleaseGitHubProject.TabIndex = 1;
            lblReleaseGitHubProject.Text = "OpenKNX Projekt wählen:";
            // 
            // inFirmwareUpload
            // 
            inFirmwareUpload.Enabled = false;
            inFirmwareUpload.Location = new Point(16, 120);
            inFirmwareUpload.Name = "inFirmwareUpload";
            inFirmwareUpload.Size = new Size(272, 23);
            inFirmwareUpload.TabIndex = 4;
            inFirmwareUpload.Text = "Firmware jetzt hochladen";
            inFirmwareUpload.UseVisualStyleBackColor = true;
            inFirmwareUpload.Click += inFirmwareUpload_Click;
            // 
            // grpRelease
            // 
            grpRelease.Controls.Add(inReleaseGitHub);
            grpRelease.Controls.Add(grpReleaseZip);
            grpRelease.Controls.Add(inReleaseZip);
            grpRelease.Controls.Add(grpReleaseGitHub);
            grpRelease.Location = new Point(16, 8);
            grpRelease.Name = "grpRelease";
            grpRelease.Size = new Size(352, 352);
            grpRelease.TabIndex = 5;
            grpRelease.TabStop = false;
            grpRelease.Text = "1. Schritt: Release auswählen";
            // 
            // grpFirmware
            // 
            grpFirmware.Controls.Add(outFirmwareUploadProgress);
            grpFirmware.Controls.Add(lblFirmwareVariant);
            grpFirmware.Controls.Add(inFirmwareVariant);
            grpFirmware.Controls.Add(inFirmwareTargetRefresh);
            grpFirmware.Controls.Add(lblFirmwareTarget);
            grpFirmware.Controls.Add(inFirmwareTarget);
            grpFirmware.Controls.Add(inFirmwareUpload);
            grpFirmware.Enabled = false;
            grpFirmware.Location = new Point(384, 8);
            grpFirmware.Name = "grpFirmware";
            grpFirmware.Size = new Size(304, 176);
            grpFirmware.TabIndex = 6;
            grpFirmware.TabStop = false;
            grpFirmware.Text = "2. Schritt: Firmware hochladen";
            // 
            // lblFirmwareVariant
            // 
            lblFirmwareVariant.AutoSize = true;
            lblFirmwareVariant.Location = new Point(16, 24);
            lblFirmwareVariant.Name = "lblFirmwareVariant";
            lblFirmwareVariant.Size = new Size(178, 15);
            lblFirmwareVariant.TabIndex = 7;
            lblFirmwareVariant.Text = "Firmware-Variante für Hardware:";
            // 
            // inFirmwareVariant
            // 
            inFirmwareVariant.DropDownStyle = ComboBoxStyle.DropDownList;
            inFirmwareVariant.FormattingEnabled = true;
            inFirmwareVariant.Location = new Point(16, 40);
            inFirmwareVariant.Name = "inFirmwareVariant";
            inFirmwareVariant.Size = new Size(272, 23);
            inFirmwareVariant.TabIndex = 6;
            inFirmwareVariant.SelectedIndexChanged += inFirmwareVariant_SelectedIndexChanged;
            // 
            // inFirmwareTargetRefresh
            // 
            inFirmwareTargetRefresh.Location = new Point(160, 88);
            inFirmwareTargetRefresh.Name = "inFirmwareTargetRefresh";
            inFirmwareTargetRefresh.Size = new Size(128, 23);
            inFirmwareTargetRefresh.TabIndex = 5;
            inFirmwareTargetRefresh.Text = "Aktualisieren";
            inFirmwareTargetRefresh.UseVisualStyleBackColor = true;
            inFirmwareTargetRefresh.Click += inFirmwareTargetRefresh_Click;
            // 
            // lblFirmwareTarget
            // 
            lblFirmwareTarget.AutoSize = true;
            lblFirmwareTarget.Location = new Point(16, 72);
            lblFirmwareTarget.Name = "lblFirmwareTarget";
            lblFirmwareTarget.Size = new Size(133, 15);
            lblFirmwareTarget.TabIndex = 1;
            lblFirmwareTarget.Text = "Ziellaufwerk auswählen:";
            // 
            // inFirmwareTarget
            // 
            inFirmwareTarget.DropDownStyle = ComboBoxStyle.DropDownList;
            inFirmwareTarget.FormattingEnabled = true;
            inFirmwareTarget.Location = new Point(16, 88);
            inFirmwareTarget.Name = "inFirmwareTarget";
            inFirmwareTarget.Size = new Size(136, 23);
            inFirmwareTarget.TabIndex = 0;
            inFirmwareTarget.SelectedIndexChanged += inFirmwareTarget_SelectedIndexChanged;
            // 
            // grpKnxprod
            // 
            grpKnxprod.Controls.Add(outKnxprodApp);
            grpKnxprod.Controls.Add(lblKnxprodApp);
            grpKnxprod.Controls.Add(lblKnxprodPath);
            grpKnxprod.Controls.Add(inKnxprodPathSelect);
            grpKnxprod.Controls.Add(inKnxprodBuild);
            grpKnxprod.Controls.Add(inKnxprodPath);
            grpKnxprod.Enabled = false;
            grpKnxprod.Location = new Point(384, 192);
            grpKnxprod.Name = "grpKnxprod";
            grpKnxprod.Size = new Size(304, 144);
            grpKnxprod.TabIndex = 7;
            grpKnxprod.TabStop = false;
            grpKnxprod.Text = "3. Schritt: KNXprod erstellen";
            // 
            // outKnxprodApp
            // 
            outKnxprodApp.AutoSize = true;
            outKnxprodApp.Location = new Point(48, 32);
            outKnxprodApp.Name = "outKnxprodApp";
            outKnxprodApp.Size = new Size(12, 15);
            outKnxprodApp.TabIndex = 7;
            outKnxprodApp.Text = "-";
            // 
            // lblKnxprodApp
            // 
            lblKnxprodApp.AutoSize = true;
            lblKnxprodApp.Location = new Point(16, 32);
            lblKnxprodApp.Name = "lblKnxprodApp";
            lblKnxprodApp.Size = new Size(32, 15);
            lblKnxprodApp.TabIndex = 6;
            lblKnxprodApp.Text = "App:";
            // 
            // lblKnxprodPath
            // 
            lblKnxprodPath.AutoSize = true;
            lblKnxprodPath.Location = new Point(16, 56);
            lblKnxprodPath.Name = "lblKnxprodPath";
            lblKnxprodPath.Size = new Size(155, 15);
            lblKnxprodPath.TabIndex = 5;
            lblKnxprodPath.Text = "KNXprod speichern in Datei:";
            // 
            // inKnxprodPathSelect
            // 
            inKnxprodPathSelect.Location = new Point(216, 72);
            inKnxprodPathSelect.Name = "inKnxprodPathSelect";
            inKnxprodPathSelect.Size = new Size(75, 23);
            inKnxprodPathSelect.TabIndex = 3;
            inKnxprodPathSelect.Text = "Wählen";
            inKnxprodPathSelect.UseVisualStyleBackColor = true;
            inKnxprodPathSelect.Click += inKnxprodPathSelect_Click;
            // 
            // inKnxprodBuild
            // 
            inKnxprodBuild.Enabled = false;
            inKnxprodBuild.Location = new Point(16, 104);
            inKnxprodBuild.Name = "inKnxprodBuild";
            inKnxprodBuild.Size = new Size(272, 23);
            inKnxprodBuild.TabIndex = 4;
            inKnxprodBuild.Text = "KNXprod jetzt erstellen";
            inKnxprodBuild.UseVisualStyleBackColor = true;
            inKnxprodBuild.Click += inKnxprodBuild_Click;
            // 
            // inKnxprodPath
            // 
            inKnxprodPath.Location = new Point(16, 72);
            inKnxprodPath.Name = "inKnxprodPath";
            inKnxprodPath.Size = new Size(192, 23);
            inKnxprodPath.TabIndex = 2;
            inKnxprodPath.TextChanged += inKnxprodPath_TextChanged;
            // 
            // outFirmwareUploadProgress
            // 
            outFirmwareUploadProgress.Location = new Point(16, 144);
            outFirmwareUploadProgress.Name = "outFirmwareUploadProgress";
            outFirmwareUploadProgress.Size = new Size(272, 16);
            outFirmwareUploadProgress.TabIndex = 8;
            // 
            // Main
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(704, 375);
            Controls.Add(grpKnxprod);
            Controls.Add(grpFirmware);
            Controls.Add(grpRelease);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "Main";
            Text = "OpenKNX: Toolbox";
            FormClosed += Main_FormClosed;
            Load += Main_Load;
            grpReleaseZip.ResumeLayout(false);
            grpReleaseZip.PerformLayout();
            grpReleaseGitHub.ResumeLayout(false);
            grpReleaseGitHub.PerformLayout();
            grpRelease.ResumeLayout(false);
            grpRelease.PerformLayout();
            grpFirmware.ResumeLayout(false);
            grpFirmware.PerformLayout();
            grpKnxprod.ResumeLayout(false);
            grpKnxprod.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private ComboBox inReleaseGitHubProject;
        private GroupBox grpReleaseZip;
        private Button inReleaseZipFileSelect;
        private TextBox inReleaseZipFile;
        private RadioButton inReleaseZip;
        private RadioButton inReleaseGitHub;
        private GroupBox grpReleaseGitHub;
        private Label lblReleaseGitHubRelease;
        private ComboBox inReleaseGitHubRelease;
        private Label lblReleaseGitHubProject;
        private Button inFirmwareUpload;
        private GroupBox grpRelease;
        private GroupBox grpFirmware;
        private Label lblFirmwareTarget;
        private ComboBox inFirmwareTarget;
        private GroupBox grpKnxprod;
        private Button inKnxprodPathSelect;
        private Button inKnxprodBuild;
        private TextBox inKnxprodPath;
        private Button inFirmwareTargetRefresh;
        private Label lblReleaseGitHubFile;
        private ComboBox inReleaseGitHubFile;
        private Button inReleaseGitHubFileDownloadExtract;
        private Label lblFirmwareVariant;
        private ComboBox inFirmwareVariant;
        private Button inReleaseZipFileExtract;
        private Label lblKnxprodPath;
        private Label outKnxprodApp;
        private Label lblKnxprodApp;
        private ProgressBar outFirmwareUploadProgress;
    }
}
