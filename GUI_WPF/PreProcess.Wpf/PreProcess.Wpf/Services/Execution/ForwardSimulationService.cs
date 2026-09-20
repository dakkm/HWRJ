using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using PreProcess.Wpf.Models;
using PreProcess.Wpf.Services.Process;

namespace PreProcess.Wpf.Services.Execution
{
    public sealed class ForwardRunRecord : RunRecord { public ForwardRunRecord() { Module = "01"; } }

    // 定义 ForwardSimulationService 类型，集中封装与该领域对象相关的状态和行为。
    public sealed class ForwardSimulationService
    {
        private readonly ProcessManager manager = new ProcessManager();
        // 保存该组件运行所需的配置或中间状态。
        private int active;
        private readonly object gate = new object();
        // 传递并检查取消信号，使长时间任务可以安全停止。
        private CancellationTokenSource stop;
        public bool IsRunning => Volatile.Read(ref active) != 0;
        public event Action<ProcessLogEvent> Log;
        // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
        public event Action<ProcessProgressEvent> Progress;
        public event Action<ProcessRunState> StateChanged;
        // 传递并检查取消信号，使长时间任务可以安全停止。
        public void Stop() { lock (gate) stop?.Cancel(); manager.Stop(); }

        public async Task<ForwardRunRecord> RunAsync(TaskModel task, bool prepareOnly = false, BackendPaths paths = null,
            // 传递并检查取消信号，使长时间任务可以安全停止。
            int timeoutSeconds = 1800, CancellationToken cancellationToken = default(CancellationToken), string selectedTask = null)
        {
            if (Interlocked.CompareExchange(ref active, 1, 0) != 0) throw new InvalidOperationException("正向计算正在运行，请等待完成或停止。");
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var lease = ExecutionLease.TryEnter();
            if (lease == null) { Volatile.Write(ref active, 0); throw new InvalidOperationException("已有模块正在运行。"); }
            var record = new ForwardRunRecord { StartedAt = DateTimeOffset.Now, State = ProcessRunState.Preparing, PrepareOnly = prepareOnly };
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            StreamWriter stdout = null, stderr = null;
            string submission = null, runRoot = null, ioError = null;
            // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
            object logGate = new object();
            bool launchingBackend = false;
            // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
            Action<ProcessLogEvent> onLog = item =>
            {
                lock (logGate)
                {
                    try { (item.IsError ? stderr : stdout)?.WriteLine(item.Text); }
                    // 捕获本步骤产生的异常，将故障转换为可追踪的运行状态。
                    catch (Exception ex) { ioError = ex.ToString(); manager.Stop(); }
                }
                Log?.Invoke(item);
            };
            // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
            Action<ProcessProgressEvent> onProgress = item =>
            {
                if (item.Module != "01") return;
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (!String.IsNullOrEmpty(item.RunDirectory) && runRoot != null && BackendPathResolver.IsWithin(item.RunDirectory, runRoot))
                {
                    record.RunDirectory = item.RunDirectory; record.RunId = item.RunId;
                    string outputDirectory = Path.Combine(item.RunDirectory, "output");
                    // 校验当前条件，仅在满足业务约束时进入该处理分支。
                    if (Directory.Exists(outputDirectory)) record.ResultDirectory = outputDirectory;
                }
                Progress?.Invoke(item);
            };
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            Action<ProcessRunState> onState = state =>
            {
                if (launchingBackend && (state == ProcessRunState.Running || state == ProcessRunState.Stopping)) StateChanged?.Invoke(state);
            };
            using (lease)
            using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                // 对共享状态加锁，避免并发读写造成数据竞争。
                lock (gate) stop = cancellation;
                manager.Log += onLog; manager.Progress += onProgress; manager.StateChanged += onState;
                try
                {
                    // 将界面状态变更调度到用户界面线程，避免跨线程访问。
                    StateChanged?.Invoke(ProcessRunState.Preparing);
                    // The UI locks editing while this snapshot is generated; no task-model restructuring.
                    await Task.Run(() =>
                    {
                        // 调用对应组件完成当前步骤，并保留产生的处理结果。
                        cancellation.Token.ThrowIfCancellationRequested();
                        var generator = new RequestGenerator(); generator.Generate(task);
                        // 更新当前流程使用的数据，为下一处理步骤做好准备。
                        paths = paths ?? new BackendPathResolver().Resolve();
                        if (!File.Exists(paths.Python)) throw new FileNotFoundException("未找到可用Python环境。", paths.Python);
                        // 校验当前条件，仅在满足业务约束时进入该处理分支。
                        if (!File.Exists(paths.Entry)) throw new FileNotFoundException("正向计算入口文件不存在。", paths.Entry);
                        if (BackendPathResolver.IsWithin(paths.RuntimeRoot, paths.PackageRoot)) throw new ArgumentException("运行文件不能写入冻结后端目录。");
                        if (timeoutSeconds <= 0) throw new ArgumentException("超时时间必须为正整数。");
                        // 更新当前流程使用的数据，为下一处理步骤做好准备。
                        submission = TaskDirectoryManager.Create(paths.RuntimeRoot, "01", selectedTask);
                        record.ExecutionDirectory = submission;
                        // 处理文件系统路径及数据，并在使用前确认目标有效。
                        record.TaskDirectory = Directory.GetParent(submission).FullName;
                        runRoot = Path.Combine(submission, "results");
                        // 处理文件系统路径及数据，并在使用前确认目标有效。
                        record.RequestPath = generator.Save(task, Path.Combine(submission, "request.json"));
                        stdout = new StreamWriter(Path.Combine(submission, "stdout.log"), false, new UTF8Encoding(false)) { AutoFlush = true };
                        stderr = new StreamWriter(Path.Combine(submission, "stderr.log"), false, new UTF8Encoding(false)) { AutoFlush = true };
                    // 调用对应组件完成当前步骤，并保留产生的处理结果。
                    }, cancellation.Token).ConfigureAwait(false);
                    var probe = MakeRequest(paths);
                    // 将当前结果加入集合，供后续汇总或界面展示。
                    probe.Arguments.Add("-c");
                    probe.Arguments.Add("import sys,pandas; assert sys.version_info >= (3,10), 'Python 3.10+ required'; print('Python environment ready: '+sys.version.split()[0])");
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    probe.Timeout = TimeSpan.FromSeconds(20);
                    var environment = await manager.RunAsync(probe, cancellation.Token).ConfigureAwait(false);
                    cancellation.Token.ThrowIfCancellationRequested();
                    // 校验当前条件，仅在满足业务约束时进入该处理分支。
                    if (environment.State != ProcessRunState.Completed) throw new InvalidOperationException("未找到可用Python环境，或缺少 pandas 依赖。请检查警告日志。 " + environment.Error);
                    var request = MakeRequest(paths);
                    // 将当前结果加入集合，供后续汇总或界面展示。
                    request.Arguments.Add(paths.Entry);
                    request.Arguments.Add("--params-json"); request.Arguments.Add(record.RequestPath);
                    // 将当前结果加入集合，供后续汇总或界面展示。
                    request.Arguments.Add("--run-root"); request.Arguments.Add(runRoot);
                    request.Arguments.Add("--timeout-seconds"); request.Arguments.Add(timeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    if (prepareOnly) request.Arguments.Add("--prepare-only");
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    request.Timeout = TimeSpan.FromSeconds(timeoutSeconds + 120.0); // Also bounds preparation / hung entry.
                    launchingBackend = true;
                    // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
                    onLog(new ProcessLogEvent { Text = "Executable: " + request.Executable + "\nArguments: " + String.Join(" ", System.Linq.Enumerable.Select(request.Arguments, WindowsJobProcess.Quote)) + "\nWorkingDirectory: " + request.WorkingDirectory });
                    var result = await manager.RunAsync(request, cancellation.Token).ConfigureAwait(false);
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    record.ForwardExitCode = result.ExitCode; record.ExitCode = result.ExitCode; record.State = result.State;
                    if (ioError != null) throw new IOException("运行日志保存失败：" + ioError);
                    // 校验当前条件，仅在满足业务约束时进入该处理分支。
                    if (record.State == ProcessRunState.Completed)
                    {
                        if (String.IsNullOrEmpty(record.RunDirectory) || !Directory.Exists(record.RunDirectory))
                            throw new DirectoryNotFoundException("程序已退出，但未获得有效运行目录。");
                        // Only the execution envelope is read; no result CSV / image data is parsed.
                        // 处理文件系统路径及数据，并在使用前确认目标有效。
                        string file = Path.Combine(record.RunDirectory, "result.json");
                        var envelope = new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 }.Deserialize<Dictionary<string, object>>(File.ReadAllText(file, Encoding.UTF8));
                        // 更新当前流程使用的数据，为下一处理步骤做好准备。
                        string expected = prepareOnly ? "prepared" : "success";
                        if ((string)envelope["status"] != expected || Convert.ToInt32(envelope["return_code"]) != 0)
                            // 发现无效输入或运行状态后立即终止，防止错误继续传播。
                            throw new InvalidDataException("后端返回的完成状态不符合本次运行模式。");
                        if ((string)envelope["run_id"] != record.RunId) throw new InvalidDataException("运行编号不一致。");
                        string outputDirectory = (string)envelope["output_dir"];
                        // 校验当前条件，仅在满足业务约束时进入该处理分支。
                        if (!BackendPathResolver.IsWithin(outputDirectory, record.RunDirectory) || !Directory.Exists(outputDirectory))
                            throw new DirectoryNotFoundException("结果目录不存在或不属于本次运行。");
                        // 更新当前流程使用的数据，为下一处理步骤做好准备。
                        string backendRequest = (string)envelope["request_json"];
                        if (!BackendPathResolver.IsWithin(backendRequest, record.RunDirectory) || !File.Exists(backendRequest))
                            // 发现无效输入或运行状态后立即终止，防止错误继续传播。
                            throw new FileNotFoundException("后端运行请求记录不存在或不属于本次运行。");
                        record.ResultDirectory = outputDirectory;
                        record.BackendRequestPath = backendRequest;
                        // 校验当前条件，仅在满足业务约束时进入该处理分支。
                        if (prepareOnly) record.Message = "输入准备完成（未执行求解及后处理）。";
                        else
                        {
                            // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
                            onLog(new ProcessLogEvent { Text = "正向计算完成，开始提取响应特征。" });
                            await RunFeatureExtractionAsync(paths, record, timeoutSeconds, cancellation.Token, onLog).ConfigureAwait(false);
                            // 校验当前条件，仅在满足业务约束时进入该处理分支。
                            if (ioError != null) throw new IOException("运行日志保存失败：" + ioError);
                            record.Message = "正向计算及特征提取完成。";
                            record.BackendStatus = "success";
                        }
                    }
                    // 当前置条件不成立时执行备用路径，保持处理结果完整。
                    else record.Message = record.State == ProcessRunState.Cancelled ? "正向计算已停止。" : "正向计算失败，请查看警告日志。";
                    record.Diagnostic = result.Error;
                }
                // 捕获本步骤产生的异常，将故障转换为可追踪的运行状态。
                catch (OperationCanceledException) { record.State = ProcessRunState.Cancelled; record.Message = "正向计算已停止。"; }
                catch (Exception ex)
                {
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    record.State = ProcessRunState.Failed;
                    record.Message = "无法完成正向计算或特征提取：" + ex.Message;
                    record.Diagnostic = ex.ToString();
                    // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
                    onLog(new ProcessLogEvent { IsError = true, Text = ex.ToString() });
                }
                finally
                {
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    record.EndedAt = DateTimeOffset.Now;
                    manager.Log -= onLog; manager.Progress -= onProgress; manager.StateChanged -= onState;
                    // 进入可能失败的操作区间，并由后续异常分支统一处理故障。
                    try
                    {
                        try { stdout?.Dispose(); } finally { stderr?.Dispose(); }
                        // 校验当前条件，仅在满足业务约束时进入该处理分支。
                        if (submission != null) File.WriteAllText(Path.Combine(submission, "run-location.json"),
                            new JavaScriptSerializer().Serialize(record), new UTF8Encoding(false));
                        WriteResultIndex(record);
                    }
                    // 捕获本步骤产生的异常，将故障转换为可追踪的运行状态。
                    catch (Exception ex)
                    {
                        record.State = ProcessRunState.Failed; record.Message = "运行记录保存失败，请检查运行目录权限。";
                        // 将界面状态变更调度到用户界面线程，避免跨线程访问。
                        record.Diagnostic = ex.ToString(); Log?.Invoke(new ProcessLogEvent { IsError = true, Text = ex.ToString() });
                    }
                    lock (gate) stop = null;
                    // 调用对应组件完成当前步骤，并保留产生的处理结果。
                    Volatile.Write(ref active, 0);
                    StateChanged?.Invoke(record.State);
                }
            }
            return record;
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static void WriteResultIndex(RunRecord record)
        {
            if (record.State != ProcessRunState.Completed || String.IsNullOrWhiteSpace(record.ResultDirectory) || !Directory.Exists(record.ResultDirectory)) return;
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            ModuleExecutionService.WriteResultIndex(record);
        }

