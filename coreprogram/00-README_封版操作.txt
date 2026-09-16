程序说明 v1.0.0-baseline 封板操作说明
========================================

一、封板定位
本次操作用于冻结“当前软件基线”。由于用户明确跳过完整全链路回归和双轮输出覆盖验收，
本版本状态为：frozen_baseline / full_regression_skipped_by_user。
这意味着：文件基线可以冻结，但不能把该状态表述为“已完成全部功能验收”。

二、补丁放置位置
将本补丁中的以下文件复制到 coreprogram 根目录：

文件：
00-版本信息.json
00-封版范围.json
00-封版检查清单.txt
00-变更控制规则.txt
freeze_release.py
verify_frozen_release.py

三、正式封板
在 CMD 中进入 coreprogram 根目录后执行：
python freeze_release.py

成功后生成：
00-文件SHA256校验清单.csv
00-封版元数据.json

四、封板校验
执行：
python verify_frozen_release.py

正常结果应包含：
status = VERIFIED
mismatch_count = 0
missing_count = 0

五、封板完成后
1. 将整个“程序说明”目录复制一份并改名为：
   程序说明_v1.0.0-baseline_20260911
2. 建议将该目录压缩为只读归档，不再直接开发。
3. 后续“统一快速正向代理开发”另建项目目录。
4. 若必须修复本基线，版本提升到 v1.0.1-baseline 或更高，并重新生成SHA256清单。

六、注意
不要把 runs、reference_runs、latest_run.json 等运行态文件纳入封板SHA256，
否则每运行一次软件都会使封板校验失效。
