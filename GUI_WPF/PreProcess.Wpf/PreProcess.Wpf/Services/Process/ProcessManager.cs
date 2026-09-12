using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PreProcess.Wpf.Services.Process
{
    public sealed class ProcessManager
    {
        private int active;
        private readonly object gate = new object();
        private CancellationTokenSource stop;
        public bool IsRunning => Volatile.Read(ref active) != 0;
        public event Action<ProcessLogEvent> Log;
        public event Action<ProcessProgressEvent> Progress;
        public event Action<ProcessRunState> StateChanged;

        public void Stop() { lock (gate) stop?.Cancel(); }

        public async Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (Interlocked.CompareExchange(ref active, 1, 0) != 0) throw new InvalidOperationException("已有程序正在运行。");
            var result = new ProcessRunResult { StartedAt = DateTimeOffset.Now, State = ProcessRunState.Preparing };
            using (var localStop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            using (var timeout = new CancellationTokenSource())
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(localStop.Token, timeout.Token))
            {
                lock (gate) stop = localStop;
                try
                {
                    Emit(StateChanged, ProcessRunState.Preparing);
                    if (request == null || !File.Exists(request.Executable)) throw new FileNotFoundException("未找到可执行程序。", request?.Executable);
                    if (!Directory.Exists(request.WorkingDirectory)) throw new DirectoryNotFoundException("程序工作目录不存在：" + request.WorkingDirectory);
                    if (request.Timeout.HasValue) timeout.CancelAfter(request.Timeout.Value);
                    await Task.Run(async () =>
                    {
                        linked.Token.ThrowIfCancellationRequested();
                        using (var child = WindowsJobProcess.Start(request))
                        {
                            result.ProcessId = child.Id;
                            Emit(StateChanged, ProcessRunState.Running);
                            using (linked.Token.Register(() =>
                            {
                                Emit(StateChanged, ProcessRunState.Stopping);
                                try { child.Terminate(); }
                                catch (Exception ex) { Emit(Log, new ProcessLogEvent { IsError = true, Text = "终止进程树失败：" + ex }); }
                            }))
                            {
                                var output = Task.Run(() => Read(child.Output, false, result, child));
                                var error = Task.Run(() => Read(child.Error, true, result, child));
                                result.ExitCode = await Task.Run(() => child.Wait()).ConfigureAwait(false);
                                // Root exit does not imply descendants exited. Close the whole job before draining EOF.
                                child.Terminate();
                                await Task.WhenAll(output, error).ConfigureAwait(false);
                            }
                        }
                    }).ConfigureAwait(false);
                    result.TimedOut = timeout.IsCancellationRequested && !localStop.IsCancellationRequested;
                    result.State = localStop.IsCancellationRequested ? ProcessRunState.Cancelled :
                        result.TimedOut || result.ExitCode != 0 ? ProcessRunState.Failed : ProcessRunState.Completed;
                    if (result.TimedOut) result.Error = "程序超过等待时限，已终止进程树。";
                    else if (result.State == ProcessRunState.Failed) result.Error = "程序异常退出，退出码：" + result.ExitCode;
                }
                catch (OperationCanceledException)
                {
                    result.TimedOut = timeout.IsCancellationRequested && !localStop.IsCancellationRequested;
                    result.State = result.TimedOut ? ProcessRunState.Failed : ProcessRunState.Cancelled;
                }
                catch (Exception ex)
                {
                    result.State = ProcessRunState.Failed; result.Error = ex.Message;
                    Emit(Log, new ProcessLogEvent { IsError = true, Text = ex.ToString() });
                }
                finally
                {
                    result.EndedAt = DateTimeOffset.Now;
                    lock (gate) stop = null;
                    Volatile.Write(ref active, 0);
                    Emit(StateChanged, result.State);
                }
            }
            return result;
        }
        private void Read(StreamReader reader, bool error, ProcessRunResult result, WindowsJobProcess child)
        {
            try
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    Emit(Log, new ProcessLogEvent { Text = line, IsError = error });
                    ProcessProgressEvent progress;
                    if (!error && GuiProgressParser.TryParse(line, out progress))
                    {
                        if (progress.RunId != null) result.RunId = progress.RunId;
                        if (progress.RunDirectory != null) result.ResultDirectory = progress.RunDirectory;
                        Emit(Progress, progress); // Unknown states are data, never lifecycle commands.
                    }
                }
            }
            catch { child.Terminate(); throw; }
        }
        private static void Emit<T>(Action<T> handlers, T value)
        {
            if (handlers == null) return;
            foreach (Action<T> handler in handlers.GetInvocationList())
                try { handler(value); } catch (Exception ex) { System.Diagnostics.Trace.WriteLine(ex); }
        }
    }
}
