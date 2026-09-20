using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PreProcess.Wpf.Services.Process
{
    // 定义 ProcessManager 类型，集中封装与该领域对象相关的状态和行为。
    public sealed class ProcessManager
    {
        private int active;
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private readonly object gate = new object();
        private CancellationTokenSource stop;
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public bool IsRunning => Volatile.Read(ref active) != 0;
        public event Action<ProcessLogEvent> Log;
        public event Action<ProcessProgressEvent> Progress;
        // 维护事件订阅关系，使运行状态能够及时传递给调用方。
        public event Action<ProcessRunState> StateChanged;

        public void Stop() { lock (gate) stop?.Cancel(); }

        // 异步等待耗时任务完成，期间保持界面线程可响应。
        public async Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (Interlocked.CompareExchange(ref active, 1, 0) != 0) throw new InvalidOperationException("已有程序正在运行。");
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var result = new ProcessRunResult { StartedAt = DateTimeOffset.Now, State = ProcessRunState.Preparing };
            using (var localStop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            using (var timeout = new CancellationTokenSource())
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(localStop.Token, timeout.Token))
            {
                lock (gate) stop = localStop;
                // 进入可能失败的操作区间，并由后续异常分支统一处理故障。
                try
                {
                    Emit(StateChanged, ProcessRunState.Preparing);
                    if (request == null || !File.Exists(request.Executable)) throw new FileNotFoundException("未找到可执行程序。", request?.Executable);
                    // 校验当前条件，仅在满足业务约束时进入该处理分支。
                    if (!Directory.Exists(request.WorkingDirectory)) throw new DirectoryNotFoundException("程序工作目录不存在：" + request.WorkingDirectory);
                    if (request.Timeout.HasValue) timeout.CancelAfter(request.Timeout.Value);
                    // 异步等待耗时任务完成，期间保持界面线程可响应。
                    await Task.Run(async () =>
                    {
                        linked.Token.ThrowIfCancellationRequested();
                        using (var child = WindowsJobProcess.Start(request))
                        {
                            // 更新当前流程使用的数据，为下一处理步骤做好准备。
                            result.ProcessId = child.Id;
                            Emit(StateChanged, ProcessRunState.Running);
                            using (linked.Token.Register(() =>
                            {
                                Emit(StateChanged, ProcessRunState.Stopping);
                                // 进入可能失败的操作区间，并由后续异常分支统一处理故障。
                                try { child.Terminate(); }
                                catch (Exception ex) { Emit(Log, new ProcessLogEvent { IsError = true, Text = "终止进程树失败：" + ex }); }
                            // 继续处理当前业务步骤，保持上下文状态一致。
                            }))
                            {
                                var output = Task.Run(() => Read(child.Output, false, result, child));
                                // 异步等待耗时任务完成，期间保持界面线程可响应。
                                var error = Task.Run(() => Read(child.Error, true, result, child));
                                result.ExitCode = await Task.Run(() => child.Wait()).ConfigureAwait(false);
                                // Root exit does not imply descendants exited. Close the whole job before draining EOF.
                                child.Terminate();
                                // 异步等待耗时任务完成，期间保持界面线程可响应。
                                await Task.WhenAll(output, error).ConfigureAwait(false);
                            }
                        }
                    }).ConfigureAwait(false);
                    // 传递并检查取消信号，使长时间任务可以安全停止。
                    result.TimedOut = timeout.IsCancellationRequested && !localStop.IsCancellationRequested;
                    // 说明当前文件承载的类型职责，便于维护者快速定位功能边界。
                    result.State = localStop.IsCancellationRequested ? ProcessRunState.Cancelled :
                        // 更新当前流程使用的数据，为下一处理步骤做好准备。
                        result.TimedOut || result.ExitCode != 0 ? ProcessRunState.Failed : ProcessRunState.Completed;
                    if (result.TimedOut) result.Error = "程序超过等待时限，已终止进程树。";
                    else if (result.State == ProcessRunState.Failed) result.Error = "程序异常退出，退出码：" + result.ExitCode;
                }
                // 捕获本步骤产生的异常，将故障转换为可追踪的运行状态。
                catch (OperationCanceledException)
                {
                    result.TimedOut = timeout.IsCancellationRequested && !localStop.IsCancellationRequested;
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    result.State = result.TimedOut ? ProcessRunState.Failed : ProcessRunState.Cancelled;
                }
                catch (Exception ex)
                {
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    result.State = ProcessRunState.Failed; result.Error = ex.Message;
                    Emit(Log, new ProcessLogEvent { IsError = true, Text = ex.ToString() });
                }
                // 无论执行成功与否都释放资源并恢复组件的可用状态。
                finally
                {
                    result.EndedAt = DateTimeOffset.Now;
                    lock (gate) stop = null;
                    // 调用对应组件完成当前步骤，并保留产生的处理结果。
                    Volatile.Write(ref active, 0);
                    Emit(StateChanged, result.State);
                }
            }
            // 返回当前步骤生成的结果，并结束本次调用。
            return result;
        }
        private void Read(StreamReader reader, bool error, ProcessRunResult result, WindowsJobProcess child)
        {
            // 进入可能失败的操作区间，并由后续异常分支统一处理故障。
            try
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
                    Emit(Log, new ProcessLogEvent { Text = line, IsError = error });
                    ProcessProgressEvent progress;
                    // 校验当前条件，仅在满足业务约束时进入该处理分支。
                    if (!error && GuiProgressParser.TryParse(line, out progress))
                    {
                        if (progress.RunId != null) result.RunId = progress.RunId;
                        // 校验当前条件，仅在满足业务约束时进入该处理分支。
                        if (progress.RunDirectory != null) result.ResultDirectory = progress.RunDirectory;
                        Emit(Progress, progress); // Unknown states are data, never lifecycle commands.
                    }
                }
            }
            catch { child.Terminate(); throw; }
        }
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static void Emit<T>(Action<T> handlers, T value)
        {
            if (handlers == null) return;
            // 遍历当前数据集合，逐项完成必要的转换或状态更新。
            foreach (Action<T> handler in handlers.GetInvocationList())
                try { handler(value); } catch (Exception ex) { System.Diagnostics.Trace.WriteLine(ex); }
        }
    }
}
