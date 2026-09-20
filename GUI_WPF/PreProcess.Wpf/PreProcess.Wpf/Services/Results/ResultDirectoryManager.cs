using System;
using System.IO;
using PreProcess.Wpf.Models.Results;
using PreProcess.Wpf.Services.Execution;

namespace PreProcess.Wpf.Services.Results
{
    public sealed class ResultDirectoryManager
    {
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public RunResult Locate(ResultModuleType moduleType, string runId, string inputRequestPath, string runsRoot)
        {
            ValidateRunId(runId);
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (String.IsNullOrWhiteSpace(runsRoot)) throw new ArgumentException("必须提供模块运行根目录。", "runsRoot");

            string normalizedRoot = Path.GetFullPath(runsRoot);
            string runDirectory = Path.GetFullPath(Path.Combine(normalizedRoot, runId));
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (!IsWithin(runDirectory, normalizedRoot)) throw new ArgumentException("run_id 定位结果超出运行根目录。", "runId");

            string outputDirectory = UsesNestedOutputDirectory(moduleType)
                // 处理文件系统路径及数据，并在使用前确认目标有效。
                ? Path.Combine(runDirectory, "output")
                : runDirectory;
            // 返回当前步骤生成的结果，并结束本次调用。
            return Create(moduleType, runId, inputRequestPath, outputDirectory, runDirectory);
        }

        public RunResult Create(ResultModuleType moduleType, string runId, string inputRequestPath,
            string outputDirectory, string runDirectory = null)
        {
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            ValidateRunId(runId);
            string normalizedOutput = NormalizeOptionalPath(outputDirectory);
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            string normalizedRun = NormalizeOptionalPath(runDirectory);
            if (normalizedRun == null && normalizedOutput != null)
            {
                // 处理文件系统路径及数据，并在使用前确认目标有效。
                DirectoryInfo parent = UsesNestedOutputDirectory(moduleType) ? Directory.GetParent(normalizedOutput) : null;
                normalizedRun = parent == null ? normalizedOutput : parent.FullName;
            }

            bool exists = normalizedOutput != null && Directory.Exists(normalizedOutput);
            // 返回当前步骤生成的结果，并结束本次调用。
            return new RunResult
            {
                RunId = runId,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                ModuleType = moduleType,
                ModuleCode = GetModuleCode(moduleType),
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                InputRequestPath = NormalizeOptionalPath(inputRequestPath),
                RunDirectory = normalizedRun,
                OutputDirectory = normalizedOutput,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                ResultExists = exists,
                AvailabilityMessage = exists ? "结果目录已定位。" : normalizedOutput == null
                    // 继续处理当前业务步骤，保持上下文状态一致。
                    ? "未提供结果目录。"
                    : "结果目录不存在：" + normalizedOutput
            };
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public RunResult Create(RunRecord record, ResultModuleType? moduleOverride = null)
        {
            if (record == null) throw new ArgumentNullException("record");
            // 将外部数据转换为目标类型，并保持约定的表示格式。
            ResultModuleType recordedModule = ParseModule(record.Module);
            ResultModuleType moduleType = moduleOverride ?? recordedModule;
            if (GetModuleCode(recordedModule) != GetModuleCode(moduleType))
                // 发现无效输入或运行状态后立即终止，防止错误继续传播。
                throw new ArgumentException("指定结果模块与运行记录不一致。", "moduleOverride");

            string outputDirectory = record.ResultDirectory;
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (String.IsNullOrWhiteSpace(outputDirectory) && !String.IsNullOrWhiteSpace(record.RunDirectory))
                outputDirectory = UsesNestedOutputDirectory(moduleType)
                    // 处理文件系统路径及数据，并在使用前确认目标有效。
                    ? Path.Combine(record.RunDirectory, "output")
                    : record.RunDirectory;

            RunResult result = Create(moduleType, record.RunId, record.RequestPath, outputDirectory, record.RunDirectory);
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            result.StartedAt = record.StartedAt;
            result.EndedAt = record.EndedAt;
            // 将外部数据转换为目标类型，并保持约定的表示格式。
            result.RunState = record.State.ToString();
            return result;
        }

        // 检查输入及依赖是否满足要求，提前阻止无效操作。
        public bool ResultExists(RunResult result)
        {
            if (result == null) throw new ArgumentNullException("result");
            return !String.IsNullOrWhiteSpace(result.OutputDirectory) && Directory.Exists(result.OutputDirectory);
        }

        // 将外部数据转换为目标类型，并保持约定的表示格式。
        public ResultModuleType ParseModule(string module)
        {
            switch ((module ?? String.Empty).Trim().ToLowerInvariant())
            {
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "01": case "forward": return ResultModuleType.Forward;
                case "02": case "prediction": return ResultModuleType.Prediction;
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "03": case "similarity": return ResultModuleType.Similarity;
                case "04": case "scene": return ResultModuleType.Scene;
                case "trajectory": case "轨迹": return ResultModuleType.Trajectory;
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                default: throw new ArgumentException("无法识别结果模块：" + module, "module");
            }
        }

        public string GetModuleCode(ResultModuleType moduleType)
        {
            // 按照当前状态分派处理逻辑，避免不同业务路径相互干扰。
            switch (moduleType)
            {
                case ResultModuleType.Forward:
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case ResultModuleType.Trajectory: return "01";
                case ResultModuleType.Prediction: return "02";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case ResultModuleType.Similarity: return "03";
                case ResultModuleType.Scene: return "04";
                default: throw new ArgumentOutOfRangeException("moduleType");
            }
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static bool UsesNestedOutputDirectory(ResultModuleType moduleType)
        {
            return moduleType == ResultModuleType.Forward || moduleType == ResultModuleType.Trajectory;
        }

        // 检查输入及依赖是否满足要求，提前阻止无效操作。
        private static void ValidateRunId(string runId)
        {
            if (String.IsNullOrWhiteSpace(runId)) throw new ArgumentException("run_id 不能为空。", "runId");
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (runId == "." || runId == ".." || runId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                runId.IndexOf(Path.DirectorySeparatorChar) >= 0 || runId.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
                throw new ArgumentException("run_id 不是有效的单级目录名。", "runId");
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static string NormalizeOptionalPath(string path)
        {
            return String.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static bool IsWithin(string candidate, string root)
        {
            string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            // 返回当前步骤生成的结果，并结束本次调用。
            return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
