@echo off
setlocal
set "SCRIPT=%~dp0package-release.ps1"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%" %*
if errorlevel 1 (
  echo Packaging failed.
  exit /b 1
)
echo Packaging completed.
