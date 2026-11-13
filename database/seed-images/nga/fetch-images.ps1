<#
.SYNOPSIS
  Downloads the ~1,500 archive sample paintings listed in manifest.csv (National Gallery of Art, CC0) into .\images.

.DESCRIPTION
  The pictures (~450 MB, 1200 px on the long side) are not kept in Git. Run this once after cloning; the next
  "dotnet run" seeds the archive competitions with them. Without the pictures the app still runs - only the
  archive data is skipped.

  Safe to run again: pictures already downloaded are skipped, broken or half-downloaded files are removed and
  fetched again. Uses curl.exe (built into Windows 10/11).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File database\seed-images\nga\fetch-images.ps1
#>
param([int]$Parallel = 6)

$ErrorActionPreference = 'Stop'
$out = Join-Path $PSScriptRoot 'images'
New-Item -ItemType Directory -Force $out | Out-Null

# A complete JPEG starts with FF D8 and ends with FF D9; anything else is removed so it is fetched again
function Test-Jpeg([string]$path) {
    $fs = [IO.File]::OpenRead($path)
    try {
        if ($fs.Length -lt 1024) { return $false }
        $head = New-Object byte[] 2; [void]$fs.Read($head, 0, 2)
        $fs.Seek(-2, 'End') | Out-Null
        $tail = New-Object byte[] 2; [void]$fs.Read($tail, 0, 2)
        return $head[0] -eq 0xFF -and $head[1] -eq 0xD8 -and $tail[0] -eq 0xFF -and $tail[1] -eq 0xD9
    } finally { $fs.Dispose() }
}

function Remove-Broken {
    Get-ChildItem $out -Filter *.jpg | Where-Object { -not (Test-Jpeg $_.FullName) } | ForEach-Object {
        Remove-Item $_.FullName -Force
    }
}

$rows = Import-Csv (Join-Path $PSScriptRoot 'manifest.csv') -Encoding UTF8
Remove-Broken
$todo = @($rows | Where-Object { -not (Test-Path (Join-Path $out "$($_.uuid).jpg")) })
Write-Host "$($rows.Count) paintings in the manifest, $($rows.Count - $todo.Count) already here, $($todo.Count) to download."

if ($todo.Count -gt 0) {
    # curl config file: one url/output pair per picture (forward slashes: a backslash is an escape in curl configs)
    $cfg = Join-Path ([IO.Path]::GetTempPath()) "ifa-nga-$([Guid]::NewGuid().ToString('N')).txt"
    $lines = foreach ($r in $todo) {
        "url = `"$($r.image_url)`""
        "output = `"$((Join-Path $out "$($r.uuid).jpg").Replace('\', '/'))`""
    }
    [IO.File]::WriteAllLines($cfg, [string[]]$lines)
    try {
        & curl.exe --parallel --parallel-max $Parallel --fail --retry 3 --retry-delay 2 --connect-timeout 20 `
            --max-time 120 -sS -A 'IFA-sample-data/1.0' -K $cfg
    } finally {
        Remove-Item $cfg -Force -ErrorAction SilentlyContinue
    }
    Remove-Broken
}

$have = @(Get-ChildItem $out -Filter *.jpg).Count
$missing = $rows.Count - $have
if ($missing -gt 0) {
    Write-Warning "$have of $($rows.Count) pictures downloaded; run the script again to fetch the remaining $missing."
} else {
    Write-Host "Done: all $have pictures are in $out"
}
