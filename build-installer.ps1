# Build Installer Script for Queue System
# This script automates the process of building the installer

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Queue System - Installer Builder" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if Inno Setup is installed
$innoSetupPaths = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 5\ISCC.exe"
)

$isccPath = $null
foreach ($path in $innoSetupPaths) {
    if (Test-Path $path) {
        $isccPath = $path
        break
    }
}

if ($null -eq $isccPath) {
    Write-Host "ERROR: Inno Setup is not installed!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please install Inno Setup from: https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Alternatively, you can create a ZIP package instead." -ForegroundColor Yellow
    Write-Host "Would you like to create a ZIP package instead? (Y/N): " -ForegroundColor Cyan -NoNewline
    $response = Read-Host
    
    if ($response -eq 'Y' -or $response -eq 'y') {
        # Create ZIP package
        $publishPath = "bin\Release\net8.0-windows\win-x64\publish"
        $zipPath = "QueueSystem-v1.0.zip"
        
        if (Test-Path $publishPath) {
            Write-Host ""
            Write-Host "Creating ZIP package..." -ForegroundColor Green
            Compress-Archive -Path "$publishPath\*" -DestinationPath $zipPath -Force
            Write-Host "ZIP package created: $zipPath" -ForegroundColor Green
            Write-Host ""
            Write-Host "You can now distribute this ZIP file!" -ForegroundColor Cyan
        } else {
            Write-Host ""
            Write-Host "ERROR: Publish folder not found!" -ForegroundColor Red
            Write-Host "Please run: dotnet publish first" -ForegroundColor Yellow
        }
    }
    exit
}

Write-Host "Found Inno Setup at: $isccPath" -ForegroundColor Green
Write-Host ""

# Check if publish folder exists
$publishPath = "bin\Release\net8.0-windows\win-x64\publish"
if (-not (Test-Path $publishPath)) {
    Write-Host "ERROR: Publish folder not found!" -ForegroundColor Red
    Write-Host "Building the application first..." -ForegroundColor Yellow
    Write-Host ""
    
    dotnet publish "Queue System.csproj" --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=false
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "ERROR: Build failed!" -ForegroundColor Red
        exit
    }
}

# Build the installer
Write-Host "Building installer..." -ForegroundColor Green
Write-Host ""

$issFile = "installer-setup.iss"
& $isccPath $issFile

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "SUCCESS! Installer created!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Installer location: installer-output\QueueSystemSetup.exe" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "You can now distribute this installer file!" -ForegroundColor Cyan
    
    # Open the output folder
    if (Test-Path "installer-output") {
        explorer "installer-output"
    }
} else {
    Write-Host ""
    Write-Host "ERROR: Installer build failed!" -ForegroundColor Red
}

Write-Host ""
Write-Host "Press any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
