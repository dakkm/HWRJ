using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using PreProcess.Wpf.Services.Process;

namespace PreProcess.Wpf.Services.Execution
{
    public sealed class TrajectoryPostprocessService
    {
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private readonly ProcessManager manager = new ProcessManager();
        private readonly object gate = new object();
        // 传递并检查取消信号，使长时间任务可以安全停止。
        private CancellationTokenSource stop;
        public event Action<ProcessLogEvent> Log;
        public event Action<ProcessRunState> StateChanged;
        // 传递并检查取消信号，使长时间任务可以安全停止。
        public void Stop() { lock (gate) stop?.Cancel(); manager.Stop(); }

        public async Task<RunRecord> RunAsync(string selectedTask, bool includePrerelease, BackendPaths paths = null,
            // 传递并检查取消信号，使长时间任务可以安全停止。
            CancellationToken token = default(CancellationToken))
        {
            var lease = ExecutionLease.TryEnter();
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (lease == null) throw new InvalidOperationException("已有模块正在运行，请等待或停止。");
            var record = new RunRecord { Module = "轨迹", StartedAt = DateTimeOffset.Now, State = ProcessRunState.Preparing };
            using (lease)
            using (var cancel = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                lock (gate) stop = cancel;
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                StreamWriter stdout = null, stderr = null;
                string submission = null;
                // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
                Action<ProcessLogEvent> onLog = item =>
                {
                    try { (item.IsError ? stderr : stdout)?.WriteLine(item.Text); } catch { manager.Stop(); }
                    // 将界面状态变更调度到用户界面线程，避免跨线程访问。
                    Log?.Invoke(item);
                };
                Action<ProcessRunState> onState = state =>
                { if (state == ProcessRunState.Running || state == ProcessRunState.Stopping) StateChanged?.Invoke(state); };
                // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
                manager.Log += onLog; manager.StateChanged += onState;
                try
                {
                    // 将界面状态变更调度到用户界面线程，避免跨线程访问。
                    StateChanged?.Invoke(ProcessRunState.Preparing);
                    paths = paths ?? new BackendPathResolver().Resolve();
                    // 校验当前条件，仅在满足业务约束时进入该处理分支。
                    if (String.IsNullOrWhiteSpace(selectedTask) || !Directory.Exists(selectedTask))
                        throw new DirectoryNotFoundException("当前任务结果目录不存在。");
                    // 处理文件系统路径及数据，并在使用前确认目标有效。
                    record.TaskDirectory = Path.GetFullPath(selectedTask);
                    string source = FindLatestForwardOutput(selectedTask);
                    string sourceTrajectory = Path.Combine(source, "trajectory_history.csv");
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    submission = TaskDirectoryManager.Create(paths.RuntimeRoot, "轨迹", selectedTask);
                    record.ExecutionDirectory = submission;
                    // 处理文件系统路径及数据，并在使用前确认目标有效。
                    record.RunId = Path.GetFileName(submission);
                    record.RunDirectory = submission;
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    record.BackendRequestPath = sourceTrajectory;
                    string resultDirectory = Path.Combine(submission, "results");
                    Directory.CreateDirectory(resultDirectory);
                    // 处理文件系统路径及数据，并在使用前确认目标有效。
                    string trajectoryCopy = Path.Combine(resultDirectory, "trajectory_history.csv");
                    File.Copy(sourceTrajectory, trajectoryCopy, false);
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    record.ResultDirectory = resultDirectory;
                    record.RequestPath = Path.Combine(submission, "trajectory-request.json");
                    // 调用对应组件完成当前步骤，并保留产生的处理结果。
                    ModuleExecutionService.WriteObject(record.RequestPath, new
                    {
                        schema_version = "trajectory-postprocess-request-v1", source_forward_output = source,
                        trajectory_history = sourceTrajectory, include_prerelease = includePrerelease
                    // 继续处理当前业务步骤，保持上下文状态一致。
                    });
                    stdout = new StreamWriter(Path.Combine(submission, "stdout.log"), false, new UTF8Encoding(false)) { AutoFlush = true };
                    // 处理文件系统路径及数据，并在使用前确认目标有效。
                    stderr = new StreamWriter(Path.Combine(submission, "stderr.log"), false, new UTF8Encoding(false)) { AutoFlush = true };

                    string runtimePackage = await Task.Run(() => RuntimePackage.Prepare(paths, cancel.Token), cancel.Token).ConfigureAwait(false);
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    record.RuntimePackage = runtimePackage;
                    string entry = Path.Combine(runtimePackage, "01-正向仿真", "02-程序", "postprocess_trajectory.py");
                    if (!BackendPathResolver.IsWithin(entry, runtimePackage) || !File.Exists(entry))
                        // 发现无效输入或运行状态后立即终止，防止错误继续传播。
                        throw new FileNotFoundException("轨迹后处理入口不存在。", entry);
                    string postprocess = Path.Combine(resultDirectory, "trajectory_postprocess");
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    var request = new ProcessRunRequest
                    {
                        Executable = paths.Python, WorkingDirectory = runtimePackage,
                        // 更新当前流程使用的数据，为下一处理步骤做好准备。
                        Arguments = new List<string> { "-B", "-u", entry, "--input", sourceTrajectory, "--outdir", postprocess },
                        Timeout = TimeSpan.FromMinutes(30),
                        EnvironmentVariables = new Dictionary<string, string> { { "PYTHONUTF8", "1" }, { "PYTHONIOENCODING", "utf-8" }, { "PYTHONDONTWRITEBYTECODE", "1" } }
                    };
                    // 校验当前条件，仅在满足业务约束时进入该处理分支。
                    if (includePrerelease) request.Arguments.Add("--include-prerelease");
                    onLog(new ProcessLogEvent { Text = "读取正向计算轨迹：" + sourceTrajectory + "\n轨迹后处理入口：" + entry });
                    // 异步等待耗时任务完成，期间保持界面线程可响应。
                    ProcessRunResult result = await manager.RunAsync(request, cancel.Token).ConfigureAwait(false);
                    record.TrajectoryExitCode = result.ExitCode; record.ExitCode = result.ExitCode; record.State = result.State;
                    // 校验当前条件，仅在满足业务约束时进入该处理分支。
                    if (record.State == ProcessRunState.Cancelled) throw new OperationCanceledException(cancel.Token);
                    if (record.State != ProcessRunState.Completed || result.ExitCode != 0)
                        // 发现无效输入或运行状态后立即终止，防止错误继续传播。
                        throw new InvalidOperationException("轨迹后处理入口执行失败。" + (String.IsNullOrWhiteSpace(result.Error) ? String.Empty : " " + result.Error));
                    string metrics = Path.Combine(postprocess, "trajectory_metrics.csv");
                    if (!File.Exists(metrics)) throw new FileNotFoundException("轨迹后处理未生成统计文件。", metrics);
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    record.TrajectoryResultDirectory = postprocess;
                    record.BackendStatus = "success";
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    record.Message = "已读取最近一次正向计算结果并完成轨迹后处理。";
                }
                catch (OperationCanceledException) { record.State = ProcessRunState.Cancelled; record.Message = "轨迹后处理已停止。"; }
                // 捕获本步骤产生的异常，将故障转换为可追踪的运行状态。
                catch (Exception ex)
                {
                    record.State = ProcessRunState.Failed; record.Message = ex.Message; record.Diagnostic = ex.ToString();
                    onLog(new ProcessLogEvent { IsError = true, Text = ex.ToString() });
                }
                // 无论执行成功与否都释放资源并恢复组件的可用状态。
                finally
                {
                    record.EndedAt = DateTimeOffset.Now;
                    // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
                    manager.Log -= onLog; manager.StateChanged -= onState;
                    try { stdout?.Dispose(); } finally { stderr?.Dispose(); }
                    // 校验当前条件，仅在满足业务约束时进入该处理分支。
                    if (submission != null) ModuleExecutionService.WriteObject(Path.Combine(submission, "run-location.json"), record);
                    if (record.State == ProcessRunState.Completed && !String.IsNullOrWhiteSpace(record.ResultDirectory) && Directory.Exists(record.ResultDirectory))
                        ModuleExecutionService.WriteResultIndex(record);
                    // 对共享状态加锁，避免并发读写造成数据竞争。
                    lock (gate) stop = null;
                    StateChanged?.Invoke(record.State);
                }
            }
            // 返回当前步骤生成的结果，并结束本次调用。
            return record;
        }

