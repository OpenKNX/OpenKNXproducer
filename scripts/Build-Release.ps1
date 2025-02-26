####################################################################################################
#   Open ■
#   ┬────┴  Build Releases
#   ■ KNX   2024 OpenKNX - Erkan Çolak
#
# This script builds the OpenKNXproducer release package for Windows, MacOS and Linux
#
####################################################################################################

# This script builds the OpenKNXproducer release package for Windows, MacOS and Linux

param(
  [switch]$Verbose = $false # To show more information during the build process. Default is $false. Please use -Verbose to enable it.
)
# To Show OpenKNX Logo in the console output
function OpenKNX_ShowLogo($AddCustomText = $null) {
  Write-Host ""
  Write-Host "Open " -NoNewline
  #Write-Host "■" -ForegroundColor Green
  Write-Host "$( [char]::ConvertFromUtf32(0x25A0) )" -ForegroundColor Green
  $unicodeString = "$( [char]::ConvertFromUtf32(0x252C) )$( [char]::ConvertFromUtf32(0x2500) )$( [char]::ConvertFromUtf32(0x2500) )$( [char]::ConvertFromUtf32(0x2500) )$( [char]::ConvertFromUtf32(0x2500) )$( [char]::ConvertFromUtf32(0x2534) ) "

  if ($AddCustomText) { 
      #Write-Host "┬────┴  $AddCustomText" -ForegroundColor Green
      Write-Host "$($unicodeString) $($AddCustomText)"  -ForegroundColor Green
  }
  else {
      #Write-Host "┬────┴" -ForegroundColor Green
      Write-Host "$($unicodeString)"  -ForegroundColor Green
  }

  #Write-Host "■" -NoNewline -ForegroundColor Green
  Write-Host "$( [char]::ConvertFromUtf32(0x25A0) )" -NoNewline -ForegroundColor Green
  Write-Host " KNX"
  Write-Host ""
}

# To check on which OS we are running
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
# To check and install (Optional and for testing) dotnet if not exists
function Test-AndInstallDotnet {
  param (
      [bool]$Install = $false
  )
  $dotnetExecutable = "dotnet.exe"
  if ($IsMacOSEnv -or $IsLinuxEnv) {
    $dotnetExecutable = "dotnet"
    if (!(Test-Path -Path /usr/local/share/dotnet/dotnet)) {
      # Ask user if we should install dotnet
      if($Install) {
        $installDotnet = Read-Host -Prompt "dotnet installation not found. Do you want to install dotnet? (y/n)"
        if ($installDotnet -eq "y") {
          Write-Host "Installing dotnet..."
          $downloadUrl = ""
          $tarCommand = ""
          if ($IsMacOSEnv) {
            $downloadUrl = "https://download.visualstudio.microsoft.com/download/pr/2b5a6c4c-5f2b-4f2b-8f1a-2c9f8f4c9f2b/2e4c6d0b0b0d0f2d4f2b0f2d4c0f4b0f/dotnet-sdk-6.0.100-rc.2.21505.57-osx-x64.tar.gz"
            $tarCommand = "tar zxf dotnet-sdk-6.0.100-rc.2.21505.57-osx-x64.tar.gz -C /usr/local/share"
          }
          elseif ($IsLinuxEnv) {
            $downloadUrl = "https://download.visualstudio.microsoft.com/download/pr/2b5a6c4c-5f2b-4f2b-8f1a-2c9f8f4c9f2b/2e4c6d0b0b0d0f2d4f2b0f2d4c0f4b0f/dotnet-sdk-6.0.100-rc.2.21505.57-linux-x64.tar.gz"
            $tarCommand = "tar zxf dotnet-sdk-6.0.100-rc.2.21505.57-linux-x64.tar.gz -C /usr/local/share"
          }
          Start-Process wget -ArgumentList $downloadUrl -NoNewWindow -Wait
          Start-Process sudo -ArgumentList $tarCommand -NoNewWindow -Wait
          Start-Process rm -ArgumentList "dotnet-sdk-6.0.100-rc.2.21505.57*.tar.gz" -NoNewWindow -Wait
        }
      }
    }
  }
  return $dotnetExecutable
}

