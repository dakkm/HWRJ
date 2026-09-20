@echo off
REM 执行当前批处理步骤，并保留命令返回状态。
setlocal
cd /d "%~dp0"

echo ============================================================
REM 执行当前批处理步骤，并保留命令返回状态。
echo Building production_main_output_interface.exe with Intel oneAPI IFX
echo ============================================================

where ifx >nul 2>nul
REM 检查命令执行条件，并在不满足要求时停止或切换路径。
if errorlevel 1 (
    echo ifx was not found in the current environment.
    if exist "C:\Program Files (x86)\Intel\oneAPI\setvars.bat" (
        REM 执行当前批处理步骤，并保留命令返回状态。
        echo Loading Intel oneAPI environment...
        call "C:\Program Files (x86)\Intel\oneAPI\setvars.bat" intel64
    ) else (
        REM 执行当前批处理步骤，并保留命令返回状态。
        echo ERROR: Intel oneAPI setvars.bat was not found.
        echo Please run this script from an Intel oneAPI command prompt.
        pause
        REM 返回当前脚本执行结果，供调用方判断是否成功。
        exit /b 1
    )
)

echo.
REM 执行当前批处理步骤，并保留命令返回状态。
echo Compiler:
ifx --version

echo.
REM 执行当前批处理步骤，并保留命令返回状态。
echo Compiling...
ifx /O2 /free /Fe:production_main_output_interface.exe production_main_output_interface.f90
if errorlevel 1 (
    REM 执行当前批处理步骤，并保留命令返回状态。
    echo.
    echo BUILD FAILED
    pause
    REM 返回当前脚本执行结果，供调用方判断是否成功。
    exit /b 1
)

echo.
REM 执行当前批处理步骤，并保留命令返回状态。
echo BUILD SUCCESS
echo Output: %CD%\production_main_output_interface.exe
if exist production_main_output_interface.obj del /q production_main_output_interface.obj
REM 执行当前批处理步骤，并保留命令返回状态。
pause
endlocal
