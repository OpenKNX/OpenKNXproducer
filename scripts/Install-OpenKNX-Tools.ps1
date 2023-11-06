Import-Module BitsTransfer

# check for working dir
if (!(Test-Path -Path ~/bin)) {
    New-Item -Path ~/bin -ItemType Directory | Out-Null
}

Write-Host "Kopiere OpenKNX-Tools..."

$os = "??-Bit"
$setExecutable = 0

Copy-Item tools/bossac* ~/bin/
if ($?) {
    if ($Env:OS -eq "Windows_NT") {
        if ([Environment]::Is64BitOperatingSystem)
        {
            $os="Windows 64-Bit"
            Copy-Item tools/OpenKNXproducer-x64.exe ~/bin/OpenKNXproducer.exe
        }
        else
        {
            $os="Windows 32-Bit"
            Copy-Item tools/OpenKNXproducer-x86.exe ~/bin/OpenKNXproducer.exe
        }
    } elseif ($IsMacOS) {
        $os = "Mac OS"
        $setExecutable = 1
        Copy-Item tools/OpenKNXproducer-osx64.exe ~/bin/OpenKNXproducer
    } elseif ($IsLinux) {
        $os = "Linux"
        $setExecutable = 1
        Copy-Item tools/OpenKNXproducer-linux64.exe ~/bin/OpenKNXproducer
    }
}
if (!$?) {
    Write-Host "Kopieren fehlgeschlagen, OpenKNX-Tools sind nicht verfuegbar. Bitte versuchen Sie es erneut."
    timeout /T 20
    Exit 1
}
$version = ~/bin/OpenKNXproducer version

Write-Host "
    Die folgenden OpenKNX-Tools ($os) wurden im Verzeichnis ~/bin verfuegbar gemacht:
        bossac          1.7.0 - Firmware-Upload fuer SAMD-Prozessoren
        $version - Erzeugung einer knxprod-Datei fuer die ETS
"
if ($setExecutable) {
    Write-Host "ACHTUNG: Die Datei ~/bin/OpenKNXproducer muss not mit chmod +x ausführbar gemacht werden. Dies muss über Kommandozeile geschehen, solange wir keine andere Lösung hierfür gefunen haben."
}

timeout /T 20
