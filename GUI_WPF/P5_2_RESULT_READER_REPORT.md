# P5.2 后端结果解析器报告

日期：2026-09-12。范围：仅 `GUI_WPF/**`。本阶段完成后停止，未进入 P5.3。

## 1. 完成结论

已基于 P5.1 `RunResult` 建立四模块真实结果读取链：

```text
P4 RunRecord
    ↓
ResultDirectoryManager
    ↓
ResultReader / 模块 Reader
    ↓
RunResult + Temperature/Trajectory/Infrared/Similarity/Scene ResultModel
```

Reader 只读取和验证后端已有输出，不重新计算、不改写结果、不构造新的物理数据。CSV 保留原始列，JSON 标量和嵌套对象转换为可展示摘要；数值使用 InvariantCulture，空值保持 null。

## 2. 读取依据

已读取：

- 用户指定的 `.docs`、P3、P4.1、P4.2 报告及 P5.1 实现；
- `coreprogram/00-软件程序说明.docx`；
- `coreprogram/01-正向仿真/00-正向仿真模块统一说明.docx`；
- 四模块输出目录 README；
- 四个正式入口源码及 01 Fortran 输出写盘源码；
- `coreprogram` 中真实 01、02、03、04 run 目录；
- P4.2 真实 03 similarity 运行归档。

Word 文档渲染因当前环境没有 LibreOffice 未能生成页面图；改用捆绑 `python-docx` 完整提取文字和表格，并以 README、入口源码和实际输出文件交叉核对。没有单独依赖 Word 文档推断字段。

## 3. 新增与修改文件

生产路径相对于 `GUI_WPF/PreProcess.Wpf/PreProcess.Wpf/`：

|操作|文件|职责|
|---|---|---|
|新增|`Services/Results/ResultReadException.cs`|携带源文件路径的格式异常|
|新增|`Services/Results/ResultReaderBase.cs`|UTF-8/UTF-8 BOM CSV、gzip CSV、JSON、字段校验、缺失提示|
|新增|`Services/Results/ForwardResultReader.cs`|01 温度、轨迹、红外响应读取|
|新增|`Services/Results/PredictionResultReader.cs`|02 温度预测与点图像结果读取|
|新增|`Services/Results/SimilarityResultReader.cs`|03 features/similarity 两种模式读取|
|新增|`Services/Results/SceneResultReader.cs`|04 summary/status/候选表/搜索日志读取|
|新增|`Services/Results/ResultReader.cs`|按模块分派并直接关联 P4 `RunRecord`|
|修改|`Models/Results/RunResult.cs`|增加全局摘要和读取问题列表|
|修改|`PreProcess.Wpf.csproj`|注册 Reader 并引用 `System.IO.Compression`|

测试新增：

- `GUI_WPF/PreProcess.Wpf/ResultReaderTest/ResultReaderTest.csproj`
- `GUI_WPF/PreProcess.Wpf/ResultReaderTest/Program.cs`

验证证据位于 `GUI_WPF/Verification/P5_2/`。其中 `actual-similarity/` 是从用户原始压缩包中按字节提取的 P4.2 实际 03 成功输出，不是新生成的物理数据。

## 4. 解析合同

### 4.1 01 正向结果

`ForwardResultReader` 支持：

- `temperature_history.csv`：要求 `frame,time_s,T_1`，并读取全部实际 `T_N` 列；
- `trajectory_history.csv`：读取实际 16 列轨迹合同；
- `infrared_response_history.csv`：读取实际 14 列红外合同；
- `solver_status.txt`：存在时读取状态摘要。

三份主 CSV 分别输出 `TemperatureResult`、`TrajectoryResult`、`InfraredResult`。`Trajectory` GUI 类型继续复用后端 01，但在 `RunResult.ModuleType` 中保持独立。

### 4.2 02 智能预测

先读取 `prediction_summary.json` 的真实 mode 与运行摘要：

- temperature/both：读取 `temperature_prediction.csv`；
- point-image/both：读取 `point_image_frame_metrics.csv`、`point_token_predictions.csv.gz` 和重构合同 JSON；
- gzip token 表直接流式解压并进入 `InfraredResult`，不重构或生成 256×256 图像。

