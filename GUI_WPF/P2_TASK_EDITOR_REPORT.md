# P2 WPF 业务任务编辑页面报告

## 1. 修改文件列表

本阶段相对 P1 开始时的快照，仅修改 `GUI_WPF/`。工程内路径相对于 `PreProcess.Wpf/PreProcess.Wpf/`：

- `MainWindow.xaml`：新增五种页面数据模板，ContentControl 绑定当前页面内容；菜单/工具栏文件操作绑定内存任务命令。四区布局、导航节点、结果区和日志区不变。
- `PreProcess.Wpf.csproj`：登记模型、页面及 ViewModel，保持 VS2019 / .NET Framework 4.8。
- `ViewModels/MainWindowViewModel.cs`：持有共享编辑会话，按场景任务节点选择专属页面 ViewModel；显示内存操作状态。
- `Resources/WorkbenchStyles.xaml`：无效输入的验证提示。
- `Models/README.md`、`Services/README.md`：更新模型职责及未接入服务的边界。

`MainWindow.xaml.cs` 本阶段未修改；没有把业务逻辑移入 code-behind。既有 `.docs` 修改保留，未写入。

验证资料位于 `Verification/P2/`；复用执行 P1 测试后，`Verification/ui-smoke-results.txt` 更新为本次回归结果。

## 2. 新增 View 列表

每个 View 均含 `.xaml` 和仅初始化的 `.xaml.cs`：

- `Views/TaskSettingsView`：任务元数据、时长、数量、内存操作按钮。
- `Views/TargetSettingsView`：统一物性、折叠的单目标独立物性编辑。
- `Views/SceneMotionView`：群体状态、按编号选择的单目标状态。
- `Views/EnvironmentObservationView`：辐射环境、观测光学和高级运动设置。
- `Views/CalculationOutputView`：五个功能模块的说明及参数预留区域。
- `Views/Vector3Editor`：可伸缩的三分量编辑控件。
- `Views/TargetPhysicsEditor`：统一参数和独立覆盖共用的物性控件，不创建新的 Model。

其他功能模块节点与运行管理节点继续使用 P1 占位页，导航名称未改变。

## 3. 新增 Model 列表

| 源文件 | 类型与职责 |
|---|---|
| `Models/BindableModel.cs` | INotifyPropertyChanged 基础、有限数/正数校验辅助 |
| `Models/TaskModel.cs` | TaskModel 聚合；GuiTaskMetadata 保存 GUI 名称/说明；TaskSettings 保存时长/数量；TargetSettings 保存统一物性；TargetInstance 保存编号、覆盖状态和单目标运动 |
| `Models/TargetPhysics.cs` | 半径、密度、比热、初温、热源、辐射系数；质量由半径/密度派生；独立物性复制 |
| `Models/Vector3.cs` | 可通知的 X/Y/Z 分量 |
| `Models/SceneMotionSettings.cs` | SceneMotionSettings 群体状态；TargetMotionSettings 单目标状态和主目标限制 |
| `Models/EnvironmentObservationSettings.cs` | 环境、孔径/探测器姿态运动和光学设置 |
| `Models/CalculationSettings.cs` | CalculationSettings 与 CalculationModule：五个模块的静态业务说明，无可执行配置或执行入口 |

目标集合内部用 ObservableCollection，外部只读集合包装，防止绕过目标数量直接增删。目标编号从1开始连续覆盖。GUI 元数据单独存于 `Settings.Metadata`，没有混入后端合同。

## 4. ViewModel 结构

新增 `ViewModels/TaskEditorViewModel.cs`：

- TaskEditorViewModel：当前 TaskModel、共享 SelectedTarget、内存新建/打开提示/保存提示命令、状态反馈；减少数量时修复失效选择。
- TaskPageViewModel：页面标题和共享编辑会话。
- TaskSettingsViewModel、TargetSettingsViewModel、SceneMotionViewModel、EnvironmentObservationViewModel、CalculationOutputViewModel：五个轻量页面上下文。

MainWindowViewModel 用导航项与页面 ViewModel 的关联表进行切换；数据模板创建对应 View。未改动导航文本或引入第三方 MVVM 框架。Models 不依赖 ViewModels。

## 5. 页面参数与源码依据

正式字段依据为只读文件 `coreprogram/01-正向仿真/01-输入文件/forward_request_schema.json`。严格输入限制依据同模块 `02-程序/forward_simulation_runner.py:117–126`；正式使用方式依据 `production_main_output_interface.f90`。以下内部字段仅用于维护说明，不作为界面名称。

