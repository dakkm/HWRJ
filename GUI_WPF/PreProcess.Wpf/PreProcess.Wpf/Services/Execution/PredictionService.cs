using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PreProcess.Wpf.Models;
namespace PreProcess.Wpf.Services.Execution
{
    public sealed class PredictionService : ModuleExecutionService
    {
        public Task<RunRecord> RunAsync(TaskModel task, string mode = "temperature", BackendPaths paths = null, CancellationToken token = default(CancellationToken))
        {
            return ExecuteAsync("02", paths, (package, submission, request, record) =>
            {
                if (mode != "temperature" && mode != "point-image" && mode != "both") throw new ArgumentException("请选择温度、点图像或两者预测模式。");
                record.RequestPath = new RequestGenerator().Save(task, Path.Combine(submission, "request.json"));
                request.Arguments.Add("--params-json"); request.Arguments.Add(record.RequestPath);
                request.Arguments.Add("--mode"); request.Arguments.Add(mode);
            }, token);
        }
    }
}
