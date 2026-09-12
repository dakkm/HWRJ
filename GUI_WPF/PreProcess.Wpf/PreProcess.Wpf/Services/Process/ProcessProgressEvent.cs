using System.Collections.Generic;

namespace PreProcess.Wpf.Services.Process
{
    public sealed class ProcessProgressEvent
    {
        public string Module { get; set; }
        public string State { get; set; }
        public double? Timestamp { get; set; }
        public string RunId { get; set; }
        public string RunDirectory { get; set; }
        public IReadOnlyDictionary<string, object> Fields { get; set; }
    }
}