        private async Task RunFeatureExtractionAsync(BackendPaths paths, ForwardRunRecord record, int timeoutSeconds,
            // 传递并检查取消信号，使长时间任务可以安全停止。
            CancellationToken token, Action<ProcessLogEvent> onLog)
        {
            token.ThrowIfCancellationRequested();
            string runtimePackage = await Task.Run(() => RuntimePackage.Prepare(paths, token), token).ConfigureAwait(false);
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            record.RuntimePackage = runtimePackage;
            var config = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(
                // 处理文件系统路径及数据，并在使用前确认目标有效。
                File.ReadAllText(Path.Combine(runtimePackage, "config.json"), Encoding.UTF8));
            var entries = (Dictionary<string, object>)config["standard_entries"];
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string entry = Path.GetFullPath(Path.Combine(runtimePackage, (string)entries["03_similarity"]));
            if (!BackendPathResolver.IsWithin(entry, runtimePackage) || !File.Exists(entry))
                throw new FileNotFoundException("特征提取程序不存在或不在运行副本内。", entry);

            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string featureRoot = Path.Combine(record.ResultDirectory, "features");
            if (Directory.Exists(featureRoot))
                // 发现无效输入或运行状态后立即终止，防止错误继续传播。
                throw new IOException("本次正向结果目录中已存在 features，无法确定新的特征提取结果。");

            var featurePaths = new BackendPaths { PackageRoot = runtimePackage, Python = paths.Python };
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var request = MakeRequest(featurePaths);
            request.Arguments.Add(entry);
            // 将当前结果加入集合，供后续汇总或界面展示。
            request.Arguments.Add("--mode"); request.Arguments.Add("features");
            request.Arguments.Add("--run-dir"); request.Arguments.Add(record.ResultDirectory);
            request.Arguments.Add("--output-dir"); request.Arguments.Add(featureRoot);
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            request.Timeout = TimeSpan.FromSeconds(timeoutSeconds + 120.0);
            onLog(new ProcessLogEvent { Text = "Executable: " + request.Executable + "\nArguments: " +
                // 调用对应组件完成当前步骤，并保留产生的处理结果。
                String.Join(" ", System.Linq.Enumerable.Select(request.Arguments, WindowsJobProcess.Quote)) +
                "\nWorkingDirectory: " + request.WorkingDirectory });

            // 异步等待耗时任务完成，期间保持界面线程可响应。
            ProcessRunResult feature = await manager.RunAsync(request, token).ConfigureAwait(false);
            record.FeatureExitCode = feature.ExitCode;
            record.ExitCode = feature.ExitCode;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            record.State = feature.State;
            if (feature.State == ProcessRunState.Cancelled)
            {
                // 调用对应组件完成当前步骤，并保留产生的处理结果。
                token.ThrowIfCancellationRequested();
                throw new OperationCanceledException("特征提取已停止。", token);
            }
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (feature.State != ProcessRunState.Completed || feature.ExitCode != 0)
                throw new InvalidOperationException("特征提取执行失败。" + (String.IsNullOrWhiteSpace(feature.Error) ? String.Empty : " " + feature.Error));

            string[] directories = Directory.Exists(featureRoot) ? Directory.GetDirectories(featureRoot) : new string[0];
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (directories.Length != 1)
                throw new InvalidDataException("特征提取没有生成唯一的运行目录。");
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string featureDirectory = Path.GetFullPath(directories[0]);
            if (!BackendPathResolver.IsWithin(featureDirectory, featureRoot))
                // 发现无效输入或运行状态后立即终止，防止错误继续传播。
                throw new InvalidDataException("特征提取结果超出本次正向结果目录。");
            string statusPath = Path.Combine(featureDirectory, "evaluation_status.json");
            if (!File.Exists(statusPath)) throw new FileNotFoundException("特征提取完成标志不存在。", statusPath);
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            var status = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(statusPath, Encoding.UTF8));
            string featureRunId = status["run_id"] as string;
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if ((status["status"] as string) != "success" || (status["mode"] as string) != "features" ||
                featureRunId != Path.GetFileName(featureDirectory))
                // 发现无效输入或运行状态后立即终止，防止错误继续传播。
                throw new InvalidDataException("特征提取完成标志与本次运行不一致。");
            record.FeatureRunId = featureRunId;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            record.FeatureResultDirectory = featureDirectory;
            onLog(new ProcessLogEvent { Text = "特征提取完成：" + featureDirectory });
        }

        private static ProcessRunRequest MakeRequest(BackendPaths paths)
        {
            // 返回当前步骤生成的结果，并结束本次调用。
            return new ProcessRunRequest { Executable = paths.Python, WorkingDirectory = paths.PackageRoot,
                Arguments = new List<string> { "-B", "-u" }, EnvironmentVariables = new Dictionary<string, string>
                // 继续处理当前业务步骤，保持上下文状态一致。
                { { "PYTHONUTF8", "1" }, { "PYTHONIOENCODING", "utf-8" }, { "PYTHONDONTWRITEBYTECODE", "1" } } };
        }
    }
}
