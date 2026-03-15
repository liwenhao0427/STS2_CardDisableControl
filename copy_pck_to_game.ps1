$ErrorActionPreference = "Stop"

$modName = "CardDisableControl"
$targetDir = "E:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\$modName"

$sourcePckPath = Join-Path $PSScriptRoot "$modName.pck"
$sourceDllPath = Join-Path $PSScriptRoot "$modName.dll"

if (-not (Test-Path -LiteralPath $sourcePckPath)) {
    throw "Source pck not found: $sourcePckPath. Export pck first."
}

if (-not (Test-Path -LiteralPath $sourceDllPath)) {
    throw "Source dll not found: $sourceDllPath. Build project first to generate dll in project root."
}

if (-not (Test-Path -LiteralPath $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
}

Copy-Item -LiteralPath $sourcePckPath -Destination (Join-Path $targetDir "$modName.pck") -Force
Copy-Item -LiteralPath $sourceDllPath -Destination (Join-Path $targetDir "$modName.dll") -Force

Write-Host "Copied pck: $sourcePckPath -> $(Join-Path $targetDir "$modName.pck")"
Write-Host "Copied dll: $sourceDllPath -> $(Join-Path $targetDir "$modName.dll")"
