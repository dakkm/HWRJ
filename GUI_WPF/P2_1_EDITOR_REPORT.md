# P2.1 业务任务编辑器完善报告

## 1. 修改文件

仅修改 `GUI_WPF/`。相对本阶段开始时的P2源码：

- `PreProcess.Wpf/PreProcess.Wpf/Models/TargetPhysics.cs`：几何对象、球壳质量、属性变化通知、独立几何深复制。
- `Models/TaskModel.cs`：增加任务级相似指标及统一几何访问入口。
- `Models/README.md`：记录字段单位与职责。
- `Views/TargetPhysicsEditor.xaml`：目标结构、材料与热物性、质量信息三组布局。
- `Views/TargetSettingsView.xaml`：更新统一设置与独立覆盖说明。
- `Views/TaskSettingsView.xaml`：增加相似指标数值输入与范围说明。
- `PreProcess.Wpf.csproj`：登记新增几何模型文件。
- `Verification/P2/Test-TaskEditor.ps1`：质量期望更新为球壳；区分目标类型选择与目标编号选择；允许指定构建目录。
- `Verification/Test-Workbench.ps1`：允许指定构建目录；其结果文件在回归运行后更新。

新增：`Models/TargetGeometrySettings.cs`、本报告、`Verification/P2_1/` 下的测试脚本、源码起始快照、构建日志、测试结果、截图与哈希记录。不修改现有`.docs`、历史MFC或冻结后端；不提交Git。

## 2. 新增字段

| 字段 | 类型/单位 | 默认与范围 |
|---|---|---|
| `TargetGeometrySettings.TargetType` | TargetType枚举 | SphericalShell（界面“球壳”）；仅此类型可选，其他类型未实现 |
| `TargetGeometrySettings.ShellThickness` | double，mm | 5；必须>0且换算为m后小于外半径 |
| `TargetGeometrySettings.Radius` | double，m | 0.2；从旧物性中迁入几何对象，仍要求>0.005m且大于壳厚 |
| `TaskSettings.SimilarityIndex` | double，百分数 | 90，范围[50,100]，允许小数 |

90%是本次选择的GUI初始设计值：位于允许区间内且表达较高相似要求，不表示后端默认值或已承诺的评估结果。模型校验拒绝越界、NaN和无穷值，界面类型转换错误也会显示红框与悬停提示。

## 3. 数据模型变化

```text
TaskModel
├─ Settings.SimilarityIndex
└─ Targets
   ├─ Geometry → Uniform.Geometry（同一个实例）
   └─ Uniform : TargetPhysics
      ├─ Geometry : TargetGeometrySettings
      │  ├─ TargetType
      │  ├─ ShellThickness (mm)
      │  ├─ Radius (m)
      │  └─ Volume (m³，只读)
      ├─ Density / HeatCapacity / 其他热物性
      └─ Mass (kg，只读)
```

保留现有材料/热物性属性和绑定，避免无关重构；`TargetPhysics.Radius`仅转发到Geometry，不重复存储。目标类型的候选集合及中文名称由模型提供，View不硬编码“球壳”选项。

单目标首次启用覆盖时深复制几何与物性；修改该目标壳厚不影响统一设置。关闭覆盖恢复继承，重新启用保留独立值。复制时一次性验证半径/壳厚组合，避免合法大尺寸几何因默认半径过小而复制失败。

本阶段新增字段属于GUI业务模型，与冻结后端现有字段不自动等价；未实施任何合同修改或映射。

## 4. 质量计算逻辑

旧式 `4πR³ρ/3` 为实心球计算，已替换。

令外半径为 `R`（m），界面壳厚为 `h`（mm），内半径为 `r = R − h/1000`：

```text
TargetType = 球壳
t = h / 1000
V = 4π/3 × [R³ − (R−t)³]
Mass = V × Density
```

实现采用等价的 `4π/3 × t × [R² + R(R−t) + (R−t)²]`，减少薄壳情况下两个近似数相减的数值精度损失。

体积按TargetType分支计算；未实现类型被拒绝，不回退为实心球。几何属性变化与密度变化触发Mass通知，UI自动刷新。

默认 `R=0.2m，h=5mm，ρ=2700kg/m³`，质量为 **6.61760784515424kg**。质量没有输入控件，只有只读显示和公式说明。

## 5. 页面变化

