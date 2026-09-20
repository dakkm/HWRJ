using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using PreProcess.Wpf.Models;
using PreProcess.Wpf.Services.Mappers;

namespace PreProcess.Wpf.Services
{
    // Only the six-section backend input is serialized, never the GUI aggregate or legacy DTOs.
    // 定义 RequestGenerator 类型，集中封装与该领域对象相关的状态和行为。
    public sealed class RequestGenerator
    {
        public string Generate(TaskModel task)
        {
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (task == null) throw new ArgumentException("缺少任务配置。");
            var e = task.Environment;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var s = task.Scene;
            var physics = new List<object>();
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var scene = new List<object>();
            foreach (var target in task.IndividualTargets)
            {
                if (target == null || target.EffectivePhysics == null || target.Motion == null)
                    // 发现无效输入或运行状态后立即终止，防止错误继续传播。
                    throw new ArgumentException("缺少目标物性或运动参数。");
                var p = target.EffectivePhysics;
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                var m = target.Motion;
                if (p.Geometry.TargetType != TargetType.SphericalShell || p.Geometry.ShellThickness != 5)
                    // 发现无效输入或运行状态后立即终止，防止错误继续传播。
                    throw new ArgumentException("后端仅支持球壳（固定5mm）。");
                physics.Add(Obj("id", target.Id, "r", p.Radius, "rho", p.Density, "cp", p.HeatCapacity,
                    "eps_ir", p.Emissivity, "alpha_s", p.SolarAbsorption, "rho_ir", p.IrReflection,
                    // 继续处理当前业务步骤，保持上下文状态一致。
                    "q_int", p.InternalPower, "t_init", p.InitialTemperature));
                scene.Add(Obj("id", target.Id, "x", m.Position.X, "y", m.Position.Y, "z", m.Position.Z,
                    // 继续处理当前业务步骤，保持上下文状态一致。
                    "vx", m.Velocity.X, "vy", m.Velocity.Y, "vz", m.Velocity.Z,
                    "ax", m.Acceleration.X, "ay", m.Acceleration.Y, "az", m.Acceleration.Z,
                    // 继续处理当前业务步骤，保持上下文状态一致。
                    "release_time", m.ReleaseTime, "active", m.Active));
            }
            var request = Obj(
                "CASE", Obj("TOTAL_TIME", task.Settings.Duration, "NUM_SPHERES", task.Settings.TargetCount),
                // 调用对应组件完成当前步骤，并保留产生的处理结果。
                "ENVIRONMENT", Obj("SOLAR_FLUX", e.SolarFlux, "SOLAR_DIRECTION", V(e.SunDirection), "ENVIRONMENT_TEMP", e.RadiationTemperature),
                "GROUP_STATE", Obj("GROUP_CENTER", V(s.Position), "GROUP_NORMAL", V(s.Direction), "GROUP_UP", V(s.Up),
                    // 调用对应组件完成当前步骤，并保留产生的处理结果。
                    "GROUP_VELOCITY", V(s.Velocity), "GROUP_ANGULAR_VELOCITY", V(s.AngularVelocity), "GROUP_ANGULAR_ACCELERATION", V(s.AngularAcceleration),
                    "COMPANION_TYPE", BackendCompanionType(s.CompanionType), "ATTITUDE_MOTION_TYPE", BackendAttitudeMotionType(s.AttitudeMotionType),
                    // SIMILARITY_LEVEL is the backend dataset/scene-contract token.  It is not
                    // the GUI's numeric similarity acceptance threshold used by module 04.
                    // 调用对应组件完成当前步骤，并保留产生的处理结果。
                    "MICRO_MOTION_PARAMS", V(s.MicroMotionParameters), "SIMILARITY_LEVEL", "DATASET_BASIC"),
                "OBSERVATION", Obj("APERTURE_SIZE", e.ApertureSize, "APERTURE_CENTER", V(e.ObserverPosition),
                    "APERTURE_NORMAL", V(e.ObserverDirection), "APERTURE_UP", V(e.ObserverUp), "APERTURE_VELOCITY", V(e.ObserverVelocity),
                    // 调用对应组件完成当前步骤，并保留产生的处理结果。
                    "APERTURE_ANGULAR_VELOCITY", V(e.ObserverAngularVelocity), "APERTURE_ANGULAR_ACCELERATION", V(e.ObserverAngularAcceleration),
                    "APERTURE_TRACK_TARGET", e.ApertureTracking, "DETECTOR_NORMAL", V(e.DetectorDirection), "DETECTOR_UP", V(e.DetectorUp),
                    // 调用对应组件完成当前步骤，并保留产生的处理结果。
                    "DETECTOR_VELOCITY", V(e.DetectorVelocity), "DETECTOR_ANGULAR_VELOCITY", V(e.DetectorAngularVelocity),
                    "DETECTOR_ANGULAR_ACCELERATION", V(e.DetectorAngularAcceleration), "DETECTOR_TRACK_TARGET", e.DetectorTracking,
                    // 继续处理当前业务步骤，保持上下文状态一致。
                    "SPOT_PLANE_SIZE", e.PlaneSize, "SPOT_FOCAL_LENGTH", e.FocalLength),
                "TARGET_PHYSICS", physics, "TARGET_SCENE", scene);
            // 在持久化格式与内存对象之间转换，供后续流程继续使用。
            string json = Serializer().Serialize(request);
            ValidateJson(json);
            return json;
        }

        // 检查输入及依赖是否满足要求，提前阻止无效操作。
        public void ValidateJson(string json) { ForwardRequestValidator.Validate(json); }

        public string Save(TaskModel task, string path, bool overwrite = false)
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            string json = Generate(task); // Complete validation before touching the destination.
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("请指定 request.json 文件路径。");
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath) && !overwrite) throw new IOException("请求文件已存在，请确认覆盖或选择其他路径。");
            string temporary = Path.Combine(Path.GetDirectoryName(fullPath), ".request-" + Guid.NewGuid().ToString("N") + ".tmp");
            // 进入可能失败的操作区间，并由后续异常分支统一处理故障。
            try
            {
                File.WriteAllText(temporary, json + Environment.NewLine, new UTF8Encoding(false));
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (overwrite && File.Exists(fullPath)) File.Replace(temporary, fullPath, null);
                else File.Move(temporary, fullPath); // Also rejects an unapproved concurrent creation.
            }
            // 无论执行成功与否都释放资源并恢复组件的可用状态。
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return fullPath;
        }

        internal static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 }; }
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static double[] V(Vector3 value)
        {
            if (value == null) throw new ArgumentException("缺少三分量向量。");
            // 返回当前步骤生成的结果，并结束本次调用。
            return ForwardSimulationMapper.MapVector(value);
        }
        private static string BackendCompanionType(string value)
        {
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (String.Equals(value, "球壳", StringComparison.Ordinal) || String.Equals(value, "SPHERE", StringComparison.OrdinalIgnoreCase)) return "SPHERE";
            throw new ArgumentException("当前红外伴飞物类型仅支持球壳。");
        }
        private static string BackendAttitudeMotionType(string value)
        {
            // 返回当前步骤生成的结果，并结束本次调用。
            return String.Equals(value, "默认", StringComparison.Ordinal) ? "NONE" : value;
        }
        private static Dictionary<string, object> Obj(params object[] pairs)
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var result = new Dictionary<string, object>();
            for (int i = 0; i < pairs.Length; i += 2) result.Add((string)pairs[i], pairs[i + 1]);
            // 返回当前步骤生成的结果，并结束本次调用。
            return result;
        }
    }
}
