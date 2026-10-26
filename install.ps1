# Installs MelonLoader, BTD6 Mod Helper and Snappy Coop into the Steam copy of BTD6.
# Usage: irm https://raw.githubusercontent.com/OliverMarcusson/SnappyCoop/main/install.ps1 | iex
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$ProgressPreference = 'SilentlyContinue'

if (Get-Process BloonsTD6 -ErrorAction SilentlyContinue) { throw 'Close BTD6 first, then run this again.' }

# Find the game in any Steam library.
$steam = (Get-ItemProperty 'HKCU:\Software\Valve\Steam').SteamPath
$libs = @($steam) + ((Get-Content "$steam\steamapps\libraryfolders.vdf" -Raw | Select-String '"path"\s+"([^"]+)"' -AllMatches).Matches |
    ForEach-Object { $_.Groups[1].Value -replace '\\\\', '\' })
$game = $libs | ForEach-Object { "$_\steamapps\common\BloonsTD6" } | Where-Object { Test-Path "$_\BloonsTD6.exe" } | Select-Object -First 1
if (-not $game) { throw 'Could not find BTD6 in your Steam libraries.' }
Write-Host "BTD6: $game"

if (-not (Test-Path "$game\MelonLoader\net6")) {
    Write-Host 'Installing MelonLoader 0.7.3...'
    $zip = "$env:TEMP\MelonLoader.x64.zip"
    Invoke-WebRequest 'https://github.com/LavaGang/MelonLoader/releases/download/v0.7.3/MelonLoader.x64.zip' -OutFile $zip -UseBasicParsing
    Expand-Archive $zip $game -Force
    Remove-Item $zip
}

New-Item -ItemType Directory -Force "$game\Mods" | Out-Null
foreach ($mod in @(
    'https://github.com/gurrenm3/BTD-Mod-Helper/releases/latest/download/Btd6ModHelper.dll',
    'https://github.com/OliverMarcusson/SnappyCoop/releases/latest/download/SnappyCoop.dll')) {
    Write-Host "Downloading $(Split-Path $mod -Leaf)..."
    Invoke-WebRequest $mod -OutFile "$game\Mods\$(Split-Path $mod -Leaf)" -UseBasicParsing
}

Write-Host 'Done. Start BTD6 from Steam; the first launch takes a few minutes while MelonLoader sets up.' -ForegroundColor Green
