####################################################################################################
#   Open ■
#   ┬────┴  Application Installer and Uninstaller
#   ■ KNX   2024 OpenKNX - Erkan Çolak
#
# Description: This script is used to install or uninstall OpenKNX applications on Windows, Linux and macOS.
#              The script reads the application settings from a JSON file and copies the files and folders
#              to the specified locations. The script also executes the specified commands.
#
# Usage:       Install-Application.ps1 [-Verbose] [-Uninstall]
#
# Parameters:  -Verbose: Show verbose output
#              -Uninstall: Uninstall the application
#              -jsonFilePath: Path to the JSON file containing the application settings. Default: Install-OpenKNXproducer.json
#              -versionFile: Path to the version.txt file containing the application version. Default: tools/version.txt
#
# Example:     Install-Application.ps1 -Verbose
#              Install-Application.ps1 -Uninstall
#
####################################################################################################

param(
  [switch]$Verbose = $false,
  [switch]$Uninstall = $false,
  [string]$jsonFilePath = "Install-Application.json",
  [string]$versionFile = "tools/version.txt"
)

# To Show OpenKNX Logo in the console output
function OpenKNX_ShowLogo($AddCustomText = $null, $Line=0 ) {
  if(($Line -band 1) -eq 1 -or ($Line -band 3) -eq 3) { Write-Host ($( [char]::ConvertFromUtf32(0x2500)*($Host.UI.RawUI.WindowSize.Width/3))) -ForegroundColor Green }
  Write-Host ""
  Write-Host "Open " -NoNewline # Open
  Write-Host "$( [char]::ConvertFromUtf32(0x25A0) )" -ForegroundColor Green # ■
  $unicodeString = "$( [char]::ConvertFromUtf32(0x252C) )$($([char]::ConvertFromUtf32(0x2500) )*4)$( [char]::ConvertFromUtf32(0x2534) ) "
  if ($AddCustomText) { Write-Host "$($unicodeString) $($AddCustomText)"  -ForegroundColor Green } # ┬────┴
  else { Write-Host "$($unicodeString)"  -ForegroundColor Green } # ┬────┴
  Write-Host "$( [char]::ConvertFromUtf32(0x25A0) )" -NoNewline -ForegroundColor Green # ■
  Write-Host " KNX   " -NoNewline # KNX
  if(($Line -band 4) -eq 4 ) { Write-Host "https://www.OpenKNX.de" -ForegroundColor Blue } else { Write-Host "" }
  if(($Line -band 2) -eq 2 -or ($Line -band 3) -eq 3) { Write-Host ($( [char]::ConvertFromUtf32(0x2500)*($Host.UI.RawUI.WindowSize.Width/3))) -ForegroundColor Green }
  Write-Host ""
}

function CheckOS {
  # check on which os we are running
  # After check, the Os-Informations are availibe in the PS-Env.
  if ($PSVersionTable.PSVersion.Major -lt 6.0) {
    switch ($([System.Environment]::OSVersion.Platform)) {
      'Win32NT' {
        New-Variable -Option Constant -Name IsWindows -Value $True -ErrorAction SilentlyContinue
        New-Variable -Option Constant -Name IsLinux -Value $false -ErrorAction SilentlyContinue
        New-Variable -Option Constant -Name IsMacOs -Value $false -ErrorAction SilentlyContinue
      }
    }
  }
  $script:IsLinuxEnv = (Get-Variable -Name "IsLinux" -ErrorAction Ignore) -and $IsLinux
  $script:IsMacOSEnv = (Get-Variable -Name "IsMacOS" -ErrorAction Ignore) -and $IsMacOS
  $script:IsWinEnv = !$IsLinuxEnv -and !$IsMacOSEnv

  $CurrentOS = switch($true) {
    $IsLinuxEnv { "Linux" }
    $IsMacOSEnv { "MacOS" }
    $IsWinEnv { "Windows" }
    default { "Unknown" }
  }
  if($IsWinEnv) { 
    $CurrentOS = $CurrentOS + " " + $(if ([System.Environment]::Is64BitOperatingSystem) { 'x64' } else { 'x86' })
  }

  $PSVersion = "$($PSVersionTable.PSVersion.Major).$($PSVersionTable.PSVersion.Minor).$($PSVersionTable.PSVersion.Patch)"
  if($Verbose) { Write-Host -ForegroundColor Green "- We are on '$CurrentOS' Build Environment with PowerShell $PSVersion"  ([Char]0x221A) }
  return $CurrentOS
}

