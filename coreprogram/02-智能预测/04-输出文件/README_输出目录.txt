02-智能预测输出目录规范（v1.0.1-baseline）
================================================

正式根目录只保留：
1. runs\
   每次调用 surrogate_prediction_runner.py 自动生成唯一 run_YYYYMMDD_HHMMSS_xxxxxxxx 目录。
2. latest_run.json
   指向最近一次任务目录及状态，供 GUI/人工快速定位。
3. README_输出目录.txt

每个 run 目录包含两层输出：

A. 02模型原生输出（run根目录）
- request.json
- normalized_surrogate_input.json
- temperature_prediction.csv                  （temperature/both模式）
- point_token_predictions.csv.gz              （point-image/both模式）
- point_image_frame_metrics.csv                （point-image/both模式）
- point_image_reconstruction_contract.json     （point-image/both模式）
- prediction_summary.json

B. 01/03统一响应合同（run\output\）
- temperature_history.csv                     （temperature/both模式）
- infrared_response_history.csv                （point-image/both模式）
- output_contract.json
- run_metadata.json
- solver_status.txt

当 --mode both 成功时：
    run_xxx\output\
可以直接作为03 similarity模式的 candidate-run。

注意：当前02能力小于01完整正向能力。
- T_1可用，T_2~T_16为空；
- detector侧功率/辐照度、屏幕位置可用；
- radiation_power_W、radiant_intensity_W_sr当前不可用并保持为空。
这些不可用字段由03显式排除，不得用0或模型原生spot_intensity伪造。
