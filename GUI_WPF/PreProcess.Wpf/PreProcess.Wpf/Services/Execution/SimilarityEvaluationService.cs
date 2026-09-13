using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
namespace PreProcess.Wpf.Services.Execution
{
    public sealed class SimilarityEvaluationService : ModuleExecutionService
    {
        public Task<RunRecord> RunAsync(string reference, string candidate, string config = null, BackendPaths paths = null, CancellationToken token = default(CancellationToken))
        {
            return ExecuteAsync("03", paths, (package, submission, request, record) =>
            {
                CheckDirectory(reference); CheckDirectory(candidate);
                string evaluation = String.IsNullOrWhiteSpace(config) ? Path.Combine(package, "03-相似度评估", "01-输入文件", "similarity_config.json") : Path.GetFullPath(config);
                if (!File.Exists(evaluation)) throw new FileNotFoundException("评价配置不存在。", evaluation);
                // This is invocation metadata, not a new physical request contract.
                record.RequestPath = Path.Combine(submission, "evaluation-reference.json");
                WriteObject(record.RequestPath, new { reference_result = Path.GetFullPath(reference), candidate_result = Path.GetFullPath(candidate), evaluation_config = evaluation });
                foreach (string arg in new[] { "--mode", "similarity", "--reference-run", Path.GetFullPath(reference), "--candidate-run", Path.GetFullPath(candidate), "--config", evaluation }) request.Arguments.Add(arg);
            }, token);
        }
        public static void CheckDirectory(string path)
        {
            if (String.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) throw new DirectoryNotFoundException("请选择有效的参考/候选结果目录。");
            if (!File.Exists(Path.Combine(path, "temperature_history.csv")) || !File.Exists(Path.Combine(path, "infrared_response_history.csv")))
                throw new ArgumentException("当前03入口要求标准响应output目录，其中必须包含温度与红外历史文件。请选择01正向output，或02在both模式下生成的run_xxx\\output。");
            // Detailed column/time-axis checks remain with the authoritative backend extractor.
        }
    }
}