function Invoke-ExecuteCommands($appSettings) {
  if($Verbose) { Write-Host "Invoke-ExecuteCommands" -ForegroundColor Yellow }
  if(![string]::IsNullOrEmpty($appSettings)) {
    if($Verbose) { Write-Host "appSettings: $appSettings" -ForegroundColor Yellow }
    # process here the executeCommand
    (1..10) | ForEach-Object {
        $executeCommand = $appSettings."ExecuteCommand$_"
        if ($executeCommand -and !$Uninstall) {
            Write-Host "- Executing custom command $($_): $executeCommand" -ForegroundColor Gree
            Invoke-Expression $executeCommand
        }
    }
  }
}

function Copy-ApplicationFiles {
  param (
      [string]$jsonFilePath,
      [string]$currentOS = (CheckOS)
  )
  if($Verbose) { Write-Host "Copy-ApplicationFiles" -ForegroundColor Yellow }
  # Read the JSON file
  #check if the file exists
  if(-not (Test-Path $jsonFilePath)) {
    Write-Host "ERROR: The specified JSON file '$jsonFilePath' does not exist." -ForegroundColor Red ($([Char]0x2717))
    exit 1
  } else {
    if($Verbose){ Write-Host "The specified JSON file '$jsonFilePath' exists." -ForegroundColor Yellow ($([Char]0x221A)) }
    $jsonContent = Get-Content -Raw -Path $jsonFilePath | ConvertFrom-Json
  }
  
  # Iterate through each application
  foreach ($application in $jsonContent.Applications) {
    # Get the application name
    $appName = $application.Name

    # If the currentos is one of "Windows x86", "Windows x64 then split the currentos to WIndows and x86 or x64 part, so i can use it for the common settings
    if($currentOS -in @("Windows x86", "Windows x64")) {
      $currentOS_x86x64 = $currentOS.Split(" ")[1]
      $currentOS = $currentOS.Split(" ")[0]
      if($Verbose) { Write-Host "currentOS: $($currentOS) - $($currentOS_x86x64) " -ForegroundColor Yellow }
    }

    # Read a version.txt file if exist, where we could find the version number of the application
    $version = if(Test-Path $versionFile) { $(Get-Content $versionFile) | Select-String -Pattern $appName | Select-Object -ExpandProperty Line } else { $null }
    # Write the application name and the current OS
    # write-host with 20 time of this: $( [char]::ConvertFromUtf32(0x2500) ) Generate it in on line. 

    Write-Host "$(('Removing', 'Installing')[!$Uninstall]): $(if ($version) { "$($version)" } else { $($appName) }) for $($currentOS)" -ForegroundColor Blue
    #Write-Host ($( [char]::ConvertFromUtf32(0x2500)*(($Host.UI.RawUI.WindowSize.Width)/3))) -ForegroundColor Green
    
    # Get the application settings based on the detected OS
    $appSettings = $application.$currentOS

    # If the application settings are not empty, process the files, folders and execute commands
    if (![string]::IsNullOrEmpty($appSettings) -and ![string]::IsNullOrEmpty($application.$currentOS)) {
        # Process common settings
        if ($appSettings.Common) {
          if($Verbose) { Write-Host "Processing - Common: of $currentOS $($currentOS_x86x64)" -ForegroundColor Yellow }
          ProcessFilesAndFolders $appName $appSettings.Common
        }
        # Process general settings
        if($Verbose) { Write-Host "Processing - File and Folders: of $currentOS $($currentOS_x86x64)" -ForegroundColor Yellow }
        ProcessFilesAndFolders $appName $appSettings
      
        # Check and process executeCommand
        if($Verbose) { Write-Host "Processing - ExecuteCommand: of $currentOS $($currentOS_x86x64)" -ForegroundColor Yellow }
        Invoke-ExecuteCommands $appSettings

        # Process the common settings for Windows - x86 and x64
        # Process common settings for Windows - x86 and x64
        if ($currentOS -eq "Windows" -and $currentOS_x86x64 -in @("x86", "x64")) {
          if($Verbose) { Write-Host "Processing - Common: of $currentOS $($currentOS_x86x64)" -ForegroundColor Yellow }
          ProcessFilesAndFolders $appName $appSettings.$currentOS_x86x64.Common

          # Now process OS specific settings for Windows - x86 or x64
          if($Verbose) { Write-Host "Processing - File and Folders: of $currentOS $($currentOS_x86x64)" -ForegroundColor Yellow }
          ProcessFilesAndFolders $appName $appSettings.$currentOS_x86x64

          # Check and process executeCommand
          if($Verbose) { Write-Host "Processing - ExecuteCommand: of $currentOS $($currentOS_x86x64)" -ForegroundColor Yellow }
          Invoke-ExecuteCommands $appSettings
        }
    } else  { 
      if($Verbose) { Write-Host "No specific settings found for OS:$currentOS. Skipping application." -ForegroundColor Yellow }
    }
  }
}