| 页面 | 显示与可编辑内容 | 源码依据/处理 |
|---|---|---|
| 任务设置 | 任务名称、任务说明 | GUI 元数据，无对应后端字段 |
| 任务设置 | 仿真时间(s)、目标数量 | `TOTAL_TIME`、`NUM_SPHERES`；编辑器数量保护1～10000，不宣称是后端上限 |
| 目标参数 | 球体半径(m)、密度(kg/m³) | 目标物性 `r/rho`；半径必须>0.005m，密度>0 |
| 目标参数 | 质量(kg)，只读 | Fortran :2461 的密度×体积；球体体积为4πr³/3，不新增独立质量输入 |
| 目标参数 | 比热(J/(kg·K))、初始温度(K)、内部热源功率(W) | `cp/t_init/q_int`；比热和初温>0 |
| 目标参数 | 红外发射率、太阳吸收率、红外反射率 | `eps_ir/alpha_s/rho_ir`；Fortran :1632 写入对应物性 |
| 目标参数·高级 | 目标编号、启用独立物性覆盖、该目标全套物性 | 编号对应目标表 `id`；覆盖/继承机制属于 GUI 业务语义。首次启用复制统一参数；关闭恢复继承；重新启用保留独立值 |
| 场景与运动 | 群中心位置、群方向、群参考上方向、群速度 | `GROUP_CENTER/GROUP_NORMAL/GROUP_UP/GROUP_VELOCITY`，三分量 |
| 场景与运动·高级 | 群角速度、群角加速度 | `GROUP_ANGULAR_VELOCITY/GROUP_ANGULAR_ACCELERATION`，rad/s、rad/s² |
| 场景与运动 | 目标编号、相对位置、相对速度、相对加速度 | 目标场景 `id/x/y/z/vx/vy/vz/ax/ay/az`；Fortran :161–162、:1020–1023、:1647–1648。相对速度/加速度用于释放后的分离运动 |
| 场景与运动 | 释放时间、启用目标 | `release_time/active`；1号目标固定时间0且启用，UI禁用修改，模型也限制 |
| 环境与观测 | 太阳辐照度、太阳方向、环境辐射温度 | `SOLAR_FLUX/SOLAR_DIRECTION/ENVIRONMENT_TEMP` |
| 环境与观测 | 观测位置、观测方向、参考上方向 | `APERTURE_CENTER/APERTURE_NORMAL/APERTURE_UP` |
| 环境与观测 | 孔径、探测器分别跟踪目标群中心 | `APERTURE_TRACK_TARGET/DETECTOR_TRACK_TARGET`；Fortran :1087、:1096 明确指向群中心，不虚构任意编号跟踪选择 |
| 环境与观测 | 孔径尺寸、焦距、像面尺寸(m) | `APERTURE_SIZE/SPOT_FOCAL_LENGTH/SPOT_PLANE_SIZE` |
| 环境与观测·高级 | 孔径速度、角速度、角加速度 | `APERTURE_VELOCITY/APERTURE_ANGULAR_VELOCITY/APERTURE_ANGULAR_ACCELERATION` |
| 环境与观测·高级 | 探测器方向、参考上方向、速度、角速度、角加速度 | 对应 `DETECTOR_NORMAL/UP/VELOCITY/ANGULAR_VELOCITY/ANGULAR_ACCELERATION` |
| 计算与输出 | 正向计算、智能预测、轨迹生成、相似度评估、红外场景构建 | 五个只读说明区域，含用途、输入说明、输出说明、参数占位；没有运行命令。轨迹生成接口细节标记为待核对，不宣称已接入独立后端模块 |

未加入地球红外、对流边界、环境流体温度、任意形状尺寸、独立质量输入或旧轨道输入模式。

初始参数属于 GUI 新建任务模板，不是后端默认值承诺；新目标相对位置沿X方向每个间隔1m。用户应在后续完整校验阶段确认模板的物理适用性。

## 6. 数据流与边界

```text
导航选择 → MainWindowViewModel.CurrentContent → DataTemplate → 对应 View
View.DataContext → 页面 ViewModel.Editor → 共享 TaskModel
有效控件输入 → TwoWay / PropertyChanged → Model → 属性通知 → 所有相关控件
目标数量变化 → 目标集合增删 → 选择修复
未覆盖目标 → 统一 TargetPhysics；覆盖目标 → 独立 TargetPhysics
```

有效输入即时写入内存，因此切页或改变目标选择不会清除模型。字段类型转换失败、非有限数、部分范围错误显示红框与悬停提示，不写入模型。**无效输入不是已保存数据**；离开页面后按模型最近有效值重建控件，未实现无效文本草稿跨页保存。

