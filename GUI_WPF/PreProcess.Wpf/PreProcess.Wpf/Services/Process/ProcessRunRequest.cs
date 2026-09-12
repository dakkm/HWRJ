using System;
using System.Collections.Generic;

namespace PreProcess.Wpf.Services.Process
{
    public enum ProcessRunState { Idle, Preparing, Running, Stopping, Completed, Failed, Cancelled }

    public sealed class ProcessRunRequest
    {
        public string Executable { get; set; }
        public IList<string> Arguments { get; set; } = new List<string>();
        public string WorkingDirectory { get; set; }
        public IDictionary<string, string> EnvironmentVariables { get; set; } = new Dictionary<string, string>();
        public TimeSpan? Timeout { get; set; }
    }
}