function Get-ApplicationVersion {
  param (
    [string]$application = "OpenKNXproducer"
  )

  $OSCommands = @{
    "Windows x86" = @{
      "OpenKNXproducer" = "release/tools/Windows/OpenKNXproducer-x86.exe --version"
      "bossac"          = "release/tools/bossac/Windows/x86/bossac.exe --help"
    }
    "Windows x64" = @{
      "OpenKNXproducer" = "release/tools/Windows/OpenKNXproducer-x64.exe --version"
      "bossac"          = "release/tools/bossac/Windows/x64/bossac.exe --help"
    }
    "MacOS"       = @{
      "OpenKNXproducer" = "release/tools/MacOS/OpenKNXproducer --version"
      "bossac"          = "release/tools/bossac/MacOS/bossac --help"
    }
    "Linux"       = @{
      "OpenKNXproducer" = "release/tools/Linux/OpenKNXproducer --version"
      "bossac"          = "release/tools/bossac/Linux/bossac --help"
    }
  }

  # Determine the current OS
  $OS = CheckOS

  # Get the version command for the current OS and application
  $VersionCommand = $OSCommands[$OS][$application]
  if ([string]::IsNullOrEmpty($VersionCommand)) {
    Write-Host "ERROR: Could not get version command for '$application' on '$OS'" -ForegroundColor Red
    exit 1
  }

  # Make version command executable on MacOS and Linux
  if ($OS -eq "MacOS" -or $OS -eq "Linux") {
    $executable = $VersionCommand.Split(" ")[0]
    if ($Verbose) { Write-Host "Setting executable permissions for '$executable'." -ForegroundColor Yellow }
    Start-Process chmod -ArgumentList "+x", $executable -NoNewWindow -Wait
  }

  # Get version from OpenKNXproducer or bossac
  if ($Verbose) { Write-Host "Get version from '$application' with command '$VersionCommand'" -ForegroundColor Yellow }
  $VersionOutput = Invoke-Expression $VersionCommand
  if ($Verbose) { Write-Host "Version output: $VersionOutput" -ForegroundColor Yellow }
  
  # the patern for OPenKNXproducer is: OpenKNXproducer 'OpenKNXproducer (\d+(\.\d+)*)'
  # the patern for bossac and others is: 'Version (\d+(\.\d+)*)'
  $pattern = switch ($application) {
    "OpenKNXproducer" { 'OpenKNXproducer (\d+(\.\d+)*)' }
    default { 'Version (\d+(\.\d+)*)' }
  }
  if ($Verbose) { Write-Host "Pattern: $pattern" -ForegroundColor Yellow }

  $VersionMatch = ($VersionOutput | Select-String -Pattern $pattern)
  if ($VersionMatch) {
    if ($Verbose) { Write-Host "Version match: $VersionMatch" -ForegroundColor Yellow }
    $Version = $VersionMatch.Matches.Groups[1].Value
    if ($Verbose) { Write-Host "Version: $Version" -ForegroundColor Yellow }
    return $Version
  }
  return $null
}

function Invoke-DotnetExecute {
  param (
    [string]$arguments,
    [string]$message = "Executing dotnet command ...",
    [string]$workingDirectory = $null
  )

  # Get the os specific dotnet command
  $dotnetCommand = Test-AndInstallDotnet -Install $false

  $processStartInfo = New-Object System.Diagnostics.ProcessStartInfo
  $processStartInfo.FileName = $dotnetCommand
  $processStartInfo.Arguments = $arguments
  $processStartInfo.RedirectStandardOutput = $true
  $processStartInfo.RedirectStandardError = $true
  $processStartInfo.UseShellExecute = $false
  $processStartInfo.CreateNoWindow = $true

  if ($workingDirectory) {
    $processStartInfo.WorkingDirectory = $workingDirectory
  }

  Test-Path "..\OpenKNX.Toolbox.Sign\OpenKNX.Toolbox.Sign\OpenKNX.Toolbox.Sign.csproj"
  Test-Path "..\OpenKNX.Toolbox.Lib\OpenKNX.Toolbox.Lib.csproj"
  Write-Host $message  -ForegroundColor Green -NoNewline
  $process = [System.Diagnostics.Process]::Start($processStartInfo)
  # Capture and suppress standard output and standard error
  $output = $process.StandardOutput.ReadToEnd()
  $errorOutput = $process.StandardError.ReadToEnd()

  $process.WaitForExit()
  Write-Host "`t$checkmarkChar Done" -ForegroundColor Green
  if ($Verbose) {
    # Display the captured output for debugging purposes
    Write-Host "Standard Output: $output"
    Write-Host "Standard Error: $errorOutput"
    return $process.ExitCode
  }
}

