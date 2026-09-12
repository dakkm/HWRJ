using System;
using System.Collections.Generic;
using System.IO;
using PreProcess.Wpf.Models.Results;

namespace PreProcess.Wpf.Services.Results
{
    public sealed class SimilarityResultReader : ResultReaderBase
    {
        public override RunResult Read(RunResult result)
        {
            if (!Prepare(result, ResultModuleType.Similarity)) return result;
            IDictionary<string, object> status = ReadJson(result, "evaluation_status.json", true);
            ValidateIdentity(status, result, "03", "evaluation_status.json");
            string mode = OptionalString(status, "mode");
            var model = new SimilarityResult { Name = mode == "features" ? "响应特征" : "相似度评估" };
            AddJsonSummary(model.Summary, status, "status.");

            if (mode == "features") ReadFeatures(result, model, String.Empty, true, "Temperature");
            else
            {
                IDictionary<string, object> summary = ReadJson(result, "similarity_summary.json", true);
                AddJsonSummary(model.Summary, summary, "summary.");
                ResultTable components = ReadCsv(result, "similarity_components.csv", "相似度分量",
                    new[] { "feature_name", "category", "raw_difference", "raw_unit", "similarity_percent", "valid_flag", "invalid_reason", "valid_sample_count" },
                    new[] { "valid_flag", "valid_sample_count" }, true);
                if (components != null) { model.Tables.Add(components); model.SourcePath = components.SourcePath; }
                ReadFeatures(result, model, "reference_features", false, "Reference");
                ReadFeatures(result, model, "candidate_features", false, "Candidate");
                if (mode != null && mode != "similarity") AddIssue(result, "evaluation_status.json 包含未知 mode：" + mode);
            }
            result.Similarities.Add(model);
            foreach (var item in model.Summary) result.Summary["similarity." + item.Key] = item.Value;
            result.Summary["reader"] = "SimilarityResultReader";
            return result;
        }

        private static void ReadFeatures(RunResult result, SimilarityResult model, string directory, bool required, string temperatureName)
        {
            string prefix = String.IsNullOrEmpty(directory) ? String.Empty : directory + Path.DirectorySeparatorChar;
            IDictionary<string, object> summary = ReadJson(result, prefix + "feature_summary.json", required);
            AddJsonSummary(model.Summary, summary, (String.IsNullOrEmpty(directory) ? "features" : directory) + ".");
            ResultTable temperature = ReadCsv(result, prefix + "feature_timeseries.csv", Name(directory, "场景特征时序"), new[] { "time_s", "temperature_object_id", "temperature_K" }, new[] { "time_s", "temperature_object_id", "temperature_K" }, required);
            AddTable(model, temperature);
            if (temperature != null)
            {
                var curve = new TemperatureResult { Name = temperatureName, SourcePath = temperature.SourcePath };
                curve.Tables.Add(temperature);
                result.Temperatures.Add(curve);
            }
            AddTable(model, ReadCsv(result, prefix + "object_feature_timeseries.csv", Name(directory, "目标特征时序"), new[] { "case_id", "frame_id", "time_s", "object_id" }, new[] { "frame_id", "time_s", "object_id" }, required));
            AddTable(model, ReadCsv(result, prefix + "periodic_features.csv", Name(directory, "周期特征"), new[] { "signal", "periodic_valid", "sample_count" }, new[] { "periodic_valid", "sample_count" }, required));
        }

        private static string Name(string directory, string name)
        {
            return String.IsNullOrEmpty(directory) ? name : directory + " " + name;
        }

        private static void AddTable(SimilarityResult model, ResultTable table)
        {
            if (table == null) return;
            model.Tables.Add(table);
            if (model.SourcePath == null) model.SourcePath = table.SourcePath;
        }
    }
}
