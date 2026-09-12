using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PreProcess.Wpf.Models;
namespace PreProcess.Wpf.Services.Execution
{
    public sealed class SceneBuildService : ModuleExecutionService
    {
        public Task<RunRecord> RunAsync(TaskModel task, int candidates, BackendPaths paths = null, CancellationToken token = default(CancellationToken))
        {
            return ExecuteAsync("04", paths, (package, submission, request, record) =>
            {
                if (candidates <= 0) throw new ArgumentException("候选数量必须为正整数。");
                string input = new RequestGenerator().Save(task, Path.Combine(submission, "request.json"));
                record.RequestPath = Path.Combine(submission, "scene-search-request.json");
                WriteObject(record.RequestPath, new { schema_version = "scene-search-request-v2", input_file = input,
                    similarity_requirement = new { metric = "temperature_similarity", target_object_id = 1, required_percent = task.Settings.SimilarityIndex }, required_candidate_count = candidates });
                request.Arguments.Add("--request"); request.Arguments.Add(record.RequestPath);
                request.Arguments.Add("--config"); request.Arguments.Add(Path.Combine(package, "04-红外场景构建", "01-输入文件", "scene_search_config.json"));
                // 04 owns the full 02/01/03 chain and its reference cache. No GUI re-orchestration.
            }, token);
        }
    }
}
