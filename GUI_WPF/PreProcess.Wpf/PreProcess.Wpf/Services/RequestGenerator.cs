using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using PreProcess.Wpf.Models;
using PreProcess.Wpf.Services.Mappers;

namespace PreProcess.Wpf.Services
{
    // Only the six-section backend input is serialized, never the GUI aggregate or legacy DTOs.
    public sealed class RequestGenerator
    {
        public string Generate(TaskModel task)
        {
            if (task == null) throw new ArgumentException("缺少任务配置。");
            var e = task.Environment;
            var s = task.Scene;
            var physics = new List<object>();
            var scene = new List<object>();
            foreach (var target in task.IndividualTargets)
            {
                if (target == null || target.EffectivePhysics == null || target.Motion == null)
                    throw new ArgumentException("缺少目标物性或运动参数。");
                var p = target.EffectivePhysics;
                var m = target.Motion;
                if (p.Geometry.TargetType != TargetType.SphericalShell || p.Geometry.ShellThickness != 5)
                    throw new ArgumentException("后端仅支持球壳（固定5mm）。");
                physics.Add(Obj("id", target.Id, "r", p.Radius, "rho", p.Density, "cp", p.HeatCapacity,
                    "eps_ir", p.Emissivity, "alpha_s", p.SolarAbsorption, "rho_ir", p.IrReflection,
                    "q_int", p.InternalPower, "t_init", p.InitialTemperature));
                scene.Add(Obj("id", target.Id, "x", m.Position.X, "y", m.Position.Y, "z", m.Position.Z,
                    "vx", m.Velocity.X, "vy", m.Velocity.Y, "vz", m.Velocity.Z,
                    "ax", m.Acceleration.X, "ay", m.Acceleration.Y, "az", m.Acceleration.Z,
                    "release_time", m.ReleaseTime, "active", m.Active));
            }
            var request = Obj(
                "CASE", Obj("TOTAL_TIME", task.Settings.Duration, "NUM_SPHERES", task.Settings.TargetCount),
                "ENVIRONMENT", Obj("SOLAR_FLUX", e.SolarFlux, "SOLAR_DIRECTION", V(e.SunDirection), "ENVIRONMENT_TEMP", e.RadiationTemperature),
                "GROUP_STATE", Obj("GROUP_CENTER", V(s.Position), "GROUP_NORMAL", V(s.Direction), "GROUP_UP", V(s.Up),
                    "GROUP_VELOCITY", V(s.Velocity), "GROUP_ANGULAR_VELOCITY", V(s.AngularVelocity), "GROUP_ANGULAR_ACCELERATION", V(s.AngularAcceleration),
                    "COMPANION_TYPE", BackendCompanionType(s.CompanionType), "ATTITUDE_MOTION_TYPE", BackendAttitudeMotionType(s.AttitudeMotionType),
                    "MICRO_MOTION_PARAMS", V(s.MicroMotionParameters), "SIMILARITY_LEVEL", task.Settings.SimilarityIndex.ToString("0.########", CultureInfo.InvariantCulture)),
                "OBSERVATION", Obj("APERTURE_SIZE", e.ApertureSize, "APERTURE_CENTER", V(e.ObserverPosition),
                    "APERTURE_NORMAL", V(e.ObserverDirection), "APERTURE_UP", V(e.ObserverUp), "APERTURE_VELOCITY", V(e.ObserverVelocity),
                    "APERTURE_ANGULAR_VELOCITY", V(e.ObserverAngularVelocity), "APERTURE_ANGULAR_ACCELERATION", V(e.ObserverAngularAcceleration),
                    "APERTURE_TRACK_TARGET", e.ApertureTracking, "DETECTOR_NORMAL", V(e.DetectorDirection), "DETECTOR_UP", V(e.DetectorUp),
                    "DETECTOR_VELOCITY", V(e.DetectorVelocity), "DETECTOR_ANGULAR_VELOCITY", V(e.DetectorAngularVelocity),
                    "DETECTOR_ANGULAR_ACCELERATION", V(e.DetectorAngularAcceleration), "DETECTOR_TRACK_TARGET", e.DetectorTracking,
                    "SPOT_PLANE_SIZE", e.PlaneSize, "SPOT_FOCAL_LENGTH", e.FocalLength),
                "TARGET_PHYSICS", physics, "TARGET_SCENE", scene);
            string json = Serializer().Serialize(request);
            ValidateJson(json);
            return json;
        }

        public void ValidateJson(string json) { ForwardRequestValidator.Validate(json); }

        public string Save(TaskModel task, string path, bool overwrite = false)
        {
            string json = Generate(task); // Complete validation before touching the destination.
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("请指定 request.json 文件路径。");
            string fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath) && !overwrite) throw new IOException("请求文件已存在，请确认覆盖或选择其他路径。");
            string temporary = Path.Combine(Path.GetDirectoryName(fullPath), ".request-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(temporary, json + Environment.NewLine, new UTF8Encoding(false));
                if (overwrite && File.Exists(fullPath)) File.Replace(temporary, fullPath, null);
                else File.Move(temporary, fullPath); // Also rejects an unapproved concurrent creation.
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return fullPath;
        }

        internal static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 }; }
        private static double[] V(Vector3 value)
        {
            if (value == null) throw new ArgumentException("缺少三分量向量。");
            return ForwardSimulationMapper.MapVector(value);
        }
        private static string BackendCompanionType(string value)
        {
            if (String.Equals(value, "球壳", StringComparison.Ordinal) || String.Equals(value, "SPHERE", StringComparison.OrdinalIgnoreCase)) return "SPHERE";
            throw new ArgumentException("当前红外伴飞物类型仅支持球壳。");
        }
        private static string BackendAttitudeMotionType(string value)
        {
            return String.Equals(value, "默认", StringComparison.Ordinal) ? "NONE" : value;
        }
        private static Dictionary<string, object> Obj(params object[] pairs)
        {
            var result = new Dictionary<string, object>();
            for (int i = 0; i < pairs.Length; i += 2) result.Add((string)pairs[i], pairs[i + 1]);
            return result;
        }
    }
}
