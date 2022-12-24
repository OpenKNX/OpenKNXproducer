# check for working dir
if (Test-Path -Path release) {
    # clean working dir
    Remove-Item -Recurse release\*
} else {
    New-Item -Path release -ItemType Directory | Out-Null
}

# create required directories
New-Item -Path release/tools -ItemType Directory | Out-Null

# build publish version of OpenKNXproducer
dotnet.exe build OpenKNXproducer.csproj
dotnet.exe publish OpenKNXproducer.csproj -c Debug -r win-x64 --self-contained true /p:PublishSingleFile=true
dotnet.exe publish OpenKNXproducer.csproj -c Debug -r win-x86 --self-contained true /p:PublishSingleFile=true

# we copy publish version also to our bin to ensure same OpenKNXproducer for our delivered products
Copy-Item bin/Debug/net6.0/win-x64/publish/OpenKNXproducer.exe ~/bin/OpenKNXproducer-x64.exe
Copy-Item bin/Debug/net6.0/win-x86/publish/OpenKNXproducer.exe ~/bin/OpenKNXproducer-x86.exe

# copy package content 
Copy-Item ~/bin/OpenKNXproducer-x64.exe release/tools
Copy-Item ~/bin/OpenKNXproducer-x86.exe release/tools
Copy-Item ~/bin/bossac.exe release/tools
Copy-Item scripts/bossac-LICENSE.txt release/tools

# add necessary scripts
Copy-Item scripts/Readme-Release.txt release/
Copy-Item scripts/Install-OpenKNX-Tools.ps1 release/

# create package 
Compress-Archive -Path release/* -DestinationPath OpenKNXproducer.zip
Remove-Item -Recurse release/*
Move-Item OpenKNXproducer.zip release/

