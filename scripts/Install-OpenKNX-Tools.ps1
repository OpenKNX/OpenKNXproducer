
####################################################################################################
#   Open ■
#   ┬────┴  Application Installer and Uninstaller
#   ■ KNX   2024 OpenKNX - Erkan Çolak
#
# Description: This script is used to install or uninstall OpenKNX applications on Windows, Linux and macOS.
#              The script reads the application settings from a JSON file and copies the files and folders
#              to the specified locations. The script also executes commands specified in the JSON file.
#
# Usage:       Install-YourApplication.ps1
#
####################################################################################################

# Import the Install-Application.ps1 script. This is needed to run the script from the release folder.
. "$PSScriptRoot\tools\Install-Application.ps1"

# Set values for the installation or uninstallation process
$Verbose    = $false  # Show verbose messages, if any errors occur during the installation. So we can see what went wrong
$Uninstall  = $false   # Uninstall the application
$WaitOnEnd  = $false   # Wait for the user to press a key before exiting, so they can read the messages
$AskUser    = $true  # Ask the user if they want to install / remove the application

# Check the operating system
$OS = CheckOS

# Messages for the user
$applicationName        = "OpenKNXproducer Tools"
$messageInstall         = "$(('Removing', 'Installing')[!$Uninstall]) $($applicationName) $(('from', 'to')[!$Uninstall]) $($OS)"
$messageComplete        = "$(('Removing', 'Installing')[!$Uninstall]) of $($applicationName) complete"
$messageInstallQuestion = "Do you really want to $(('Remove', 'Install')[!$Uninstall]) $($applicationName)? Please enter 'y' or 'n': "
$messageAbort           = "Aborting the $(('Removing', 'Installation')[!$Uninstall]) of $($applicationName)."

# Your application json configuration file
$jsonFilePath = "tools/Install-OpenKNXproducer.json"

OpenKNX_ShowLogo -AddCustomText $messageInstall -Line 4
  $messageDone = $messageComplete  
  if($AskUser) { Write-Host -NoNewline $messageInstallQuestion -ForegroundColor Blue; $answer = Read-Host } else { $answer = "y" }
  if($answer -eq "y") { Copy-ApplicationFiles -jsonFilePath $jsonFilePath } else { $messageDone = $messageAbort }
OpenKNX_ShowLogo -AddCustomText $messageDone -Line 0

# Show the user press any key to continue, so they can read the messages
if($WaitOnEnd) { Write-Host "Press any key to continue..." -ForegroundColor Blue; $null = $host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") }
