# P3 Request Generator 完成报告

日期：2026-09-12。范围仅 GUI_WPF/**。

## 1. 定位与完成结果

WPF 是任务配置、后端输入文件生成与调用/展示准备层，不承担物理求解。
数据流为 TaskModel → RequestGenerator → forward-request-v1 的 request.json → 后续统一入口。
本次完成请求生成与校验、路径选择、覆盖检查。没有修改或启动任何 Python/Fortran/后端程序，没有实现 ProcessManager 或结果解析。

已读取用户指定的七份 .docs 文档和 P3_MODULE_IO_AUDIT_REPORT.md；当前请求优先于旧报告的阶段建议。只在 GUI_WPF 内写报告，不改 .docs。工作区原有未提交变更全部保留，未提交或暂存 Git。

使用方法：VS2019 打开 PreProcess.Wpf/PreProcess.Wpf.sln，以 Release / Any CPU 编译运行；左侧“场景任务 → 计算与输出 → 生成 request.json…”选择路径。已有文件会要求确认覆盖。也可以直接调用以下 API：

```csharp
var generator = new PreProcess.Wpf.Services.RequestGenerator();
string json = generator.Generate(task);
generator.ValidateJson(json);
string path = generator.Save(task, @"D:\Pre-process\GUI_WPF\request.json");
// 仅在调用方明确同意覆盖后传 overwrite: true。
```

## 2. 修改文件清单

以下源码路径相对 GUI_WPF/PreProcess.Wpf/PreProcess.Wpf/。

|操作|文件|目的|
|---|---|---|
|新增|Services/RequestGenerator.cs|完整字段组装、序列化、验证、安全保存|
|新增|Services/ForwardRequestValidator.cs|消费后端自定义 schema 字段与类型描述，并补齐入口关系约束|
|新增|Resources/forward_request_schema.json|后端文件原样副本，嵌入程序集，未改变定义|
|调整|PreProcess.Wpf.csproj|注册服务与 schema 资源；引用 .NET 自带 System.Web.Extensions，无 NuGet 依赖|
|调整|Models/TargetGeometrySettings.cs|固定5mm，setter拒绝其他厚度，类型标签明确后端限制|
|调整|Views/TargetPhysicsEditor.xaml|球壳与壳厚固定显示，移除自由输入；质量只作显示|
|调整|Views/CalculationOutputView.xaml|增加生成按钮、状态反馈、共享请求说明、场景搜索阈值绑定|
|调整|Views/CalculationOutputView.xaml.cs|保存路径对话框、覆盖确认、当前可见输入校验和错误提示|
|调整|Services/README.md|记录正式生成入口与旧 Mapper 的边界|

验证新增文件位于 Verification/P3_RequestGenerator/：

- Test-RequestGenerator.ps1、Test-P2Regression.ps1：可重复验证脚本。
- request.json：默认任务示例；changed-request.json：单目标修改示例。
- request-tests.log、mapper-tests.log、ui-results.txt、build-release.log、target-page.png。
- before_hashes.csv、after_hashes.csv、protected-check.json：会话前后证据。
- git-diff-stat.txt、git-diff.patch：最终全工作区 tracked diff 快照，包含用户原有更改，不能全部归因本次。

另新增本报告。编译产生的 bin/obj 文件均在 GUI_WPF 内。

## 3. RequestGenerator 设计

只建立一个正式请求生成服务，不扩展 Mapper 框架或平行物理模型。使用显式字段白名单组装六分区；每目标读取 EffectivePhysics，保持统一参数与单目标覆盖语义。向量复用已有 MapVector 拷贝，不转换单位或补默认物理参数。

JavaScriptSerializer 生成标准 JSON 数值、三分量数组和 true/false；不是 Fortran 的 T/F。质量、壳厚、GUI名称、任务说明、相似阈值、UseOverride、schema_version 均不进入文件。schema_version 只存在于 schema 描述中。

Generate 序列化后执行 ValidateJson。Save 在任何文件写入前生成并验证完整 JSON；默认拒绝覆盖，同目录临时文件写 UTF-8 无 BOM，使用 File.Move 或 File.Replace 完成最终保存，失败时清理临时文件。父目录须已存在；路径、权限、覆盖、参数错误均通过异常返回，GUI显示“生成失败”及原因。不会自动创建运行目录。

GUI生成当前内存中已经接受的有效值，延续P2输入策略；模型setter拒绝无效值，不会用空文本覆盖有效数值。生成页明确提示这一点。当前可见红框阻止生成；此前已销毁页面的无效文本不会作为新的任务状态保留。完整 TaskModel 的各参数子对象由构造器建立；缺失任务在生成入口拒绝，缺失请求节/字段在JSON验证入口拒绝。

## 4. 依据与映射表

权威输入：coreprogram/01-正向仿真/01-输入文件/forward_request_schema.json；coreprogram/00-软件参数接口表.xlsx 的六个工作表；coreprogram/01-正向仿真/02-程序/forward_simulation_runner.py 的 SECTION_FIELDS、PHYS_FIELDS、SCENE_FIELDS、validate_request；production_main_output_interface.f90 的 TARGET_SHELL_THICKNESS=0.005d0。均只读。

接口表对02/03/04的概括与实际入口存在差异，沿用审计报告的判断，以实际入口为准。01字段、单位和输入规则已对照接口表。表中并列项严格按顺序一一对应；向量为[X,Y,Z]。

|TaskModel来源|request字段|单位/处理|
|---|---|---|
|Settings.Duration / TargetCount|CASE.TOTAL_TIME / NUM_SPHERES|s / 整数|
|Environment.SolarFlux / SunDirection / RadiationTemperature|ENVIRONMENT.SOLAR_FLUX / SOLAR_DIRECTION / ENVIRONMENT_TEMP|W/m² / 向量 / K|
|Scene.Position / Direction / Up|GROUP_STATE.GROUP_CENTER / GROUP_NORMAL / GROUP_UP|m / 方向 / 方向|
|Scene.Velocity / AngularVelocity / AngularAcceleration|GROUP_STATE.GROUP_VELOCITY / GROUP_ANGULAR_VELOCITY / GROUP_ANGULAR_ACCELERATION|m/s / rad/s / rad/s²|
|Environment.ApertureSize|OBSERVATION.APERTURE_SIZE|m|
|Environment.ObserverPosition / ObserverDirection / ObserverUp|OBSERVATION.APERTURE_CENTER / APERTURE_NORMAL / APERTURE_UP|m / 方向 / 方向|
|Environment.ObserverVelocity / ObserverAngularVelocity / ObserverAngularAcceleration|OBSERVATION.APERTURE_VELOCITY / APERTURE_ANGULAR_VELOCITY / APERTURE_ANGULAR_ACCELERATION|m/s / rad/s / rad/s²|
|Environment.ApertureTracking|OBSERVATION.APERTURE_TRACK_TARGET|boolean|
|Environment.DetectorDirection / DetectorUp|OBSERVATION.DETECTOR_NORMAL / DETECTOR_UP|方向向量|
|Environment.DetectorVelocity / DetectorAngularVelocity / DetectorAngularAcceleration|OBSERVATION.DETECTOR_VELOCITY / DETECTOR_ANGULAR_VELOCITY / DETECTOR_ANGULAR_ACCELERATION|m/s / rad/s / rad/s²|
|Environment.DetectorTracking|OBSERVATION.DETECTOR_TRACK_TARGET|boolean|
|Environment.PlaneSize / FocalLength|OBSERVATION.SPOT_PLANE_SIZE / SPOT_FOCAL_LENGTH|m|
|IndividualTargets[i].Id|TARGET_PHYSICS[i].id、TARGET_SCENE[i].id|相同ID，完整覆盖1..N|
|EffectivePhysics.Radius / Density / HeatCapacity|TARGET_PHYSICS[i].r / rho / cp|m / kg/m³ / J/(kg·K)|
|EffectivePhysics.Emissivity / SolarAbsorption / IrReflection|TARGET_PHYSICS[i].eps_ir / alpha_s / rho_ir|显式独立数值，不自动推导反射率|
|EffectivePhysics.InternalPower / InitialTemperature|TARGET_PHYSICS[i].q_int / t_init|W / K|
|Motion.Position.X/Y/Z|TARGET_SCENE[i].x / y / z|相对位置，m|
|Motion.Velocity.X/Y/Z|TARGET_SCENE[i].vx / vy / vz|m/s|
|Motion.Acceleration.X/Y/Z|TARGET_SCENE[i].ax / ay / az|m/s²|
|Motion.ReleaseTime / Active|TARGET_SCENE[i].release_time / active|s / boolean|
|Geometry.TargetType / ShellThickness|不写入|球壳（固定5mm）；不能配置其他后端能力|
|Mass|不写入|仅显示既有派生质量|
|Settings.Metadata、UseOverride|不写入|GUI编辑信息|
|Settings.SimilarityIndex|不写入forward请求|04场景搜索温度验收百分数，保留默认90及用户设置|

## 5. Mapper 保留/调整说明

没有删除 Mapper、DTO 或已有测试。以下文件全部保留原样：

- Services/Mappers/ForwardSimulationMapper.cs：原部分字段映射留作兼容，复用其 MapVector；不作为正式JSON生成器。
- Services/Mappers/PredictionMapper.cs：原内存结构保留，正式预测输入沿用完整forward请求；其三参数附属属性不是新后端合同。
- Services/Mappers/SceneBuildMapper.cs：保留源请求引用及相似度验收百分数语义；本阶段不生成04外围控制文件。
- Services/Mappers/SimilarityEvaluationMapper.cs、TrajectoryMapper.cs：保留结果引用语义，不生成物理请求、不读取结果。
- Requests/ForwardSimulationRequest.cs、ModuleRequests.cs、Validation/CapabilityValidator.cs：为兼容保留。旧Forward DTO不完整，旧CapabilityValidator不是完整schema/预测适用性验证；正式生成链不依赖它们。
- MapperTest/Program.cs 与 MapperTest.csproj：不改不删，5项测试全部运行通过。

01完整请求已实现。02使用同一份完整请求，不新增预测参数页；默认GUI场景不能据此宣称兼容固定代理场景，页面已有提示。本阶段不实现02训练域/固定场景适用性报告。03仍以结果为输入。04保留温度相似度验收阈值，不将阈值混入01物理请求。

## 6. JSON验证结果

后端schema是自定义描述文件，不含标准JSON Schema的type/properties/required结构；不能使用通用JSON Schema库空验后声称通过。因此验证器直接读取嵌入的原文件，校验分区、字段与类型，再执行入口的关系和数值约束：

- 精确六分区及字段白名单，拒绝缺失和未知字段。
- 数值有限、整数、三分量有限向量、严格布尔值；GUI生成的数值不采用数字字符串。
- TOTAL_TIME>0，NUM_SPHERES>=1；两张目标表均恰好N行，唯一ID完整覆盖1..N。
- r>0.005；rho/cp/t_init>0；release_time>=0；1号目标释放时间容差1e-12且active=true，与入口一致。
- 不把代理0～300W训练域当作01通用输入范围。

默认request.json和修改示例都通过C#验证；PowerShell独立读取根后端schema比对两张表及四个对象的键集合。缺失每一个业务字段、全部缺失分区、未知字段、行数/ID错误、非法数值及布尔表示均有负例测试。没有执行后端validate_request函数或prepare-only；后者还会生成后端中间文件，不属于本次范围。

## 7. 测试结果

|验收项|结果/证据|
|---|---|
|VS2019 Release构建|通过，MSBuild 16 / .NET Framework 4.8；0警告、0错误；build-release.log|
|原Mapper测试|5通过，0失败；mapper-tests.log|
|请求生成/合同/文件测试|114个断言通过；request-tests.log|
|P2页面及新增UI回归|50项通过；ui-results.txt；target-page.png已检查|
|默认请求|已保存request.json，UTF-8无BOM，六分区完整|
|参数变化|changed-request.json独立目标半径/反射率/运动及观测变化正确|
|固定后端参数|模型拒绝其他壳厚；UI无壳厚输入框；无Mass等非法字段|
|必要参数缺失|缺失任务拒绝；六分区及每个必需字段删除后验证失败；可见无效输入显示生成失败|
|覆盖/失败保护|默认拒绝覆盖，显式覆盖成功，失败不破坏已有文件|

UI回归从原P2脚本复制扩展，保留原脚本与其历史输出；验证五页切换、共享任务、覆盖继承、目标增减、绑定错误、窗口布局、新建任务及新增按钮。P2.1旧可变壳厚行为被本次固定5mm需求有意取代，旧测试文件仍保留，没有将可变壳厚断言冒称通过。保存对话框的人工交互未自动操作；保存服务路径与覆盖分支已自动验证。

## 8. Git diff与冻结检查

本轮既有源码/说明共调整6个文件、新增3个生产文件，清单见第2节；原Mapper和测试源码哈希不变。全工作区tracked diff快照为13个文件、416行新增、1308行删除，包含用户先前的.docs、P1/P2/P3.2修改，不能把整份快照当作本次差异。新增未跟踪服务/视图等不会完整显示在普通git diff中，使用before/after SHA清单结合新增文件清单验收。

SHA-256复核：coreprogram 119个文件全部一致，GUI_MFC_Legacy 1743个文件全部一致，.docs 9个文件全部一致。采用只读共享文件流读取被占用文档，未关闭用户文件。具体每文件哈希见before_hashes.csv与after_hashes.csv；汇总见protected-check.json。另检查目录文件数量以发现新增文件，Git两个冻结目录的tracked diff和untracked列表均为空。

后端schema原文件和GUI嵌入副本SHA-256相同：F4D19F1F517626BF2403F9F2B6A458363645A7A7E9E94F09ECF07919A3196C08，未变更forward-request-v1定义。git diff --check最终通过。

## 9. 停止边界

本阶段完成并停止。未推进ProcessManager、后端运行、预测执行、结果解析或结果展示逻辑。请求生成成功只表示输入合同有效，不等同于后端数值计算成功或02/04固定场景适用性通过。

