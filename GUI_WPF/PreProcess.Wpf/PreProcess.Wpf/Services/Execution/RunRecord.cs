using System;
using System.Threading;
using PreProcess.Wpf.Services.Process;

namespace PreProcess.Wpf.Services.Execution
{
    public class RunRecord
    {
        public string Module { get; set; }
        public string RunId { get; set; }
        public string RequestPath { get; set; }
        public string BackendRequestPath { get; set; }
        public string RunDirectory { get; set; }
        public string ResultDirectory { get; set; }
        public string RuntimePackage { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset EndedAt { get; set; }
        public ProcessRunState State { get; set; }
        public int? ExitCode { get; set; }
        public bool PrepareOnly { get; set; }
        public string Message { get; set; }
        public string Diagnostic { get; set; }
        public string BackendStatus { get; set; }
    }
    internal sealed class ExecutionLease : IDisposable
    {
        private static int busy;
        internal static ExecutionLease TryEnter() => Interlocked.CompareExchange(ref busy, 1, 0) == 0 ? new ExecutionLease() : null;
        public void Dispose() { Interlocked.Exchange(ref busy, 0); }
    }
}
