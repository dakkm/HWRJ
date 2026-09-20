@echo off
REM 执行当前批处理步骤，并保留命令返回状态。
setlocal
cd /d "%~dp0"
python postprocess_trajectory.py --input "..\03-输出文件\trajectory_history.csv" --outdir "..\03-输出文件\trajectory_postprocess"
REM 检查命令执行条件，并在不满足要求时停止或切换路径。
if errorlevel 1 (
  echo [ERROR] Trajectory post-processing failed.
  pause
  REM 返回当前脚本执行结果，供调用方判断是否成功。
  exit /b 1
)
echo [OK] Results written to ..\03-输出文件\trajectory_postprocess
REM 执行当前批处理步骤，并保留命令返回状态。
pause
endlocal
