using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using PreProcess.Wpf.Services.Process;

namespace PreProcess.Wpf.Services.Execution
{
    // Shared execution bookkeeping ONLY. All OS process handling is still P4.1 ProcessManager.
    public abstract class ModuleExecutionService
    {
        private readonly ProcessManager manager = new ProcessManager();
        private readonly object gate = new object();
        private CancellationTokenSource stop;
        public event Action<ProcessLogEvent> Log;
        public event Action<ProcessProgressEvent> Progress;
        public event Action<ProcessRunState> StateChanged;
        public void Stop() { lock (gate) stop?.Cancel(); manager.Stop(); }
        protected async Task<RunRecord> ExecuteAsync(string module, BackendPaths paths,
            Action<string, string, ProcessRunRequest, RunRecord> prepare, CancellationToken token, TimeSpan? limit = null)
        {
            var lease = ExecutionLease.TryEnter();
            if (lease == null) throw new InvalidOperationException("已有模块正在运行，请等待或停止。");
            var record = new RunRecord { Module = module, StartedAt = DateTimeOffset.Now, State = ProcessRunState.Preparing };
            using (lease)
            using (var cancel = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                lock (gate) stop = cancel;
                string submission = null, latest = null, previous = null, outputRoot = null;
                StreamWriter stdout = null, stderr = null;
                object logGate = new object(); string storageError = null;
                Action<ProcessLogEvent> onLog = item =>
                {
                    lock (logGate) try { (item.IsError ? stderr : stdout)?.WriteLine(item.Text); }
                    catch (Exception ex) { storageError = ex.Message; manager.Stop(); }
                    Log?.Invoke(item);
                };
                Action<ProcessProgressEvent> onProgress = item =>
                {
                    // 04 does not include module in its own progress. Nested 01 events remain identifiable.
                    Progress?.Invoke(item);
                };
                Action<ProcessRunState> onState = state => { if (state == ProcessRunState.Running || state == ProcessRunState.Stopping) StateChanged?.Invoke(state); };
                manager.Log += onLog; manager.Progress += onProgress; manager.StateChanged += onState;
                try
                {
                    StateChanged?.Invoke(ProcessRunState.Preparing);
                    ProcessRunRequest request = null;
                    await Task.Run(() =>
                    {
                        paths = paths ?? new BackendPathResolver().Resolve();
                        string package = RuntimePackage.Prepare(paths, cancel.Token);
                        record.RuntimePackage = package;
                        var config = ReadObject(Path.Combine(package, "config.json"));
                        var entries = (Dictionary<string, object>)config["standard_entries"];
                        string key = module == "02" ? "02_surrogate" : module == "03" ? "03_similarity" : "04_scene_search";
                        string entry = Path.GetFullPath(Path.Combine(package, (string)entries[key]));
                        if (!BackendPathResolver.IsWithin(entry, package) || !File.Exists(entry)) throw new FileNotFoundException("模块入口不存在或不在运行副本内。", entry);
                        string moduleRoot = Directory.GetParent(Path.GetDirectoryName(entry)).FullName;
                        outputRoot = Path.Combine(moduleRoot, module == "02" ? "04-输出文件" : "03-输出文件");
                        latest = Path.Combine(outputRoot, "latest_run.json");
                        previous = File.Exists(latest) ? File.ReadAllText(latest) : null;
                        submission = Path.Combine(paths.RuntimeRoot, "requests", module, Guid.NewGuid().ToString("N"));
                        Directory.CreateDirectory(submission);
                        stdout = new StreamWriter(Path.Combine(submission, "stdout.log"), false, new UTF8Encoding(false)) { AutoFlush = true };
                        stderr = new StreamWriter(Path.Combine(submission, "stderr.log"), false, new UTF8Encoding(false)) { AutoFlush = true };
                        request = new ProcessRunRequest { Executable = paths.Python, WorkingDirectory = package,
                            Arguments = new List<string> { "-B", "-u", entry }, Timeout = limit ?? TimeSpan.FromHours(6),
                            EnvironmentVariables = new Dictionary<string, string> { { "PYTHONUTF8", "1" }, { "PYTHONIOENCODING", "utf-8" }, { "PYTHONDONTWRITEBYTECODE", "1" } } };
                        prepare(package, submission, request, record);
                        onLog(new ProcessLogEvent { Text = "Executable: " + request.Executable + "\nArguments: " + String.Join(" ", System.Linq.Enumerable.Select(request.Arguments, WindowsJobProcess.Quote)) });
                    }, cancel.Token).ConfigureAwait(false);
                    var result = await manager.RunAsync(request, cancel.Token).ConfigureAwait(false);
                    record.State = result.State; record.ExitCode = result.ExitCode; record.Diagnostic = result.Error;
                    if (storageError != null) throw new IOException(storageError);
                    if (File.Exists(latest) && File.ReadAllText(latest) != previous)
                    {
                        var pointer = ReadObject(latest);
                        string directory = pointer["run_dir"] as string;
                        if (!BackendPathResolver.IsWithin(directory, Path.Combine(outputRoot, "runs")) || !Directory.Exists(directory))
                            throw new InvalidDataException("后端返回的运行目录无效。");
                        record.RunId = pointer["run_id"] as string;
                        if (Path.GetFileName(directory) != record.RunId) throw new InvalidDataException("运行编号与目录不一致。");
                        record.ResultDirectory = record.RunDirectory = directory;
                        record.BackendStatus = pointer["status"] as string;
                    }
                    if (record.State == ProcessRunState.Completed)
                    {
                        if (record.RunDirectory == null || record.BackendStatus != "success") throw new InvalidDataException("退出码为0，但没有本次成功运行记录。");
                        string envelopeName = module == "02" ? "prediction_summary.json" : module == "03" ? "evaluation_status.json" : "scene_search_summary.json";
                        var envelope = ReadObject(Path.Combine(record.RunDirectory, envelopeName));
                        string status = envelope["status"] as string;
                        if ((string)envelope["run_id"] != record.RunId || (status != "success" && status != "Completed"))
                            throw new InvalidDataException("模块完成信封不符合正式成功状态。");
                        record.BackendStatus = status;
                    }
                    record.Message = record.State == ProcessRunState.Completed ? "模块运行完成。" : record.State == ProcessRunState.Cancelled ? "模块已停止。" :
                        record.BackendStatus == "incomplete" ? "场景搜索未获得足量候选，请查看运行记录。" : "模块运行失败，请查看警告日志。";
                }
                catch (OperationCanceledException) { record.State = ProcessRunState.Cancelled; record.Message = "模块已停止。"; }
                catch (Exception ex)
                {
                    record.State = ProcessRunState.Failed; record.Message = ex.Message; record.Diagnostic = ex.ToString();
                    onLog(new ProcessLogEvent { IsError = true, Text = ex.ToString() });
                }
                finally
                {
                    record.EndedAt = DateTimeOffset.Now;
                    manager.Log -= onLog; manager.Progress -= onProgress; manager.StateChanged -= onState;
                    try
                    {
                        try { stdout?.Dispose(); } finally { stderr?.Dispose(); }
                        if (submission != null) WriteObject(Path.Combine(submission, "run-location.json"), record);
                    }
                    catch (Exception ex) { record.State = ProcessRunState.Failed; record.Message = "运行记录保存失败：" + ex.Message; }
                    lock (gate) stop = null;
                    StateChanged?.Invoke(record.State);
                }
            }
            return record;
        }
        internal static Dictionary<string, object> ReadObject(string path) => new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 }.Deserialize<Dictionary<string, object>>(File.ReadAllText(path, Encoding.UTF8));
        internal static void WriteObject(string path, object value) => File.WriteAllText(path, new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 }.Serialize(value), new UTF8Encoding(false));
    }
}
