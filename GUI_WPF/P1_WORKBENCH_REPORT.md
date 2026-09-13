# P1 WPF 主窗口工作台骨架报告

## 1. 修改文件

以下路径均相对于 `GUI_WPF/PreProcess.Wpf/PreProcess.Wpf/`：

- `App.config`：运行时声明改为 .NET Framework 4.8。
- `App.xaml`：加载工作台共享样式。
- `MainWindow.xaml`：四区 Grid 工作台、菜单、工具栏、导航、占位页面和分隔条。
- `MainWindow.xaml.cs`：仅初始化及 TreeView 选择事件转发，不承载业务逻辑。
- `PreProcess.Wpf.csproj`：目标框架由4.7.2改为4.8，登记新增源文件和资源。

未修改维护文档；开始时 `.docs/01` 至 `.docs/07` 已有用户修改，全部保留。

## 2. 新增文件

工程内新增：

- `ViewModels/ObservableObject.cs`
- `ViewModels/RelayCommand.cs`
- `ViewModels/NavigationItemViewModel.cs`
- `ViewModels/MainWindowViewModel.cs`
- `Views/PlaceholderPage.xaml`、`Views/PlaceholderPage.xaml.cs`
- `Resources/WorkbenchStyles.xaml`
- `Models/README.md`、`Services/README.md`：仅目录职责说明，无业务模型或服务实现。

工程外、仍位于 `GUI_WPF/` 内：

- `P1_WORKBENCH_REPORT.md`：本报告。
- `Verification/Test-Workbench.ps1`：可重复运行的 WPF 冒烟检查。
- `Verification/ui-smoke-results.txt`：自动检查记录。
- `Verification/build-release.log`：VS2019 Release 构建日志。
- `Verification/exe-startup.json`：独立 EXE 启动记录。
- `Verification/protected-before.csv`、`Verification/protected-check.json`：只读目录完整性证据。

构建同时生成 `bin/Release`、`obj/Release` 等常规产物，不属于源码提交内容。

## 3. WPF 窗口结构

默认尺寸1400×850，最小尺寸1000×650。顶部菜单为文件、任务、计算、查看、帮助；工具栏为新建任务、打开任务、保存任务、运行、停止。

主体由五列 Grid 组织：导航、垂直分隔条、中部、垂直分隔条、结果。中部另设三行 Grid：编辑区、水平分隔条、日志区。列/行最小尺寸防止区域被完全压缩，内容按需要滚动。三个 GridSplitter 支持左右宽度及编辑区/日志区高度调整。

导航包含：

- 场景任务：任务设置、目标参数、场景与运动、环境与观测、计算与输出。
- 功能模块：正向计算、智能预测、轨迹生成、相似度评估、红外场景构建。
- 运行管理：当前任务、历史任务、输出结果、日志与告警。

中部统一 ContentControl 显示所选节点的标题、说明及占位区域。右侧 Tab 为曲线、点图像、数据表、结果摘要。底部 Tab 为日志、告警、进度，进度条固定0，无输出连接。

文件、运行、停止等操作仅更新状态栏，明确显示“尚未接入”。没有文件存取、模拟计算成功、真实进度或结果读取。

## 4. View / ViewModel 结构

- MainWindow：布局、资源和绑定；TreeView 的只读 SelectedItem 通过少量视图事件桥接到 NavigateCommand。
- PlaceholderPage：通用占位 UserControl，由数据模板显示导航项的 Title 和 Description；不是参数表单。
- MainWindowViewModel：导航集合、当前页面、状态文本、导航命令和占位命令。
- NavigationItemViewModel：仅导航表现信息及子项集合，不是业务任务数据模型。
- ObservableObject / RelayCommand：轻量 INotifyPropertyChanged / ICommand 基础，无第三方 MVVM 依赖。
- Models / Services：仅预留目录，本阶段不进入模型、JSON映射或运行管理。

## 5. 编译结果

使用现有 Visual Studio 2019 Professional 的 MSBuild 16.11，Release / Any CPU，目标 .NET Framework 4.8，编译成功。

从仓库根目录，在 VS2019 Developer Command Prompt 执行：

```bat
MSBuild GUI_WPF\PreProcess.Wpf\PreProcess.Wpf.sln /m /p:Configuration=Release
```

输出：`GUI_WPF/PreProcess.Wpf/PreProcess.Wpf/bin/Release/PreProcess.Wpf.exe`。

本次独立启动该 EXE，确认进程存活、Responding=true、主窗口标题“红外场景工作台”，随后正常关闭。首次 WaitForInputIdle 时主窗口句柄尚未生成，等待窗口创建后复核成功；未将此时序现象误判为启动失败。

## 6. 测试步骤与结果

运行自动化冒烟检查：

```powershell
powershell -NoProfile -STA -ExecutionPolicy Bypass -File GUI_WPF/Verification/Test-Workbench.ps1
```

检查加载本次编译程序集并实际创建 WPF 窗口，在屏幕外执行，不调用 Python/Fortran：

- 默认1400×850、三个导航组与14个叶节点名称：通过。
- 逐项选择真实 TreeViewItem，核对 ContentControl 切换：通过。
- 右侧4个结果 Tab、底部3个日志 Tab 逐项切换：通过。
- 新建/打开/保存/运行/停止占位命令反馈：通过。
- 1000×650、1400×850、1800×1000及最大化：主要区域保持正尺寸，通过。
- 调整导航/结果列宽、日志行高后内容保持可用，三个 GridSplitter 存在：通过。
- `git diff --check -- GUI_WPF`：通过。

测试边界：自动检查通过改变 Grid 行列尺寸验证重排，尚未模拟真实鼠标拖动，也未验证多显示器或125%/150%缩放。请人工启动 EXE，逐一拖动三个分隔条、缩小至最小窗口并最大化，检查视觉表现和拖动手感；这些不等同于已完成的自动检查。

## 7. Git diff 摘要

已跟踪文件：5个修改，最终检查时206行新增、153行删除（包含文本换行规范化）；另有上文列出的新 View、ViewModel、资源和验证文件。Git diff --stat 不包含未跟踪新文件，新增清单见第2节。

没有修改根目录解决方案、冻结后端、历史 MFC 或现有 `.docs` 文件。未自动提交 Git。

## 8. coreprogram / Legacy 检查结果

`git diff -- coreprogram GUI_MFC_Legacy` 输出为空。

对两个目录全部文件进行 UTF-8 路径及 SHA-256 前后比较：修改前1862个，修改后1862个，差异0。没有后端调用或合同修改。

## 9. 下一阶段建议

先人工确认四区比例、最小窗口尺寸、分隔条手感与字体缩放。确认后另开 Prompt 设计五个业务页面的具体范围与交互；再按维护流程逐步推进参数、模型和映射。

本次停在 P1 工作台骨架，不进入参数页面、业务数据模型、JSON映射、ProcessManager或后端调用。
