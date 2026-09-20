using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using PreProcess.Wpf.Models.Results;

namespace PreProcess.Wpf.Services.Results
{
    // 定义 SimilarityResultReader 类型，集中封装与该领域对象相关的状态和行为。
    public sealed class SimilarityResultReader : ResultReaderBase
    {
        public override RunResult Read(RunResult result)
        {
            if (!Prepare(result, ResultModuleType.Similarity)) return result;
            // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
            IDictionary<string, object> status = ReadJson(result, "evaluation_status.json", true);
            ValidateIdentity(status, result, "03", "evaluation_status.json");
            // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
            string mode = OptionalString(status, "mode");
            var model = new SimilarityResult { Name = mode == "features" ? "响应特征" : "相似度评估" };
            // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
            AddJsonSummary(model.Summary, status, "status.");

            if (mode == "features") ReadFeatures(result, model, String.Empty, true, "Temperature");
            // 当前置条件不成立时执行备用路径，保持处理结果完整。
            else
            {
                IDictionary<string, object> summary = ReadJson(result, "similarity_summary.json", true);
                AddJsonSummary(model.Summary, summary, "summary.");
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                ResultTable components = ReadCsv(result, "similarity_components.csv", "相似度分量",
                    new[] { "feature_name", "category", "raw_difference", "raw_unit", "similarity_percent", "valid_flag", "invalid_reason", "valid_sample_count" },
                    // 继续处理当前业务步骤，保持上下文状态一致。
                    new[] { "valid_flag", "valid_sample_count" }, true);
                ResultTable overview = BuildSimilarityOverview(summary, components == null ? null : components.SourcePath);
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (overview != null) { model.Tables.Add(overview); model.SourcePath = overview.SourcePath; }
                if (components != null) { model.Tables.Add(components); if (model.SourcePath == null) model.SourcePath = components.SourcePath; }
                ReadFeatures(result, model, "reference_features", false, "Reference");
                // 调用对应组件完成当前步骤，并保留产生的处理结果。
                ReadFeatures(result, model, "candidate_features", false, "Candidate");
                if (mode != null && mode != "similarity") AddIssue(result, "evaluation_status.json 包含未知 mode：" + mode);
            }
            // 将当前结果加入集合，供后续汇总或界面展示。
            result.Similarities.Add(model);
            foreach (var item in model.Summary) result.Summary["similarity." + item.Key] = item.Value;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            result.Summary["reader"] = "SimilarityResultReader";
            return result;
        }

        private static void ReadFeatures(RunResult result, SimilarityResult model, string directory, bool required, string temperatureName)
        {
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string prefix = String.IsNullOrEmpty(directory) ? String.Empty : directory + Path.DirectorySeparatorChar;
            IDictionary<string, object> summary = ReadJson(result, prefix + "feature_summary.json", required);
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            AddJsonSummary(model.Summary, summary, (String.IsNullOrEmpty(directory) ? "features" : directory) + ".");
            ResultTable temperature = ReadCsv(result, prefix + "feature_timeseries.csv", Name(directory, "场景特征时序"), new[] { "time_s", "temperature_object_id", "temperature_K" }, new[] { "time_s", "temperature_object_id", "temperature_K" }, required);
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            AddTable(model, temperature);
            if (temperature != null)
            {
                var curve = new TemperatureResult { Name = temperatureName, SourcePath = temperature.SourcePath };
                // 将当前结果加入集合，供后续汇总或界面展示。
                curve.Tables.Add(temperature);
                result.Temperatures.Add(curve);
            }
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            AddTable(model, ReadCsv(result, prefix + "object_feature_timeseries.csv", Name(directory, "目标特征时序"), new[] { "case_id", "frame_id", "time_s", "object_id" }, new[] { "frame_id", "time_s", "object_id" }, required));
            AddTable(model, ReadCsv(result, prefix + "periodic_features.csv", Name(directory, "周期特征"), new[] { "signal", "periodic_valid", "sample_count" }, new[] { "periodic_valid", "sample_count" }, required));
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static string Name(string directory, string name)
        {
            return String.IsNullOrEmpty(directory) ? name : directory + " " + name;
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static void AddTable(SimilarityResult model, ResultTable table)
        {
            if (table == null) return;
            model.Tables.Add(table);
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (model.SourcePath == null) model.SourcePath = table.SourcePath;
        }

        private static ResultTable BuildSimilarityOverview(IDictionary<string, object> summary, string sourcePath)
        {
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (summary == null) return null;
            var table = new ResultTable { Name = "相似度结果", SourcePath = sourcePath };
            // 将当前结果加入集合，供后续汇总或界面展示。
            table.Columns.Add(new ResultColumn { Key = "similarity_type", DisplayName = "评价类型" });
            table.Columns.Add(new ResultColumn { Key = "similarity_percent", DisplayName = "相似度" });
            table.Columns.Add(new ResultColumn { Key = "limiting_feature", DisplayName = "限制特征" });
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            AddOverviewRow(table, summary, "scene_similarity_percent", "scene_limiting_feature", "场景综合相似度");
            AddOverviewRow(table, summary, "temperature_similarity_percent", "temperature_limiting_feature", "温度相似度");
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            AddOverviewRow(table, summary, "infrared_similarity_percent", "infrared_limiting_feature", "红外相似度");
            return table.Rows.Count == 0 ? null : table;
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static void AddOverviewRow(ResultTable table, IDictionary<string, object> summary,
            string similarityKey, string limitingKey, string displayName)
        {
            object value;
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (!summary.TryGetValue(similarityKey, out value) || value == null) return;
            double similarity;
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (!Double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float,
                CultureInfo.InvariantCulture, out similarity) || Double.IsNaN(similarity) || Double.IsInfinity(similarity)) return;
            // 继续处理当前业务步骤，保持上下文状态一致。
            object limiting;
            summary.TryGetValue(limitingKey, out limiting);
            var row = new ResultRow();
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            row.Values["similarity_type"] = displayName;
            row.Values["similarity_percent"] = similarity.ToString("0.00", CultureInfo.InvariantCulture) + "%";
            // 将外部数据转换为目标类型，并保持约定的表示格式。
            row.Values["limiting_feature"] = FeatureName(Convert.ToString(limiting, CultureInfo.InvariantCulture));
            table.Rows.Add(row);
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static string FeatureName(string value)
        {
            switch (value)
            {
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "temperature_rmse": return "温度均方根误差";
                case "temperature_mae": return "温度平均绝对误差";
                case "temperature_max_abs": return "温度最大绝对误差";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "temperature_final_abs": return "温度末值绝对误差";
                case "temperature_plateau_abs": return "温度平台段误差";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "temperature_early_abs": return "温度初期误差";
                case "temperature_rate_trend": return "温度变化趋势";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "scene_total_radiant_intensity": return "场景总辐射强度";
                case "scene_total_received_power": return "场景总接收功率";
                case "scene_peak_irradiance": return "场景峰值辐照度";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "scene_total_gray": return "场景总灰度";
                case "screen_centroid_position": return "屏幕质心位置";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "screen_peak_position": return "屏幕峰值位置";
                case "object_radiation_power": return "目标辐射功率";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "object_radiant_intensity": return "目标辐射强度";
                case "object_screen_position": return "目标屏幕位置";
                default: return String.IsNullOrWhiteSpace(value) ? String.Empty : value;
            }
        }
    }
}
