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

    public sealed class ForwardSimulationService
    {
        private readonly ProcessManager manager = new ProcessManager();
        private int active;
        private readonly object gate = new object();
        private CancellationTokenSource stop;
        public bool IsRunning => Volatile.Read(ref active) != 0;
        public event Action<ProcessLogEvent> Log;
        public event Action<ProcessProgressEvent> Progress;
        public event Action<ProcessRunState> StateChanged;
        public void Stop() { lock (gate) stop?.Cancel(); manager.Stop(); }

        public async Task<ForwardRunRecord> RunAsync(TaskModel task, bool prepareOnly = false, BackendPaths paths = null,
            int timeoutSeconds = 1800, CancellationToken cancellationToken = default(CancellationToken), string selectedTask = null)
        {
            if (Interlocked.CompareExchange(ref active, 1, 0) != 0) throw new InvalidOperationException("正向计算正在运行，请等待完成或停止。");
            var lease = ExecutionLease.TryEnter();
            if (lease == null) { Volatile.Write(ref active, 0); throw new InvalidOperationException("已有模块正在运行。"); }
            var record = new ForwardRunRecord { StartedAt = DateTimeOffset.Now, State = ProcessRunState.Preparing, PrepareOnly = prepareOnly };
            StreamWriter stdout = null, stderr = null;
            string submission = null, runRoot = null, ioError = null;
            object logGate = new object();
            bool launchingBackend = false;
            Action<ProcessLogEvent> onLog = item =>
            {
                lock (logGate)
                {
                    try { (item.IsError ? stderr : stdout)?.WriteLine(item.Text); }
                    catch (Exception ex) { ioError = ex.ToString(); manager.Stop(); }
                }
                Log?.Invoke(item);
            };
            Action<ProcessProgressEvent> onProgress = item =>
            {
                if (item.Module != "01") return;
                if (!String.IsNullOrEmpty(item.RunDirectory) && runRoot != null && BackendPathResolver.IsWithin(item.RunDirectory, runRoot))
                {
                    record.RunDirectory = item.RunDirectory; record.RunId = item.RunId;
                    string outputDirectory = Path.Combine(item.RunDirectory, "output");
                    if (Directory.Exists(outputDirectory)) record.ResultDirectory = outputDirectory;
                }
                Progress?.Invoke(item);
            };
            Action<ProcessRunState> onState = state =>
            {
                if (launchingBackend && (state == ProcessRunState.Running || state == ProcessRunState.Stopping)) StateChanged?.Invoke(state);
            };
            using (lease)
            using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                lock (gate) stop = cancellation;
                manager.Log += onLog; manager.Progress += onProgress; manager.StateChanged += onState;
                try
                {
                    StateChanged?.Invoke(ProcessRunState.Preparing);
                    // The UI locks editing while this snapshot is generated; no task-model restructuring.
                    await Task.Run(() =>
                    {
                        cancellation.Token.ThrowIfCancellationRequested();
                        var generator = new RequestGenerator(); generator.Generate(task);
                        paths = paths ?? new BackendPathResolver().Resolve();
                        if (!File.Exists(paths.Python)) throw new FileNotFoundException("未找到可用Python环境。", paths.Python);
                        if (!File.Exists(paths.Entry)) throw new FileNotFoundException("正向计算入口文件不存在。", paths.Entry);
                        if (BackendPathResolver.IsWithin(paths.RuntimeRoot, paths.PackageRoot)) throw new ArgumentException("运行文件不能写入冻结后端目录。");
                        if (timeoutSeconds <= 0) throw new ArgumentException("超时时间必须为正整数。");
                        submission = TaskDirectoryManager.Create(paths.RuntimeRoot, "01", selectedTask);
                        record.ExecutionDirectory = submission;
                        record.TaskDirectory = Directory.GetParent(Path.GetDirectoryName(submission)).FullName;
                        runRoot = Path.Combine(submission, "results");
                        record.RequestPath = generator.Save(task, Path.Combine(submission, "request.json"));
                        stdout = new StreamWriter(Path.Combine(submission, "stdout.log"), false, new UTF8Encoding(false)) { AutoFlush = true };
                        stderr = new StreamWriter(Path.Combine(submission, "stderr.log"), false, new UTF8Encoding(false)) { AutoFlush = true };
                    }, cancellation.Token).ConfigureAwait(false);
                    var probe = MakeRequest(paths);
                    probe.Arguments.Add("-c");
                    probe.Arguments.Add("import sys,pandas; assert sys.version_info >= (3,10), 'Python 3.10+ required'; print('Python environment ready: '+sys.version.split()[0])");
                    probe.Timeout = TimeSpan.FromSeconds(20);
                    var environment = await manager.RunAsync(probe, cancellation.Token).ConfigureAwait(false);
                    cancellation.Token.ThrowIfCancellationRequested();
                    if (environment.State != ProcessRunState.Completed) throw new InvalidOperationException("未找到可用Python环境，或缺少 pandas 依赖。请检查警告日志。 " + environment.Error);
                    var request = MakeRequest(paths);
                    request.Arguments.Add(paths.Entry);
                    request.Arguments.Add("--params-json"); request.Arguments.Add(record.RequestPath);
                    request.Arguments.Add("--run-root"); request.Arguments.Add(runRoot);
                    request.Arguments.Add("--timeout-seconds"); request.Arguments.Add(timeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    if (prepareOnly) request.Arguments.Add("--prepare-only");
                    request.Timeout = TimeSpan.FromSeconds(timeoutSeconds + 120.0); // Also bounds preparation / hung entry.
                    launchingBackend = true;
                    onLog(new ProcessLogEvent { Text = "Executable: " + request.Executable + "\nArguments: " + String.Join(" ", System.Linq.Enumerable.Select(request.Arguments, WindowsJobProcess.Quote)) + "\nWorkingDirectory: " + request.WorkingDirectory });
                    var result = await manager.RunAsync(request, cancellation.Token).ConfigureAwait(false);
                    record.ForwardExitCode = result.ExitCode; record.ExitCode = result.ExitCode; record.State = result.State;
                    if (ioError != null) throw new IOException("运行日志保存失败：" + ioError);
                    if (record.State == ProcessRunState.Completed)
                    {
                        if (String.IsNullOrEmpty(record.RunDirectory) || !Directory.Exists(record.RunDirectory))
                            throw new DirectoryNotFoundException("程序已退出，但未获得有效运行目录。");
                        // Only the execution envelope is read; no result CSV / image data is parsed.
                        string file = Path.Combine(record.RunDirectory, "result.json");
                        var envelope = new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 }.Deserialize<Dictionary<string, object>>(File.ReadAllText(file, Encoding.UTF8));
                        string expected = prepareOnly ? "prepared" : "success";
                        if ((string)envelope["status"] != expected || Convert.ToInt32(envelope["return_code"]) != 0)
                            throw new InvalidDataException("后端返回的完成状态不符合本次运行模式。");
                        if ((string)envelope["run_id"] != record.RunId) throw new InvalidDataException("运行编号不一致。");
                        string outputDirectory = (string)envelope["output_dir"];
                        if (!BackendPathResolver.IsWithin(outputDirectory, record.RunDirectory) || !Directory.Exists(outputDirectory))
                            throw new DirectoryNotFoundException("结果目录不存在或不属于本次运行。");
                        string backendRequest = (string)envelope["request_json"];
                        if (!BackendPathResolver.IsWithin(backendRequest, record.RunDirectory) || !File.Exists(backendRequest))
                            throw new FileNotFoundException("后端运行请求记录不存在或不属于本次运行。");
                        record.ResultDirectory = outputDirectory;
                        record.BackendRequestPath = backendRequest;
                        if (prepareOnly) record.Message = "输入准备完成（未执行求解及后处理）。";
                        else
                        {
                            onLog(new ProcessLogEvent { Text = "01 正向计算完成，开始调用 03 特征提取入口。" });
                            await RunFeatureExtractionAsync(paths, record, timeoutSeconds, cancellation.Token, onLog).ConfigureAwait(false);
                            if (ioError != null) throw new IOException("运行日志保存失败：" + ioError);
                            record.Message = "正向计算及特征提取完成。";
                            record.BackendStatus = "success";
                        }
                    }
                    else record.Message = record.State == ProcessRunState.Cancelled ? "正向计算已停止。" : "正向计算失败，请查看警告日志。";
                    record.Diagnostic = result.Error;
                }
                catch (OperationCanceledException) { record.State = ProcessRunState.Cancelled; record.Message = "正向计算已停止。"; }
                catch (Exception ex)
                {
                    record.State = ProcessRunState.Failed;
                    record.Message = "无法完成正向计算或特征提取：" + ex.Message;
                    record.Diagnostic = ex.ToString();
                    onLog(new ProcessLogEvent { IsError = true, Text = ex.ToString() });
                }
                finally
                {
                    record.EndedAt = DateTimeOffset.Now;
                    manager.Log -= onLog; manager.Progress -= onProgress; manager.StateChanged -= onState;
                    try
                    {
                        try { stdout?.Dispose(); } finally { stderr?.Dispose(); }
                        if (submission != null) File.WriteAllText(Path.Combine(submission, "run-location.json"),
                            new JavaScriptSerializer().Serialize(record), new UTF8Encoding(false));
                    }
                    catch (Exception ex)
                    {
                        record.State = ProcessRunState.Failed; record.Message = "运行记录保存失败，请检查运行目录权限。";
                        record.Diagnostic = ex.ToString(); Log?.Invoke(new ProcessLogEvent { IsError = true, Text = ex.ToString() });
                    }
                    lock (gate) stop = null;
                    Volatile.Write(ref active, 0);
                    StateChanged?.Invoke(record.State);
                }
            }
            return record;
        }

        private async Task RunFeatureExtractionAsync(BackendPaths paths, ForwardRunRecord record, int timeoutSeconds,
            CancellationToken token, Action<ProcessLogEvent> onLog)
        {
            token.ThrowIfCancellationRequested();
            string runtimePackage = await Task.Run(() => RuntimePackage.Prepare(paths, token), token).ConfigureAwait(false);
            record.RuntimePackage = runtimePackage;
            var config = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(
                File.ReadAllText(Path.Combine(runtimePackage, "config.json"), Encoding.UTF8));
            var entries = (Dictionary<string, object>)config["standard_entries"];
            string entry = Path.GetFullPath(Path.Combine(runtimePackage, (string)entries["03_similarity"]));
            if (!BackendPathResolver.IsWithin(entry, runtimePackage) || !File.Exists(entry))
                throw new FileNotFoundException("03 特征提取入口不存在或不在运行副本内。", entry);

            string featureRoot = Path.Combine(record.ResultDirectory, "features");
            if (Directory.Exists(featureRoot))
                throw new IOException("本次正向结果目录中已存在 features，无法确定新的特征提取结果。");

            var featurePaths = new BackendPaths { PackageRoot = runtimePackage, Python = paths.Python };
            var request = MakeRequest(featurePaths);
            request.Arguments.Add(entry);
            request.Arguments.Add("--mode"); request.Arguments.Add("features");
            request.Arguments.Add("--run-dir"); request.Arguments.Add(record.ResultDirectory);
            request.Arguments.Add("--output-dir"); request.Arguments.Add(featureRoot);
            request.Timeout = TimeSpan.FromSeconds(timeoutSeconds + 120.0);
            onLog(new ProcessLogEvent { Text = "Executable: " + request.Executable + "\nArguments: " +
                String.Join(" ", System.Linq.Enumerable.Select(request.Arguments, WindowsJobProcess.Quote)) +
                "\nWorkingDirectory: " + request.WorkingDirectory });

            ProcessRunResult feature = await manager.RunAsync(request, token).ConfigureAwait(false);
            record.FeatureExitCode = feature.ExitCode;
            record.ExitCode = feature.ExitCode;
            record.State = feature.State;
            if (feature.State == ProcessRunState.Cancelled)
            {
                token.ThrowIfCancellationRequested();
                throw new OperationCanceledException("03 特征提取已停止。", token);
            }
            if (feature.State != ProcessRunState.Completed || feature.ExitCode != 0)
                throw new InvalidOperationException("03 特征提取入口执行失败。" + (String.IsNullOrWhiteSpace(feature.Error) ? String.Empty : " " + feature.Error));

            string[] directories = Directory.Exists(featureRoot) ? Directory.GetDirectories(featureRoot) : new string[0];
            if (directories.Length != 1)
                throw new InvalidDataException("03 特征提取没有生成唯一的运行目录。");
            string featureDirectory = Path.GetFullPath(directories[0]);
            if (!BackendPathResolver.IsWithin(featureDirectory, featureRoot))
                throw new InvalidDataException("03 特征提取结果超出本次正向结果目录。");
            string statusPath = Path.Combine(featureDirectory, "evaluation_status.json");
            if (!File.Exists(statusPath)) throw new FileNotFoundException("03 特征提取完成标志不存在。", statusPath);
            var status = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(statusPath, Encoding.UTF8));
            string featureRunId = status["run_id"] as string;
            if ((status["status"] as string) != "success" || (status["mode"] as string) != "features" ||
                featureRunId != Path.GetFileName(featureDirectory))
                throw new InvalidDataException("03 特征提取完成标志与本次运行不一致。");
            record.FeatureRunId = featureRunId;
            record.FeatureResultDirectory = featureDirectory;
            onLog(new ProcessLogEvent { Text = "03 特征提取完成：" + featureDirectory });
        }

        private static ProcessRunRequest MakeRequest(BackendPaths paths)
        {
            return new ProcessRunRequest { Executable = paths.Python, WorkingDirectory = paths.PackageRoot,
                Arguments = new List<string> { "-B", "-u" }, EnvironmentVariables = new Dictionary<string, string>
                { { "PYTHONUTF8", "1" }, { "PYTHONIOENCODING", "utf-8" }, { "PYTHONDONTWRITEBYTECODE", "1" } } };
        }
    }
}