- 目标结构：目标类型下拉框、壳厚(mm)、外半径(m)。当前仅可选球壳。
- 材料与热物性：密度、比热、初始温度、内部热源、红外发射率、太阳吸收率、红外反射率。
- 质量信息：自动计算质量(kg)、球壳公式和单位换算说明。
- 统一参数与高级单目标覆盖共用同一编辑控件，几何和质量规则一致。
- 任务设置新增“相似指标(%)”，显示50%～100%范围和90%默认值。
- 五页导航、四区布局、日志/结果容器保持不变；页面继续支持滚动和窗口缩放。

## 6. 测试与编译结果

VS2019 Release / .NET Framework 4.8源码编译通过。标准输出目录的旧EXE正被已有进程占用，首次标准构建在复制阶段报MSB3027/MSB3021。保留该进程，改用同一Release配置输出到独立目录，**0警告、0错误**。

从仓库根目录执行：

```bat
MSBuild GUI_WPF\PreProcess.Wpf\PreProcess.Wpf.sln /m /p:Configuration=Release /p:OutDir=bin\P2_1_Release\
```

交互与启动测试使用 `PreProcess.Wpf/PreProcess.Wpf/bin/P2_1_Release/PreProcess.Wpf.exe`。独立启动后主窗口正常响应，并正常关闭。首次等待遇到WPF初始窗口创建延迟，等待实际主窗口标题出现后复核正常。随后原有标准EXE占用解除，已重新执行普通Release构建并成功更新 `bin/Release/PreProcess.Wpf.exe`，0警告、0错误；最终日志为 `Verification/P2_1/build-standard-final.log`。

```powershell
powershell -NoProfile -STA -ExecutionPolicy Bypass -File GUI_WPF/Verification/P2_1/Test-ShellEditor.ps1 -BuildFolder P2_1_Release
powershell -NoProfile -STA -ExecutionPolicy Bypass -File GUI_WPF/Verification/Test-Workbench.ps1 -BuildFolder P2_1_Release
```

编辑器检查71项通过，工作台回归53项通过，包含：

1. 默认类型球壳、壳厚5mm、相似指标90%。
2. 外半径变化后的质量与独立解析式一致。
3. 密度翻倍，质量翻倍；质量文本刷新。
4. 修改壳厚后的mm/m转换正确；页面往返保留几何及相似指标。
5. 新建任务恢复所有新增默认值。
6. 壳厚0、负值、等于/超过半径、NaN、非数字，以及半径小于壳厚：界面报错、模型保留有效值。
7. 相似指标50、100、92.5接受；49、101、NaN、非数字拒绝。
8. 几何深复制、较厚大尺寸组合复制、统一与独立覆盖保留规则正确。
9. 五个页面打开、输入写入共享模型、切页保留、主目标约束、数量同步、窗口布局及Tab回归通过。

截图已检查：`Verification/P2_1/target-page.png`。构建/启动/测试证据同目录。不同DPI、全部物理极端数值和完整运行前校验不在本次验证范围。无效文本仍沿用P2规则：不写入Model，切页后使用最近有效值。

## 7. Git diff

已执行 `git diff --check -- GUI_WPF`，通过；`git diff -- coreprogram GUI_MFC_Legacy` 为空。

P1/P2改动尚未提交，普通Git diff显示累积变化；不能把所有差异算作P2.1。P2.1工程源文件与起始快照的逐项变化见 `Verification/P2_1/source-changes.json`。上述清单仅说明本次工作，未修改或撤回用户已有改动。

## 8. coreprogram哈希检查

对根目录下两个保护目录的全部文件做UTF-8路径与SHA-256比较：

| 目录 | 修改前 | 修改后 | 差异 |
|---|---:|---:|---:|
| coreprogram | 119 | 119 | 0 |
| GUI_MFC_Legacy | 1743 | 1743 | 0 |

证据：`Verification/P2_1/protected-before.csv`、`protected-check.json`。未启动任何后端程序。

## 9. 下一阶段建议

先确认相似指标90%的初始选择、壳厚单位与有效范围，以及独立覆盖是否应包含整套几何参数。随后可单独完善输入草稿保留、完整几何/材料边界校验与错误提示；后端合同与新增GUI几何的兼容性需在将来P3明确设计。

本次停止在P2.1，不推进P3映射、JSON导出、ProcessManager或后端调用。
