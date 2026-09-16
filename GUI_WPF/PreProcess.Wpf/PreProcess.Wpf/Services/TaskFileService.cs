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
        public const string SchemaVersion = "preprocess-task-v1";

        public void Save(TaskModel task, string path)
        {
            if (task == null) throw new ArgumentNullException("task");
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("任务文件路径不能为空。", "path");
            string fullPath = Path.GetFullPath(path);
            string parent = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(parent)) throw new DirectoryNotFoundException("任务文件目录不存在：" + parent);
            string temporary = Path.Combine(parent, ".task-" + Guid.NewGuid().ToString("N") + ".tmp");
            string json = Serializer().Serialize(CreateDocument(task));
            try
            {
                File.WriteAllText(temporary, json + Environment.NewLine, new UTF8Encoding(false));
                if (File.Exists(fullPath)) File.Replace(temporary, fullPath, null);
                else File.Move(temporary, fullPath);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        public TaskModel Load(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("任务文件路径不能为空。", "path");
            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath)) throw new FileNotFoundException("任务文件不存在。", fullPath);
            TaskDocument document;
            try { document = Serializer().Deserialize<TaskDocument>(File.ReadAllText(fullPath, Encoding.UTF8)); }
            catch (Exception ex) { throw new InvalidDataException("任务文件不是有效 JSON：" + ex.Message, ex); }
            if (document == null || document.SchemaVersion != SchemaVersion)
                throw new InvalidDataException("任务文件版本不受支持，应为 " + SchemaVersion + "。");
            try { return CreateTask(document); }
            catch (Exception ex) when (!(ex is InvalidDataException))
            { throw new InvalidDataException("任务文件参数无效：" + ex.Message, ex); }
        }

        private static TaskDocument CreateDocument(TaskModel task)
        {
            var targets = new List<TargetDocument>();
            foreach (TargetInstance target in task.IndividualTargets)
                targets.Add(new TargetDocument { Id = target.Id, UseOverride = target.UseOverride,
                    Physics = target.UseOverride ? PhysicsDocument.From(target.EffectivePhysics) : null,
                    Motion = MotionDocument.From(target.Motion) });
            return new TaskDocument
            {
                SchemaVersion = SchemaVersion,
                Metadata = new MetadataDocument { Name = task.Settings.Metadata.Name, Description = task.Settings.Metadata.Description },
                Duration = task.Settings.Duration,
                TargetCount = task.Settings.TargetCount,
                SimilarityIndex = task.Settings.SimilarityIndex,
                UniformTarget = PhysicsDocument.From(task.Targets.Uniform),
                Targets = targets,
                Scene = SceneDocument.From(task.Scene),
                Environment = EnvironmentDocument.From(task.Environment)
            };
        }

        private static TaskModel CreateTask(TaskDocument document)
        {
            if (document.Metadata == null || document.UniformTarget == null || document.Targets == null ||
                document.Scene == null || document.Environment == null)
                throw new InvalidDataException("任务文件缺少必要分区。");
            if (document.Targets.Count != document.TargetCount) throw new InvalidDataException("目标列表数量与 TargetCount 不一致。");
            var task = new TaskModel();
            task.Settings.Metadata.Name = document.Metadata.Name ?? String.Empty;
            task.Settings.Metadata.Description = document.Metadata.Description ?? String.Empty;
            task.Settings.Duration = document.Duration;
            double similarity = document.SimilarityIndex;
            if (similarity < 50 || similarity > 100)
            {
                string legacy = document.Scene.SimilarityLevel;
                if (!Double.TryParse(legacy, NumberStyles.Float, CultureInfo.InvariantCulture, out similarity) || similarity < 50 || similarity > 100)
                    similarity = 90;
            }
            task.Settings.SimilarityIndex = similarity;
            Apply(document.UniformTarget, task.Targets.Uniform);
            task.Settings.TargetCount = document.TargetCount;
            Apply(document.Scene, task.Scene);
            Apply(document.Environment, task.Environment);
            for (int i = 0; i < task.IndividualTargets.Count; i++)
            {
                TargetDocument source = document.Targets[i];
                TargetInstance target = task.IndividualTargets[i];
                if (source == null || source.Id != target.Id || source.Motion == null)
                    throw new InvalidDataException("目标列表必须按连续编号保存并包含运动参数。");
                target.UseOverride = source.UseOverride;
                if (source.UseOverride)
                {
                    if (source.Physics == null) throw new InvalidDataException("启用独立覆盖的目标缺少物性参数：" + source.Id);
                    Apply(source.Physics, target.EffectivePhysics);
                }
                Apply(source.Motion, target.Motion);
            }
            return task;
        }

        private static void Apply(PhysicsDocument source, TargetPhysics target)
        {
            if (source.TargetType != TargetType.SphericalShell.ToString()) throw new InvalidDataException("当前仅支持球壳目标。");
            target.Geometry.TargetType = TargetType.SphericalShell;
            target.Geometry.ShellThickness = source.ShellThickness;
            target.Radius = source.Radius; target.Density = source.Density; target.HeatCapacity = source.HeatCapacity;
            target.InitialTemperature = source.InitialTemperature; target.InternalPower = source.InternalPower;
            target.Emissivity = source.Emissivity; target.SolarAbsorption = source.SolarAbsorption; target.IrReflection = source.IrReflection;
        }
        private static void Apply(MotionDocument source, TargetMotionSettings target)
        { Apply(source.Position, target.Position); Apply(source.Velocity, target.Velocity); Apply(source.Acceleration, target.Acceleration); target.ReleaseTime = source.ReleaseTime; target.Active = source.Active; }
        private static void Apply(SceneDocument source, SceneMotionSettings target)
        {
            Apply(source.Position, target.Position); Apply(source.Direction, target.Direction); Apply(source.Up, target.Up); Apply(source.Velocity, target.Velocity);
            Apply(source.AngularVelocity, target.AngularVelocity); Apply(source.AngularAcceleration, target.AngularAcceleration);
            if (source.MicroMotionParameters != null) Apply(source.MicroMotionParameters, target.MicroMotionParameters);
            if (!String.IsNullOrWhiteSpace(source.CompanionType)) target.CompanionType = source.CompanionType;
            if (!String.IsNullOrWhiteSpace(source.AttitudeMotionType)) target.AttitudeMotionType = source.AttitudeMotionType;
        }
        private static void Apply(EnvironmentDocument source, EnvironmentObservationSettings target)
        {
            target.SolarFlux = source.SolarFlux; target.RadiationTemperature = source.RadiationTemperature;
            target.ApertureSize = source.ApertureSize; target.FocalLength = source.FocalLength; target.PlaneSize = source.PlaneSize;
            Apply(source.SunDirection, target.SunDirection); Apply(source.ObserverPosition, target.ObserverPosition);
            Apply(source.ObserverDirection, target.ObserverDirection); Apply(source.ObserverUp, target.ObserverUp);
            Apply(source.ObserverVelocity, target.ObserverVelocity); Apply(source.ObserverAngularVelocity, target.ObserverAngularVelocity);
            Apply(source.ObserverAngularAcceleration, target.ObserverAngularAcceleration); Apply(source.DetectorDirection, target.DetectorDirection);
            Apply(source.DetectorUp, target.DetectorUp); Apply(source.DetectorVelocity, target.DetectorVelocity);
            Apply(source.DetectorAngularVelocity, target.DetectorAngularVelocity); Apply(source.DetectorAngularAcceleration, target.DetectorAngularAcceleration);
            target.ApertureTracking = source.ApertureTracking; target.DetectorTracking = source.DetectorTracking;
        }
        private static void Apply(VectorDocument source, Vector3 target)
        { if (source == null) throw new InvalidDataException("任务文件缺少三分量向量。"); target.X = source.X; target.Y = source.Y; target.Z = source.Z; }
        private static JavaScriptSerializer Serializer() => new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024, RecursionLimit = 256 };

        public sealed class TaskDocument
        {
            public string SchemaVersion { get; set; }
            public MetadataDocument Metadata { get; set; }
            public double Duration { get; set; }
            public int TargetCount { get; set; }
            public double SimilarityIndex { get; set; }
            public PhysicsDocument UniformTarget { get; set; }
            public List<TargetDocument> Targets { get; set; }
            public SceneDocument Scene { get; set; }
            public EnvironmentDocument Environment { get; set; }
        }
        public sealed class MetadataDocument { public string Name { get; set; } public string Description { get; set; } }
        public sealed class VectorDocument
        {
            public double X { get; set; } public double Y { get; set; } public double Z { get; set; }
            public static VectorDocument From(Vector3 value) => new VectorDocument { X = value.X, Y = value.Y, Z = value.Z };
        }
        public sealed class PhysicsDocument
        {
            public string TargetType { get; set; } public double Radius { get; set; } public double ShellThickness { get; set; }
            public double Density { get; set; } public double HeatCapacity { get; set; } public double InitialTemperature { get; set; }
            public double InternalPower { get; set; } public double Emissivity { get; set; } public double SolarAbsorption { get; set; } public double IrReflection { get; set; }
            public static PhysicsDocument From(TargetPhysics value) => new PhysicsDocument { TargetType = value.Geometry.TargetType.ToString(), Radius = value.Radius,
                ShellThickness = value.Geometry.ShellThickness, Density = value.Density, HeatCapacity = value.HeatCapacity,
                InitialTemperature = value.InitialTemperature, InternalPower = value.InternalPower, Emissivity = value.Emissivity,
                SolarAbsorption = value.SolarAbsorption, IrReflection = value.IrReflection };
        }
        public sealed class MotionDocument
        {
            public VectorDocument Position { get; set; } public VectorDocument Velocity { get; set; } public VectorDocument Acceleration { get; set; }
            public double ReleaseTime { get; set; } public bool Active { get; set; }
            public static MotionDocument From(TargetMotionSettings value) => new MotionDocument { Position = VectorDocument.From(value.Position),
                Velocity = VectorDocument.From(value.Velocity), Acceleration = VectorDocument.From(value.Acceleration), ReleaseTime = value.ReleaseTime, Active = value.Active };
        }
        public sealed class TargetDocument { public int Id { get; set; } public bool UseOverride { get; set; } public PhysicsDocument Physics { get; set; } public MotionDocument Motion { get; set; } }
        public sealed class SceneDocument
        {
            public VectorDocument Position { get; set; } public VectorDocument Direction { get; set; } public VectorDocument Up { get; set; }
            public VectorDocument Velocity { get; set; } public VectorDocument AngularVelocity { get; set; } public VectorDocument AngularAcceleration { get; set; }
            public string CompanionType { get; set; } public string AttitudeMotionType { get; set; } public VectorDocument MicroMotionParameters { get; set; }
            // Legacy read compatibility: early development builds stored this target parameter under Scene.
            public string SimilarityLevel { get; set; }
            public static SceneDocument From(SceneMotionSettings value) => new SceneDocument { Position = VectorDocument.From(value.Position), Direction = VectorDocument.From(value.Direction),
                Up = VectorDocument.From(value.Up), Velocity = VectorDocument.From(value.Velocity), AngularVelocity = VectorDocument.From(value.AngularVelocity), AngularAcceleration = VectorDocument.From(value.AngularAcceleration),
                CompanionType = value.CompanionType, AttitudeMotionType = value.AttitudeMotionType, MicroMotionParameters = VectorDocument.From(value.MicroMotionParameters) };
        }
        public sealed class EnvironmentDocument
        {
            public double SolarFlux { get; set; } public double RadiationTemperature { get; set; } public double ApertureSize { get; set; }
            public double FocalLength { get; set; } public double PlaneSize { get; set; }
            public VectorDocument SunDirection { get; set; } public VectorDocument ObserverPosition { get; set; } public VectorDocument ObserverDirection { get; set; }
            public VectorDocument ObserverUp { get; set; } public VectorDocument ObserverVelocity { get; set; } public VectorDocument ObserverAngularVelocity { get; set; }
            public VectorDocument ObserverAngularAcceleration { get; set; } public VectorDocument DetectorDirection { get; set; } public VectorDocument DetectorUp { get; set; }
            public VectorDocument DetectorVelocity { get; set; } public VectorDocument DetectorAngularVelocity { get; set; } public VectorDocument DetectorAngularAcceleration { get; set; }
            public bool ApertureTracking { get; set; } public bool DetectorTracking { get; set; }
            public static EnvironmentDocument From(EnvironmentObservationSettings value) => new EnvironmentDocument { SolarFlux = value.SolarFlux,
                RadiationTemperature = value.RadiationTemperature, ApertureSize = value.ApertureSize, FocalLength = value.FocalLength, PlaneSize = value.PlaneSize,
                SunDirection = VectorDocument.From(value.SunDirection), ObserverPosition = VectorDocument.From(value.ObserverPosition), ObserverDirection = VectorDocument.From(value.ObserverDirection),
                ObserverUp = VectorDocument.From(value.ObserverUp), ObserverVelocity = VectorDocument.From(value.ObserverVelocity), ObserverAngularVelocity = VectorDocument.From(value.ObserverAngularVelocity),
                ObserverAngularAcceleration = VectorDocument.From(value.ObserverAngularAcceleration), DetectorDirection = VectorDocument.From(value.DetectorDirection),
                DetectorUp = VectorDocument.From(value.DetectorUp), DetectorVelocity = VectorDocument.From(value.DetectorVelocity), DetectorAngularVelocity = VectorDocument.From(value.DetectorAngularVelocity),
                DetectorAngularAcceleration = VectorDocument.From(value.DetectorAngularAcceleration), ApertureTracking = value.ApertureTracking, DetectorTracking = value.DetectorTracking };
        }
    }
}
