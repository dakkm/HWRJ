04-红外场景构建输出目录规范（run-based）
======================================

输出根目录只保留：
1. runs\
   每次 scene_search_controller.py 调用创建一个独立 run_YYYYMMDD_HHMMSS_xxxxxxxx。
2. reference_runs\
   跨任务共享的01基准正向缓存。该目录不是某一轮任务的私有输出，不应移动到 runs 内。
3. latest_run.json
   指向最近一次04任务。
4. README_输出目录.txt

每个04任务 run 目录包含：
- scene_search_request_used.json
- source_forward_request_used.json
- candidate_parameters.csv
- candidate_temperature_curves.csv
- scene_search_log.csv
- scene_search_summary.json
- scene_search_status.json
- forward_runs\（仅当本轮确实触发候选01复核时创建）

旧兼容输出 ten_valid_parameters.csv 和 ten_valid_temperature_long.csv 已停止生成。
configs / figures / logs / manifests / 根目录 forward_runs 不再由正式流程预创建。
