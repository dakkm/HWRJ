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
        private readonly ProcessManager manager = new ProcessManager();
        private readonly object gate = new object();
        private CancellationTokenSource stop;
        public event Action<ProcessLogEvent> Log;
        public event Action<ProcessRunState> StateChanged;
        public void Stop() { lock (gate) stop?.Cancel(); manager.Stop(); }

        public async Task<RunRecord> RunAsync(string selectedTask, bool includePrerelease, BackendPaths paths = null,
            CancellationToken token = default(CancellationToken))
        {
            var lease = ExecutionLease.TryEnter();
            if (lease == null) throw new InvalidOperationException("已有模块正在运行，请等待或停止。");
            var record = new RunRecord { Module = "轨迹", StartedAt = DateTimeOffset.Now, State = ProcessRunState.Preparing };
            using (lease)
            using (var cancel = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                lock (gate) stop = cancel;
                StreamWriter stdout = null, stderr = null;
                string submission = null;
                Action<ProcessLogEvent> onLog = item =>
                {
                    try { (item.IsError ? stderr : stdout)?.WriteLine(item.Text); } catch { manager.Stop(); }
                    Log?.Invoke(item);
                };
                Action<ProcessRunState> onState = state =>
                { if (state == ProcessRunState.Running || state == ProcessRunState.Stopping) StateChanged?.Invoke(state); };
                manager.Log += onLog; manager.StateChanged += onState;
                try
                {
                    StateChanged?.Invoke(ProcessRunState.Preparing);
                    paths = paths ?? new BackendPathResolver().Resolve();
                    if (String.IsNullOrWhiteSpace(selectedTask) || !Directory.Exists(selectedTask))
                        throw new DirectoryNotFoundException("当前任务结果目录不存在。");
                    record.TaskDirectory = Path.GetFullPath(selectedTask);
                    string source = FindLatestForwardOutput(selectedTask);
                    string sourceTrajectory = Path.Combine(source, "trajectory_history.csv");
                    submission = TaskDirectoryManager.Create(paths.RuntimeRoot, "轨迹", selectedTask);
                    record.ExecutionDirectory = submission;
                    record.RunId = Path.GetFileName(submission);
                    record.RunDirectory = submission;
                    record.BackendRequestPath = sourceTrajectory;
                    string resultDirectory = Path.Combine(submission, "results");
                    Directory.CreateDirectory(resultDirectory);
                    string trajectoryCopy = Path.Combine(resultDirectory, "trajectory_history.csv");
                    File.Copy(sourceTrajectory, trajectoryCopy, false);
                    record.ResultDirectory = resultDirectory;
                    record.RequestPath = Path.Combine(submission, "trajectory-request.json");
                    ModuleExecutionService.WriteObject(record.RequestPath, new
                    {
                        schema_version = "trajectory-postprocess-request-v1", source_forward_output = source,
                        trajectory_history = sourceTrajectory, include_prerelease = includePrerelease
                    });
                    stdout = new StreamWriter(Path.Combine(submission, "stdout.log"), false, new UTF8Encoding(false)) { AutoFlush = true };
                    stderr = new StreamWriter(Path.Combine(submission, "stderr.log"), false, new UTF8Encoding(false)) { AutoFlush = true };

                    string runtimePackage = await Task.Run(() => RuntimePackage.Prepare(paths, cancel.Token), cancel.Token).ConfigureAwait(false);
                    record.RuntimePackage = runtimePackage;
                    string entry = Path.Combine(runtimePackage, "01-正向仿真", "02-程序", "postprocess_trajectory.py");
                    if (!BackendPathResolver.IsWithin(entry, runtimePackage) || !File.Exists(entry))
                        throw new FileNotFoundException("轨迹后处理入口不存在。", entry);
                    string postprocess = Path.Combine(resultDirectory, "trajectory_postprocess");
                    var request = new ProcessRunRequest
                    {
                        Executable = paths.Python, WorkingDirectory = runtimePackage,
                        Arguments = new List<string> { "-B", "-u", entry, "--input", sourceTrajectory, "--outdir", postprocess },
                        Timeout = TimeSpan.FromMinutes(30),
                        EnvironmentVariables = new Dictionary<string, string> { { "PYTHONUTF8", "1" }, { "PYTHONIOENCODING", "utf-8" }, { "PYTHONDONTWRITEBYTECODE", "1" } }
                    };
                    if (includePrerelease) request.Arguments.Add("--include-prerelease");
                    onLog(new ProcessLogEvent { Text = "读取正向计算轨迹：" + sourceTrajectory + "\n轨迹后处理入口：" + entry });
                    ProcessRunResult result = await manager.RunAsync(request, cancel.Token).ConfigureAwait(false);
                    record.TrajectoryExitCode = result.ExitCode; record.ExitCode = result.ExitCode; record.State = result.State;
                    if (record.State == ProcessRunState.Cancelled) throw new OperationCanceledException(cancel.Token);
                    if (record.State != ProcessRunState.Completed || result.ExitCode != 0)
                        throw new InvalidOperationException("轨迹后处理入口执行失败。" + (String.IsNullOrWhiteSpace(result.Error) ? String.Empty : " " + result.Error));
                    string metrics = Path.Combine(postprocess, "trajectory_metrics.csv");
                    if (!File.Exists(metrics)) throw new FileNotFoundException("轨迹后处理未生成统计文件。", metrics);
                    record.TrajectoryResultDirectory = postprocess;
                    record.BackendStatus = "success";
                    record.Message = "已读取最近一次正向计算结果并完成轨迹后处理。";
                }
                catch (OperationCanceledException) { record.State = ProcessRunState.Cancelled; record.Message = "轨迹后处理已停止。"; }
                catch (Exception ex)
                {
                    record.State = ProcessRunState.Failed; record.Message = ex.Message; record.Diagnostic = ex.ToString();
                    onLog(new ProcessLogEvent { IsError = true, Text = ex.ToString() });
                }
                finally
                {
                    record.EndedAt = DateTimeOffset.Now;
                    manager.Log -= onLog; manager.StateChanged -= onState;
                    try { stdout?.Dispose(); } finally { stderr?.Dispose(); }
                    if (submission != null) ModuleExecutionService.WriteObject(Path.Combine(submission, "run-location.json"), record);
                    lock (gate) stop = null;
                    StateChanged?.Invoke(record.State);
                }
            }
            return record;
        }

        internal static string FindLatestForwardOutput(string selectedTask)
        {
            string forward = Path.Combine(Path.GetFullPath(selectedTask), "01-正向计算");
            if (!Directory.Exists(forward)) throw new DirectoryNotFoundException("当前任务尚无正向计算结果，请先运行正向计算。");
            var serializer = new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 };
            RunRecord latest = Directory.GetFiles(forward, "run-location.json", SearchOption.AllDirectories)
                .Select(path => { try { return serializer.Deserialize<RunRecord>(File.ReadAllText(path, Encoding.UTF8)); } catch { return null; } })
                .Where(item => item != null && item.State == ProcessRunState.Completed && !String.IsNullOrWhiteSpace(item.ResultDirectory) &&
                    BackendPathResolver.IsWithin(item.ResultDirectory, selectedTask) && Directory.Exists(item.ResultDirectory) &&
                    File.Exists(Path.Combine(item.ResultDirectory, "trajectory_history.csv")))
                .OrderByDescending(item => item.EndedAt).FirstOrDefault();
            if (latest == null) throw new InvalidOperationException("当前任务没有成功且包含 trajectory_history.csv 的正向计算结果，请先完成正向计算。");
            return Path.GetFullPath(latest.ResultDirectory);
        }
    }
}
