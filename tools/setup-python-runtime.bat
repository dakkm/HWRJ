@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup-python-runtime.ps1" %*
if errorlevel 1 exit /b 1
pause