function Resolve-EnvVariablesInJson {
  param (
    [string]$jsonString
  )

  # Find all occurrences of $env:VariableName in the JSON string
  $matches_ = [regex]::Matches($jsonString, '\$env:([^' + ('\\', '/')[!$IsWinEnv] + ']+)')

  If($Verbose) { Write-Host "Resolve-Env: Input '$($jsonString)'" }

  foreach ($match in $matches_) {
    # Extract the variable name from the match
    $envVariableName = $match.Groups[1].Value

    # Get the actual value of the environment variable
    $envVariableValue = [System.Environment]::GetEnvironmentVariable($envVariableName)

    # Replace $env:VariableName in the JSON string with the actual value
    $jsonString = $jsonString.Replace("`$env:$envVariableName", $envVariableValue)
  }

  If($Verbose) { Write-Host "Resolve-Env: Output '$($jsonString)'" }

  return $jsonString
}

function Ensure-ParentDirectoriesExist {
  param (
      [string]$path
  )

  # Split the path to get the parent directories
  $parentDirectories = $path | Split-Path -Parent

  # Determine the directory separator based on the platform
  $splitChar = If ($IsWinEnv) { '\\' } Else { '/' }

  # Split the parent directories using the determined separator
  $directories = $parentDirectories -split $splitChar

  # Initialize the current path
  $currentPath = ''

  # Iterate through each directory and ensure it exists
  foreach ($directory in $directories) {
    $currentPath += $directory + $splitChar
    # Check if the current path exists; if not, create it
    if (-not (Test-Path $currentPath)) {
      New-Item -ItemType Directory -Force -Path $currentPath
    }
  }
}

function RemoveFilesAndFolders {
  param (
    [string]$appName,
    [object]$settings
  )
  # If settings are not defined, skip processing
  if (![string]::IsNullOrEmpty($settings)) {
    # Process each file and folder
    foreach ($fileOrFolderName in $settings.FilesAndFolders.PSObject.Properties.Name) {
      $fileOrFolder = $settings.FilesAndFolders.$fileOrFolderName
      # If CopyEntireFolder is specified, remove the entire folder
      if ($fileOrFolder.CopyEntireFolder) {
        # Check if the destination folder exists
        $fileOrFolder.DestinationPath = Resolve-EnvVariablesInJson $fileOrFolder.DestinationPath
        if (Test-Path -Path $fileOrFolder.DestinationPath) {
          if($Verbose) { Write-Host "Remove-Item -Recurse -Force -Path "$($fileOrFolder.DestinationPath)" -ErrorAction Stop" -ForegroundColor Yellow }
          Remove-Item -Recurse -Force -Path $($fileOrFolder.DestinationPath) -ErrorAction Stop
          # Check if the folder was removed
          if (Test-Path -Path $fileOrFolder.DestinationPath) {
            Write-Host "ERROR: The folder '$($fileOrFolder.DestinationPath)' could not be removed." -ForegroundColor Red
            exit 1
          } else {
            Write-Host "- Removed entire folder '$($fileOrFolder.DestinationPath)' for $appName." -ForegroundColor Green
          }
        }
      } else {
        # Otherwise, remove individual file
        # Check if the destination file exists
        $fileOrFolder.DestinationPath = Resolve-EnvVariablesInJson $fileOrFolder.DestinationPath
        if (Test-Path -Path $fileOrFolder.DestinationPath) {
          if($Verbose) { Write-Host "Remove-Item -Path $($fileOrFolder.DestinationPath) -Force -ErrorAction Stop" -ForegroundColor Yellow }
          Remove-Item -Path $fileOrFolder.DestinationPath -Force -ErrorAction Stop
          # Check if the file was removed
          if (Test-Path -Path $fileOrFolder.DestinationPath) {
            Write-Host "ERROR: The file '$($fileOrFolder.DestinationPath)' could not be removed." -ForegroundColor Red
            exit 1
          } else {
            Write-Host "- Removed file '$($fileOrFolder.DestinationPath)' for $appName." -ForegroundColor Green
          }
        }
      }
    }
  }
}

