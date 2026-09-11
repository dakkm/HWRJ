02-智能预测输出目录规范（run-based）
====================================

正式目录只保留：
1. runs\
   每次调用 surrogate_prediction_runner.py 自动生成唯一 run_YYYYMMDD_HHMMSS_xxxxxxxx 目录。
2. latest_run.json
   指向最近一次任务目录及状态，供 GUI/人工快速定位。
3. README_输出目录.txt
   本说明文件。

每个 run 目录保存本轮输入快照、规范化输入、温度预测、点图像预测和 prediction_summary.json。
不同轮次不覆盖。

正式运行不要直接调用 Stage F 生成业务输出；Stage F 仅保留模型合同验证和内部依赖功能。
