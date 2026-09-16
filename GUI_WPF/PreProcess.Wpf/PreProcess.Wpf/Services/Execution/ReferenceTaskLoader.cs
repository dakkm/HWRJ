using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using PreProcess.Wpf.Models;
namespace PreProcess.Wpf.Services.Execution
{
    // Adapts fixed fields to the frozen surrogate contract while preserving its learned GUI inputs.
    public static class ReferenceTaskLoader
    {
        public static TaskModel AdjustForPrediction(TaskModel current, string package)
        {
            if (current == null) throw new ArgumentNullException("current");
            double power = current.Targets.Uniform.InternalPower;
            double emissivity = current.Targets.Uniform.Emissivity;
            double absorption = current.Targets.Uniform.SolarAbsorption;
            var adjusted = Load(package);
            adjusted.Settings.Metadata.Name = current.Settings.Metadata.Name;
            adjusted.Settings.Metadata.Description = current.Settings.Metadata.Description;
            adjusted.Settings.SimilarityIndex = current.Settings.SimilarityIndex;
            adjusted.Targets.Uniform.InternalPower = power;
            adjusted.Targets.Uniform.Emissivity = emissivity;
            adjusted.Targets.Uniform.SolarAbsorption = absorption;
            adjusted.Targets.Uniform.IrReflection = 1.0 - emissivity;
            return adjusted;
        }

        public static TaskModel Load(string package)
        {
            string json = File.ReadAllText(Path.Combine(package, "02-智能预测", "03-模型文件", "surrogate_reference_request.json"));
            new RequestGenerator().ValidateJson(json);
            var root = RequestGenerator.Serializer().Deserialize<Dictionary<string, object>>(json);
            var task = new TaskModel();
            var section = (Dictionary<string, object>)root["CASE"];
            task.Settings.Duration = Number(section, "TOTAL_TIME"); task.Settings.TargetCount = (int)Number(section, "NUM_SPHERES");
            task.Settings.Metadata.Name = "后端固定参考任务";
            var env = task.Environment;
            section = (Dictionary<string, object>)root["ENVIRONMENT"];
            env.SolarFlux = Number(section, "SOLAR_FLUX"); env.RadiationTemperature = Number(section, "ENVIRONMENT_TEMP"); Vector(env.SunDirection, section, "SOLAR_DIRECTION");
            section = (Dictionary<string, object>)root["GROUP_STATE"];
            Vector(task.Scene.Position, section, "GROUP_CENTER"); Vector(task.Scene.Direction, section, "GROUP_NORMAL"); Vector(task.Scene.Up, section, "GROUP_UP");
            Vector(task.Scene.Velocity, section, "GROUP_VELOCITY"); Vector(task.Scene.AngularVelocity, section, "GROUP_ANGULAR_VELOCITY"); Vector(task.Scene.AngularAcceleration, section, "GROUP_ANGULAR_ACCELERATION");
            if (section.ContainsKey("COMPANION_TYPE")) task.Scene.CompanionType = Convert.ToString(section["COMPANION_TYPE"]);
            if (section.ContainsKey("ATTITUDE_MOTION_TYPE")) task.Scene.AttitudeMotionType = Convert.ToString(section["ATTITUDE_MOTION_TYPE"]);
            if (section.ContainsKey("MICRO_MOTION_PARAMS")) Vector(task.Scene.MicroMotionParameters, section, "MICRO_MOTION_PARAMS");
            if (section.ContainsKey("SIMILARITY_LEVEL"))
            {
                double similarity;
                if (Double.TryParse(Convert.ToString(section["SIMILARITY_LEVEL"]), NumberStyles.Float, CultureInfo.InvariantCulture, out similarity) && similarity >= 50 && similarity <= 100)
                    task.Settings.SimilarityIndex = similarity;
            }
            section = (Dictionary<string, object>)root["OBSERVATION"];
            Vector(env.ObserverPosition, section, "APERTURE_CENTER"); Vector(env.ObserverDirection, section, "APERTURE_NORMAL"); Vector(env.ObserverUp, section, "APERTURE_UP");
            Vector(env.ObserverVelocity, section, "APERTURE_VELOCITY"); Vector(env.ObserverAngularVelocity, section, "APERTURE_ANGULAR_VELOCITY"); Vector(env.ObserverAngularAcceleration, section, "APERTURE_ANGULAR_ACCELERATION");
            Vector(env.DetectorDirection, section, "DETECTOR_NORMAL"); Vector(env.DetectorUp, section, "DETECTOR_UP"); Vector(env.DetectorVelocity, section, "DETECTOR_VELOCITY");
            Vector(env.DetectorAngularVelocity, section, "DETECTOR_ANGULAR_VELOCITY"); Vector(env.DetectorAngularAcceleration, section, "DETECTOR_ANGULAR_ACCELERATION");
            env.ApertureSize = Number(section, "APERTURE_SIZE"); env.FocalLength = Number(section, "SPOT_FOCAL_LENGTH"); env.PlaneSize = Number(section, "SPOT_PLANE_SIZE");
            env.ApertureTracking = (bool)section["APERTURE_TRACK_TARGET"]; env.DetectorTracking = (bool)section["DETECTOR_TRACK_TARGET"];
            var physics = (IList)root["TARGET_PHYSICS"];
            // Reference contract uses identical physics; verify before assigning the shared editor values.
            var first = (Dictionary<string, object>)physics[0];
            foreach (Dictionary<string, object> row in physics)
                foreach (string name in first.Keys) if (name != "id" && Number(row, name) != Number(first, name)) throw new ArgumentException("参考物性并非统一值，不能作为统一预设载入。");
            var p = task.Targets.Uniform;
            p.Radius = Number(first, "r"); p.Density = Number(first, "rho"); p.HeatCapacity = Number(first, "cp"); p.InitialTemperature = Number(first, "t_init");
            p.InternalPower = Number(first, "q_int"); p.Emissivity = Number(first, "eps_ir"); p.SolarAbsorption = Number(first, "alpha_s"); p.IrReflection = Number(first, "rho_ir");
            foreach (Dictionary<string, object> row in (IList)root["TARGET_SCENE"])
            {
                var motion = task.IndividualTargets[(int)Number(row, "id") - 1].Motion;
                motion.Position.X = Number(row, "x"); motion.Position.Y = Number(row, "y"); motion.Position.Z = Number(row, "z");
                motion.Velocity.X = Number(row, "vx"); motion.Velocity.Y = Number(row, "vy"); motion.Velocity.Z = Number(row, "vz");
                motion.Acceleration.X = Number(row, "ax"); motion.Acceleration.Y = Number(row, "ay"); motion.Acceleration.Z = Number(row, "az");
                motion.ReleaseTime = Number(row, "release_time"); motion.Active = (bool)row["active"];
            }
            return task;
        }
        private static double Number(Dictionary<string, object> row, string key) => Convert.ToDouble(row[key]);
        private static void Vector(Vector3 vector, Dictionary<string, object> row, string key)
        { var values = (IList)row[key]; vector.X = Convert.ToDouble(values[0]); vector.Y = Convert.ToDouble(values[1]); vector.Z = Convert.ToDouble(values[2]); }
    }
}
