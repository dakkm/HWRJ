using System;
using System.Threading;
using PreProcess.Wpf.Services.Process;

namespace PreProcess.Wpf.Services.Execution
{
    // 定义 RunRecord 类型，集中封装与该领域对象相关的状态和行为。
    public class RunRecord
    {
        public string Module { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string RunId { get; set; }
        public string RequestPath { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string BackendRequestPath { get; set; }
        public string TaskDirectory { get; set; }
        public string ExecutionDirectory { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string RunDirectory { get; set; }
        public string ResultDirectory { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string FeatureRunId { get; set; }
        public string FeatureResultDirectory { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string RuntimePackage { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset EndedAt { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public ProcessRunState State { get; set; }
        public int? ExitCode { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public int? ForwardExitCode { get; set; }
        public int? FeatureExitCode { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public int? TrajectoryExitCode { get; set; }
        public string TrajectoryResultDirectory { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public bool PrepareOnly { get; set; }
        public string Message { get; set; }
        public string Diagnostic { get; set; }
        // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
        public string BackendStatus { get; set; }
    }
    internal sealed class ExecutionLease : IDisposable
    {
        // 保存该组件运行所需的配置或中间状态。
        private static int busy;
        internal static ExecutionLease TryEnter() => Interlocked.CompareExchange(ref busy, 1, 0) == 0 ? new ExecutionLease() : null;
        // 结束当前资源的使用，避免残留句柄或后台任务。
        public void Dispose() { Interlocked.Exchange(ref busy, 0); }
    }
}
