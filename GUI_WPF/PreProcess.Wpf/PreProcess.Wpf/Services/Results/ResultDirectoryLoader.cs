using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using PreProcess.Wpf.Models.Results;
using PreProcess.Wpf.Services.Execution;

namespace PreProcess.Wpf.Services.Results
{
    // 定义 ResultDirectoryLoader 类型，集中封装与该领域对象相关的状态和行为。
    public sealed class ResultDirectoryLoader
    {
        public RunResult Load(string selectedDirectory)
        {
            if (String.IsNullOrWhiteSpace(selectedDirectory))
                // 发现无效输入或运行状态后立即终止，防止错误继续传播。
                throw new ArgumentException("请选择结果索引或结果文件。", "selectedDirectory");
            string selected = Path.GetFullPath(selectedDirectory);
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string selectedFile = File.Exists(selected) ? selected : null;
            if (selectedFile != null) selected = Path.GetDirectoryName(selectedFile);
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (!Directory.Exists(selected)) throw new DirectoryNotFoundException("结果位置不存在：" + selected);

            RunRecord record = ReadRunRecord(selectedFile, selected);
            if (record != null && !String.IsNullOrWhiteSpace(record.ResultDirectory) && Directory.Exists(record.ResultDirectory))
                // 处理文件系统路径及数据，并在使用前确认目标有效。
                selected = Path.GetFullPath(record.ResultDirectory);

            string nested = Path.Combine(selected, "output");
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string output = Directory.Exists(nested) && HasKnownMarker(nested) ? nested : selected;
            IDictionary<string, object> index = ReadIndex(selectedFile, output);
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var manager = new ResultDirectoryManager();
            ResultModuleType module = index != null && index.ContainsKey("module")
                ? manager.ParseModule(Convert.ToString(index["module"]))
                // 将外部数据转换为目标类型，并保持约定的表示格式。
                : record != null ? manager.ParseModule(record.Module) : DetectModule(output);
            string runDirectory = String.Equals(Path.GetFileName(output), "output", StringComparison.OrdinalIgnoreCase)
                // 处理文件系统路径及数据，并在使用前确认目标有效。
                ? Directory.GetParent(output).FullName : selected;
            string runId = index != null && index.ContainsKey("run_id") ? Convert.ToString(index["run_id"]) :
                // 检查输入及依赖是否满足要求，提前阻止无效操作。
                record != null && !String.IsNullOrWhiteSpace(record.RunId) ? record.RunId :
                Path.GetFileName(runDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (String.IsNullOrWhiteSpace(runId)) runId = "imported_result";

            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            RunResult seed = manager.Create(module, runId, FindRequest(runDirectory, output), output, runDirectory);
            seed.RunState = "Completed";
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            seed.AvailabilityMessage = "已导入已有计算结果。";
            return new ResultReader().Read(seed);
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static RunRecord ReadRunRecord(string selectedFile, string selectedDirectory)
        {
            string path = selectedFile != null && String.Equals(Path.GetFileName(selectedFile), "run-location.json", StringComparison.OrdinalIgnoreCase)
                ? selectedFile : Path.Combine(selectedDirectory, "run-location.json");
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (!File.Exists(path)) return null;
            try { return new JavaScriptSerializer().Deserialize<RunRecord>(File.ReadAllText(path, Encoding.UTF8)); }
            // 捕获本步骤产生的异常，将故障转换为可追踪的运行状态。
            catch (Exception) { return null; }
        }

        private static IDictionary<string, object> ReadIndex(string selectedFile, string outputDirectory)
        {
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string path = selectedFile != null && String.Equals(Path.GetFileName(selectedFile), "result-index.json", StringComparison.OrdinalIgnoreCase)
                ? selectedFile : Path.Combine(outputDirectory, "result-index.json");
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (!File.Exists(path)) return null;
            try
            {
                var value = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(path, Encoding.UTF8)) as IDictionary<string, object>;
                // 返回当前步骤生成的结果，并结束本次调用。
                return value != null && Convert.ToString(value.ContainsKey("schema_version") ? value["schema_version"] : null) == "preprocess-result-index-v1" ? value : null;
            }
            catch (Exception) { return null; }
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public ResultModuleType DetectModule(string outputDirectory)
        {
            if (String.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
                // 发现无效输入或运行状态后立即终止，防止错误继续传播。
                throw new DirectoryNotFoundException("无法识别不存在的结果目录。" );
            if (File.Exists(Path.Combine(outputDirectory, "prediction_summary.json")) ||
                File.Exists(Path.Combine(outputDirectory, "temperature_prediction.csv")) ||
                // 处理文件系统路径及数据，并在使用前确认目标有效。
                File.Exists(Path.Combine(outputDirectory, "point_token_predictions.csv.gz"))) return ResultModuleType.Prediction;
            if (File.Exists(Path.Combine(outputDirectory, "evaluation_status.json")) ||
                // 处理文件系统路径及数据，并在使用前确认目标有效。
                File.Exists(Path.Combine(outputDirectory, "similarity_summary.json"))) return ResultModuleType.Similarity;
            if (File.Exists(Path.Combine(outputDirectory, "scene_search_summary.json")) ||
                // 处理文件系统路径及数据，并在使用前确认目标有效。
                File.Exists(Path.Combine(outputDirectory, "candidate_temperature_curves.csv"))) return ResultModuleType.Scene;
            if (File.Exists(Path.Combine(outputDirectory, "temperature_history.csv")) ||
                File.Exists(Path.Combine(outputDirectory, "infrared_response_history.csv"))) return ResultModuleType.Forward;
            // 发现无效输入或运行状态后立即终止，防止错误继续传播。
            throw new InvalidDataException("无法识别模块类型：目录中没有受支持的结果标志文件。" );
        }

        private static bool HasKnownMarker(string path)
        {
            // 返回当前步骤生成的结果，并结束本次调用。
            return File.Exists(Path.Combine(path, "temperature_history.csv")) || File.Exists(Path.Combine(path, "infrared_response_history.csv")) ||
                   File.Exists(Path.Combine(path, "prediction_summary.json")) || File.Exists(Path.Combine(path, "evaluation_status.json")) ||
                   // 处理文件系统路径及数据，并在使用前确认目标有效。
                   File.Exists(Path.Combine(path, "scene_search_summary.json"));
        }

        private static string FindRequest(string runDirectory, string outputDirectory)
        {
            string path = Path.Combine(runDirectory, "request.json");
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (File.Exists(path)) return path;
            path = Path.Combine(outputDirectory, "request.json");
            // 返回当前步骤生成的结果，并结束本次调用。
            return File.Exists(path) ? path : null;
        }
    }
}
