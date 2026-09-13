# Task Editor

已完成：
- 任务编辑
- 参数配置
- 请求生成
- 01调用基础
- P6.1 结果导入：支持选择 run_xxxxxx 或 output 目录，自动识别模块并通过 ResultReader 加载
- P6.1 温度曲线：支持 01 温度历史、02 prediction/reference、03 Reference/Candidate、04 Target/Proxy/Forward
- P6.1 红外图像：支持 01 红外响应离散矩阵、02 point token 像素矩阵及色标；03/04 保留空态接口

P6.1 验证：
- Release 主程序构建通过
- P6.1 专项自动化测试 18/18 通过
- GUI 启动冒烟测试通过

后续：
- 多模块验证
- 红外帧序列选择/播放及 03/04 后端图像格式接入
- 软件发布准备
