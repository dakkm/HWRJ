using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
// 说明当前文件承载的类型职责，便于维护者快速定位功能边界。
using PreProcess.Wpf.Models;
namespace PreProcess.Wpf.Services.Execution
{
    // 定义 PredictionService 类型，集中封装与该领域对象相关的状态和行为。
    public sealed class PredictionService : ModuleExecutionService
    {
        public Task<RunRecord> RunAsync(TaskModel task, string mode = "both", BackendPaths paths = null, CancellationToken token = default(CancellationToken), string selectedTask = null)
        {
            return ExecuteAsync("02", paths, (package, submission, request, record) =>
            {
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (mode != "temperature" && mode != "point-image" && mode != "both") throw new ArgumentException("智能预测运行模式无效。");
                // 本文件只处理所属模块的数据或界面逻辑，不在此处引入额外副作用。
                record.RequestPath = new RequestGenerator().Save(task, Path.Combine(submission, "request.json"));
                // 将当前结果加入集合，供后续汇总或界面展示。
                request.Arguments.Add("--params-json"); request.Arguments.Add(record.RequestPath);
                request.Arguments.Add("--mode"); request.Arguments.Add(mode);
            // 继续处理当前业务步骤，保持上下文状态一致。
            }, token, null, selectedTask);
        }
    }
}
