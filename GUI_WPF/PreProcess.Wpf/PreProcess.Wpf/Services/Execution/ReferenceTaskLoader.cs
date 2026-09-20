using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using PreProcess.Wpf.Models;
namespace PreProcess.Wpf.Services.Execution
{
    // Uses the software default scene as the surrogate reference while preserving its learned GUI inputs.
    public static class ReferenceTaskLoader
    {
        // 保存该组件运行所需的配置或中间状态。
        public const double MaximumPredictionDurationSeconds = 1000.0;

        public static TaskModel AdjustForPrediction(TaskModel current, string package)
        {
            if (current == null) throw new ArgumentNullException("current");
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            double duration = current.Settings.Duration;
            if (duration <= 0 || duration > MaximumPredictionDurationSeconds)
                // 发现无效输入或运行状态后立即终止，防止错误继续传播。
                throw new ArgumentException("智能预测和场景构建时间必须大于 0 且不超过 1000 s。");
            var adjusted = AdjustToReferenceScene(current, package);
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            adjusted.Settings.Duration = duration;
            return adjusted;
        }

        public static TaskModel AdjustForScene(TaskModel current, string package)
        {
            // 返回当前步骤生成的结果，并结束本次调用。
            return AdjustForPrediction(current, package);
        }

        private static TaskModel AdjustToReferenceScene(TaskModel current, string package)
        {
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (current == null) throw new ArgumentNullException("current");
            double power = current.Targets.Uniform.InternalPower;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            double emissivity = current.Targets.Uniform.Emissivity;
            double absorption = current.Targets.Uniform.SolarAbsorption;
            var adjusted = Load(package);
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            adjusted.Settings.Metadata.Name = current.Settings.Metadata.Name;
            adjusted.Settings.Metadata.Description = current.Settings.Metadata.Description;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            adjusted.Settings.SimilarityIndex = current.Settings.SimilarityIndex;
            adjusted.Targets.Uniform.InternalPower = power;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            adjusted.Targets.Uniform.Emissivity = emissivity;
            adjusted.Targets.Uniform.SolarAbsorption = absorption;
            adjusted.Targets.Uniform.IrReflection = 1.0 - emissivity;
            // 返回当前步骤生成的结果，并结束本次调用。
            return adjusted;
        }

