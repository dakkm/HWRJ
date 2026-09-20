using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PreProcess.Wpf.Models.Results;

namespace PreProcess.Wpf.Services.Results
{
    // 定义 ForwardResultReader 类型，集中封装与该领域对象相关的状态和行为。
    public sealed class ForwardResultReader : ResultReaderBase
    {
        private static readonly string[] TemperatureRequired = { "frame", "time_s", "T_1" };
        // 保存该组件运行所需的配置或中间状态。
        private static readonly string[] TrajectoryRequired = { "case_id", "frame_id", "time_s", "object_id", "active_flag", "released_flag", "motion_stage", "release_time_s", "x_m", "y_m", "z_m", "vx_m_s", "vy_m_s", "vz_m_s", "speed_m_s", "range_to_detector_m" };
        private static readonly string[] TrajectoryMetricsRequired = { "case_id", "object_id", "sample_count", "release_time_s", "trajectory_start_time_s", "trajectory_end_time_s", "trajectory_duration_s", "path_length_m", "net_displacement_m", "average_path_speed_m_s", "min_speed_m_s", "max_speed_m_s", "min_range_to_detector_m", "max_range_to_detector_m", "start_x_m", "start_y_m", "start_z_m", "end_x_m", "end_y_m", "end_z_m" };
        private static readonly string[] InfraredRequired = { "case_id", "frame_id", "time_s", "object_id", "active_flag", "released_flag", "radiation_power_W", "radiant_intensity_W_sr", "detector_received_power_W", "detector_irradiance_W_m2", "screen_x_m", "screen_y_m", "in_screen_flag", "range_to_detector_m" };

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public override RunResult Read(RunResult result)
        {
            if (!Prepare(result, ResultModuleType.Forward, ResultModuleType.Trajectory)) return result;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            bool trajectoryOnly = result.ModuleType == ResultModuleType.Trajectory;

            ResultTable temperature = trajectoryOnly ? null : ReadCsv(result, "temperature_history.csv", "温度历史", TemperatureRequired, TemperatureRequired, true);
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (temperature != null)
            {
                var dynamicTemperatureColumns = new List<string>();
                foreach (var column in temperature.Columns) if (column.Key.StartsWith("T_", StringComparison.Ordinal)) dynamicTemperatureColumns.Add(column.Key);
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (dynamicTemperatureColumns.Count == 0) throw new ResultReadException(temperature.SourcePath, "没有目标温度列。");
                ValidateNumericTable(temperature, dynamicTemperatureColumns);
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                var model = new TemperatureResult { Name = "正向温度历史", SourcePath = temperature.SourcePath };
                model.Tables.Add(temperature); result.Temperatures.Add(model);
            }

            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            ResultTable trajectory = ReadCsv(result, "trajectory_history.csv", "轨迹历史", TrajectoryRequired,
                new[] { "frame_id", "time_s", "object_id", "active_flag", "released_flag", "motion_stage", "release_time_s", "x_m", "y_m", "z_m", "vx_m_s", "vy_m_s", "vz_m_s", "speed_m_s", "range_to_detector_m" }, true);
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (trajectory != null)
            {
                var model = new TrajectoryResult { Name = "目标轨迹历史", SourcePath = trajectory.SourcePath };
                model.Tables.Add(trajectory); result.Trajectories.Add(model);
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (trajectoryOnly)
                {
                    ResultTable metrics = ReadCsv(result, Path.Combine("trajectory_postprocess", "trajectory_metrics.csv"), "轨迹统计",
                        // 更新当前流程使用的数据，为下一处理步骤做好准备。
                        TrajectoryMetricsRequired, TrajectoryMetricsRequired.Where(x => x != "case_id"), true);
                    if (metrics != null) model.Tables.Add(metrics);
                    // 处理文件系统路径及数据，并在使用前确认目标有效。
                    string postprocess = Path.Combine(result.OutputDirectory, "trajectory_postprocess");
                    result.Summary["trajectory.postprocess_directory"] = postprocess;
                    foreach (string image in new[] { "trajectory_3d.png", "trajectory_range.png" })
                    {
                        // 处理文件系统路径及数据，并在使用前确认目标有效。
                        string path = Path.Combine(postprocess, image);
                        if (File.Exists(path)) result.Summary["trajectory." + image] = path;
                    }
                }
            }

            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            ResultTable infrared = trajectoryOnly ? null : ReadCsv(result, "infrared_response_history.csv", "红外响应历史", InfraredRequired,
                new[] { "frame_id", "time_s", "object_id", "active_flag", "released_flag", "radiation_power_W", "radiant_intensity_W_sr", "detector_received_power_W", "detector_irradiance_W_m2", "screen_x_m", "screen_y_m", "in_screen_flag", "range_to_detector_m" }, true);
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (infrared != null)
            {
                var model = new InfraredResult { Name = "正向红外响应", SourcePath = infrared.SourcePath };
                model.Tables.Add(infrared); result.InfraredResponses.Add(model);
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                PointImageResult image = PointImageBuilder.FromForward(infrared);
                if (image != null) result.PointImages.Add(image);
            }

            // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
            ReadSolverStatus(result);
            if (!trajectoryOnly) ReadExtractedFeatures(result);
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            result.Summary["reader"] = "ForwardResultReader";
            return result;
        }

