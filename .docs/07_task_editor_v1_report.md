# PROMPT-20260911-002 实施报告

本阶段代码已实现；**尚未完成编译和运行验收**。环境只有 VS2019/v142，缺少 v143 和 MFC 库，不能宣称 x64 Release 编译成功或新 EXE 启动成功。未推进后端调用、映射层、结果显示等下一阶段。

## 1. 修改文件

- `Pre-process/MainFrm.cpp`、`MainFrm.h`：按系统 DPI 设置 800×600 逻辑像素的最小窗口尺寸。
- `Pre-process/Pre-processView.cpp`、`Pre-processView.h`：挂载编辑器、尺寸同步、文档刷新；原四模块命令与完成消息映射保留。
- `Pre-process/Pre-processDoc.cpp`、`Pre-processDoc.h`：文档持有业务任务，提交在编辑内容，处理新建、GUI 任务存取和关闭前保存。
- `Pre-process/Preprocess.rc`：菜单入口名称改为“编辑计算设置/编辑评估设置”，文件过滤器使用 `.pptask`；四模块菜单及命令 ID 保留。
- `Pre-process/resource.h`：新增独立 4200～4223 控件 ID 段。
- `Pre-process/Pre-process.vcxproj`、`Pre-process.vcxproj.filters`：登记新文件，保留全部原配置及 v143 工具集。
- `.docs/01_architecture.md`、`03_experiments.md`、`04_decisions.md`、`05_known_issues.md`、`06_prompt_log.md`：补充本次实现和验收限制。

`Pre-process.sln`、旧参数 Dialog、冻结后端未修改。

## 2. 新增文件

- `Pre-process/BusinessTask.h`、`BusinessTask.cpp`
- `Pre-process/TaskEditor.h`、`TaskEditor.cpp`
- `.docs/07_task_editor_v1_report.md`（本报告）
- `.work/task-editor/`：修改前 GUI 快照、生成/检查辅助脚本、构建日志、源码差异、静态检查结果、后端哈希证据；均为本次工作辅助文件，不参加工程编译。`generate.py` 是初始生成记录，不应再次运行覆盖后续修正。

## 3. 类与职责

| 类/结构 | 职责 |
|---|---|
| `CTaskEditor` | 左侧树、五个常驻属性网格页面、切页、动态计算设置、控件与模型绑定、文件按钮转发 |
| `BusinessTask` | GUI 业务任务聚合与带版本标识的 MFC archive 保存；不生成后端 JSON |
| `GuiTaskMetadata` | 任务名称、任务说明；明确不映射到后端合同 |
| `TaskSettings` | 元数据、仿真时长、目标数量 |
| `TargetSettings` | 全部目标共用的一套球体物性；编辑即统一赋值 |
| `SceneMotionSettings` | 目标群位置、朝向、运动 |
| `TargetMotionSettings` | 每个目标的相对位置、相对速度/加速度、释放时间和启用状态；编号由数组次序+1产生 |
| `EnvironmentObservationSettings` | 辐射环境、孔径与探测器姿态及运动、成像尺寸和跟踪开关 |
| `CalculationOutputSettings` | 当前模块、运行/预测/评估选择和 run 路径；构建模块本阶段仅预留禁用控件区域 |