        public static TaskModel Load(string package)
        {
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string json = File.ReadAllText(Path.Combine(package, "02-智能预测", "03-模型文件", "surrogate_reference_request.json"));
            new RequestGenerator().ValidateJson(json);
            // 在持久化格式与内存对象之间转换，供后续流程继续使用。
            var root = RequestGenerator.Serializer().Deserialize<Dictionary<string, object>>(json);
            var task = new TaskModel();
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var section = (Dictionary<string, object>)root["CASE"];
            task.Settings.Duration = Number(section, "TOTAL_TIME"); task.Settings.TargetCount = (int)Number(section, "NUM_SPHERES");
            task.Settings.Metadata.Name = "软件默认参考场景";
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var env = task.Environment;
            section = (Dictionary<string, object>)root["ENVIRONMENT"];
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            env.SolarFlux = Number(section, "SOLAR_FLUX"); env.RadiationTemperature = Number(section, "ENVIRONMENT_TEMP"); Vector(env.SunDirection, section, "SOLAR_DIRECTION");
            section = (Dictionary<string, object>)root["GROUP_STATE"];
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            Vector(task.Scene.Position, section, "GROUP_CENTER"); Vector(task.Scene.Direction, section, "GROUP_NORMAL"); Vector(task.Scene.Up, section, "GROUP_UP");
            Vector(task.Scene.Velocity, section, "GROUP_VELOCITY"); Vector(task.Scene.AngularVelocity, section, "GROUP_ANGULAR_VELOCITY"); Vector(task.Scene.AngularAcceleration, section, "GROUP_ANGULAR_ACCELERATION");
            if (section.ContainsKey("COMPANION_TYPE")) task.Scene.CompanionType = Convert.ToString(section["COMPANION_TYPE"]);
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (section.ContainsKey("ATTITUDE_MOTION_TYPE")) task.Scene.AttitudeMotionType = Convert.ToString(section["ATTITUDE_MOTION_TYPE"]);
            if (section.ContainsKey("MICRO_MOTION_PARAMS")) Vector(task.Scene.MicroMotionParameters, section, "MICRO_MOTION_PARAMS");
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (section.ContainsKey("SIMILARITY_LEVEL"))
            {
                double similarity;
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (Double.TryParse(Convert.ToString(section["SIMILARITY_LEVEL"]), NumberStyles.Float, CultureInfo.InvariantCulture, out similarity) && similarity >= 50 && similarity <= 100)
                    task.Settings.SimilarityIndex = similarity;
            }
            section = (Dictionary<string, object>)root["OBSERVATION"];
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            Vector(env.ObserverPosition, section, "APERTURE_CENTER"); Vector(env.ObserverDirection, section, "APERTURE_NORMAL"); Vector(env.ObserverUp, section, "APERTURE_UP");
            Vector(env.ObserverVelocity, section, "APERTURE_VELOCITY"); Vector(env.ObserverAngularVelocity, section, "APERTURE_ANGULAR_VELOCITY"); Vector(env.ObserverAngularAcceleration, section, "APERTURE_ANGULAR_ACCELERATION");
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            Vector(env.DetectorDirection, section, "DETECTOR_NORMAL"); Vector(env.DetectorUp, section, "DETECTOR_UP"); Vector(env.DetectorVelocity, section, "DETECTOR_VELOCITY");
            Vector(env.DetectorAngularVelocity, section, "DETECTOR_ANGULAR_VELOCITY"); Vector(env.DetectorAngularAcceleration, section, "DETECTOR_ANGULAR_ACCELERATION");
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            env.ApertureSize = Number(section, "APERTURE_SIZE"); env.FocalLength = Number(section, "SPOT_FOCAL_LENGTH"); env.PlaneSize = Number(section, "SPOT_PLANE_SIZE");
            env.ApertureTracking = (bool)section["APERTURE_TRACK_TARGET"]; env.DetectorTracking = (bool)section["DETECTOR_TRACK_TARGET"];
            var physics = (IList)root["TARGET_PHYSICS"];
            // Reference contract uses identical physics; verify before assigning the shared editor values.
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var first = (Dictionary<string, object>)physics[0];
            foreach (Dictionary<string, object> row in physics)
                // 遍历当前数据集合，逐项完成必要的转换或状态更新。
                foreach (string name in first.Keys) if (name != "id" && Number(row, name) != Number(first, name)) throw new ArgumentException("参考物性并非统一值，不能作为统一预设载入。");
            var p = task.Targets.Uniform;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            p.Radius = Number(first, "r"); p.Density = Number(first, "rho"); p.HeatCapacity = Number(first, "cp"); p.InitialTemperature = Number(first, "t_init");
            p.InternalPower = Number(first, "q_int"); p.Emissivity = Number(first, "eps_ir"); p.SolarAbsorption = Number(first, "alpha_s"); p.IrReflection = Number(first, "rho_ir");
            // 遍历当前数据集合，逐项完成必要的转换或状态更新。
            foreach (Dictionary<string, object> row in (IList)root["TARGET_SCENE"])
            {
                var motion = task.IndividualTargets[(int)Number(row, "id") - 1].Motion;
                motion.Position.X = Number(row, "x"); motion.Position.Y = Number(row, "y"); motion.Position.Z = Number(row, "z");
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                motion.Velocity.X = Number(row, "vx"); motion.Velocity.Y = Number(row, "vy"); motion.Velocity.Z = Number(row, "vz");
                motion.Acceleration.X = Number(row, "ax"); motion.Acceleration.Y = Number(row, "ay"); motion.Acceleration.Z = Number(row, "az");
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                motion.ReleaseTime = Number(row, "release_time"); motion.Active = (bool)row["active"];
            }
            return task;
        }
        // 将外部数据转换为目标类型，并保持约定的表示格式。
        private static double Number(Dictionary<string, object> row, string key) => Convert.ToDouble(row[key]);
        private static void Vector(Vector3 vector, Dictionary<string, object> row, string key)
        { var values = (IList)row[key]; vector.X = Convert.ToDouble(values[0]); vector.Y = Convert.ToDouble(values[1]); vector.Z = Convert.ToDouble(values[2]); }
    }
}
