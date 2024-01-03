
# This script builds the OpenKNXproducer release package for Windows, MacOS and Linux

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
  if($true) { Write-Host -ForegroundColor Green "- We are on '$CurrentOS' Build Environment with PowerShell $PSVersion"  ([Char]0x221A) }
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
        $installDotnet = Read-Host -Prompt "donet installation not found. Do you want to install dotnet? (y/n)"
        if($installDotnet -eq "y") {
          Write-Host "Installing dotnet..."
          $downloadUrl = ""
          $tarCommand = ""
          if ($IsMacOSEnv) {
            $downloadUrl = "https://download.visualstudio.microsoft.com/download/pr/2b5a6c4c-5f2b-4f2b-8f1a-2c9f8f4c9f2b/2e4c6d0b0b0d0f2d4f2b0f2d4c0f4b0f/dotnet-sdk-6.0.100-rc.2.21505.57-osx-x64.tar.gz"
            $tarCommand = "tar zxf dotnet-sdk-6.0.100-rc.2.21505.57-osx-x64.tar.gz -C /usr/local/share"
          } elseif ($IsLinuxEnv) {
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

# To get the OpenKNXproducer version from the executable
function Get-OpenKNXProducerVersion {
  $OSCommands = @{
      "Windows" = "release/tools/Windows/OpenKNXproducer-x64.exe --version"
      "MacOS" = "release/tools/MacOS/OpenKNXproducer-osx64 --version"
      "Linux" = "release/tools/Linux/OpenKNXproducer-linux64 --version"
  }

  # Determine the current OS
  $OS = "Windows"
  if ($IsMacOSEnv) {
      $OS = "MacOS"
  } elseif ($IsLinuxEnv) {
      $OS = "Linux"
  }

  # Get the version command for the current OS
  $VersionCommand = $OSCommands[$OS]

  # Make version command executable on MacOS and Linux
  if ($OS -eq "MacOS" -or $OS -eq "Linux") {
      Start-Process chmod -ArgumentList "+x", $VersionCommand.Split(" ")[0] -NoNewWindow -Wait
  }

  # Get version from OpenKNXproducer
  $ReleaseName = Invoke-Expression $VersionCommand
  return $ReleaseName
}

# Check on which OS we are running
OpenKNX_ShowLogo "Build OpenKNXproducer Release"
CheckOS

# Check and get dotnet executable
$donetExecute = Test-AndInstallDotnet -Install $false

# check for working dir and create if not exists
if (Test-Path -Path release) {  Remove-Item -Recurse release\* -Force
} else { New-Item -Path release -ItemType Directory | Out-Null }

# Create further required release folders
New-Item -Path release/tools -ItemType Directory | Out-Null
New-Item -Path release/tools/Windows -ItemType Directory | Out-Null
New-Item -Path release/tools/MacOS -ItemType Directory | Out-Null
New-Item -Path release/tools/Linux -ItemType Directory | Out-Null
New-Item -Path release/tools/bossac -ItemType Directory | Out-Null

# Build OpenKNXproducer
Write-Host "Build OpenKNXproducer with $donetExecute"
Start-Process $donetExecute -ArgumentList "build OpenKNXproducer.csproj" -NoNewWindow -Wait
Start-Process $donetExecute -ArgumentList "publish OpenKNXproducer.csproj -c Debug -r win-x64   --self-contained true /p:PublishSingleFile=true" -NoNewWindow -Wait
Start-Process $donetExecute -ArgumentList "publish OpenKNXproducer.csproj -c Debug -r win-x86   --self-contained true /p:PublishSingleFile=true" -NoNewWindow -Wait
Start-Process $donetExecute -ArgumentList "publish OpenKNXproducer.csproj -c Debug -r osx-x64   --self-contained true /p:PublishSingleFile=true" -NoNewWindow -Wait
Start-Process $donetExecute -ArgumentList "publish OpenKNXproducer.csproj -c Debug -r linux-x64 --self-contained true /p:PublishSingleFile=true" -NoNewWindow -Wait

# Local build for testing (To be discussed if and why we need this)
if($false) {
  # we copy publish version also to our bin to ensure same OpenKNXproducer for our delivered products
  Copy-Item bin/Debug/net6.0/win-x64/publish/OpenKNXproducer.exe   ~/bin/OpenKNXproducer-x64.exe
  Copy-Item bin/Debug/net6.0/win-x86/publish/OpenKNXproducer.exe   ~/bin/OpenKNXproducer-x86.exe
  Copy-Item bin/Debug/net6.0/osx-x64/publish/OpenKNXproducer   ~/bin/OpenKNXproducer-osx64.exe
  Copy-Item bin/Debug/net6.0/linux-x64/publish/OpenKNXproducer ~/bin/OpenKNXproducer-linux64.exe

  # copy package content 
  Copy-Item ~/bin/OpenKNXproducer-x64.exe     release/tools/Windows
  Copy-Item ~/bin/OpenKNXproducer-x86.exe     release/tools/Windows
  Copy-Item ~/bin/OpenKNXproducer-osx64.exe   release/tools/MacOS
  Copy-Item ~/bin/OpenKNXproducer-linux64.exe release/tools/Linux
}

# we copy publish version also to our bin to ensure same OpenKNXproducer for our delivered products
Copy-Item bin/Debug/net6.0/win-x64/publish/OpenKNXproducer.exe   release/tools/Windows/OpenKNXproducer-x64.exe
Copy-Item bin/Debug/net6.0/win-x86/publish/OpenKNXproducer.exe   release/tools/Windows/OpenKNXproducer-x86.exe
Copy-Item bin/Debug/net6.0/osx-x64/publish/OpenKNXproducer       release/tools/MacOS/OpenKNXproducer-osx64
Copy-Item bin/Debug/net6.0/linux-x64/publish/OpenKNXproducer     release/tools/Linux/OpenKNXproducer-linux64

# Copy bossac tool to the release package into the dedicated folder 
Copy-Item -Path tools/bossac/Windows -Destination release/tools/bossac/Windows -Recurse
Copy-Item -Path tools/bossac/MacOS -Destination release/tools/bossac/MacOS -Recurse
Copy-Item -Path tools/bossac/Linux -Destination release/tools/bossac/Linux -Recurse
Copy-Item tools/bossac/LICENSE.txt release/tools/bossac/LICENSE.txt

# add necessary scripts
Copy-Item scripts/Readme-Release.txt release/
Copy-Item scripts/Install-OpenKNX-Tools.ps1 release/

# Get version from OpenKNXproducer and remove spaces from release name
$ReleaseName = (Get-OpenKNXProducerVersion).Replace(" ", "-") + ".zip"

# create package 
Compress-Archive -Force -Path release/* -DestinationPath "$ReleaseName"

# Move package to release folder
Move-Item "$ReleaseName" release/

# Clean working dir
#Remove-Item -Recurse -Force release/*

OpenKNX_ShowLogo "Finished Build OpenKNXproducer Release"