新建重置共享任务；打开只提示未接入并保留当前任务；保存提示有效值已在内存。未定义任何任务文件格式，程序关闭不持久化。减少数量会移除末尾目标及其覆盖/运动设置，页面有明确说明。

尚未实现完整的向量归一化、共线性、碰撞、系数物理范围、跨字段一致性或运行前校验；不能把当前可编辑任务视为经过完整后端校验的请求。

## 7. 编译结果

VS2019 Professional / MSBuild 16.11，Release / Any CPU，.NET Framework 4.8：**构建成功，0错误**。

最终编译记录含1条临时 `*_wpftmp.csproj` 文件被其他进程占用的清理警告；不影响程序集产出。构建结束后已清理本次遗留的两个临时项目文件，没有更改工具链设置。日志在 `Verification/P2/build-release.log`。

```bat
MSBuild GUI_WPF\PreProcess.Wpf\PreProcess.Wpf.sln /m /p:Configuration=Release
```

产物：`PreProcess.Wpf/PreProcess.Wpf/bin/Release/PreProcess.Wpf.exe`。独立启动本次 EXE，确认主窗口创建、响应正常，正常关闭；记录为 `Verification/P2/exe-startup.json`。

## 8. 测试步骤与结果

```powershell
powershell -NoProfile -STA -ExecutionPolicy Bypass -File GUI_WPF/Verification/P2/Test-TaskEditor.ps1
powershell -NoProfile -STA -ExecutionPolicy Bypass -File GUI_WPF/Verification/Test-Workbench.ps1
```

P2实际创建WPF窗口并修改真实控件：45项检查通过；P1回归53项通过。

- 五个实际 View 均加载，并共享同一编辑会话。
- 文本、数字、复选框、编号选择可以更新 Model。
- 半径/密度派生质量正确；统一参数继承与独立覆盖互不误写；覆盖关闭/重新启用行为正确。
- 单目标运动与物性编号共享；主目标释放时间锁定。
- 所有页面往返后名称、独立半径、运动值保留。
- 非法目标数量出现验证错误，模型不接受；增加/减少数量和失效选择修复正确。
- 新建更换 Model 并刷新绑定；打开/保存不会替换内存任务。
- 最小窗口下五页均有可用滚动区；最大化可用。
- P1的真实树节点切换、结果/日志Tab、分隔条结构与缩放回归通过。
- `git diff --check -- GUI_WPF` 通过。

截图 `Verification/P2/target-page.png` 已检查：四区保持，编辑控件未与日志/结果区重叠。自动测试窗口在屏幕外创建，独立EXE启动后关闭，没有运行后端。

人工建议复核：不同DPI下文字与输入框、拖动分隔条的手感、长任务说明、高级物性编辑滚动、真实业务数据的单位和默认值。自动测试没有覆盖所有物理边界，也没有验证真实后端行为。

## 9. Git diff 摘要

P1尚未提交，普通 Git diff 会同时包含P1/P2累积改动，不能全部算作本阶段。

相对本阶段开始时 `Verification/P2/before/` 的工程快照：**6个已有源码/说明文件修改、22个工程文件新增**；逐项见 `Verification/P2/source-changes.json`。额外新增本报告、检查/初始生成脚本、截图及验证记录，均在 `GUI_WPF/`。生成脚本仅记录初始搭建，不应重跑覆盖人工修订。

未修改 `.docs`、`MainWindow.xaml.cs` 或 P1其他控件布局。未执行 Git 提交。

## 10. 冻结目录哈希检查

| 目录 | 修改前文件数 | 修改后文件数 | 路径及SHA-256差异 |
|---|---:|---:|---:|
| coreprogram | 119 | 119 | 0 |
| GUI_MFC_Legacy | 1743 | 1743 | 0 |

使用UTF-8路径记录，比较两个目录各自根路径下全部文件（包括未跟踪文件）。证据为 `Verification/P2/protected-before.csv` 和 `protected-check.json`。

`git diff -- coreprogram GUI_MFC_Legacy` 为空。没有修改合同、调用Python/Fortran或启动后端。

## 11. 下一阶段建议

先确认五页字段组织、单位、默认值、独立覆盖策略，以及减少数量/新建的交互。下一条 Prompt 可限定为“完善WPF任务输入校验与错误反馈”，明确无效草稿保留、向量几何约束、跨字段验证和模板规则。

本次停止在P2，不推进 forward-request-v1 映射、JSON导入导出、ProcessManager或后端运行。