function ProcessFilesAndFolders {
  param (
    [string]$appName,
    [object]$settings, 
    [switch]$remove = $false
  )
  if($remove -or $Uninstall) {
    RemoveFilesAndFolders $appName $settings
  } else {
    IntallFilesAndFolders $appName $settings
  }
}

function IntallFilesAndFolders {
  param (
    [string]$appName,
    [object]$settings
  )
  # If settings are not defined, skip processing
  if (![string]::IsNullOrEmpty($settings)) {
    # Process each file and folder
    foreach ($fileOrFolderName in $settings.FilesAndFolders.PSObject.Properties.Name) {
      $fileOrFolder = $settings.FilesAndFolders.$fileOrFolderName
      # If CopyEntireFolder is specified, copy the entire folder
      if ($fileOrFolder.CopyEntireFolder) {
        #Check if the source folder exists
        if (!(Test-Path -Path $fileOrFolder.SourcePath)) {
          Write-Host "ERROR: The source folder '$($fileOrFolder.SourcePath)' does not exist." -ForegroundColor Red
          exit 1
        } else {
          $fileOrFolder.DestinationPath = Resolve-EnvVariablesInJson $fileOrFolder.DestinationPath
          if($Verbose) { Write-Host "Copy-Item -Recurse -Force -Path "$($fileOrFolder.SourcePath)" -Destination "$($fileOrFolder.DestinationPath)" -ErrorAction Stop" -ForegroundColor Yellow }
          Ensure-ParentDirectoriesExist $($fileOrFolder.DestinationPath)
          Copy-Item -Recurse -Force -Path $($fileOrFolder.SourcePath) -Destination $($fileOrFolder.DestinationPath) -ErrorAction Stop
          #CHeck if the folder was copied
          if (!(Test-Path -Path $fileOrFolder.DestinationPath)) {
            Write-Host "ERROR: The folder '$($fileOrFolder.DestinationPath)' could not be copied." -ForegroundColor Red
            exit 1
          } else {
            Write-Host "- Copied entire folder '$($fileOrFolder.SourcePath)' to '$($fileOrFolder.DestinationPath)' for $appName." -ForegroundColor Green
          }
        }
      } else {
        # Otherwise, copy individual file
        #check if the source file exists
        if (!(Test-Path -Path $fileOrFolder.SourcePath)) {
          Write-Host "ERROR: The source file '$($fileOrFolder.SourcePath)' does not exist." -ForegroundColor Red
          exit 1
        } else {
          #Now copy the file
          $fileOrFolder.DestinationPath = Resolve-EnvVariablesInJson $fileOrFolder.DestinationPath
          if($Verbose) { Write-Host "Copy-Item -Path $($fileOrFolder.SourcePath) -Destination $($fileOrFolder.DestinationPath) -Force" -ForegroundColor Yellow }
          Ensure-ParentDirectoriesExist $($fileOrFolder.DestinationPath)
          Copy-Item -Path $fileOrFolder.SourcePath -Destination $fileOrFolder.DestinationPath -Force -ErrorAction Stop
          #Check if the file was copied
          if (!(Test-Path -Path $fileOrFolder.DestinationPath)) {
            Write-Host "ERROR: The file '$($fileOrFolder.DestinationPath)' could not be copied." -ForegroundColor Red
            exit 1
          } else {
            Write-Host "- Copied file '$($fileOrFolder.SourcePath)' to '$($fileOrFolder.DestinationPath)' for $appName." -ForegroundColor Green
          }
        }
        
      }
    }
  }
}


# Example usage
#OpenKNX_ShowLogo -AddCustomText "$(('Removing', 'Installing')[!$Uninstall]) OpenKNXproducer Tools" -Line 0
#$jsonFilePath = "Install-OpenKNXproducer.json"
#Copy-ApplicationFiles -jsonFilePath $jsonFilePath
#OpenKNX_ShowLogo -AddCustomText "$(('Removing', 'Installing')[!$Uninstall]) of OpenKNXproducer Tools complete" -Line 4
