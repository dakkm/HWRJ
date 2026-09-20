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
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private readonly ProcessManager manager = new ProcessManager();
        private readonly object gate = new object();
        private CancellationTokenSource stop;
        // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
        public event Action<ProcessLogEvent> Log;
        public event Action<ProcessProgressEvent> Progress;
        // 维护事件订阅关系，使运行状态能够及时传递给调用方。
        public event Action<ProcessRunState> StateChanged;
        public void Stop() { lock (gate) stop?.Cancel(); manager.Stop(); }
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        protected async Task<RunRecord> ExecuteAsync(string module, BackendPaths paths,
            Action<string, string, ProcessRunRequest, RunRecord> prepare, CancellationToken token, TimeSpan? limit = null, string selectedTask = null)
        {
            var lease = ExecutionLease.TryEnter();
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (lease == null) throw new InvalidOperationException("已有模块正在运行，请等待或停止。");
            var record = new RunRecord { Module = module, StartedAt = DateTimeOffset.Now, State = ProcessRunState.Preparing };
            using (lease)
            using (var cancel = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                // 对共享状态加锁，避免并发读写造成数据竞争。
                lock (gate) stop = cancel;
                string submission = null, latest = null, previous = null, outputRoot = null, resultRoot = null;
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                StreamWriter stdout = null, stderr = null;
                object logGate = new object(); string storageError = null;
                Action<ProcessLogEvent> onLog = item =>
                {
                    // 对共享状态加锁，避免并发读写造成数据竞争。
                    lock (logGate) try { (item.IsError ? stderr : stdout)?.WriteLine(item.Text); }
                    catch (Exception ex) { storageError = ex.Message; manager.Stop(); }
                    // 将界面状态变更调度到用户界面线程，避免跨线程访问。
                    Log?.Invoke(item);
                };
                Action<ProcessProgressEvent> onProgress = item =>
                {
                    // 04 does not include module in its own progress. Nested 01 events remain identifiable.
                    // 将界面状态变更调度到用户界面线程，避免跨线程访问。
                    Progress?.Invoke(item);
                };
                Action<ProcessRunState> onState = state => { if (state == ProcessRunState.Running || state == ProcessRunState.Stopping) StateChanged?.Invoke(state); };
                // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
                manager.Log += onLog; manager.Progress += onProgress; manager.StateChanged += onState;
                try
                {
                    StateChanged?.Invoke(ProcessRunState.Preparing);
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    ProcessRunRequest request = null;
                    await Task.Run(() =>
                    {
                        // 更新当前流程使用的数据，为下一处理步骤做好准备。
                        paths = paths ?? new BackendPathResolver().Resolve();
                        string package = RuntimePackage.Prepare(paths, cancel.Token);
                        // 更新当前流程使用的数据，为下一处理步骤做好准备。
                        record.RuntimePackage = package;
                        var config = ReadObject(Path.Combine(package, "config.json"));
                        var entries = (Dictionary<string, object>)config["standard_entries"];
                        // 更新当前流程使用的数据，为下一处理步骤做好准备。
                        string key = module == "02" ? "02_surrogate" : module == "03" ? "03_similarity" : "04_scene_search";
                        string entry = Path.GetFullPath(Path.Combine(package, (string)entries[key]));
                        // 校验当前条件，仅在满足业务约束时进入该处理分支。
                        if (!BackendPathResolver.IsWithin(entry, package) || !File.Exists(entry)) throw new FileNotFoundException("模块入口不存在或不在运行副本内。", entry);
                        string moduleRoot = Directory.GetParent(Path.GetDirectoryName(entry)).FullName;
                        // 处理文件系统路径及数据，并在使用前确认目标有效。
                        outputRoot = Path.Combine(moduleRoot, module == "02" ? "04-输出文件" : "03-输出文件");
                        latest = Path.Combine(outputRoot, "latest_run.json");
                        previous = File.Exists(latest) ? File.ReadAllText(latest) : null;
                        // 更新当前流程使用的数据，为下一处理步骤做好准备。
                        submission = TaskDirectoryManager.Create(paths.RuntimeRoot, module, selectedTask);
                        record.ExecutionDirectory = submission;
                        // 处理文件系统路径及数据，并在使用前确认目标有效。
                        record.TaskDirectory = Directory.GetParent(submission).FullName;
                        resultRoot = Path.Combine(submission, "results");
                        // 处理文件系统路径及数据，并在使用前确认目标有效。
                        stdout = new StreamWriter(Path.Combine(submission, "stdout.log"), false, new UTF8Encoding(false)) { AutoFlush = true };
                        stderr = new StreamWriter(Path.Combine(submission, "stderr.log"), false, new UTF8Encoding(false)) { AutoFlush = true };
                        request = new ProcessRunRequest { Executable = paths.Python, WorkingDirectory = package,
                            // 更新当前流程使用的数据，为下一处理步骤做好准备。
                            Arguments = new List<string> { "-B", "-u", entry }, Timeout = limit ?? TimeSpan.FromHours(6),
                            EnvironmentVariables = new Dictionary<string, string> { { "PYTHONUTF8", "1" }, { "PYTHONIOENCODING", "utf-8" }, { "PYTHONDONTWRITEBYTECODE", "1" } } };
                        // 调用对应组件完成当前步骤，并保留产生的处理结果。
                        prepare(package, submission, request, record);
                        if (module == "02") { request.Arguments.Add("--run-root"); request.Arguments.Add(resultRoot); }
                        // 当前置条件不成立时执行备用路径，保持处理结果完整。
                        else if (module == "03") { request.Arguments.Add("--output-dir"); request.Arguments.Add(resultRoot); }
                        onLog(new ProcessLogEvent { Text = "Executable: " + request.Executable + "\nArguments: " + String.Join(" ", System.Linq.Enumerable.Select(request.Arguments, WindowsJobProcess.Quote)) });
                    // 调用对应组件完成当前步骤，并保留产生的处理结果。
                    }, cancel.Token).ConfigureAwait(false);
                    var result = await manager.RunAsync(request, cancel.Token).ConfigureAwait(false);
                    record.State = result.State; record.ExitCode = result.ExitCode; record.Diagnostic = result.Error;
                    // 校验当前条件，仅在满足业务约束时进入该处理分支。
                    if (storageError != null) throw new IOException(storageError);
                    if (File.Exists(latest) && File.ReadAllText(latest) != previous)
                    {
                        // 更新当前流程使用的数据，为下一处理步骤做好准备。
                        var pointer = ReadObject(latest);
                        string directory = pointer["run_dir"] as string;
                        // 处理文件系统路径及数据，并在使用前确认目标有效。
                        string expectedRoot = module == "04" ? Path.Combine(outputRoot, "runs") : resultRoot;
                        if (!BackendPathResolver.IsWithin(directory, expectedRoot) || !Directory.Exists(directory))
                            throw new InvalidDataException("后端返回的运行目录无效。");
                        // 更新当前流程使用的数据，为下一处理步骤做好准备。
                        record.RunId = pointer["run_id"] as string;
                        if (Path.GetFileName(directory) != record.RunId) throw new InvalidDataException("运行编号与目录不一致。");
                        // 更新当前流程使用的数据，为下一处理步骤做好准备。
                        record.ResultDirectory = record.RunDirectory = directory;
                        record.BackendStatus = pointer["status"] as string;
                        // 校验当前条件，仅在满足业务约束时进入该处理分支。
                        if (module == "04") MoveResultIntoTask(record, resultRoot);
                    }
                    if (record.State == ProcessRunState.Completed)
                    {
                        if (record.RunDirectory == null || record.BackendStatus != "success") throw new InvalidDataException("退出码为0，但没有本次成功运行记录。");
                        // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
                        string envelopeName = module == "02" ? "prediction_summary.json" : module == "03" ? "evaluation_status.json" : "scene_search_summary.json";
                        var envelope = ReadObject(Path.Combine(record.RunDirectory, envelopeName));
                        // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
                        string status = envelope["status"] as string;
                        if ((string)envelope["run_id"] != record.RunId || (status != "success" && status != "Completed"))
                            // 发现无效输入或运行状态后立即终止，防止错误继续传播。
                            throw new InvalidDataException("模块完成信封不符合正式成功状态。");
                        record.BackendStatus = status;
                    }
                    record.Message = record.State == ProcessRunState.Completed ? "模块运行完成。" : record.State == ProcessRunState.Cancelled ? "模块已停止。" :
                        // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
                        record.BackendStatus == "incomplete" ? "场景搜索未获得足量候选，请查看运行记录。" : "模块运行失败，请查看警告日志。";
                }
                catch (OperationCanceledException) { record.State = ProcessRunState.Cancelled; record.Message = "模块已停止。"; }
                // 捕获本步骤产生的异常，将故障转换为可追踪的运行状态。
                catch (Exception ex)
                {
                    record.State = ProcessRunState.Failed; record.Message = ex.Message; record.Diagnostic = ex.ToString();
                    // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
                    onLog(new ProcessLogEvent { IsError = true, Text = ex.ToString() });
                }
                finally
                {
                    record.EndedAt = DateTimeOffset.Now;
                    // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
                    manager.Log -= onLog; manager.Progress -= onProgress; manager.StateChanged -= onState;
                    try
                    {
                        // 进入可能失败的操作区间，并由后续异常分支统一处理故障。
                        try { stdout?.Dispose(); } finally { stderr?.Dispose(); }
                        if (submission != null) WriteObject(Path.Combine(submission, "run-location.json"), record);
                        // 校验当前条件，仅在满足业务约束时进入该处理分支。
                        if (record.State == ProcessRunState.Completed && !String.IsNullOrWhiteSpace(record.ResultDirectory) && Directory.Exists(record.ResultDirectory))
                            WriteResultIndex(record);
                    }
                    // 捕获本步骤产生的异常，将故障转换为可追踪的运行状态。
                    catch (Exception ex) { record.State = ProcessRunState.Failed; record.Message = "运行记录保存失败：" + ex.Message; }
                    lock (gate) stop = null;
                    StateChanged?.Invoke(record.State);
                }
            }
            // 返回当前步骤生成的结果，并结束本次调用。
            return record;
        }
        private static void MoveResultIntoTask(RunRecord record, string resultRoot)
        {
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            Directory.CreateDirectory(resultRoot);
            string destination = Path.Combine(resultRoot, record.RunId);
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (Directory.Exists(destination)) throw new IOException("任务结果目录已存在：" + destination);
            Directory.Move(record.RunDirectory, destination);
            record.RunDirectory = record.ResultDirectory = destination;
        }
        // 处理文件系统路径及数据，并在使用前确认目标有效。
        internal static Dictionary<string, object> ReadObject(string path) => new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 }.Deserialize<Dictionary<string, object>>(File.ReadAllText(path, Encoding.UTF8));
        internal static void WriteObject(string path, object value) => File.WriteAllText(path, new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 }.Serialize(value), new UTF8Encoding(false));
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        internal static void WriteResultIndex(RunRecord record)
        {
            WriteObject(Path.Combine(record.ResultDirectory, "result-index.json"), new
            {
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                schema_version = "preprocess-result-index-v1",
                module = record.Module,
                run_id = record.RunId,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                result_directory = ".",
                started_at = record.StartedAt.ToString("o"),
                // 将外部数据转换为目标类型，并保持约定的表示格式。
                ended_at = record.EndedAt.ToString("o")
            });
        }
    }
}