        private static void ReadExtractedFeatures(RunResult result)
        {
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string root = Path.Combine(result.OutputDirectory, "features");
            if (!Directory.Exists(root)) return;
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string[] directories = Directory.GetDirectories(root)
                .Where(path => File.Exists(Path.Combine(path, "evaluation_status.json")))
                // 处理文件系统路径及数据，并在使用前确认目标有效。
                .OrderByDescending(Directory.GetLastWriteTimeUtc).ToArray();
            if (directories.Length == 0)
            {
                // 调用对应组件完成当前步骤，并保留产生的处理结果。
                AddIssue(result, "features 目录中没有可读取的 03 特征结果。");
                return;
            }
            if (directories.Length > 1) AddIssue(result, "features 目录包含多次结果，当前显示最新一次。");

            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            string directory = directories[0];
            string runId = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var features = new RunResult
            {
                RunId = runId,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                ModuleType = ResultModuleType.Similarity,
                ModuleCode = "03",
                RunDirectory = directory,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                OutputDirectory = directory,
                ResultExists = true,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                AvailabilityMessage = "03 特征结果目录已定位。"
            };
            new SimilarityResultReader().Read(features);
            // 遍历当前数据集合，逐项完成必要的转换或状态更新。
            foreach (var item in features.Summary) result.Summary["features." + item.Key] = item.Value;
            foreach (string issue in features.Issues) AddIssue(result, "03 特征提取：" + issue);
            foreach (TemperatureResult item in features.Temperatures) result.Temperatures.Add(item);
            // 遍历当前数据集合，逐项完成必要的转换或状态更新。
            foreach (SimilarityResult item in features.Similarities) result.Similarities.Add(item);
            result.Summary["features.output_directory"] = directory;
        }

        // 检查输入及依赖是否满足要求，提前阻止无效操作。
        private static void ValidateNumericTable(ResultTable table, IEnumerable<string> columns)
        {
            foreach (ResultRow row in table.Rows)
                // 遍历当前数据集合，逐项完成必要的转换或状态更新。
                foreach (string column in columns)
                {
                    object value;
                    if (!row.Values.TryGetValue(column, out value) || !(value is long) && !(value is double))
                        // 发现无效输入或运行状态后立即终止，防止错误继续传播。
                        throw new ResultReadException(table.SourcePath, column + " 包含非数值。");
                }
        }

        private static void ReadSolverStatus(RunResult result)
        {
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string path = Path.Combine(result.OutputDirectory, "solver_status.txt");
            if (!File.Exists(path)) return;
            // 遍历当前数据集合，逐项完成必要的转换或状态更新。
            foreach (string line in File.ReadAllLines(path))
            {
                int split = line.IndexOf('=');
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (split <= 0) continue;
                result.Summary["solver." + line.Substring(0, split).Trim()] = line.Substring(split + 1).Trim();
            }
        }
    }
}
