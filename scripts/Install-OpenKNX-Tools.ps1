Import-Module BitsTransfer

# check for working dir
if (!(Test-Path -Path ~/bin)) {
    New-Item -Path ~/bin -ItemType Directory | Out-Null
}

Write-Host "Kopiere OpenKNX-Tools..."

Copy-Item tools/* ~/bin/
if (!$?) {
    Write-Host "Kopieren fehlgeschlagen, OpenKNX-Tools sind nicht verfuegbar. Bitte versuchen Sie es erneut."
    timeout /T 20
    Exit 1
}
$version = ~/bin/OpenKNXproducer version

Write-Host "
    Die folgenden OpenKNX-Tools wurden im Verzeichnis ~/bin verfuegbar gemacht:
        bossac          1.7.0 - Firmware-Upload fuer SAMD-Prozessoren
        $version - Erzeugung einer knxprod-Datei fuer die ETS
"

timeout /T 20
