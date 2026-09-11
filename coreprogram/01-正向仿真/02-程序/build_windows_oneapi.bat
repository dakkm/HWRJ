@echo off
setlocal
cd /d "%~dp0"

echo ============================================================
echo Building production_main_output_interface.exe with Intel oneAPI IFX
echo ============================================================

where ifx >nul 2>nul
if errorlevel 1 (
    echo ifx was not found in the current environment.
    if exist "C:\Program Files (x86)\Intel\oneAPI\setvars.bat" (
        echo Loading Intel oneAPI environment...
        call "C:\Program Files (x86)\Intel\oneAPI\setvars.bat" intel64
    ) else (
        echo ERROR: Intel oneAPI setvars.bat was not found.
        echo Please run this script from an Intel oneAPI command prompt.
        pause
        exit /b 1
    )
)

echo.
echo Compiler:
ifx --version

echo.
echo Compiling...
ifx /O2 /free /Fe:production_main_output_interface.exe production_main_output_interface.f90
if errorlevel 1 (
    echo.
    echo BUILD FAILED
    pause
    exit /b 1
)

echo.
echo BUILD SUCCESS
echo Output: %CD%\production_main_output_interface.exe
if exist production_main_output_interface.obj del /q production_main_output_interface.obj
pause
endlocal
