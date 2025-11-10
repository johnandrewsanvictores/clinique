@echo off
echo ========================================
echo Queue System - Installer Builder
echo ========================================
echo.
echo This will create an installer for Queue System
echo.
pause

PowerShell -NoProfile -ExecutionPolicy Bypass -File "build-installer.ps1"

pause
