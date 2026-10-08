#!/usr/bin/env pwsh
# Appends every public symbol the analyzer reports as untracked (RS0016) to the owning
# project's PublicAPI.Unshipped.txt. Development convenience only: the file it maintains is
# reviewed like any other source file, and an addition that should not have been made is a
# review finding, not a formality.
param([string]$Solution = 'SensitiveDataGateway.slnx')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

# The diagnostic is parsed, so it is read in a known language: a localized message carries
# apostrophes of its own, and the symbol is quoted.
$env:DOTNET_CLI_UI_LANGUAGE = 'en'

$log = dotnet build (Join-Path $root $Solution) -c Release 2>&1 | Out-String
$flat = ($log -replace "`r`n", ' ' -replace "`n", ' ' -replace '\s+', ' ')

# Note on the pattern: a symbol can itself contain apostrophes, as a char constant does
# (`Open = '[' -> char`), so the closing quote is the one the message tail follows.
$byProject = @{}
foreach ($match in [regex]::Matches($flat, "RS0016: Symbol '(.+?)' is not part of the declared public API[^\[]*\[([^\]]+)\]")) {
    $symbol = $match.Groups[1].Value
    $project = ($match.Groups[2].Value -split '::')[0]
    $file = Join-Path (Split-Path -Parent $project) 'PublicAPI.Unshipped.txt'
    if (-not $byProject.ContainsKey($file)) {
        $byProject[$file] = New-Object System.Collections.Generic.HashSet[string]
    }
    [void]$byProject[$file].Add($symbol)
}

if ($byProject.Count -eq 0) {
    Write-Host 'No untracked public API. Nothing to do.'
    exit 0
}

foreach ($file in $byProject.Keys) {
    $existing = if (Test-Path $file) { [System.IO.File]::ReadAllText($file) -split "`n" } else { @('#nullable enable') }
    $header = @($existing | Where-Object { $_ -like '#*' })
    $entries = New-Object System.Collections.Generic.HashSet[string]
    foreach ($line in $existing) {
        if ($line -and $line -notlike '#*') { [void]$entries.Add($line.Trim()) }
    }
    foreach ($symbol in $byProject[$file]) { [void]$entries.Add($symbol) }

    $ordered = $header + (@($entries) | Sort-Object)
    [System.IO.File]::WriteAllText($file, (($ordered -join "`n") + "`n"))
    Write-Host "Updated $file with $($byProject[$file].Count) symbol(s)."
}
