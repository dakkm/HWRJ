using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PreProcess.Wpf.Services.Process;

namespace PreProcess.Wpf.Services.Execution
{
    public sealed class TrajectoryExecutionService
    {
        private readonly ProcessManager manager = new ProcessManager();
        private readonly object gate = new object();
        private CancellationTokenSource stop;

        public event Action<ProcessLogEvent> Log;
        public event Action<ProcessProgressEvent> Progress;
        public event Action<ProcessRunState> StateChanged;

        public void Stop()
        {
            lock (gate) stop?.Cancel();
            manager.Stop();
        }

        public async Task<RunRecord> RunAsync(string trajectoryFile, string inputMode, bool includePrerelease,
            BackendPaths paths = null, CancellationToken token = default(CancellationToken))
        {
            var lease = ExecutionLease.TryEnter();
            if (lease == null) throw new InvalidOperationException("已有模块正在运行，请等待或停止。");
            var record = new RunRecord { Module = "轨迹", StartedAt = DateTimeOffset.Now, State = ProcessRunState.Preparing };
            using (lease)
            using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                lock (gate) stop = cancellation;
                string submission = null;
                StreamWriter stdout = null, stderr = null;
                string storageError = null;
                object logGate = new object();
                Action<ProcessLogEvent> onLog = item =>
                {
                    lock (logGate)
                    {
                        try { (item.IsError ? stderr : stdout)?.WriteLine(item.Text); }
                        catch (Exception ex) { storageError = ex.Message; manager.Stop(); }
                    }
                    Log?.Invoke(item);
                };
                Action<ProcessProgressEvent> onProgress = item => Progress?.Invoke(item);
                Action<ProcessRunState> onState = state =>
                {
                    if (state == ProcessRunState.Running || state == ProcessRunState.Stopping) StateChanged?.Invoke(state);
                };
                manager.Log += onLog;
                manager.Progress += onProgress;
                manager.StateChanged += onState;
                try
                {
                    StateChanged?.Invoke(ProcessRunState.Preparing);
                    ProcessRunRequest request = null;
                    await Task.Run(() =>
                    {
                        cancellation.Token.ThrowIfCancellationRequested();
                        if (String.IsNullOrWhiteSpace(trajectoryFile)) throw new ArgumentException("请选择轨迹输入文件。");
                        string source = Path.GetFullPath(trajectoryFile);
                        if (!File.Exists(source)) throw new FileNotFoundException("轨迹输入文件不存在。", source);
                        if (new FileInfo(source).Length == 0) throw new InvalidDataException("轨迹输入文件为空。");

                        paths = paths ?? new BackendPathResolver().Resolve();
                        if (!File.Exists(paths.Python)) throw new FileNotFoundException("未找到可用Python环境。", paths.Python);
                        string package = RuntimePackage.Prepare(paths, cancellation.Token);
                        record.RuntimePackage = package;
                        string entry = Path.GetFullPath(Path.Combine(package, "01-正向仿真", "02-程序", "postprocess_trajectory.py"));
                        if (!BackendPathResolver.IsWithin(entry, package) || !File.Exists(entry))
                            throw new FileNotFoundException("01轨迹专用入口不存在。", entry);

                        string runId = "run_trajectory_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                        record.RunId = runId;
                        record.RunDirectory = Path.Combine(paths.RuntimeRoot, "runs", "trajectory", runId);
                        record.ResultDirectory = Path.Combine(record.RunDirectory, "output");
                        record.RequestPath = source;
                        Directory.CreateDirectory(record.ResultDirectory);
                        string copiedInput = Path.Combine(record.ResultDirectory, "trajectory_history.csv");
                        File.Copy(source, copiedInput, false);
                        record.BackendRequestPath = copiedInput;

                        submission = Path.Combine(paths.RuntimeRoot, "requests", "trajectory", Guid.NewGuid().ToString("N"));
                        Directory.CreateDirectory(submission);
                        stdout = new StreamWriter(Path.Combine(submission, "stdout.log"), false, new UTF8Encoding(false)) { AutoFlush = true };
                        stderr = new StreamWriter(Path.Combine(submission, "stderr.log"), false, new UTF8Encoding(false)) { AutoFlush = true };
                        request = new ProcessRunRequest
                        {
                            Executable = paths.Python,
                            WorkingDirectory = package,
                            Arguments = new List<string> { "-B", "-u", entry, "--input", copiedInput, "--outdir", record.ResultDirectory, "--no-plot" },
                            Timeout = TimeSpan.FromMinutes(30),
                            EnvironmentVariables = new Dictionary<string, string>
                            {
                                { "PYTHONUTF8", "1" }, { "PYTHONIOENCODING", "utf-8" }, { "PYTHONDONTWRITEBYTECODE", "1" }
                            }
                        };
                        if (includePrerelease) request.Arguments.Add("--include-prerelease");
                        ModuleExecutionService.WriteObject(Path.Combine(record.RunDirectory, "trajectory_run.json"), new
                        {
                            module = "trajectory", run_id = runId, input_mode = inputMode, source_file = source,
                            copied_input = copiedInput, include_prerelease = includePrerelease, status = "prepared"
                        });
                        onLog(new ProcessLogEvent
                        {
                            Text = "调用01轨迹专用入口（不启动正向计算）。\nExecutable: " + request.Executable +
                                "\nArguments: " + String.Join(" ", System.Linq.Enumerable.Select(request.Arguments, WindowsJobProcess.Quote))
                        });
                    }, cancellation.Token).ConfigureAwait(false);

                    var result = await manager.RunAsync(request, cancellation.Token).ConfigureAwait(false);
                    record.State = result.State;
                    record.ExitCode = result.ExitCode;
                    record.Diagnostic = result.Error;
                    if (storageError != null) throw new IOException("运行日志保存失败：" + storageError);
                    if (record.State == ProcessRunState.Completed)
                    {
                        string metrics = Path.Combine(record.ResultDirectory, "trajectory_metrics.csv");
                        if (!File.Exists(metrics)) throw new InvalidDataException("轨迹入口已退出，但未生成trajectory_metrics.csv。");
                        record.BackendStatus = "success";
                        record.Message = "轨迹处理完成。";
                    }
                    else
                    {
                        record.BackendStatus = record.State == ProcessRunState.Cancelled ? "cancelled" : "failed";
                        record.Message = record.State == ProcessRunState.Cancelled ? "轨迹处理已停止。" : "轨迹处理失败，请查看警告日志。";
                    }
                }
                catch (OperationCanceledException)
                {
                    record.State = ProcessRunState.Cancelled;
                    record.BackendStatus = "cancelled";
                    record.Message = "轨迹处理已停止。";
                }
                catch (Exception ex)
                {
                    record.State = ProcessRunState.Failed;
                    record.BackendStatus = "failed";
                    record.Message = "无法处理轨迹：" + ex.Message;
                    record.Diagnostic = ex.ToString();
                    onLog(new ProcessLogEvent { IsError = true, Text = ex.ToString() });
                }
                finally
                {
                    record.EndedAt = DateTimeOffset.Now;
                    manager.Log -= onLog;
                    manager.Progress -= onProgress;
                    manager.StateChanged -= onState;
                    try
                    {
                        try { stdout?.Dispose(); } finally { stderr?.Dispose(); }
                        if (record.RunDirectory != null && Directory.Exists(record.RunDirectory))
                            ModuleExecutionService.WriteObject(Path.Combine(record.RunDirectory, "trajectory_run.json"), new
                            {
                                module = "trajectory", run_id = record.RunId, input_mode = inputMode,
                                source_file = record.RequestPath, copied_input = record.BackendRequestPath,
                                include_prerelease = includePrerelease, status = record.BackendStatus
                            });
                        if (submission != null) ModuleExecutionService.WriteObject(Path.Combine(submission, "run-location.json"), record);
                    }
                    catch (Exception ex)
                    {
                        record.State = ProcessRunState.Failed;
                        record.Message = "运行记录保存失败：" + ex.Message;
                    }
                    lock (gate) stop = null;
                    StateChanged?.Invoke(record.State);
                }
            }
            return record;
        }
    }
}
