####################################################################################################
# Description: Scans XML files in a directory for Text/SuffixText/FunctionText attributes
#              that are missing an op:Translation child for the specified language.
#              Skips debug.xml files, fully-placeholder values (%FOO%), template tokens,
#              and values that are language-neutral (unit suffixes etc.).
#
# Usage:       FindMissingTranslations.ps1 [-Path <dir>] [-Language <lang>] [-Limit <n>]
#
# Parameters:  -Path:     Directory containing XML files to scan. Default: current directory.
#              -Language: Target translation language identifier. Default: en-US.
#              -Limit:    Max entries to show per file before truncating. Default: 60.
#
# Example:     FindMissingTranslations.ps1
#              FindMissingTranslations.ps1 -Path src -Language de-DE
#
####################################################################################################

param(
    [string]$Path     = ".",
    [string]$Language = "en-US",
    [int]$Limit       = 60
)

$pythonScript = @"
import xml.etree.ElementTree as ET
from pathlib import Path
import re
import sys

scan_dir = sys.argv[1]
language = sys.argv[2]
limit    = int(sys.argv[3])

OP_NS = 'http://github.com/OpenKNX/OpenKNXproducer'

attrs = ['Text', 'SuffixText', 'FunctionText']
same_tokens = {
    '', 's', 'min', 'h',
    'O1', 'O2', 'O3', 'O4',
    'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'T',
    'A/B', 'C/D', 'E/F', 'G/H',
    'x', 'z', 'd',
}
placeholder_full = re.compile(r'^%[^%]+%$')

files = sorted(Path(scan_dir).glob('*.xml'))
any_missing = False

for path in files:
    if path.name.lower().endswith('debug.xml'):
        continue
    try:
        tree = ET.parse(path)
    except ET.ParseError as e:
        print(f"{path.name}: PARSE ERROR: {e}")
        continue
    root = tree.getroot()
    missing = []
    for elem in root.iter():
        if not isinstance(elem.tag, str):
            continue
        if elem.tag.startswith('{%s}' % OP_NS):
            continue
        for attr in attrs:
            val = elem.attrib.get(attr)
            if val is None:
                continue
            v = val.strip()
            if v in same_tokens:
                continue
            if 'trans(' in v:
                continue
            if '{{' in v or '}}' in v:
                continue
            if placeholder_full.match(v):
                continue
            has_translation = False
            for child in elem:
                if child.tag == '{%s}Translation' % OP_NS:
                    if child.attrib.get('Language') != language:
                        continue
                    if attr in child.attrib:
                        has_translation = True
                        break
            if not has_translation:
                missing.append((attr, val, elem.tag.split('}')[-1], elem.attrib.get('Id', ''), elem.attrib.get('Name', '')))
    if missing:
        any_missing = True
        print(f"{path.name}: {len(missing)} missing")
        for attr, val, tag, eid, name in missing[:limit]:
            print(f"  [{attr}] {val!r}  <{tag}>  Id={eid}  Name={name}")
        if len(missing) > limit:
            print(f"  ... and {len(missing) - limit} more")
    else:
        print(f"{path.name}: complete")

sys.exit(1 if any_missing else 0)
"@

$resolvedPath = Resolve-Path $Path -ErrorAction Stop

$tmpFile = [System.IO.Path]::GetTempFileName() + ".py"
[System.IO.File]::WriteAllText($tmpFile, $pythonScript, [System.Text.UTF8Encoding]::new($false))

try {
    & python $tmpFile $resolvedPath $Language $Limit
    $exitCode = $LASTEXITCODE
} finally {
    Remove-Item $tmpFile -ErrorAction SilentlyContinue
}

exit $exitCode
