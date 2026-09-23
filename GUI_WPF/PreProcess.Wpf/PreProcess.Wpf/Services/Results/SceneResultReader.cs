using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PreProcess.Wpf.Models.Results;

namespace PreProcess.Wpf.Services.Results
{
    public sealed class SceneResultReader : ResultReaderBase
    {
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public override RunResult Read(RunResult result)
        {
            if (!Prepare(result, ResultModuleType.Scene)) return result;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            IDictionary<string, object> summary = ReadJson(result, "scene_search_summary.json", true);
            ValidateIdentity(summary, result, "04", "scene_search_summary.json");
            // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
            IDictionary<string, object> status = ReadJson(result, "scene_search_status.json", true);
            ValidateIdentity(status, result, "04", "scene_search_status.json");

            var model = new SceneResult { Name = "红外场景构建结果" };
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            AddJsonSummary(model.Summary, summary, "summary.");
            AddJsonSummary(model.Summary, status, "status.");
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            ResultTable parameters = ReadCsv(result, "candidate_parameters.csv", "有效候选参数",
                new[] { "solution_index", "sequence_index", "target_id", "source_case_id", "candidate_id", "q_int", "emissivity_ir", "absorptivity_solar" },
                // 继续处理当前业务步骤，保持上下文状态一致。
                new[] { "solution_index", "sequence_index", "q_int", "emissivity_ir", "absorptivity_solar" }, true);
            ResultTable temperature = ReadCsv(result, "candidate_temperature_curves.csv", "候选温度曲线",
                // 继续处理当前业务步骤，保持上下文状态一致。
                new[] { "solution_index", "target_id", "source_case_id", "candidate_id", "time_s", "target_temperature_K", "m2_temperature_K", "forward_temperature_K" },
                new[] { "solution_index", "time_s", "target_temperature_K", "m2_temperature_K", "forward_temperature_K" }, true);
            ResultTable searchLog = ReadCsv(result, "scene_search_log.csv", "场景搜索日志",
                // 继续处理当前业务步骤，保持上下文状态一致。
                new[] { "sequence_index", "target_id", "source_case_id", "candidate_id", "q_int", "emissivity_ir", "absorptivity_solar", "decision" },
                new[] { "sequence_index", "q_int", "emissivity_ir", "absorptivity_solar" }, true);

            // Keep the detailed backend tables available, but make the concise
            // similarity result the first (default) table shown by the GUI.
            // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
            // The requested count is the number of accepted candidates. The
            // search log also contains candidates that were evaluated and
            // rejected, so use candidate_parameters.csv as the acceptance set
            // when building the concise result table.
            AddTable(model, BuildCandidateSimilarityTable(searchLog, parameters));
            AddTable(model, parameters);
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            AddTable(model, temperature);
            if (temperature != null)
            {
                var curves = new TemperatureResult { Name = "Target / Proxy / Forward", SourcePath = temperature.SourcePath };
                // 将当前结果加入集合，供后续汇总或界面展示。
                curves.Tables.Add(temperature);
                result.Temperatures.Add(curves);
            }
            // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
            AddTable(model, searchLog);
            result.Scenes.Add(model);
            // 遍历当前数据集合，逐项完成必要的转换或状态更新。
            foreach (var item in model.Summary) result.Summary["scene." + item.Key] = item.Value;
            result.Summary["reader"] = "SceneResultReader";
            return result;
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static void AddTable(SceneResult model, ResultTable table)
        {
            if (table == null) return;
            // 将当前结果加入集合，供后续汇总或界面展示。
            model.Tables.Add(table);
            if (model.SourcePath == null) model.SourcePath = table.SourcePath;
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static ResultTable BuildCandidateSimilarityTable(ResultTable source, ResultTable acceptedParameters)
        {
            if (source == null) return null;
            var table = new ResultTable { Name = "候选相似度结果", SourcePath = source.SourcePath };
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            AddDisplayColumn(table, "candidate_id", "候选");
            AddDisplayColumn(table, "proxy_temperature_similarity_percent", "代理预测相似度");
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            AddDisplayColumn(table, "forward_temperature_similarity_percent", "正向复核相似度");
            AddDisplayColumn(table, "forward_elapsed_seconds", "单次复核耗时");

            // 遍历当前数据集合，逐项完成必要的转换或状态更新。
            var acceptedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (acceptedParameters != null)
            {
                foreach (ResultRow acceptedRow in acceptedParameters.Rows)
                {
                    string id = Convert.ToString(Value(acceptedRow, "candidate_id"), CultureInfo.InvariantCulture);
                    if (!String.IsNullOrWhiteSpace(id)) acceptedIds.Add(id);
                }
            }

            bool hasAcceptedParameters = acceptedParameters != null && acceptedParameters.Rows.Count > 0;
            foreach (ResultRow sourceRow in source.Rows.Where(row =>
                IsRelevantCandidate(row) && (!hasAcceptedParameters || acceptedIds.Contains(Convert.ToString(Value(row, "candidate_id"), CultureInfo.InvariantCulture)))))
            {
                var row = new ResultRow();
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                row.Values["candidate_id"] = Value(sourceRow, "candidate_id");
                row.Values["proxy_temperature_similarity_percent"] = Format(Value(sourceRow, "proxy_temperature_similarity_percent"), "0.00", "%");
                row.Values["forward_temperature_similarity_percent"] = Format(Value(sourceRow, "forward_temperature_similarity_percent"), "0.00", "%");
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                row.Values["forward_elapsed_seconds"] = Format(Value(sourceRow, "forward_elapsed_seconds"), "0", "秒");
                table.Rows.Add(row);
            }
            // 返回当前步骤生成的结果，并结束本次调用。
            return table;
        }

        private static bool IsRelevantCandidate(ResultRow row)
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            object value = Value(row, "proxy_pass");
            if (value is bool && (bool)value) return true;
            value = Value(row, "forward_attempted");
            // 返回当前步骤生成的结果，并结束本次调用。
            return value is bool && (bool)value;
        }

        private static void AddDisplayColumn(ResultTable table, string key, string displayName)
        {
            // 将当前结果加入集合，供后续汇总或界面展示。
            table.Columns.Add(new ResultColumn { Key = key, DisplayName = displayName });
        }

        private static object Value(ResultRow row, string key)
        {
            // 继续处理当前业务步骤，保持上下文状态一致。
            object value;
            return row.Values.TryGetValue(key, out value) ? value : null;
        }

        private static string Format(object value, string format, string suffix)
        {
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (value == null) return String.Empty;
            try { return Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString(format, CultureInfo.InvariantCulture) + suffix; }
            // 捕获本步骤产生的异常，将故障转换为可追踪的运行状态。
            catch (FormatException) { return Convert.ToString(value, CultureInfo.InvariantCulture); }
            catch (InvalidCastException) { return Convert.ToString(value, CultureInfo.InvariantCulture); }
        }
    }
}