# Check on which OS we are running
$infoChar = [char]::ConvertFromUtf32(0x2139);
$checkmarkChar = [char]::ConvertFromUtf32(0x2714);
OpenKNX_ShowLogo "Build OpenKNXproducer Release on $(CheckOS)"
CheckOS | Out-Null

if (-not $Verbose) {
  Write-Host "$infoChar Verbose mode is disabled. Use -Verbose to enable it! $infoChar" -ForegroundColor Cyan
}

# check for working dir and create if not exists
Write-Host "- Create release folder structure ..." -ForegroundColor Green -NoNewline
if (Test-Path -Path release) {
  Remove-Item -Recurse release\* -Force
}
else { New-Item -Path release -ItemType Directory | Out-Null }
Write-Host "`t$checkmarkChar Done" -ForegroundColor Green

# Create further required release folders
New-Item -Path release/tools -ItemType Directory | Out-Null
New-Item -Path release/tools/Windows -ItemType Directory | Out-Null
New-Item -Path release/tools/MacOS -ItemType Directory | Out-Null
New-Item -Path release/tools/Linux -ItemType Directory | Out-Null
New-Item -Path release/tools/bossac -ItemType Directory | Out-Null
New-Item -Path release/tools/esptools -ItemType Directory | Out-Null

# Build OpenKNXproducer

dotnet build OpenKNXproducer.csproj
Invoke-DotnetExecute -message "- Building OpenKNXproducer                ..." -arguments "build OpenKNXproducer.csproj"
Invoke-DotnetExecute -message "- Publish OpenKNXproducer for Windows x64 ..." -arguments "publish OpenKNXproducer.csproj -c Debug -r win-x64   --self-contained true /p:PublishSingleFile=true"
Invoke-DotnetExecute -message "- Publish OpenKNXproducer for Windows x86 ..." -arguments "publish OpenKNXproducer.csproj -c Debug -r win-x86   --self-contained true /p:PublishSingleFile=true"
Invoke-DotnetExecute -message "- Publish OpenKNXproducer for MacOS       ..." -arguments "publish OpenKNXproducer.csproj -c Debug -r osx-x64   --self-contained true /p:PublishSingleFile=true"
Invoke-DotnetExecute -message "- Publish OpenKNXproducer for Linux       ..." -arguments "publish OpenKNXproducer.csproj -c Debug -r linux-x64 --self-contained true /p:PublishSingleFile=true"

# Copy publish version to release folder structure
Write-Host "- Copy publish openKNXproducer binaries to release folder structure ..." -ForegroundColor Green -NoNewline
Copy-Item bin/Debug/net8.0/win-x64/publish/OpenKNXproducer.exe   release/tools/Windows/OpenKNXproducer-x64.exe
Copy-Item bin/Debug/net8.0/win-x86/publish/OpenKNXproducer.exe   release/tools/Windows/OpenKNXproducer-x86.exe
Copy-Item bin/Debug/net8.0/osx-x64/publish/OpenKNXproducer       release/tools/MacOS/OpenKNXproducer
Copy-Item bin/Debug/net8.0/linux-x64/publish/OpenKNXproducer     release/tools/Linux/OpenKNXproducer
Write-Host "`t$checkmarkChar Done" -ForegroundColor Green


