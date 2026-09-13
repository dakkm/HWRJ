03-相似度评估输出目录规范（v1.0.1-baseline）
================================================

正式目录只保留：
1. runs\
   每次调用 similarity_evaluator.py 自动创建唯一 run_YYYYMMDD_HHMMSS_xxxxxxxx。
2. latest_run.json
   指向最近一次评价任务。
3. README_输出目录.txt

输入响应目录可以来自01或02，只要包含统一核心合同：
- temperature_history.csv
- infrared_response_history.csv

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

02当前无法提供的 source-radiation 字段会在 similarity_components.csv 中以 valid_flag=0 体现，
不参与 minimum_valid_component 综合值。
