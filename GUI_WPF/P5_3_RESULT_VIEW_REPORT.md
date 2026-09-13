# P5.3 结果浏览器界面阶段报告

日期：2026-09-12。范围：仅 `GUI_WPF/**`。

## 1. 阶段结论

P5.3 已完成，右侧原有“曲线 / 点图像 / 数据表 / 结果摘要”占位区已替换为结果浏览器。浏览器直接消费 P5.1 `RunResult` 与 P5.2 `ResultReader` 的输出，提供运行状态、结果概要、告警和只读数据表入口；不重新计算、不修改后端输出、不创建物理数据，也没有实现复杂曲线、点图像重建或三维显示。

运行结束后，现有 P4 `RunRecord` 会交给 P5.2 解析器。结果解析及表格适配通过后台任务执行，完成后一次性切回界面绑定，避免在 UI 线程读取 CSV/JSON/gzip 或构造表格。各模块保留本次应用会话内的最近一次结果，切换 01、02、轨迹、03、04 时同步切换右侧内容；未运行模块显示明确空状态。

按用户本阶段指令，**跳过完整 WPF/VS2019 Release 重建和重建后 GUI 启动验证**。本次完成独立源码编译、真实结果夹具、XAML 运行时解析及 P5.1/P5.2 回归。未进入 P5 整体验收。

## 2. 新增与修改文件

路径相对于 `GUI_WPF/`。

| 状态 | 文件 | 用途 |
|---|---|---|
| 新增 | `PreProcess.Wpf/PreProcess.Wpf/ViewModels/ResultBrowserViewModel.cs` | 状态、概要、问题、数据集适配和模块结果缓存 |
| 新增 | `PreProcess.Wpf/PreProcess.Wpf/Views/ResultBrowserView.xaml` | 右侧结果浏览器界面 |
| 新增 | `PreProcess.Wpf/PreProcess.Wpf/Views/ResultBrowserView.xaml.cs` | WPF 控件代码后置 |
| 修改 | `PreProcess.Wpf/PreProcess.Wpf/ViewModels/ForwardRunViewModel.cs` | P4 完成记录与 P5.2 解析、P5.3 显示联动；模块切换 |
| 修改 | `PreProcess.Wpf/PreProcess.Wpf/MainWindow.xaml` | 右侧占位区替换为 `ResultBrowserView` |
| 修改 | `PreProcess.Wpf/PreProcess.Wpf/PreProcess.Wpf.csproj` | 注册新增 ViewModel 和 View |
| 新增 | `PreProcess.Wpf/ResultViewTest/ResultViewTest.csproj` | P5.3 测试项目 |
| 新增 | `PreProcess.Wpf/ResultViewTest/Program.cs` | 状态、真实数据、切换和界面结构测试 |
| 新增 | `Verification/P5_3/**` | 编译、测试、XAML 与范围检查证据 |

P5.1/P5.2 的结果模型和解析器源码未修改，只有主项目文件按预期增加编译项。

## 3. 数据与界面结构

```text
P4 RunRecord
    ↓（Completed 且具有结果目录）
Task.Run
    ↓
P5.2 ResultReader → P5.1 RunResult
    ↓
ResultBrowserViewModel
    ├─ 运行状态 / run_id / 结果位置
    ├─ 结果概要 / 解析问题
    └─ ResultTable → 只读 DataView
                        ↓
                 ResultBrowserView
```

### 3.1 运行状态

状态卡固定显示当前模块、状态、run_id 和可用性说明。Preparing/Running/Stopping/Completed/Failed/Cancelled 映射为中文显示；后端失败、取消、未产生结果、结果读取异常分别保留原运行记录和问题提示，不把解析失败改写成后端计算失败。

### 3.2 结果概要

概要页显示模块、run_id、运行状态、输入请求、输出目录、开始/结束时间，以及 P5.2 从真实状态 JSON/文本中读取的概要字段。解析器报告的缺失文件等问题单独显示为告警。概要只浏览后端数据，不推导新的物理指标。

### 3.3 数据查看入口

数据页提供数据集下拉选择、源文件路径、行数和只读 `DataGrid`。列名及单位来自 P5.2 的 `ResultColumn`；值保持 reader 已解析的 long/double/bool/string/null 类型。表格启用行列虚拟化和横纵滚动。空表仍作为合法数据集显示，例如 04 没有有效候选时的候选参数表与温度曲线表。

| 模块 | 可显示数据 |
|---|---|
| 01 / 轨迹 | 温度历史、轨迹历史、红外响应历史 |
| 02 | 温度预测、点图像帧汇总、逐目标预测 |
| 03 | 相似度分量，或特征时序/目标特征/周期特征 |
| 04 | 候选参数、候选温度曲线、场景搜索日志 |