# Copy bossac tool to the release package into the dedicated folder 
Write-Host "- Copy external bossac tool to release folder structure ..." -ForegroundColor Green -NoNewline
Copy-Item -Path tools/bossac/Windows -Destination release/tools/bossac/Windows -Recurse
Copy-Item -Path tools/bossac/MacOS -Destination release/tools/bossac/MacOS -Recurse
Copy-Item -Path tools/bossac/Linux -Destination release/tools/bossac/Linux -Recurse
Copy-Item tools/bossac/LICENSE.txt release/tools/bossac/LICENSE.txt
Write-Host "`t$checkmarkChar Done" -ForegroundColor Green

# Copy esptools  tool to the release package into the dedicated folder 
Write-Host "- Copy external esp tools to release folder structure ..." -ForegroundColor Green -NoNewline
Copy-Item -Path tools/esptools/Windows -Destination release/tools/esptools/Windows/ -Recurse
Copy-Item tools/esptools/LICENSE release/tools/esptools/LICENSE
Copy-Item tools/esptools/Readme.md release/tools/esptools/Readme.md
Write-Host "`t$checkmarkChar Done" -ForegroundColor Green


# add necessary scripts
Write-Host "- Copying scripts to release folder structure ..." -ForegroundColor Green -NoNewline
Copy-Item scripts/Readme-Release.txt release/                   # This is the readme file for the release
Copy-Item scripts/Install-Application.ps1 release/tools/        # This script is used to install the application on Windows, Linux and macOS.WIll be included in Install-OpenKNX-Tools.ps1 script
Copy-Item scripts/Install-OpenKNXproducer.json  release/tools/  # This is the json file for the Install-OpenKNX-Tools.ps1 script
Copy-Item scripts/Install-OpenKNX-Tools.ps1 release/            # This script is used to install the application on Windows, Linux and macOS
Copy-Item scripts/Remove-OpenKNX-Tools.ps1 release/             # This script is used to uninstall the application on Windows, Linux and macOS
Write-Host "`t$checkmarkChar Done" -ForegroundColor Green


Write-Host "- Checking and getting versions directly from builded executables ..." -ForegroundColor Green -NoNewline
# Get version from OpenKNXproducer and remove spaces from release name
$OpenKNXproducerVersion = (Get-ApplicationVersion)
$bossacVersion = (Get-ApplicationVersion -application "bossac")

if ( [string]::IsNullOrEmpty($OpenKNXproducerVersion) -or [string]::IsNullOrEmpty($bossacVersion) ) {
  Write-Host "ERROR: Could not get version from the executable." -ForegroundColor Red
  exit 1
}
else {
  Write-Host "`t$checkmarkChar Done" -ForegroundColor Green
  Write-Host "`tOpenKNXproducer version: $OpenKNXproducerVersion" -ForegroundColor Green
  Write-Host "`tbossac version: $bossacVersion" -ForegroundColor Green
  
  #Write the application version strings into the file version.txt. Remove version.txt if exists and create a new one.
  if (Test-Path -Path release/tools/version.txt) { Remove-Item -Path release/tools/version.txt -Force }
  New-Item -Path release/tools/version.txt -ItemType File | Out-Null
  Add-Content -Path release/tools/version.txt -Value "OpenKNXproducer $($OpenKNXproducerVersion)"
  Add-Content -Path release/tools/version.txt -Value "bossac $($bossacVersion)"
}

# create package 
$ReleaseName = "OpenKNXproducer-$($OpenKNXproducerVersion).zip"
Write-Host "- Create release package: $ReleaseName ..." -ForegroundColor Green -NoNewline
Compress-Archive -Force -Path release/* -DestinationPath "$ReleaseName"
Write-Host "`t$checkmarkChar Done" -ForegroundColor Green

# Move package to release folder
Write-Host "- Move release package to release folder ..." -ForegroundColor Green -NoNewline
Move-Item "$ReleaseName" release/
Write-Host "`t$checkmarkChar Done" -ForegroundColor Green

# Clean working dir
#Remove-Item -Recurse -Force release/*

OpenKNX_ShowLogo "Finished Build OpenKNXproducer Release"