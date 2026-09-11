03-相似度评估输出目录规范（run-based）
======================================

正式目录只保留：
1. runs\
   每次调用 similarity_evaluator.py 自动创建唯一 run_YYYYMMDD_HHMMSS_xxxxxxxx。
2. latest_run.json
   指向最近一次评价任务。
3. README_输出目录.txt

features 模式的 run 目录：
- evaluation_request.json
- similarity_config_used.json
- feature_timeseries.csv
- object_feature_timeseries.csv
- periodic_features.csv
- feature_summary.json
- evaluation_status.json

similarity 模式另外包含：
- reference_features\
- candidate_features\
- similarity_components.csv
- similarity_summary.json

--output-dir 现在表示“任务输出根目录”，程序仍会在其下自动创建唯一 run_xxx，避免覆盖。
