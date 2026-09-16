@echo off
setlocal

rem ==============================
rem 默认配置
rem ==============================

set "DefaultPythonRuntime=D:\PreProcessPython38"
set "DefaultSevenZip=D:\Program Files\7-Zip\7z.exe"
set "DefaultSfxModule=D:\Program Files\7-Zip\7z.sfx"


rem ==============================
rem 参数处理
rem 如果用户没有输入，则使用默认值
rem ==============================

if "%~1"=="" (
    set "PythonRuntimeRoot=%DefaultPythonRuntime%"
) else (
    set "PythonRuntimeRoot=%~1"
)

if "%~2"=="" (
    set "SevenZip=%DefaultSevenZip%"
) else (
    set "SevenZip=%~2"
)

if "%~3"=="" (
    set "SfxModule=%DefaultSfxModule%"
) else (
    set "SfxModule=%~3"
)


echo.
echo ==============================
echo Python Runtime:
echo %PythonRuntimeRoot%
echo.
echo 7-Zip:
echo %SevenZip%
echo.
echo SFX Module:
echo %SfxModule%
echo ==============================
echo.


rem ==============================
rem 检查文件
rem ==============================

if not exist "%PythonRuntimeRoot%\python.exe" (
    echo ERROR: Python runtime not found:
    echo %PythonRuntimeRoot%
    pause
    exit /b 2
)

if not exist "%SevenZip%" (
    echo ERROR: 7z.exe not found:
    echo %SevenZip%
    pause
    exit /b 2
)

if not exist "%SfxModule%" (
    echo ERROR: 7z.sfx not found:
    echo %SfxModule%
    pause
    exit /b 2
)


rem ==============================
rem 调用 PowerShell 打包
rem ==============================

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass ^
-File "%~dp0package-release.ps1" ^
-SkipBuild ^
-PythonRuntimeRoot "%PythonRuntimeRoot%" ^
-SevenZip "%SevenZip%" ^
-SfxModule "%SfxModule%"


pause
exit /b %errorlevel%