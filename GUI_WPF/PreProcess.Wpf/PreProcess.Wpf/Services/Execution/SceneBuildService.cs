using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
// 说明当前文件承载的类型职责，便于维护者快速定位功能边界。
using PreProcess.Wpf.Models;
namespace PreProcess.Wpf.Services.Execution
{
    // 定义 SceneBuildService 类型，集中封装与该领域对象相关的状态和行为。
    public sealed class SceneBuildService : ModuleExecutionService
    {
        public Task<RunRecord> RunAsync(TaskModel task, int candidates, BackendPaths paths = null, CancellationToken token = default(CancellationToken), string selectedTask = null)
        {
            // 返回当前步骤生成的结果，并结束本次调用。
            return ExecuteAsync("04", paths, (package, submission, request, record) =>
            {
                if (candidates <= 0) throw new ArgumentException("候选数量必须为正整数。");
                string input = new RequestGenerator().Save(task, Path.Combine(submission, "request.json"));
                // 处理文件系统路径及数据，并在使用前确认目标有效。
                record.RequestPath = Path.Combine(submission, "scene-search-request.json");
                WriteObject(record.RequestPath, new { schema_version = "scene-search-request-v2", input_file = input,
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    similarity_requirement = new { metric = "temperature_similarity", target_object_id = 1, required_percent = task.Settings.SimilarityIndex }, required_candidate_count = candidates });
                // 本文件只处理所属模块的数据或界面逻辑，不在此处引入额外副作用。
                request.Arguments.Add("--request"); request.Arguments.Add(record.RequestPath);
                // 处理文件系统路径及数据，并在使用前确认目标有效。
                request.Arguments.Add("--config"); request.Arguments.Add(Path.Combine(package, "04-红外场景构建", "01-输入文件", "scene_search_config.json"));
                // 04 owns the full 02/01/03 chain and its reference cache. No GUI re-orchestration.
            }, token, null, selectedTask);
        }
    }
}
