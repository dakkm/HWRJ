using System;
using System.IO;
using PreProcess.Wpf.Models.Results;
using PreProcess.Wpf.Services.Execution;

namespace PreProcess.Wpf.Services.Results
{
    public sealed class ResultDirectoryManager
    {
        public RunResult Locate(ResultModuleType moduleType, string runId, string inputRequestPath, string runsRoot)
        {
            ValidateRunId(runId);
            if (String.IsNullOrWhiteSpace(runsRoot)) throw new ArgumentException("必须提供模块运行根目录。", "runsRoot");

            string normalizedRoot = Path.GetFullPath(runsRoot);
            string runDirectory = Path.GetFullPath(Path.Combine(normalizedRoot, runId));
            if (!IsWithin(runDirectory, normalizedRoot)) throw new ArgumentException("run_id 定位结果超出运行根目录。", "runId");

            string outputDirectory = UsesNestedOutputDirectory(moduleType)
                ? Path.Combine(runDirectory, "output")
                : runDirectory;
            return Create(moduleType, runId, inputRequestPath, outputDirectory, runDirectory);
        }

        public RunResult Create(ResultModuleType moduleType, string runId, string inputRequestPath,
            string outputDirectory, string runDirectory = null)
        {
            ValidateRunId(runId);
            string normalizedOutput = NormalizeOptionalPath(outputDirectory);
            string normalizedRun = NormalizeOptionalPath(runDirectory);
            if (normalizedRun == null && normalizedOutput != null)
            {
                DirectoryInfo parent = UsesNestedOutputDirectory(moduleType) ? Directory.GetParent(normalizedOutput) : null;
                normalizedRun = parent == null ? normalizedOutput : parent.FullName;
            }

            bool exists = normalizedOutput != null && Directory.Exists(normalizedOutput);
            return new RunResult
            {
                RunId = runId,
                ModuleType = moduleType,
                ModuleCode = GetModuleCode(moduleType),
                InputRequestPath = NormalizeOptionalPath(inputRequestPath),
                RunDirectory = normalizedRun,
                OutputDirectory = normalizedOutput,
                ResultExists = exists,
                AvailabilityMessage = exists ? "结果目录已定位。" : normalizedOutput == null
                    ? "未提供结果目录。"
                    : "结果目录不存在：" + normalizedOutput
            };
        }

        public RunResult Create(RunRecord record, ResultModuleType? moduleOverride = null)
        {
            if (record == null) throw new ArgumentNullException("record");
            ResultModuleType recordedModule = ParseModule(record.Module);
            ResultModuleType moduleType = moduleOverride ?? recordedModule;
            if (GetModuleCode(recordedModule) != GetModuleCode(moduleType))
                throw new ArgumentException("指定结果模块与运行记录不一致。", "moduleOverride");

            string outputDirectory = record.ResultDirectory;
            if (String.IsNullOrWhiteSpace(outputDirectory) && !String.IsNullOrWhiteSpace(record.RunDirectory))
                outputDirectory = UsesNestedOutputDirectory(moduleType)
                    ? Path.Combine(record.RunDirectory, "output")
                    : record.RunDirectory;

            RunResult result = Create(moduleType, record.RunId, record.RequestPath, outputDirectory, record.RunDirectory);
            result.StartedAt = record.StartedAt;
            result.EndedAt = record.EndedAt;
            result.RunState = record.State.ToString();
            return result;
        }

        public bool ResultExists(RunResult result)
        {
            if (result == null) throw new ArgumentNullException("result");
            return !String.IsNullOrWhiteSpace(result.OutputDirectory) && Directory.Exists(result.OutputDirectory);
        }

        public ResultModuleType ParseModule(string module)
        {
            switch ((module ?? String.Empty).Trim().ToLowerInvariant())
            {
                case "01": case "forward": return ResultModuleType.Forward;
                case "02": case "prediction": return ResultModuleType.Prediction;
                case "03": case "similarity": return ResultModuleType.Similarity;
                case "04": case "scene": return ResultModuleType.Scene;
                case "trajectory": case "轨迹": return ResultModuleType.Trajectory;
                default: throw new ArgumentException("无法识别结果模块：" + module, "module");
            }
        }

        public string GetModuleCode(ResultModuleType moduleType)
        {
            switch (moduleType)
            {
                case ResultModuleType.Forward:
                case ResultModuleType.Trajectory: return "01";
                case ResultModuleType.Prediction: return "02";
                case ResultModuleType.Similarity: return "03";
                case ResultModuleType.Scene: return "04";
                default: throw new ArgumentOutOfRangeException("moduleType");
            }
        }

        private static bool UsesNestedOutputDirectory(ResultModuleType moduleType)
        {
            return moduleType == ResultModuleType.Forward || moduleType == ResultModuleType.Trajectory;
        }

        private static void ValidateRunId(string runId)
        {
            if (String.IsNullOrWhiteSpace(runId)) throw new ArgumentException("run_id 不能为空。", "runId");
            if (runId == "." || runId == ".." || runId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                runId.IndexOf(Path.DirectorySeparatorChar) >= 0 || runId.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
                throw new ArgumentException("run_id 不是有效的单级目录名。", "runId");
        }

        private static string NormalizeOptionalPath(string path)
        {
            return String.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
        }

        private static bool IsWithin(string candidate, string root)
        {
            string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
