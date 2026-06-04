#Requires -Version 5.1
<#
.SYNOPSIS
    Builds the SlideProRelay Windows installer.

.PARAMETER Version
    Version number to embed (e.g. 1.0.0). Defaults to 1.0.0.

.EXAMPLE
    .\installer\build.ps1
    .\installer\build.ps1 -Version 1.2.0
#>
param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$Root     = Split-Path $PSScriptRoot -Parent
$Publish  = "$Root\publish\windows"
$IssFile  = "$PSScriptRoot\SlideProRelay.iss"

# ── Find Inno Setup ───────────────────────────────────────────────────────────

$InnoSetup = @(
    "${env:ProgramFiles(x86)}\Inno Setup 7\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 7\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $InnoSetup) {
    Write-Host ""
    Write-Host "  Inno Setup not found." -ForegroundColor Red
    Write-Host "  Download and install it from: https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

# ── Publish self-contained EXE ────────────────────────────────────────────────

Write-Host ""
Write-Host "  Building SlideProRelay $Version..." -ForegroundColor Cyan

dotnet publish "$Root\src\SlideProRelay.Tray\SlideProRelay.Tray.csproj" `
    -r win-x64 `
    -c Release `
    -p:PublishSingleFile=true `
    -p:Version=$Version `
    --self-contained true `
    -o $Publish `
    --nologo -v quiet

if ($LASTEXITCODE -ne 0) {
    Write-Host "  dotnet publish failed." -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "  Build complete." -ForegroundColor Green

# ── Compile installer ─────────────────────────────────────────────────────────

Write-Host "  Creating installer..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force "$PSScriptRoot\output" | Out-Null

& $InnoSetup /DAppVersion=$Version /Q $IssFile

if ($LASTEXITCODE -ne 0) {
    Write-Host "  Inno Setup compilation failed." -ForegroundColor Red
    exit $LASTEXITCODE
}

$Installer = "$PSScriptRoot\output\SlideProRelay-$Version-Setup.exe"
Write-Host ""
Write-Host "  Done! Installer ready:" -ForegroundColor Green
Write-Host "  $Installer" -ForegroundColor White
Write-Host ""
