# P5 结果解析与显示阶段验收报告

日期：2026-09-12  
范围：`GUI_WPF/**`。`coreprogram/**`、`GUI_MFC_Legacy/**` 与后端输出格式均未修改。

## 1. 验收结论

P5 功能验收通过：现有证据中的 **360/360** 条断言全部通过，已验证完整链路：

```text
GUI 输入 → RequestGenerator → ProcessManager → 后端运行
        → 后端结果目录 → ResultReader → RunResult → 右侧结果浏览器
```

正式的“VS2019 Release 编译”项为**条件通过/待补验**：当前机器未安装 VS2019（亦无 .NET Framework 4.8 Targeting Pack），因此不能声明已完成原生 VS2019 编译。使用系统 MSBuild 4.8、官方 Roslyn 4.8 编译器和 .NET Framework 4.8 Reference Assemblies 进行的等价 Release 重建成功，0 个编译错误、0 个警告诊断；应在具备 VS2019 + .NET Framework 4.8 Targeting Pack 的环境中补跑一次原生 Release 编译，以关闭该环境前置项。

本报告只汇总既有构建、运行和测试证据；在用户要求继续后没有重复执行任何测试。

## 2. P5.1：结果目录管理与统一结果模型

P5.1 已建立格式无关的统一结果模型：`RunResult` 保存 run_id、模块类型、输入请求、运行/输出目录、运行状态、可用性和解析后的结果集合；`TemperatureResult`、`TrajectoryResult`、`InfraredResult`、`SimilarityResult`、`SceneResult` 用于承载 GUI 展示所需的强类型结果。`ResultTable` 保留列定义和行数据，不预设 CSV 分隔符、列顺序或物理字段。

`ResultDirectoryManager` 按模块和 run_id 定位目录，关联 P4 `RunRecord`，保存输入 request 路径与输出目录，并以 `ResultExists` / 可显示提示报告目录是否存在。目录规则为：01（含轨迹）使用 `<runsRoot>/<run_id>/output`，02/03/04 使用 `<runsRoot>/<run_id>`；非法 run_id、未知模块和路径穿越均会被拒绝。

- P5.1 `ResultModelTest`：24/24 通过。
- 覆盖：给定目录创建 `RunResult`、缺失目录提示、模块区分、P4 运行记录关联、格式无关容器与非法输入。

## 3. P5.2：后端结果解析器

P5.2 基于后端说明、入口代码和实际输出目录实现只读解析；不重新计算、不改写输出、不创建物理数据。新增 `ResultReader` 分派四个模块 Reader：

- 01 `ForwardResultReader`：温度、轨迹、红外响应及 solver 状态；
- 02 `PredictionResultReader`：温度预测、点图像帧汇总、gzip token 预测和重构摘要；
- 03 `SimilarityResultReader`：features 与 similarity 两种结果模式；
- 04 `SceneResultReader`：summary/status、候选参数、候选温度曲线和搜索日志。

Reader 会将缺失文件转为可显示问题，针对 CSV/JSON 格式异常抛出带源文件定位的 `ResultReadException`，并校验 summary/status 的模块及 run_id 与 P4 记录一致。真实目录读取覆盖 01、02、03、04；其中 01 至少读取温度、轨迹、红外三类数据。

- P5.2 `ResultReaderTest`：33/33 通过。
- 已验证真实样例包括：01 的 78 行温度、1248 行轨迹、128 行红外；02 温度预测 101 行；02 both 的 101 行温度、101 行帧汇总、1616 行 gzip token；03 features/similarity；04 的候选表与搜索日志。

## 4. P5.3：结果浏览器界面

右侧原有占位区已替换为 `ResultBrowserView`。它只浏览 P5.1/5.2 已产生的数据，提供：

- 运行状态：当前模块、状态、run_id 和可用性；
- 结果概要：输入请求、输出目录、运行时间、后端摘要和解析告警；
- 数据入口：只读、虚拟化 `DataGrid`，可选择不同 `ResultTable`。

P4 运行完成后，结果读取与表格适配通过 `Task.Run` 在后台执行，再回到界面绑定；模块切换缓存本次会话中各模块最近一次结果。无结果、失败、取消、prepare-only 与解析异常均分别显示，且不会把读取失败改写为后端运行失败。未实现绘图、三维显示或点图像重建。

- P5.3 `ResultViewTest`：25/25 通过。
- WPF XAML 运行时解析：1/1 通过（计入阶段证据，但未重复计入下表的总断言）。
- GUI 闭环测试：10/10 通过。以当前 Release 产物启动 WPF，实际通过 GUI 运行 02 温度预测，读取后端结果并确认“已完成”、101 行预测表、可见 DataGrid、切换至无结果 01 后的提示，以及切回 02 后的结果恢复和 UI 空闲状态。

## 5. 文件清单

生产代码（均在 `GUI_WPF/PreProcess.Wpf/PreProcess.Wpf/`）：

- `Models/Results/ResultModuleType.cs`、`ResultData.cs`、`RunResult.cs`；
- `Models/Results/TemperatureResult.cs`、`TrajectoryResult.cs`、`InfraredResult.cs`、`SimilarityResult.cs`、`SceneResult.cs`；
- `Services/Results/ResultDirectoryManager.cs`、`ResultReadException.cs`、`ResultReaderBase.cs`、`ForwardResultReader.cs`、`PredictionResultReader.cs`、`SimilarityResultReader.cs`、`SceneResultReader.cs`、`ResultReader.cs`；
- `ViewModels/ResultBrowserViewModel.cs`、`Views/ResultBrowserView.xaml`、`Views/ResultBrowserView.xaml.cs`；
- 修改：`ViewModels/ForwardRunViewModel.cs`、`MainWindow.xaml`、`PreProcess.Wpf.csproj`。

