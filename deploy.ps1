<#
.SYNOPSIS
    Build and deploy AutoEquipBest mod to Bannerlord Modules folder.
.DESCRIPTION
    Builds the project and copies all module files to the game's Modules directory.
.PARAMETER GameFolder
    Path to your Bannerlord installation. Defaults to Steam's default location.
.PARAMETER Configuration
    Build configuration. Default: Release.
#>
param(
    [string]$GameFolder = "C:\Games\steamapps\common\Mount & Blade II Bannerlord",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$ModuleName = "AutoEquipBest"
$TargetModuleDir = Join-Path $GameFolder "Modules\$ModuleName"

Write-Host "=== Building $ModuleName ===" -ForegroundColor Cyan

# Build the project
dotnet build "src\AutoEquipBest\AutoEquipBest.csproj" `
    -c $Configuration `
    -p:GameFolder="$GameFolder"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed!"
    exit 1
}

Write-Host "=== Deploying to $TargetModuleDir ===" -ForegroundColor Cyan

# Create target directories
$binDir = Join-Path $TargetModuleDir "bin\Win64_Shipping_Client"
$guiDir = Join-Path $TargetModuleDir "GUI\Prefabs"

New-Item -ItemType Directory -Path $binDir -Force | Out-Null
New-Item -ItemType Directory -Path $guiDir -Force | Out-Null

# Copy SubModule.xml
Copy-Item "Module\SubModule.xml" -Destination $TargetModuleDir -Force

# Copy GUI files
Copy-Item "Module\GUI\Prefabs\*" -Destination $guiDir -Force -Recurse

# Copy built DLL (already handled by post-build, but ensure it's there)
$builtDll = "src\AutoEquipBest\bin\$Configuration\net472\AutoEquipBest.dll"
if (Test-Path $builtDll) {
    Copy-Item $builtDll -Destination $binDir -Force
}

# Copy Harmony DLL if needed
$harmonyDll = "src\AutoEquipBest\bin\$Configuration\net472\0Harmony.dll"
if (Test-Path $harmonyDll) {
    Copy-Item $harmonyDll -Destination $binDir -Force
}

Write-Host "=== Done! ===" -ForegroundColor Green
Write-Host "Module deployed to: $TargetModuleDir"
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Launch Bannerlord"
Write-Host "  2. Go to Mods and enable 'Auto Equip Best'"
Write-Host "  3. Load a save game"
Write-Host "  4. Open your inventory"
Write-Host "  5. Click the 'Auto Equip Best' button or press Ctrl+E"