02 点图像部分功率/强度字段的单位仍沿用 P5.2 的“未确认”处理：只显示原字段和值，不补写单位。

## 4. 运行完成与模块切换

- 启动运行时右侧立即显示“运行中”，旧结果不会冒充本次结果。
- Completed 且存在正式结果目录时，在后台调用 `ResultReader.Read(RunRecord)`；“轨迹”通过 P5.2 的模块 override 保持为独立 GUI 类型，但仍读取 01 输出。
- Failed、Cancelled、prepare-only 无正式数据或没有结果目录时，显示运行记录和无结果提示，不尝试猜测文件。
- 解析异常被捕获并写入告警；后端输出和 `RunRecord` 保留。
- `ResultBrowserStore` 以 GUI 模块键保存本会话最近结果；模块下拉切换时立即恢复相应浏览器状态。没有建立历史数据库或跨会话存储。

## 5. 测试结果

### 5.1 P5.3 新增测试

`ResultViewTest` 使用真实 01/02/03/04 输出，经生产 P5.2 readers 解析后进入生产 `ResultBrowserViewModel`：**25/25 通过**。

覆盖：

- 尚未运行、运行中、失败、读取失败提示；
- 01 三类数据共 1,454 行、默认表和 K 单位列；
- 02 三个数据入口及 1,616 行 gzip 表；
- 03 相似度表和 100% 概要；
- 04 三个数据入口、两个合法空表及 5 行搜索日志；
- 01/02 结果往返切换、切到无结果 04；
- 右侧占位区替换、概要/数据控件、表格虚拟化；
- 完成联动源码明确通过 `Task.Run` 执行结果转换。

生产 P5.1/P5.2 源码、P5.3 ViewModel 与测试程序使用本机 .NET Framework 编译器独立编译成功。该编译不包含 WPF markup/code-behind，也不等同完整项目重建。

### 5.2 XAML 检查

- `ResultBrowserView.xaml`、`MainWindow.xaml`、主项目文件 XML 解析通过。
- 去除 code-behind 类型标记并解除项目级样式依赖后，`ResultBrowserView.xaml` 由 WPF `XamlReader` 运行时解析通过，根类型为 `UserControl`：**1/1 通过**。

### 5.3 回归

| 测试集 | 结果 | 证据 |
|---|---:|---|
| P5.3 ResultViewTest | 25/25 | `Verification/P5_3/result-view-tests.log` |
| P5.3 XAML 运行时解析 | 1/1 | `Verification/P5_3/xaml-parse.log` |
| P5.2 ResultReaderTest | 33/33 | `Verification/P5_3/p5_2-regression.log` |
| P5.1 ResultModelTest | 24/24 | `Verification/P5_3/p5_1-regression.log` |
| 合计 | **83/83** | 全部通过 |

P1-P4 没有新增业务模型、请求映射、进程管理或后端调用修改。由于用户要求跳过重建，本阶段没有重新运行依赖新 WPF 可执行文件的 P1-P4 GUI 自动化；其已验收基线由 P4.2 报告保留，P5.3 范围检查确认仅修改结果联动所需文件。

## 6. 非阻塞边界

结果解析、gzip 解压及 `ResultTable` 到 `DataTable` 的适配均位于 `Task.Run`。只有最终 ViewModel 引用赋值发生在 UI continuation；数据表启用 WPF 行列虚拟化。因此完成后的文件读取不会占用 Dispatcher。测试验证该异步边界与虚拟化设置；由于按指令未重建应用，没有执行重建后真实窗口的长结果交互计时，留待 P5 整体验收。

## 7. 修改范围检查

以 P5.2 累计 overlay 和原 P4.2 ZIP 为基线：

- P5.2 overlay 中 21 个源码/项目文件：20 个保持一致，1 个主项目文件按预期修改；缺失 0。
- 原 P4.2 的 `MainWindow.xaml`、`ForwardRunViewModel.cs` 按预期修改。
- `Models/Results/**`、`Services/Results/**` 均与 P5.2 一致。
- `coreprogram/**`：P5.3 写入 0；只读副本未修改。
- `GUI_MFC_Legacy/**`：未解压、未写入。
- 未修改任何后端输出格式。

证据见 `Verification/P5_3/source-scope-check.txt`。

## 8. 未执行项与下一步

本阶段按要求未执行：

- VS2019 Release 完整重建；
- 重建后窗口自动化与人工视觉检查；
- P1-P4 全量 GUI 回归；
- P5 统一验收、coreprogram/Legacy 最终哈希和最终验收报告；
- 复杂绘图、图像重建、三维显示、历史数据库。

P5.3 至此停止。下一次收到明确指令后，才进入 P5 整体验收并生成 `P5_RESULT_DISPLAY_ACCEPTANCE_REPORT.md`。
