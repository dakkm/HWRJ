using System;

namespace PreProcess.Wpf.Services.Process
{
    public sealed class ProcessRunResult
    {
        public ProcessRunState State { get; set; }
        public int? ExitCode { get; set; }
        public int? ProcessId { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset EndedAt { get; set; }
        public string RunId { get; set; }
        public string ResultDirectory { get; set; }
        public string Error { get; set; }
        public bool TimedOut { get; set; }
    }
}
