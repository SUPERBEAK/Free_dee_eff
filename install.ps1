# Freedeeeff Windows Installer Script
param(
    [string]$InstallPath = "$env:LOCALAPPDATA\Programs\Freedeeeff",
    [switch]$NoDesktopShortcut
)

$ErrorActionPreference = "Stop"
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Installing Freedeeeff on Windows     " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $scriptDir) { $scriptDir = (Get-Location).Path }
Set-Location $scriptDir

# 1. Terminate any running instances of Freedeeeff
Write-Host "[1/5] Checking for running instances..." -ForegroundColor Yellow
Get-Process -Name "Freedeeeff" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

# 2. Publish Release build
Write-Host "[2/5] Publishing Release build..." -ForegroundColor Yellow
if (Test-Path $InstallPath) {
    Get-ChildItem -Path $InstallPath -Exclude "UserData","Signatures" | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
} else {
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
}

$publishArgs = @(
    "publish",
    "$scriptDir\Freedeeeff.csproj",
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "false",
    "-o", $InstallPath
)
& dotnet $publishArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

# 3. Create Shortcuts using WScript.Shell
Write-Host "[3/5] Creating Windows Shortcuts..." -ForegroundColor Yellow
$exePath = Join-Path $InstallPath "Freedeeeff.exe"
$wshShell = New-Object -ComObject WScript.Shell

# Start Menu Shortcut
$startMenuPath = [System.IO.Path]::Combine($env:APPDATA, "Microsoft\Windows\Start Menu\Programs", "Freedeeeff.lnk")
$startShortcut = $wshShell.CreateShortcut($startMenuPath)
$startShortcut.TargetPath = $exePath
$startShortcut.WorkingDirectory = $InstallPath
$startShortcut.IconLocation = "$exePath,0"
$startShortcut.Description = "Freedeeeff - Modern PDF Editor and Reader"
$startShortcut.Save()
Write-Host "   [OK] Start Menu shortcut created: $startMenuPath" -ForegroundColor Green

# Desktop Shortcut
$desktopPath = [System.IO.Path]::Combine([Environment]::GetFolderPath('Desktop'), "Freedeeeff.lnk")
if (-not $NoDesktopShortcut) {
    $desktopShortcut = $wshShell.CreateShortcut($desktopPath)
    $desktopShortcut.TargetPath = $exePath
    $desktopShortcut.WorkingDirectory = $InstallPath
    $desktopShortcut.IconLocation = "$exePath,0"
    $desktopShortcut.Description = "Freedeeeff - Modern PDF Editor and Reader"
    $desktopShortcut.Save()
    Write-Host "   [OK] Desktop shortcut created: $desktopPath" -ForegroundColor Green
}

# 4. Register in Windows Add/Remove Programs (Registry HKCU)
Write-Host "[4/5] Registering with Windows Add/Remove Programs..." -ForegroundColor Yellow
$regKeyPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Freedeeeff"
if (-not (Test-Path $regKeyPath)) {
    New-Item -Path $regKeyPath -Force | Out-Null
}

$uninstallCmd = 'powershell.exe -ExecutionPolicy Bypass -Command "Remove-Item -Recurse -Force \"' + $InstallPath + '\"; Remove-Item -Force \"' + $startMenuPath + '\"; if (Test-Path \"' + $desktopPath + '\") { Remove-Item -Force \"' + $desktopPath + '\" }; Remove-Item -Recurse -Force \"' + $regKeyPath + '\""'

Set-ItemProperty -Path $regKeyPath -Name "DisplayName" -Value "Freedeeeff PDF Editor"
Set-ItemProperty -Path $regKeyPath -Name "DisplayVersion" -Value "1.0.0"
Set-ItemProperty -Path $regKeyPath -Name "Publisher" -Value "SUPERBEAK"
Set-ItemProperty -Path $regKeyPath -Name "DisplayIcon" -Value "$exePath,0"
Set-ItemProperty -Path $regKeyPath -Name "InstallLocation" -Value $InstallPath
Set-ItemProperty -Path $regKeyPath -Name "UninstallString" -Value $uninstallCmd
Write-Host "   [OK] Registered in Windows Add/Remove Programs" -ForegroundColor Green

# 5. Update dist folder
Write-Host "[5/5] Updating dist package..." -ForegroundColor Yellow
$distDir = Join-Path $scriptDir "dist\Freedeeeff-win-x64"
if (Test-Path $distDir) {
    Copy-Item -Path "$InstallPath\*" -Destination $distDir -Recurse -Force -ErrorAction SilentlyContinue
    $zipPath = Join-Path $scriptDir "dist\Freedeeeff-v1.0.0-win-x64.zip"
    if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
    Compress-Archive -Path "$distDir\*" -DestinationPath $zipPath -Force
    Write-Host "   [OK] Updated $zipPath" -ForegroundColor Green
}

Write-Host ""
Write-Host "Freedeeeff has been successfully installed to your PC!" -ForegroundColor Green
Write-Host "   Location: $InstallPath" -ForegroundColor Cyan
Write-Host "   Executable: $exePath" -ForegroundColor Cyan
Write-Host "You can now launch Freedeeeff from your Start Menu, Desktop, or Windows Search." -ForegroundColor White
