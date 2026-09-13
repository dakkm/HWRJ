# P5.1 结果目录管理与统一结果模型报告

日期：2026-09-12。范围：仅 `GUI_WPF/**`。本阶段完成后停止，未进入 P5.2 或 P5.3。

## 1. 完成结论

已建立格式无关的统一结果模型和只读结果目录管理服务，形成 P5 后续链路的数据基础：

```text
P4 RunRecord / run_id / 结果路径
              ↓
ResultDirectoryManager
              ↓
RunResult + 各类 ResultModel
```

本阶段不读取 CSV、结果 JSON、图像或其他复杂数据，不设计绘图，不修改后端输出，不接入结果界面。GUI 仍只承担后端调用器和结果浏览器职责，不重新计算或创建物理结果。

## 2. 新增与修改文件

生产文件相对于 `GUI_WPF/PreProcess.Wpf/PreProcess.Wpf/`：

|操作|文件|职责|
|---|---|---|
|新增|`Models/Results/ResultModuleType.cs`|区分 Forward、Prediction、Trajectory、Similarity、Scene|
|新增|`Models/Results/ResultData.cs`|格式无关的摘要、列、行、表及结果基类|
|新增|`Models/Results/RunResult.cs`|统一运行结果元数据和强类型结果集合|
|新增|`Models/Results/TemperatureResult.cs`|温度结果类型|
|新增|`Models/Results/TrajectoryResult.cs`|轨迹结果类型|
|新增|`Models/Results/InfraredResult.cs`|红外响应结果类型|
|新增|`Models/Results/SimilarityResult.cs`|相似度结果类型|
|新增|`Models/Results/SceneResult.cs`|场景结果类型|
|新增|`Services/Results/ResultDirectoryManager.cs`|run_id 定位、P4 记录关联、模块校验、存在性提示|
|修改|`PreProcess.Wpf.csproj`|注册上述生产源码|

测试文件：

- `GUI_WPF/PreProcess.Wpf/ResultModelTest/ResultModelTest.csproj`
- `GUI_WPF/PreProcess.Wpf/ResultModelTest/Program.cs`

验证证据位于 `GUI_WPF/Verification/P5_1/`，包含测试日志、构建尝试日志、P2/P3 回归输出和源码范围检查。本报告为 `GUI_WPF/P5_1_RESULT_MODEL_REPORT.md`。

## 3. 数据结构

`RunResult` 保存：

- `RunId`；
- `ModuleType` 和后端 `ModuleCode`；
- GUI 输入 `InputRequestPath`；
- `RunDirectory`；
- `OutputDirectory`；
- 创建时的 `ResultExists` 快照和可直接显示的 `AvailabilityMessage`；
- P4 的开始/结束时间与运行状态；
- 温度、轨迹、红外、相似度、场景五类强类型结果集合。

各具体结果类型继承 `ResultArtifact`。共同结构只有 `Name`、`SourcePath`、字符串摘要和零到多个 `ResultTable`。表格由列定义和键值行组成，未预设 CSV 文件名、分隔符、列顺序或物理字段名。P5.2 必须读取真实输出说明与实际文件后再填充这些容器。

`TemperatureResult`、`TrajectoryResult`、`InfraredResult`、`SimilarityResult`、`SceneResult` 保持独立类型，使 P5.2 Reader 和 P5.3 View 可以按业务结果类型切换，而不需要把所有结果压成一个无类型列表。

## 4. 目录管理方式

`ResultDirectoryManager` 提供三种入口：

1. `Locate(moduleType, runId, inputRequestPath, runsRoot)`：按模块运行根与 `run_id` 定位；
2. `Create(moduleType, runId, inputRequestPath, outputDirectory, runDirectory)`：由给定结果目录建立 `RunResult`；
3. `Create(RunRecord, moduleOverride)`：直接关联 P4 的统一运行记录。

目录规则沿用 P4 已确认行为：

|GUI结果类型|后端模块码|结果目录|
|---|---:|---|
|Forward|01|`<runsRoot>/<run_id>/output`|
|Trajectory|01|`<runsRoot>/<run_id>/output`；GUI类型仍与Forward区分|
|Prediction|02|`<runsRoot>/<run_id>`|
|Similarity|03|`<runsRoot>/<run_id>`|
|Scene|04|`<runsRoot>/<run_id>`|

`run_id` 必须是单级目录名，空值、未知模块和路径穿越会被拒绝。路径统一转换为绝对路径。目录不存在时仍返回可用于界面提示的 `RunResult`，`ResultExists=false`，并给出“结果目录不存在：...”消息；`ResultExists(RunResult)` 可在之后重新进行实时文件系统检查。

P4 的轨迹入口实际调用后端 01，且 `ForwardRunRecord.Module` 仍为 `01`。因此 `moduleOverride=Trajectory` 仅允许从 01 记录分类为轨迹，不允许把 02/03/04 记录错误改类。

## 5. 测试结果

|测试集|结果|说明|
|---|---:|---|
|P5.1 ResultModelTest|24/24|给定目录、缺失目录提示、五类模块、P4 RunRecord、格式无关容器、非法 run_id/模块|
|原 MapperTest 回归|5/5|已有映射测试全部通过|
|P2 界面回归|50/50|使用 P4.2 已编译程序集，五页/绑定/固定壳厚/生成入口等通过|
|P3 RequestGenerator 回归|114/114|schema、字段、负例、覆盖与 UTF-8 等通过|
|合计通过断言|193/193|未包含构建工具环境检查|

P5.1 新增源码和测试程序使用本机 .NET Framework C# 编译器独立编译成功，0 警告、0 错误，并运行得到 24/24。该独立编译引用压缩包内 P4.2 的 `PreProcess.Wpf.exe`，实际覆盖了 `RunRecord` / `ProcessRunState` 关联。

已尝试完整 WPF Release 重建，但当前机器未安装 VS2019/VS2022 MSBuild，也没有 .NET Framework 4.8 Targeting Pack；仅有的 MSBuild 4.8.9037 会把 ToolsVersion 15 强制降为 4.0，并使用只支持到 C# 5 的旧编译器，因此在未修改的 P1–P4 现代 C# 语法处产生大量错误。完整 Release 构建在本环境不能作为通过项，原始输出保存在 `release-build.log` 与 `result-model-build.log`。P5 整体验收所要求的 VS2019 Release 编译仍须在具备 VS2019 + .NET Framework 4.8 Targeting Pack 的环境执行。

## 6. P1–P4 与修改范围检查

以用户提供的 `J:\Pre-process (2).zip` 中 `GUI_WPF` 为基线，对 `*.cs`、`*.xaml`、`*.config`、`*.csproj`、`*.sln` 逐文件 SHA-256 比较（排除生成目录 `bin/obj`）：

- 新增 11 个 P5.1 源码/测试文件；
- 仅修改 1 个既有文件：`PreProcess.Wpf.csproj`，内容为新增源码注册；
- P1–P4 既有生产源码意外变化：0；
- 删除文件：0。

原始压缩包未被写入。没有修改或打包 `coreprogram/**`、`GUI_MFC_Legacy/**`、`.docs/**`，没有改变后端输出格式。回归使用的 schema 是从原压缩包只读提取到临时工作区的副本。

## 7. 当前边界

未实现任何 Reader，未读取真实后端数据字段，未加入结果展示或绘图，未修改运行完成后的 GUI 流程。结果集合当前由 P5.2 Reader 填充；P5.3 才能绑定界面。

P5.1 到此完成并停止，等待下一步指令。