        internal static string FindLatestForwardOutput(string selectedTask)
        {
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string forward = Path.GetFullPath(selectedTask);
            if (!Directory.Exists(forward)) throw new DirectoryNotFoundException("当前任务尚无正向计算结果，请先运行正向计算。");
            var serializer = new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 };
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            RunRecord latest = Directory.GetFiles(forward, "run-location.json", SearchOption.AllDirectories)
                .Select(path => { try { return serializer.Deserialize<RunRecord>(File.ReadAllText(path, Encoding.UTF8)); } catch { return null; } })
                // 检查输入及依赖是否满足要求，提前阻止无效操作。
                .Where(item => item != null && item.Module == "01" && item.State == ProcessRunState.Completed && !String.IsNullOrWhiteSpace(item.ResultDirectory) &&
                    BackendPathResolver.IsWithin(item.ResultDirectory, selectedTask) && Directory.Exists(item.ResultDirectory) &&
                    // 处理文件系统路径及数据，并在使用前确认目标有效。
                    File.Exists(Path.Combine(item.ResultDirectory, "trajectory_history.csv")))
                .OrderByDescending(item => item.EndedAt).FirstOrDefault();
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (latest == null) throw new InvalidOperationException("当前任务没有成功且包含 trajectory_history.csv 的正向计算结果，请先完成正向计算。");
            return Path.GetFullPath(latest.ResultDirectory);
        }
    }
}
