Open ■
┬────┴  OpenKNX Tool - Installation Instructions:
■ KNX 

This release is compatible with Linux, MacOS, and Windows 10/11 (x86, x64, and Arm). Follow the steps below for installation:

1. Extract Zip File:
   - If not already done, extract the entire contents of the zip file into a directory.

2. Run Installation Script:
   - Open the directory where the files were extracted.
   - Right-click on Install-OpenKNX-Tools.ps1
   - Choose 'Run with PowerShell'.
   - Confirm any security warnings by selecting 'Open File' if prompted.
   (The tools will be copied to the target directories based on the detected OS. Progress will be displayed in the console. No further action is required.)

3. MacOS and Linux Users:
   - Ensure PowerShell (pwsh) is installed on your system. You can install it from the official PowerShell GitHub releases page: [PowerShell GitHub Releases](https://github.com/PowerShell/PowerShell/releases)
   - Execute the script with PowerShell for proper installation.

4. Uninstalling/Removing:
   - For uninstallation, use Remove-OpenKNX-Tools.ps1 following similar steps.

5. Windows Script Execution Bypass:
   - If encountering a "not digitally signed" error on Windows:
     Open PowerShell console and execute:
     PowerShell.exe -ExecutionPolicy Bypass -File .\Install-OpenKNX-Tools.ps1

6. Completion:
   - Once completed, the installation or uninstallation process is done.