测试与验证：

- `GUI_WPF/PreProcess.Wpf/ResultModelTest/**`；
- `GUI_WPF/PreProcess.Wpf/ResultReaderTest/**`；
- `GUI_WPF/PreProcess.Wpf/ResultViewTest/**`；
- `GUI_WPF/Verification/P5_1/**`、`P5_2/**`、`P5_3/**`、`P5_ACCEPTANCE/**`；
- 阶段报告：`P5_1_RESULT_MODEL_REPORT.md`、`P5_2_RESULT_READER_REPORT.md`、`P5_3_RESULT_VIEW_REPORT.md`；
- 本报告：`P5_RESULT_DISPLAY_ACCEPTANCE_REPORT.md`。

## 6. 测试统计

| 测试组 | 通过 | 总数 | 说明 |
|---|---:|---:|---|
| P4 回归套件 | 268 | 268 | Mapper 5、P2 UI 50、P3 114、ProcessManager 37、P4 执行 UI 14、多模块 30、多模块 UI 18 |
| P5.1 ResultModelTest | 24 | 24 | 目录管理和统一模型 |
| P5.2 ResultReaderTest | 33 | 33 | 四模块 Reader、缺失与异常处理 |
| P5.3 ResultViewTest | 25 | 25 | 状态、概要、表格、切换和界面结构 |
| P5 GUI 完整闭环 | 10 | 10 | 当前 WPF 产物 + 实际 02 后端输出 + 结果显示 |
| **总计** | **360** | **360** | **0 失败** |

未计入失败项：历史 `Test-Workbench.ps1` 断言的是 P4/P5 前的占位 UI 语义，已不属于当前 P4 回归集；早期长路径测试工作目录复制失败属于 .NET Framework `MAX_PATH` 环境限制，改为短目录后同一测试已通过，均不是产品测试失败。

## 7. 编译结果

- 原生 VS2019 Release：未执行。环境未发现 VS2019/VS2022，且未安装 .NET Framework 4.8 Targeting Pack；系统旧编译器无法编译项目已有的现代 C# 语法。原始日志：`Verification/P5_ACCEPTANCE/release-build.log`。
- 等价 Release 重建：通过。使用系统 32 位 MSBuild 4.8 + 官方 `Microsoft.Net.Compilers.Toolset 4.8.0` + 官方 `.NETFramework.ReferenceAssemblies.net48`；0 errors、0 warning diagnostics。日志：`Verification/P5_ACCEPTANCE/release-build-equivalent.log`。
- 测试项目重建：ResultModelTest、ResultReaderTest、ResultViewTest 及 P1–P4 对应测试项目均通过。日志：`Verification/P5_ACCEPTANCE/test-builds.log`。
- 产物：`PreProcess.Wpf/bin/P5_AcceptanceRelease/PreProcess.Wpf.exe`，SHA-256：`2B8954CF81F1C92A266ABC68CD2CCCFD21046AF4CF1680C02C06CE20A2AE82D8`。

## 8. 冻结目录哈希与修改范围

哈希算法：按相对路径排序，连接 `relative/path<TAB>fileSHA256<LF>` 后计算 SHA-256。

| 目录 | 文件数 | 清单 SHA-256 | 结论 |
|---|---:|---|---|
| `coreprogram` 当前 | 119 | `B41E01CD8B73124092C4E2B3C7D65F9B607DD231A3FC2A184E7445B40FC7C3F9` | 与冻结基线 119 文件逐项一致，差异 0 |
| `coreprogram` 冻结基线 | 119 | `B41E01CD8B73124092C4E2B3C7D65F9B607DD231A3FC2A184E7445B40FC7C3F9` | 基线 |
| `GUI_MFC_Legacy` 冻结基线 | 1743 | `3D73CE952D0612593B7F0FD218ECC52CDAC9D7F1313602BB366D9D98B81CEAA2` | 未在此工作区解压或写入；基于交付压缩包预先捕获的清单 |

`coreprogram` 已直接复核无改动。`GUI_MFC_Legacy` 没有在本工作区物化，因此无法在本地对它再作“当前目录”散列；但没有对其进行任何解压、写入或打包操作。详细清单见 `Verification/P5_ACCEPTANCE/protected-hashes.json`、`coreprogram-manifest.csv` 和 `legacy-baseline-manifest.csv`。

## 9. 当前剩余问题

1. 必须在含 VS2019 + .NET Framework 4.8 Targeting Pack 的机器上补跑一次原生 Release 编译，才能将该项从条件通过改为通过。
2. 02 点图像相关的 `total_power`、`peak_power`、`total_intensity` 字段在后端文件和代码中没有明确单位；GUI 保留原字段和值，未猜测单位。
3. 运行闭环实跑覆盖 02 温度预测。03 的解析覆盖真实归档输出，04 覆盖既有结果和 P4 调用策略/取消行为；尚未以当前环境完成 03 全量正式搜索和 04 场景搜索成功态的端到端实跑。
4. 结果浏览器定位为浏览器，当前只提供文本和表格；图表、三维展示、点图像重建不属于 P5 范围。

## 10. 下一阶段建议

先完成原生 VS2019 Release 补验，再进入下一阶段。建议优先做受控的结果浏览体验完善：结果目录刷新/打开入口、表格导出和可追溯的解析告警展示；在后端明确单位和可视化数据契约后，再评估是否引入轻量二维图表。任何新增展示都应继续只读消费后端输出，不在 GUI 中重新计算或写回物理数据。
