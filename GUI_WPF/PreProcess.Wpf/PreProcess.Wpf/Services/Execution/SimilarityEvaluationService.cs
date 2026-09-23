using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
namespace PreProcess.Wpf.Services.Execution
// 说明当前文件承载的类型职责，便于维护者快速定位功能边界。
{
    public sealed class SimilarityEvaluationService : ModuleExecutionService
    {
        // 异步等待耗时任务完成，期间保持界面线程可响应。
        public Task<RunRecord> RunAsync(string reference, string candidate, string config = null, BackendPaths paths = null, CancellationToken token = default(CancellationToken), string selectedTask = null)
        {
            return ExecuteAsync("03", paths, (package, submission, request, record) =>
            {
                // 检查输入及依赖是否满足要求，提前阻止无效操作。
                CheckDirectory(reference); CheckDirectory(candidate);
                string evaluation = String.IsNullOrWhiteSpace(config) ? Path.Combine(package, "03-相似度评估", "01-输入文件", "similarity_config.json") : Path.GetFullPath(config);
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (!File.Exists(evaluation)) throw new FileNotFoundException("评价配置不存在。", evaluation);
                // This is invocation metadata, not a new physical request contract.
                record.RequestPath = Path.Combine(submission, "evaluation-reference.json");
                WriteObject(record.RequestPath, new { reference_result = Path.GetFullPath(reference), candidate_result = Path.GetFullPath(candidate), evaluation_config = evaluation });
                // 遍历当前数据集合，逐项完成必要的转换或状态更新。
                foreach (string arg in new[] { "--mode", "similarity", "--reference-run", Path.GetFullPath(reference), "--candidate-run", Path.GetFullPath(candidate), "--config", evaluation }) request.Arguments.Add(arg);
            }, token, null, selectedTask);
        }
        // 检查输入及依赖是否满足要求，提前阻止无效操作。
        public static void CheckDirectory(string path)
        // 本文件只处理所属模块的数据或界面逻辑，不在此处引入额外副作用。
        {
            if (String.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) throw new DirectoryNotFoundException("请选择有效的参考/候选结果目录。");
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            bool formalForward = File.Exists(Path.Combine(path, "temperature_history.csv")) && File.Exists(Path.Combine(path, "infrared_response_history.csv"));
            bool prediction = File.Exists(Path.Combine(path, "temperature_prediction.csv")) && File.Exists(Path.Combine(path, "point_token_predictions.csv.gz"));
            if (!formalForward && !prediction)
                throw new ArgumentException("请选择01正向计算结果，或同时包含温度预测和点图像预测文件的02智能预测结果目录。");
            // Detailed column/time-axis checks remain with the authoritative backend extractor.
        }
    }
}
