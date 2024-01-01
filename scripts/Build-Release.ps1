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

# Check on which OS we are running
CheckOS

# check for working dir
if (Test-Path -Path release) {
    # clean working dir
    Remove-Item -Recurse release\* -Force
} else {
    New-Item -Path release -ItemType Directory | Out-Null
}

# create required directories
New-Item -Path release/tools -ItemType Directory | Out-Null
New-Item -Path release/tools/Windows -ItemType Directory | Out-Null
New-Item -Path release/tools/MacOS -ItemType Directory | Out-Null
New-Item -Path release/tools/Linux -ItemType Directory | Out-Null
New-Item -Path release/tools/bossac -ItemType Directory | Out-Null

# build publish version of OpenKNXproducer
$donetExecute = "dotnet.exe"
if($IsMacOSEnv -or $IsLinuxEnv) {
  $donetExecute = "dotnet"
  #check for dotnet installation and install if not available
  if(!(Test-Path -Path /usr/local/share/dotnet/dotnet)) {
    # Ask user if we should install dotnet
    $installDotnet = Read-Host -Prompt "donet installation not found. Do you want to install dotnet? (y/n)"
    if($installDotnet -eq "y") {
      if($IsMacOSEnv) {
        Write-Host "Install dotnet"
        Start-Process wget -ArgumentList "https://download.visualstudio.microsoft.com/download/pr/2b5a6c4c-5f2b-4f2b-8f1a-2c9f8f4c9f2b/2e4c6d0b0b0d0f2d4f2b0f2d4c0f4b0f/dotnet-sdk-6.0.100-rc.2.21505.57-osx-x64.tar.gz" -NoNewWindow -Wait
        Start-Process sudo -ArgumentList "tar zxf dotnet-sdk-6.0.100-rc.2.21505.57-osx-x64.tar.gz -C /usr/local/share" -NoNewWindow -Wait
        Start-Process rm -ArgumentList "dotnet-sdk-6.0.100-rc.2.21505.57-osx-x64.tar.gz" -NoNewWindow -Wait
      } elseif ($IsLinuxEnv) {
        Write-Host "Install dotnet"
        Start-Process wget -ArgumentList "https://download.visualstudio.microsoft.com/download/pr/2b5a6c4c-5f2b-4f2b-8f1a-2c9f8f4c9f2b/2e4c6d0b0b0d0f2d4f2b0f2d4c0f4b0f/dotnet-sdk-6.0.100-rc.2.21505.57-linux-x64.tar.gz" -NoNewWindow -Wait
        Start-Process sudo -ArgumentList "tar zxf dotnet-sdk-6.0.100-rc.2.21505.57-linux-x64.tar.gz -C /usr/local/share" -NoNewWindow -Wait
        Start-Process rm -ArgumentList "dotnet-sdk-6.0.100-rc.2.21505.57-linux-x64.tar.gz" -NoNewWindow -Wait
      }
    }
  }
}

Write-Host "Build OpenKNXproducer with $donetExecute"

Start-Process $donetExecute -ArgumentList "build OpenKNXproducer.csproj" -NoNewWindow -Wait
Start-Process $donetExecute -ArgumentList "publish OpenKNXproducer.csproj -c Debug -r win-x64   --self-contained true /p:PublishSingleFile=true" -NoNewWindow -Wait
Start-Process $donetExecute -ArgumentList "publish OpenKNXproducer.csproj -c Debug -r win-x86   --self-contained true /p:PublishSingleFile=true" -NoNewWindow -Wait
Start-Process $donetExecute -ArgumentList "publish OpenKNXproducer.csproj -c Debug -r osx-x64   --self-contained true /p:PublishSingleFile=true" -NoNewWindow -Wait
Start-Process $donetExecute -ArgumentList "publish OpenKNXproducer.csproj -c Debug -r linux-x64 --self-contained true /p:PublishSingleFile=true" -NoNewWindow -Wait

# Local build for testing
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
#Copy-Item -Path tools/bossac/Linux -Destination release/tools/bossac/Linux -Recurse
Copy-Item tools/bossac/LICENSE.txt release/tools/bossac/LICENSE.txt


# add necessary scripts
Copy-Item scripts/Readme-Release.txt release/
Copy-Item scripts/Install-OpenKNX-Tools.ps1 release/

$OSCommands = @{
  "Windows" = "release/tools/Windows/OpenKNXproducer-x64.exe --version";
  "MacOS" = "release/tools/MacOS/OpenKNXproducer-osx64 --version";
  "Linux" = "release/tools/Linux/OpenKNXproducer-linux64 --version";
}

# get version command for current OS
$OS = "Windows" 
if($IsMacOSEnv) { 
  $OS = "MacOS" 
} elseif($IsLinuxEnv) {
  $OS = "Linux"
}
$VersionCommand = $OSCommands[$OS]

# make version command executable on MacOS and Linux
if($OS -eq "MacOS" -or $OS -eq "Linux") {
  Start-Process chmod -ArgumentList "+x", $VersionCommand.Split(" ")[0] -NoNewWindow -Wait
}

# get version from OpenKNXproducer
$ReleaseName = Invoke-Expression $VersionCommand

# remove spaces from release name
$ReleaseName = $ReleaseName.Replace(" ", "-") + ".zip"

# create package 
Compress-Archive -Force -Path release/* -DestinationPath "$ReleaseName"
#Remove-Item -Recurse release/*
Move-Item "$ReleaseName" release/

