using System;
using System.Collections.Generic;
using PreProcess.Wpf.Models.Results;

namespace PreProcess.Wpf.Services.Results
{
    public sealed class SceneResultReader : ResultReaderBase
    {
        public override RunResult Read(RunResult result)
        {
            if (!Prepare(result, ResultModuleType.Scene)) return result;
            IDictionary<string, object> summary = ReadJson(result, "scene_search_summary.json", true);
            ValidateIdentity(summary, result, "04", "scene_search_summary.json");
            IDictionary<string, object> status = ReadJson(result, "scene_search_status.json", true);
            ValidateIdentity(status, result, "04", "scene_search_status.json");

            var model = new SceneResult { Name = "红外场景构建结果" };
            AddJsonSummary(model.Summary, summary, "summary.");
            AddJsonSummary(model.Summary, status, "status.");
            AddTable(model, ReadCsv(result, "candidate_parameters.csv", "有效候选参数",
                new[] { "solution_index", "sequence_index", "target_id", "source_case_id", "candidate_id", "q_int", "emissivity_ir", "absorptivity_solar" },
                new[] { "solution_index", "sequence_index", "q_int", "emissivity_ir", "absorptivity_solar" }, true));
            ResultTable temperature = ReadCsv(result, "candidate_temperature_curves.csv", "候选温度曲线",
                new[] { "solution_index", "target_id", "source_case_id", "candidate_id", "time_s", "target_temperature_K", "m2_temperature_K", "forward_temperature_K" },
                new[] { "solution_index", "time_s", "target_temperature_K", "m2_temperature_K", "forward_temperature_K" }, true);
            AddTable(model, temperature);
            if (temperature != null)
            {
                var curves = new TemperatureResult { Name = "Target / Proxy / Forward", SourcePath = temperature.SourcePath };
                curves.Tables.Add(temperature);
                result.Temperatures.Add(curves);
            }
            AddTable(model, ReadCsv(result, "scene_search_log.csv", "场景搜索日志",
                new[] { "sequence_index", "target_id", "source_case_id", "candidate_id", "q_int", "emissivity_ir", "absorptivity_solar", "decision" },
                new[] { "sequence_index", "q_int", "emissivity_ir", "absorptivity_solar" }, true));
            result.Scenes.Add(model);
            foreach (var item in model.Summary) result.Summary["scene." + item.Key] = item.Value;
            result.Summary["reader"] = "SceneResultReader";
            return result;
        }

        private static void AddTable(SceneResult model, ResultTable table)
        {
            if (table == null) return;
            model.Tables.Add(table);
            if (model.SourcePath == null) model.SourcePath = table.SourcePath;
        }
    }
}
