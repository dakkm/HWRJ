using System;

namespace PreProcess.Wpf.Services.Process
{
    public sealed class ProcessLogEvent
    {
        public DateTimeOffset Timestamp { get; } = DateTimeOffset.Now;
        public string Text { get; set; }
        public bool IsError { get; set; }
    }
}
