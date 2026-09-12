using System;
using System.IO;
using PreProcess.Wpf.Models.Results;

namespace PreProcess.Wpf.Services.Results
{
    public sealed class ResultDirectoryLoader
    {
        public RunResult Load(string selectedDirectory)
        {
            if (String.IsNullOrWhiteSpace(selectedDirectory))
                throw new ArgumentException("请选择运行结果目录。", "selectedDirectory");
            string selected = Path.GetFullPath(selectedDirectory);
            if (!Directory.Exists(selected)) throw new DirectoryNotFoundException("结果目录不存在：" + selected);

            string nested = Path.Combine(selected, "output");
            string output = Directory.Exists(nested) && HasKnownMarker(nested) ? nested : selected;
            ResultModuleType module = DetectModule(output);
            string runDirectory = String.Equals(Path.GetFileName(output), "output", StringComparison.OrdinalIgnoreCase)
                ? Directory.GetParent(output).FullName : selected;
            string runId = Path.GetFileName(runDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (String.IsNullOrWhiteSpace(runId)) runId = "imported_result";

            RunResult seed = new ResultDirectoryManager().Create(module, runId, FindRequest(runDirectory, output), output, runDirectory);
            seed.RunState = "Completed";
            seed.AvailabilityMessage = "已导入已有计算结果。";
            return new ResultReader().Read(seed);
        }

        public ResultModuleType DetectModule(string outputDirectory)
        {
            if (String.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
                throw new DirectoryNotFoundException("无法识别不存在的结果目录。" );
            if (File.Exists(Path.Combine(outputDirectory, "prediction_summary.json")) ||
                File.Exists(Path.Combine(outputDirectory, "temperature_prediction.csv")) ||
                File.Exists(Path.Combine(outputDirectory, "point_token_predictions.csv.gz"))) return ResultModuleType.Prediction;
            if (File.Exists(Path.Combine(outputDirectory, "evaluation_status.json")) ||
                File.Exists(Path.Combine(outputDirectory, "similarity_summary.json"))) return ResultModuleType.Similarity;
            if (File.Exists(Path.Combine(outputDirectory, "scene_search_summary.json")) ||
                File.Exists(Path.Combine(outputDirectory, "candidate_temperature_curves.csv"))) return ResultModuleType.Scene;
            if (File.Exists(Path.Combine(outputDirectory, "temperature_history.csv")) ||
                File.Exists(Path.Combine(outputDirectory, "infrared_response_history.csv"))) return ResultModuleType.Forward;
            throw new InvalidDataException("无法识别模块类型：目录中没有受支持的结果标志文件。" );
        }

        private static bool HasKnownMarker(string path)
        {
            return File.Exists(Path.Combine(path, "temperature_history.csv")) || File.Exists(Path.Combine(path, "infrared_response_history.csv")) ||
                   File.Exists(Path.Combine(path, "prediction_summary.json")) || File.Exists(Path.Combine(path, "evaluation_status.json")) ||
                   File.Exists(Path.Combine(path, "scene_search_summary.json"));
        }

        private static string FindRequest(string runDirectory, string outputDirectory)
        {
            string path = Path.Combine(runDirectory, "request.json");
            if (File.Exists(path)) return path;
            path = Path.Combine(outputDirectory, "request.json");
            return File.Exists(path) ? path : null;
        }
    }
}