布局使用项目已有 MFC 的 `CMFCPropertyGridCtrl`，分组、折叠、列宽调整及滚动由 MFC 承担；外围树/编辑区按客户区比例布局。没有引入第三方库。API 核对参见 [Microsoft MFC 属性网格文档](https://learn.microsoft.com/en-us/cpp/mfc/reference/cmfcpropertygridctrl-class?view=msvc-170)。

## 4～5. 五页参数及逐项源码依据

共同合同依据：`coreprogram/01-正向仿真/01-输入文件/forward_request_schema.json` 的 `fields`。运行时严格校验依据：`coreprogram/01-正向仿真/02-程序/forward_simulation_runner.py:24`（字段集合）、`:90`（校验）、`:117`（半径、密度、比热、温度及主目标规则）。正式 Fortran 使用依据：同目录 `production_main_output_interface.f90:1420` 起的输入解析、`:1584` 起的目标表解析。以下程序字段仅在维护报告出现，不是用户一级导航。

| 页面 | 用户参数 | 后端字段/当前源码依据 |
|---|---|---|
| 任务设置 | 任务名称、任务说明 | GUI 元数据，合同没有这两个字段，保存于 `task.metadata` |
| 任务设置 | 仿真时长 (s) | `CASE.TOTAL_TIME`；runner 要求有限且 >0 |
| 任务设置 | 目标数量 | `CASE.NUM_SPHERES`；完整编号范围 1..N；GUI 为内存防护限制 1..100000，此上限不是合同约束 |
| 任务设置 | 新建/打开/保存/另存为 | MFC 文档命令；独立 `.pptask` GUI 格式，无后端字段 |
| 目标参数 | 球体半径 (m) | `TARGET_PHYSICS.r`；Fortran :1621～1632；不是任意形状尺寸 |
| 目标参数 | 密度 (kg/m³) | `TARGET_PHYSICS.rho`；同上 |
| 目标参数 | 比热 (J/(kg·K)) | `TARGET_PHYSICS.cp`；同上 |
| 目标参数 | 初始温度 (K) | `TARGET_PHYSICS.t_init`；同上 |
| 目标参数 | 内部热源功率 (W) | `TARGET_PHYSICS.q_int`；同上 |
| 目标参数 | 红外发射率 | `TARGET_PHYSICS.eps_ir`；同上 |
| 目标参数 | 太阳吸收率 | `TARGET_PHYSICS.alpha_s`；同上 |
| 目标参数 | 红外反射率 | `TARGET_PHYSICS.rho_ir`；Fortran :1632 存入 `sphere_ir_reflectivity` |
| 目标参数 | 单目标独立物性 | 禁用说明项，后续开放；本版统一设置，无独立物性覆盖 |
| 场景与运动 | 群中心位置 X/Y/Z (m) | `GROUP_STATE.GROUP_CENTER[0..2]`；Fortran :1460 |
| 场景与运动 | 群方向 X/Y/Z | `GROUP_STATE.GROUP_NORMAL[0..2]`；Fortran :1463 |
| 场景与运动 | 群参考上方向 X/Y/Z | `GROUP_STATE.GROUP_UP[0..2]`；Fortran :1466 |
| 场景与运动 | 群速度 X/Y/Z (m/s) | `GROUP_STATE.GROUP_VELOCITY[0..2]`；Fortran :1469 |
| 场景与运动 | 群角速度 X/Y/Z (rad/s) | `GROUP_STATE.GROUP_ANGULAR_VELOCITY[0..2]`；Fortran :1472 |
| 场景与运动 | 群角加速度 X/Y/Z (rad/s²) | `GROUP_STATE.GROUP_ANGULAR_ACCELERATION[0..2]`；Fortran :1475 |
| 场景与运动 | 目标编号 | `TARGET_SCENE.id`，与物性编号一致；选择仅影响当前显示，不改变目标数据 |
| 场景与运动 | 相对位置 X/Y/Z (m) | `TARGET_SCENE.x/y/z`；Fortran :160、:1637～1648，群中心相对偏移 |
| 场景与运动 | 相对速度 X/Y/Z (m/s) | `TARGET_SCENE.vx/vy/vz`；Fortran :161，释放后的相对分离速度 |
| 场景与运动 | 相对加速度 X/Y/Z (m/s²) | `TARGET_SCENE.ax/ay/az`；Fortran :162、:1648 |
| 场景与运动 | 释放时间 (s) | `TARGET_SCENE.release_time`；runner :121～125；1号目标固定为0 |
| 场景与运动 | 启用目标 | `TARGET_SCENE.active`；runner :126；1号目标固定启用。统一放在此页，避免与物性页出现两个冲突开关 |
| 环境与观测 | 太阳辐照度 (W/m²) | `ENVIRONMENT.SOLAR_FLUX` |
| 环境与观测 | 太阳方向 X/Y/Z | `ENVIRONMENT.SOLAR_DIRECTION[0..2]` |
| 环境与观测 | 环境辐射温度 (K) | `ENVIRONMENT.ENVIRONMENT_TEMP`；Fortran :1459；不是环境流体温度 |
| 环境与观测 | 观测位置 X/Y/Z (m) | `OBSERVATION.APERTURE_CENTER[0..2]` |
| 环境与观测 | 观测方向 X/Y/Z | `OBSERVATION.APERTURE_NORMAL[0..2]` |
| 环境与观测 | 观测参考上方向 X/Y/Z | `OBSERVATION.APERTURE_UP[0..2]` |
| 环境与观测 | 孔径尺寸 (m) | `OBSERVATION.APERTURE_SIZE` |
| 环境与观测 | 焦距 (m) | `OBSERVATION.SPOT_FOCAL_LENGTH` |
| 环境与观测 | 像面尺寸 (m) | `OBSERVATION.SPOT_PLANE_SIZE` |
| 环境与观测 | 孔径跟踪目标群中心 | `OBSERVATION.APERTURE_TRACK_TARGET`；Fortran :1087，明确指向群中心 |
| 环境与观测 | 探测器跟踪目标群中心 | `OBSERVATION.DETECTOR_TRACK_TARGET`；Fortran :1095～1100 |
| 环境与观测 | 孔径速度 X/Y/Z (m/s) | `OBSERVATION.APERTURE_VELOCITY[0..2]` |
| 环境与观测 | 孔径角速度 X/Y/Z (rad/s) | `OBSERVATION.APERTURE_ANGULAR_VELOCITY[0..2]` |
| 环境与观测 | 孔径角加速度 X/Y/Z (rad/s²) | `OBSERVATION.APERTURE_ANGULAR_ACCELERATION[0..2]` |
| 环境与观测 | 探测器方向 X/Y/Z | `OBSERVATION.DETECTOR_NORMAL[0..2]` |
| 环境与观测 | 探测器参考上方向 X/Y/Z | `OBSERVATION.DETECTOR_UP[0..2]` |
| 环境与观测 | 探测器速度 X/Y/Z (m/s) | `OBSERVATION.DETECTOR_VELOCITY[0..2]` |
| 环境与观测 | 探测器角速度 X/Y/Z (rad/s) | `OBSERVATION.DETECTOR_ANGULAR_VELOCITY[0..2]` |
| 环境与观测 | 探测器角加速度 X/Y/Z (rad/s²) | `OBSERVATION.DETECTOR_ANGULAR_ACCELERATION[0..2]` |
| 计算与输出 | 当前功能模块 | GUI 状态，通过左侧模块节点或原菜单命令切换 |
| 计算与输出·正向 | 正式计算/仅准备输入 | runner :207 的 `--prepare-only`；选择值本版只保存，不执行 |
| 计算与输出·正向 | 超时时间 (s) | runner :207 的 `--timeout-seconds`，GUI 初值1800 |
| 计算与输出·正向 | 输出选项 | 禁用说明“采用正式输出策略”；依据 `production_output_policy.inc`，不虚构 CLI 开关 |
| 计算与输出·预测 | 温度/点图像/温度与点图像 | `02-智能预测/02-程序/surrogate_prediction_runner.py:362` 的 `temperature/point-image/both` |
| 计算与输出·评估 | 特征提取/相似度评估 | `03-相似度评估/02-程序/similarity_evaluator.py:152` 的 `features/similarity` |
| 计算与输出·评估 | 参考运行目录、候选运行目录 | 同文件 :154～155 的 `--reference-run/--candidate-run`；特征提取以后需映射单一 `--run-dir`，本版无执行映射 |
| 计算与输出·构建 | 相似度要求、候选数量、搜索设置 | 禁用预留区域。`04-红外场景构建/02-程序/scene_search_controller.py:366～369` 的 `similarity_requirement.required_percent`、`required_candidate_count`；:634～638 的 request/config/max-candidates。此版不假装这些区域已接入 |

新建任务使用 GUI 初始模板，不宣称后端存在默认回退；相对位置初值沿 X 每目标间隔1m，用户可以修改。半径、辐射参数与运动初值尚需专业用户确认，不能视作经过完整物理校验的场景。

## 6. 高级设置

- 红外反射率、单目标独立物性预留说明。
- 目标群角速度和角加速度。
- 通过目标编号选择的单目标相对位置/速度/加速度、释放时间与启用；不会铺开全部目标。
- 孔径运动，以及探测器方向、参考上方向、速度和角运动。

## 7. 明确排除

- 地球红外、对流换热边界、环境流体温度。
- 旧轨道输入模式、经典/开普勒轨道要素、平近点角等旧轨道选择字段：Fortran :1802～1810 明确返回 `UNSUPPORTED_LEGACY_FIELD`；正式合同不接收。
- 任意编号的“跟踪对象选择”：当前接口只支持是否跟踪目标群中心。
- 任意形状/长宽高：当前物性合同是球体半径。
- 数值步长、射线数量等未列入正式请求合同的内部配置不作为普通业务输入恢复。

## 8. 未完成功能与兼容边界

- x64 Release 编译、新 EXE 启动、实际点击/保存回读/缩放回归尚未完成，需有 v143 + 对应 x64 MFC 的环境。
- 完整 forward-request-v1 映射、JSON 导入导出、完整物理/几何/交叉字段校验未实现。现有输入仅做有限数、目标数量/编号保护，固定主目标启用及释放时间。
- `.pptask` 是新 GUI archive 格式，与旧无版本 archive 不兼容；错误版本被拒绝，不进行静默迁移。后端 JSON 不能作为 GUI 任务直接打开。
- 单目标独立物性覆盖尚未实现；统一参数修改即应用到全部目标；减少目标数量会删除尾部目标运动数据。
- 四模块菜单可路由至编辑器，旧启停及完成消息 ID 保留；启动命令本版不执行后端。旧线程/进程代码和旧 Dialog 保留但不会被新编辑入口调用。
- ProcessManager、Python 调用重构、stdout/stderr、GUI_PROGRESS、曲线/点图像、历史 run 均未推进。
- 未新增右侧结果区与底部日志工作台；本次仅实现左树和参数编辑器。
- 当前不是 Git 仓库，无法确认分支、基线、Git diff 或提交。

## 9. 编译方式

在安装了 VS2022 C++ 桌面开发、v143 x64/x86 工具和相应 MFC 组件的开发者命令提示符中，从工程根目录执行：

```bat
MSBuild.exe Pre-process.sln /m /p:Configuration=Release /p:Platform=x64
```

输出应为 `x64/Release/Pre-process.exe`。本次没有改变工具集，也没有用旧 EXE 冒充新构建。

## 10. 测试结果

| 检查 | 结果 |
|---|---|
| 原项目 x64 Release 构建 | 阻塞：`MSB8020`，找不到 v143；日志 `.work/task-editor/build-v143.log` |
| 命令行临时指定 v142 | 阻塞：`MSB8041`，缺少 MFC；日志 `.work/task-editor/build.log`，未改工程工具集 |
| vcxproj / filters XML 解析 | 通过 |
| 原 View 四模块命令、完成消息映射逐条比较 | 通过，命令 ID/处理函数绑定保留 |
| 五个中文一级导航及禁用参数检查 | 通过源码检查 |
| 新资源 ID 冲突检查 | 通过；旧资源同值别名保留 |
| coreprogram 内容核对 | 修改前后均119个文件，SHA-256 与规范化路径配对差异0；初始 PowerShell CSV 将中文路径转为 ASCII，比较时对当前路径做相同规范化，哈希不受影响 |
| 新 EXE 启动/切页/保存回读/窗口缩放/四模块点击 | 未执行，不能以源码检查替代运行验收 |
| Git diff | 无法执行：根目录不是 Git 仓库；替代差异见 `.work/task-editor/source.diff` |

建议人工回归：五页各改一项，切换往返；新增/减少目标并选择目标编号；保存、关闭、打开核对；取消新建/打开；编辑中直接按 Ctrl+S、关闭窗口；四模块菜单和树往返；800×600至最大化以及125%/150%缩放检查。观察只有当前页面可见、控件可滚动、没有重叠。

## 11. 需人工确认的界面设计点

1. 是否接受“分组属性编辑器 + 可折叠高级设置”的第一版表现形式。
2. 单目标启用/编号统一放在“场景与运动”，目标物性页只做统一设置。
3. 初始参数模板、相对位置间隔和最小窗口尺寸是否符合实际工作习惯。
4. `.pptask` 独立任务格式与旧文件不兼容；是否在后续阶段需要显式迁移工具。
5. 本阶段菜单“编辑计算设置”替代真正启动，后续正式接入时再恢复运行操作。

## 12. 建议下一条 Prompt

优先补齐本阶段验收，不开始映射层：

> 继续 PROMPT-20260911-002 的验收收尾。读取维护文档与 07_task_editor_v1_report.md，在具备 VS2022/v143/x64 MFC 的环境编译 Release，运行新 EXE，验证五页切换、编号选择、增减目标、保存回读、四模块菜单和缩放；修复发现的问题。coreprogram 只读，不推进 JSON 映射、进程管理或结果显示。提供实际验收记录并检查 Git diff。

验收通过后，下一阶段才考虑业务任务到 forward-request-v1 的正式映射、JSON 导入导出和任务校验。
