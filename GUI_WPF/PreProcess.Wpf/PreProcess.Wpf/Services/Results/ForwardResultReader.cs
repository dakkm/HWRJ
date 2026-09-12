using System;
using System.Collections.Generic;
using System.IO;
using PreProcess.Wpf.Models.Results;

namespace PreProcess.Wpf.Services.Results
{
    public sealed class ForwardResultReader : ResultReaderBase
    {
        private static readonly string[] TemperatureRequired = { "frame", "time_s", "T_1" };
        private static readonly string[] TrajectoryRequired = { "case_id", "frame_id", "time_s", "object_id", "active_flag", "released_flag", "motion_stage", "release_time_s", "x_m", "y_m", "z_m", "vx_m_s", "vy_m_s", "vz_m_s", "speed_m_s", "range_to_detector_m" };
        private static readonly string[] InfraredRequired = { "case_id", "frame_id", "time_s", "object_id", "active_flag", "released_flag", "radiation_power_W", "radiant_intensity_W_sr", "detector_received_power_W", "detector_irradiance_W_m2", "screen_x_m", "screen_y_m", "in_screen_flag", "range_to_detector_m" };

        public override RunResult Read(RunResult result)
        {
            if (!Prepare(result, ResultModuleType.Forward, ResultModuleType.Trajectory)) return result;

            ResultTable temperature = ReadCsv(result, "temperature_history.csv", "温度历史", TemperatureRequired, TemperatureRequired, true);
            if (temperature != null)
            {
                var dynamicTemperatureColumns = new List<string>();
                foreach (var column in temperature.Columns) if (column.Key.StartsWith("T_", StringComparison.Ordinal)) dynamicTemperatureColumns.Add(column.Key);
                if (dynamicTemperatureColumns.Count == 0) throw new ResultReadException(temperature.SourcePath, "没有目标温度列。");
                ValidateNumericTable(temperature, dynamicTemperatureColumns);
                var model = new TemperatureResult { Name = "正向温度历史", SourcePath = temperature.SourcePath };
                model.Tables.Add(temperature); result.Temperatures.Add(model);
            }

            ResultTable trajectory = ReadCsv(result, "trajectory_history.csv", "轨迹历史", TrajectoryRequired,
                new[] { "frame_id", "time_s", "object_id", "active_flag", "released_flag", "motion_stage", "release_time_s", "x_m", "y_m", "z_m", "vx_m_s", "vy_m_s", "vz_m_s", "speed_m_s", "range_to_detector_m" }, true);
            if (trajectory != null)
            {
                var model = new TrajectoryResult { Name = "目标轨迹历史", SourcePath = trajectory.SourcePath };
                model.Tables.Add(trajectory); result.Trajectories.Add(model);
            }

            ResultTable infrared = ReadCsv(result, "infrared_response_history.csv", "红外响应历史", InfraredRequired,
                new[] { "frame_id", "time_s", "object_id", "active_flag", "released_flag", "radiation_power_W", "radiant_intensity_W_sr", "detector_received_power_W", "detector_irradiance_W_m2", "screen_x_m", "screen_y_m", "in_screen_flag", "range_to_detector_m" }, true);
            if (infrared != null)
            {
                var model = new InfraredResult { Name = "正向红外响应", SourcePath = infrared.SourcePath };
                model.Tables.Add(infrared); result.InfraredResponses.Add(model);
                PointImageResult image = PointImageBuilder.FromForward(infrared);
                if (image != null) result.PointImages.Add(image);
            }

            ReadSolverStatus(result);
            result.Summary["reader"] = "ForwardResultReader";
            return result;
        }

        private static void ValidateNumericTable(ResultTable table, IEnumerable<string> columns)
        {
            foreach (ResultRow row in table.Rows)
                foreach (string column in columns)
                {
                    object value;
                    if (!row.Values.TryGetValue(column, out value) || !(value is long) && !(value is double))
                        throw new ResultReadException(table.SourcePath, column + " 包含非数值。");
                }
        }

        private static void ReadSolverStatus(RunResult result)
        {
            string path = Path.Combine(result.OutputDirectory, "solver_status.txt");
            if (!File.Exists(path)) return;
            foreach (string line in File.ReadAllLines(path))
            {
                int split = line.IndexOf('=');
                if (split <= 0) continue;
                result.Summary["solver." + line.Substring(0, split).Trim()] = line.Substring(split + 1).Trim();
            }
        }
    }
}
