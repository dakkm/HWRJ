@echo off
setlocal
cd /d "%~dp0"
python postprocess_trajectory.py --input "..\03-输出文件\trajectory_history.csv" --outdir "..\03-输出文件\trajectory_postprocess"
if errorlevel 1 (
  echo [ERROR] Trajectory post-processing failed.
  pause
  exit /b 1
)
echo [OK] Results written to ..\03-输出文件\trajectory_postprocess
pause
endlocal