`point_image_frame_metrics.csv` 的 `total_power`、`peak_power`、`total_intensity` 列名和源码没有在字段名中携带明确单位。Reader 保留原值，单位标记留空，列入未确定项；没有猜测为 W 或 W/m²。

### 4.3 03 相似度评估

根据 `evaluation_status.json.mode` 区分：

- features：读取 `feature_summary.json`、`feature_timeseries.csv`、`object_feature_timeseries.csv`、`periodic_features.csv`；
- similarity：读取 `similarity_summary.json` 和 `similarity_components.csv`；存在 reference/candidate feature 子目录时一并读取。

无效相似度分量中的空数值保持 null，不替换为 0。

### 4.4 04 场景结果

读取 `scene_search_summary.json`、`scene_search_status.json`、`candidate_parameters.csv`、`candidate_temperature_curves.csv`、`scene_search_log.csv`。实际 ProxyOnlyCompleted 样例的两个候选表只有表头、没有数据行；Reader 将其识别为合法空结果，不当作格式损坏。

## 5. 缺失与异常处理

- 结果目录不存在：返回 `RunResult`，`ResultExists=false`，在 `Issues` 中保存可显示提示；
- 预期文件缺失：保留已成功读取的其他结果，并在 `Issues` 中逐项记录；
- CSV 表头缺失/重复、行列数不一致、必需数值为空或非有限数：抛出 `ResultReadException`，包含文件路径和行/列说明；
- JSON 语法错误或根节点不是对象：抛出 `ResultReadException`；
- summary/status 的 module 或 run_id 与 P4 记录冲突：拒绝关联；
- UTF-8 BOM、Fortran 科学计数法、CSV 引号及 gzip 均按实际文件处理。

每次 `Read` 会先清空同一 `RunResult` 的旧解析内容，避免重复读取产生重复行。

## 6. 真实目录测试

|模块/样例|验证结果|
|---|---|
|01 `run_20260911_131206_54dd28bb`|温度 78 行、轨迹 1248 行、红外 128 行|
|02 temperature `run_20260910_205207_f10e224c`|温度预测 101 行|
|02 both `run_20260910_205238_9608c9ba`|温度 101 行、帧汇总 101 行、gzip token 1616 行|
|03 features `run_20260911_151825_9403cacb`|场景特征 8 行、目标特征 128 行、周期特征 4 行|
|03 similarity `run_20260912_044041_2f565756`|相似度分量 28 行，温度汇总相似度 100%|
|04 `run_20260911_151927_cfafd7eb`|候选参数 0 行、候选曲线 0 行、搜索日志 5 行|

P5.2 `ResultReaderTest` 共 **33/33** 通过，覆盖四模块真实输出、四模块 P4 `RunRecord` 关联、01 轨迹分类、缺失目录、缺失文件、非法数值 CSV、损坏 JSON 和错配 run_id。

P5.1 `ResultModelTest` 重新编译运行，**24/24** 通过。P5.1 + P5.2 本轮验证合计 **57/57**。

按用户指令跳过完整 WPF/VS Release 重建验证。P5.2 新增源码和测试使用本机可用的 .NET Framework C# 编译器独立编译成功，0 警告、0 错误，并引用 P4.2 `PreProcess.Wpf.exe` 验证 `RunRecord` 集成。

## 7. 修改范围与边界

以 P5.1 增量包为源码基线复核：P5.1 既有文件只修改 `RunResult.cs` 与项目注册文件，其他 P5.1 源码变化为 0，删除为 0。P1-P4 生产源码未修改。

`coreprogram` 仅从原压缩包提取到临时工作区供只读核对，没有修改；`GUI_MFC_Legacy` 未提取或写入；后端输出格式未改变。没有修改 Views 或 ViewModels，P5.3 绑定数量为 0。

## 8. 停止边界

P5.2 到此完成并停止。Reader 目前是同步文件读取服务；P5.3 应在后台任务调用后一次性回写 UI，避免在界面线程读取大表。本阶段未实现该界面行为、结果切换、绘图或三维显示。
