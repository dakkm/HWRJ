@echo off
setlocal
if "%~1"=="" (
  echo Usage: %~nx0 ^<Win7-compatible Python runtime directory^>
  echo Example: %~nx0 D:\PreProcessPython38
  exit /b 2
)
if not exist "%~1\python.exe" (
  echo ERROR: runtime directory must contain python.exe
  exit /b 2
)
if "%~2"=="" (
  echo Usage: %~nx0 ^<PythonRuntimeRoot^> ^<7z.exe^> ^<7zS.sfx^>
  exit /b 2
)
if "%~3"=="" (
  echo Usage: %~nx0 ^<PythonRuntimeRoot^> ^<7z.exe^> ^<7zS.sfx^>
  exit /b 2
)
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0package-release.ps1" -SkipBuild -PythonRuntimeRoot "%~1" -SevenZip "%~2" -SfxModule "%~3"
exit /b %errorlevel%
