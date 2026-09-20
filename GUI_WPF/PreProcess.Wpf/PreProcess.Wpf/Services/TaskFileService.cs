using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using PreProcess.Wpf.Models;

namespace PreProcess.Wpf.Services
{
    public sealed class TaskFileService
    {
        // 保存该组件运行所需的配置或中间状态。
        public const string SchemaVersion = "preprocess-task-v1";

        public void Save(TaskModel task, string path)
        {
            if (task == null) throw new ArgumentNullException("task");
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("任务文件路径不能为空。", "path");
            string fullPath = Path.GetFullPath(path);
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string parent = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(parent)) throw new DirectoryNotFoundException("任务文件目录不存在：" + parent);
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string temporary = Path.Combine(parent, ".task-" + Guid.NewGuid().ToString("N") + ".tmp");
            string json = Serializer().Serialize(CreateDocument(task));
            try
            {
                // 处理文件系统路径及数据，并在使用前确认目标有效。
                File.WriteAllText(temporary, json + Environment.NewLine, new UTF8Encoding(false));
                if (File.Exists(fullPath)) File.Replace(temporary, fullPath, null);
                // 当前置条件不成立时执行备用路径，保持处理结果完整。
                else File.Move(temporary, fullPath);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public TaskModel Load(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("任务文件路径不能为空。", "path");
            string fullPath = Path.GetFullPath(path);
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (!File.Exists(fullPath)) throw new FileNotFoundException("任务文件不存在。", fullPath);
            TaskDocument document;
            // 进入可能失败的操作区间，并由后续异常分支统一处理故障。
            try { document = Serializer().Deserialize<TaskDocument>(File.ReadAllText(fullPath, Encoding.UTF8)); }
            catch (Exception ex) { throw new InvalidDataException("任务文件不是有效 JSON：" + ex.Message, ex); }
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (document == null || document.SchemaVersion != SchemaVersion)
                throw new InvalidDataException("任务文件版本不受支持，应为 " + SchemaVersion + "。");
            try { return CreateTask(document); }
            // 捕获本步骤产生的异常，将故障转换为可追踪的运行状态。
            catch (Exception ex) when (!(ex is InvalidDataException))
            { throw new InvalidDataException("任务文件参数无效：" + ex.Message, ex); }
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static TaskDocument CreateDocument(TaskModel task)
        {
            var targets = new List<TargetDocument>();
            // 遍历当前数据集合，逐项完成必要的转换或状态更新。
            foreach (TargetInstance target in task.IndividualTargets)
                targets.Add(new TargetDocument { Id = target.Id, UseOverride = target.UseOverride,
                    Physics = target.UseOverride ? PhysicsDocument.From(target.EffectivePhysics) : null,
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    Motion = MotionDocument.From(target.Motion) });
            return new TaskDocument
            {
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                SchemaVersion = SchemaVersion,
                Metadata = new MetadataDocument { Name = task.Settings.Metadata.Name, Description = task.Settings.Metadata.Description },
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                Duration = task.Settings.Duration,
                TargetCount = task.Settings.TargetCount,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                SimilarityIndex = task.Settings.SimilarityIndex,
                UniformTarget = PhysicsDocument.From(task.Targets.Uniform),
                Targets = targets,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                Scene = SceneDocument.From(task.Scene),
                Environment = EnvironmentDocument.From(task.Environment)
            };
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static TaskModel CreateTask(TaskDocument document)
        {
            if (document.Metadata == null || document.UniformTarget == null || document.Targets == null ||
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                document.Scene == null || document.Environment == null)
                throw new InvalidDataException("任务文件缺少必要分区。");
            if (document.Targets.Count != document.TargetCount) throw new InvalidDataException("目标列表数量与 TargetCount 不一致。");
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var task = new TaskModel();
            task.Settings.Metadata.Name = document.Metadata.Name ?? String.Empty;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            task.Settings.Metadata.Description = document.Metadata.Description ?? String.Empty;
            task.Settings.Duration = document.Duration;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            double similarity = document.SimilarityIndex;
            if (similarity < 50 || similarity > 100)
            {
                string legacy = document.Scene.SimilarityLevel;
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (!Double.TryParse(legacy, NumberStyles.Float, CultureInfo.InvariantCulture, out similarity) || similarity < 50 || similarity > 100)
                    similarity = 90;
            }
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            task.Settings.SimilarityIndex = similarity;
            Apply(document.UniformTarget, task.Targets.Uniform);
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            task.Settings.TargetCount = document.TargetCount;
            Apply(document.Scene, task.Scene);
            Apply(document.Environment, task.Environment);
            // 遍历当前数据集合，逐项完成必要的转换或状态更新。
            for (int i = 0; i < task.IndividualTargets.Count; i++)
            {
                TargetDocument source = document.Targets[i];
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                TargetInstance target = task.IndividualTargets[i];
                if (source == null || source.Id != target.Id || source.Motion == null)
                    // 发现无效输入或运行状态后立即终止，防止错误继续传播。
                    throw new InvalidDataException("目标列表必须按连续编号保存并包含运动参数。");
                target.UseOverride = source.UseOverride;
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (source.UseOverride)
                {
                    if (source.Physics == null) throw new InvalidDataException("启用独立覆盖的目标缺少物性参数：" + source.Id);
                    Apply(source.Physics, target.EffectivePhysics);
                }
                // 调用对应组件完成当前步骤，并保留产生的处理结果。
                Apply(source.Motion, target.Motion);
            }
            return task;
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static void Apply(PhysicsDocument source, TargetPhysics target)
        {
            if (source.TargetType != TargetType.SphericalShell.ToString()) throw new InvalidDataException("当前仅支持球壳目标。");
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            target.Geometry.TargetType = TargetType.SphericalShell;
            target.Geometry.ShellThickness = source.ShellThickness;
            target.Radius = source.Radius; target.Density = source.Density; target.HeatCapacity = source.HeatCapacity;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            target.InitialTemperature = source.InitialTemperature; target.InternalPower = source.InternalPower;
            target.Emissivity = source.Emissivity; target.SolarAbsorption = source.SolarAbsorption; target.IrReflection = source.IrReflection;
        }
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static void Apply(MotionDocument source, TargetMotionSettings target)
        { Apply(source.Position, target.Position); Apply(source.Velocity, target.Velocity); Apply(source.Acceleration, target.Acceleration); target.ReleaseTime = source.ReleaseTime; target.Active = source.Active; }
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static void Apply(SceneDocument source, SceneMotionSettings target)
        {
            Apply(source.Position, target.Position); Apply(source.Direction, target.Direction); Apply(source.Up, target.Up); Apply(source.Velocity, target.Velocity);
            Apply(source.AngularVelocity, target.AngularVelocity); Apply(source.AngularAcceleration, target.AngularAcceleration);
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (source.MicroMotionParameters != null) Apply(source.MicroMotionParameters, target.MicroMotionParameters);
            if (!String.IsNullOrWhiteSpace(source.CompanionType)) target.CompanionType = source.CompanionType;
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (!String.IsNullOrWhiteSpace(source.AttitudeMotionType)) target.AttitudeMotionType = source.AttitudeMotionType;
        }
        private static void Apply(EnvironmentDocument source, EnvironmentObservationSettings target)
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            target.SolarFlux = source.SolarFlux; target.RadiationTemperature = source.RadiationTemperature;
            target.ApertureSize = source.ApertureSize; target.FocalLength = source.FocalLength; target.PlaneSize = source.PlaneSize;
            Apply(source.SunDirection, target.SunDirection); Apply(source.ObserverPosition, target.ObserverPosition);
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            Apply(source.ObserverDirection, target.ObserverDirection); Apply(source.ObserverUp, target.ObserverUp);
            Apply(source.ObserverVelocity, target.ObserverVelocity); Apply(source.ObserverAngularVelocity, target.ObserverAngularVelocity);
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            Apply(source.ObserverAngularAcceleration, target.ObserverAngularAcceleration); Apply(source.DetectorDirection, target.DetectorDirection);
            Apply(source.DetectorUp, target.DetectorUp); Apply(source.DetectorVelocity, target.DetectorVelocity);
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            Apply(source.DetectorAngularVelocity, target.DetectorAngularVelocity); Apply(source.DetectorAngularAcceleration, target.DetectorAngularAcceleration);
            target.ApertureTracking = source.ApertureTracking; target.DetectorTracking = source.DetectorTracking;
        }
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static void Apply(VectorDocument source, Vector3 target)
        { if (source == null) throw new InvalidDataException("任务文件缺少三分量向量。"); target.X = source.X; target.Y = source.Y; target.Z = source.Z; }
        private static JavaScriptSerializer Serializer() => new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024, RecursionLimit = 256 };

        // 定义 TaskDocument 类型，集中封装与该领域对象相关的状态和行为。
        public sealed class TaskDocument
        {
            public string SchemaVersion { get; set; }
            // 通过属性封装状态访问，并在变更时执行必要的同步操作。
            public MetadataDocument Metadata { get; set; }
            public double Duration { get; set; }
            // 通过属性封装状态访问，并在变更时执行必要的同步操作。
            public int TargetCount { get; set; }
            public double SimilarityIndex { get; set; }
            public PhysicsDocument UniformTarget { get; set; }
            // 通过属性封装状态访问，并在变更时执行必要的同步操作。
            public List<TargetDocument> Targets { get; set; }
            public SceneDocument Scene { get; set; }
            // 通过属性封装状态访问，并在变更时执行必要的同步操作。
            public EnvironmentDocument Environment { get; set; }
        }
        public sealed class MetadataDocument { public string Name { get; set; } public string Description { get; set; } }
        // 定义 VectorDocument 类型，集中封装与该领域对象相关的状态和行为。
        public sealed class VectorDocument
        {
            public double X { get; set; } public double Y { get; set; } public double Z { get; set; }
            public static VectorDocument From(Vector3 value) => new VectorDocument { X = value.X, Y = value.Y, Z = value.Z };
        }
        // 定义 PhysicsDocument 类型，集中封装与该领域对象相关的状态和行为。
        public sealed class PhysicsDocument
        {
            public string TargetType { get; set; } public double Radius { get; set; } public double ShellThickness { get; set; }
            // 通过属性封装状态访问，并在变更时执行必要的同步操作。
            public double Density { get; set; } public double HeatCapacity { get; set; } public double InitialTemperature { get; set; }
            public double InternalPower { get; set; } public double Emissivity { get; set; } public double SolarAbsorption { get; set; } public double IrReflection { get; set; }
            // 将外部数据转换为目标类型，并保持约定的表示格式。
            public static PhysicsDocument From(TargetPhysics value) => new PhysicsDocument { TargetType = value.Geometry.TargetType.ToString(), Radius = value.Radius,
                ShellThickness = value.Geometry.ShellThickness, Density = value.Density, HeatCapacity = value.HeatCapacity,
                InitialTemperature = value.InitialTemperature, InternalPower = value.InternalPower, Emissivity = value.Emissivity,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                SolarAbsorption = value.SolarAbsorption, IrReflection = value.IrReflection };
        }
        public sealed class MotionDocument
        {
            // 通过属性封装状态访问，并在变更时执行必要的同步操作。
            public VectorDocument Position { get; set; } public VectorDocument Velocity { get; set; } public VectorDocument Acceleration { get; set; }
            public double ReleaseTime { get; set; } public bool Active { get; set; }
            // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
            public static MotionDocument From(TargetMotionSettings value) => new MotionDocument { Position = VectorDocument.From(value.Position),
                Velocity = VectorDocument.From(value.Velocity), Acceleration = VectorDocument.From(value.Acceleration), ReleaseTime = value.ReleaseTime, Active = value.Active };
        }
        // 定义 TargetDocument 类型，集中封装与该领域对象相关的状态和行为。
        public sealed class TargetDocument { public int Id { get; set; } public bool UseOverride { get; set; } public PhysicsDocument Physics { get; set; } public MotionDocument Motion { get; set; } }
        public sealed class SceneDocument
        {
            public VectorDocument Position { get; set; } public VectorDocument Direction { get; set; } public VectorDocument Up { get; set; }
            // 通过属性封装状态访问，并在变更时执行必要的同步操作。
            public VectorDocument Velocity { get; set; } public VectorDocument AngularVelocity { get; set; } public VectorDocument AngularAcceleration { get; set; }
            public string CompanionType { get; set; } public string AttitudeMotionType { get; set; } public VectorDocument MicroMotionParameters { get; set; }
            // Legacy read compatibility: early development builds stored this target parameter under Scene.
            // 通过属性封装状态访问，并在变更时执行必要的同步操作。
            public string SimilarityLevel { get; set; }
            public static SceneDocument From(SceneMotionSettings value) => new SceneDocument { Position = VectorDocument.From(value.Position), Direction = VectorDocument.From(value.Direction),
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                Up = VectorDocument.From(value.Up), Velocity = VectorDocument.From(value.Velocity), AngularVelocity = VectorDocument.From(value.AngularVelocity), AngularAcceleration = VectorDocument.From(value.AngularAcceleration),
                CompanionType = value.CompanionType, AttitudeMotionType = value.AttitudeMotionType, MicroMotionParameters = VectorDocument.From(value.MicroMotionParameters) };
        }
        public sealed class EnvironmentDocument
        {
            // 通过属性封装状态访问，并在变更时执行必要的同步操作。
            public double SolarFlux { get; set; } public double RadiationTemperature { get; set; } public double ApertureSize { get; set; }
            public double FocalLength { get; set; } public double PlaneSize { get; set; }
            // 通过属性封装状态访问，并在变更时执行必要的同步操作。
            public VectorDocument SunDirection { get; set; } public VectorDocument ObserverPosition { get; set; } public VectorDocument ObserverDirection { get; set; }
            public VectorDocument ObserverUp { get; set; } public VectorDocument ObserverVelocity { get; set; } public VectorDocument ObserverAngularVelocity { get; set; }
            // 通过属性封装状态访问，并在变更时执行必要的同步操作。
            public VectorDocument ObserverAngularAcceleration { get; set; } public VectorDocument DetectorDirection { get; set; } public VectorDocument DetectorUp { get; set; }
            public VectorDocument DetectorVelocity { get; set; } public VectorDocument DetectorAngularVelocity { get; set; } public VectorDocument DetectorAngularAcceleration { get; set; }
            public bool ApertureTracking { get; set; } public bool DetectorTracking { get; set; }
            // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
            public static EnvironmentDocument From(EnvironmentObservationSettings value) => new EnvironmentDocument { SolarFlux = value.SolarFlux,
                RadiationTemperature = value.RadiationTemperature, ApertureSize = value.ApertureSize, FocalLength = value.FocalLength, PlaneSize = value.PlaneSize,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                SunDirection = VectorDocument.From(value.SunDirection), ObserverPosition = VectorDocument.From(value.ObserverPosition), ObserverDirection = VectorDocument.From(value.ObserverDirection),
                ObserverUp = VectorDocument.From(value.ObserverUp), ObserverVelocity = VectorDocument.From(value.ObserverVelocity), ObserverAngularVelocity = VectorDocument.From(value.ObserverAngularVelocity),
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                ObserverAngularAcceleration = VectorDocument.From(value.ObserverAngularAcceleration), DetectorDirection = VectorDocument.From(value.DetectorDirection),
                DetectorUp = VectorDocument.From(value.DetectorUp), DetectorVelocity = VectorDocument.From(value.DetectorVelocity), DetectorAngularVelocity = VectorDocument.From(value.DetectorAngularVelocity),
                DetectorAngularAcceleration = VectorDocument.From(value.DetectorAngularAcceleration), ApertureTracking = value.ApertureTracking, DetectorTracking = value.DetectorTracking };
        }
    }
}
