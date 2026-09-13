# P3.2 GUI TaskModel 到 Module Request Model 映射报告

日期：2026-09-12。

## 1. 范围与结论

本阶段只修改 `GUI_WPF/**`。已实现五个 Mapper、把 P3.1 Request Model 与 CapabilityValidator 以 VS2019 可编译语法并入 WPF 工程，并建立独立的 `MapperTest` 控制台测试工程。

本阶段没有修改或调用 `coreprogram/**`、`GUI_MFC_Legacy/**`，没有实现 ProcessManager，没有生成 JSON 或正式运行 request，也没有启动任何计算程序。

调用责任保持为：

```text
GUI Model -> Mapper -> Module Request Model -> CapabilityValidator
```

Mapper 只负责投影数据，不包含能力校验。ViewModel 没有新增 Request 生成逻辑；Request Model 也不引用 WPF 页面或 ViewModel。

## 2. Mapper 文件列表

- `Services/Mappers/ForwardSimulationMapper.cs`
- `Services/Mappers/PredictionMapper.cs`
- `Services/Mappers/TrajectoryMapper.cs`
- `Services/Mappers/SimilarityEvaluationMapper.cs`
- `Services/Mappers/SceneBuildMapper.cs`

配套集成文件：

- `Requests/ForwardSimulationRequest.cs`
- `Requests/ModuleRequests.cs`
- `Validation/CapabilityValidator.cs`
- `MapperTest/MapperTest.csproj`
- `MapperTest/Program.cs`

## 3. 映射关系

### 3.1 ForwardSimulationMapper

| GUI 来源 | Request 目标 | 已映射内容 |
|---|---|---|
| `TaskSettings` | `CaseRequest` | `Duration -> TotalTimeSeconds`；`TargetCount -> TargetCount` |
| `EnvironmentObservationSettings` | `EnvironmentRequest` | `SolarFlux`；`RadiationTemperature -> EnvironmentTemperature`；`SunDirection -> SolarDirection` |
| `SceneMotionSettings` | `GroupStateRequest` | `Position -> Center`；`Direction`；`Velocity`；`AngularVelocity` |
| `EnvironmentObservationSettings` | `ObservationRequest` | `ObserverPosition -> ApertureCenter`；`DetectorDirection -> DetectorNormal`；`FocalLength` |
| `TargetInstance.EffectivePhysics` | `TargetPhysicsRequest` | `Id`、外半径、密度、比热、内部热源、红外发射率、太阳吸收率、初温 |
| `TargetInstance.Motion` | `TargetSceneRequest` | `Id`、相对位置、分离速度、释放时间、启用状态 |

统一物性与单目标覆盖通过 `EffectivePhysics` 解析；每个目标场景状态独立映射。Mapper 不读取或发送 GUI 派生质量。类型、壳厚、半径、密度与质量的所有权关系仍保留在 GUI Model 中，未通过改密度或手工质量规避冻结球壳语义。

### 3.2 PredictionMapper

P3.1 `PredictionRequest` 要求保留完整 `SourceRequest`，因此该属性由 `ForwardSimulationMapper` 生成。Prediction 自身只新增代理模型明确支持的三个物性字段：`InternalPower`、`EmissivityIr`、`SolarAbsorption`，没有在 Prediction 根对象复制其他 `TargetPhysics` 字段。`Mode` 由调用者显式传入，因为当前 `TaskModel` 没有预测模式字段。

### 3.3 TrajectoryMapper

轨迹是正向结果的后处理输入，不从任务物性重新构造。Mapper 接收 `trajectory_history.csv` 结果引用和 `IncludePrerelease` 选项，生成 `TrajectoryRequest`。

### 3.4 SimilarityEvaluationMapper

只接收参考结果 `ResultReference`、候选结果 `ResultReference` 和 `EvaluationConfig`，生成 `SimilarityEvaluationRequest`。方法签名不接收 `TaskModel`，因此不会输入目标参数、环境参数或物性参数。

### 3.5 SceneBuildMapper

- `TaskSettings.SimilarityIndex -> RequiredSimilarityPercent`，保留百分数值语义，90% 映射为 `90.0`，不是 `0.9`。
- 调用者提供的候选数量映射到 `RequiredCandidateCount`。
- 调用者提供的源请求引用映射到 `SourceRequestFile`。

## 4. Request Model 缺失或尚未确定的字段

遵照本阶段要求，以下字段没有扩展 P3.1 Request Model，只记录待后续设计：

| Request 区域 | GUI 已有但 P3.1 Request 缺失 |
|---|---|
| `GroupStateRequest` | 群参考上方向 `Up`、群角加速度 `AngularAcceleration` |
| `ObservationRequest` | 孔径方向/上方向/速度/角速度/角加速度、孔径跟踪、孔径尺寸；探测器上方向/速度/角速度/角加速度/跟踪；像面尺寸 |
| `TargetPhysicsRequest` | `IrReflection`；`TargetType`、`ShellThickness`、`Mass` 没有合法后端输入字段，其中质量只能保持为 GUI 派生显示值 |
| `TargetSceneRequest` | 单目标加速度 `Acceleration` |
| `PredictionRequest` / Validator | 固定参考场景、统一目标物性和 `rho_ir = 1 - eps_ir` 的完整能力检查尚未由 P3.1 Validator 实现 |
| `TaskModel` | 预测模式、轨迹结果引用/选项、相似度结果引用/评价配置、场景构建源请求文件和候选数量没有 GUI 数据所有者，当前由对应 Mapper 参数显式传入 |

任务名称、说明属于 GUI 元数据，本来就不应进入当前 Request Model。

## 5. 测试结果

`MapperTest` 在 Release 下执行，结果为 5 passed、0 failed：

1. 默认 16 目标球壳任务可生成 `ForwardSimulationRequest`，且 `TargetPhysics` 存在并完整覆盖目标 ID。
2. 修改 2 号目标的物性覆盖和独立运动后，仅对应 `TargetPhysicsRequest` / `TargetSceneRequest` 发生预期变化。
3. `SimilarityIndex = 90%` 映射为 `SceneBuildRequest.RequiredSimilarityPercent = 90.0`，源引用与候选数量同时正确映射。
4. Mapper 不执行校验；随后独立调用 `CapabilityValidator`，可拦截代理内部热源越界和候选数量为零。
5. 相似度与轨迹 Mapper 只使用结果引用及各自选项，不依赖任务物性。

## 6. 编译与回归结果

机器未安装 VS2019。使用便携式 Roslyn 3.11（VS2019 16.11 同代编译器）、.NET Framework 4.8 引用程序集和系统 MSBuild 执行 `Release|Any CPU` 重建：WPF 与 MapperTest 均成功，无编译错误。系统 MSBuild 仅提示本机未安装 ToolsVersion 15，按 ToolsVersion 4 任务宿主执行；通过 `FrameworkPathOverride` 后没有框架或架构引用警告。

与原 ZIP 的源文件逐项比较：P2 的 `Views/**` 无变化，现有 GUI 源文件除项目/解决方案元数据外无变化。实现差异为 12 个文件、534 行新增，其中两个文件为项目/解决方案更新；未改现有页面逻辑。

## 7. 下一阶段建议

在进入序列化或运行控制前，先单独评审并补齐第 4 节的 Request/Validator 缺口，尤其是固定 5 mm 球壳能力、完整 Observation、`rho_ir` 和代理固定参考场景。完成该合同评审后再设计非正式的序列化单元测试；不要直接进入 ProcessManager、后端调用或正式运行 JSON。
