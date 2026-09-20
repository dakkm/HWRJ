using System;
using System.Collections.Generic;

namespace PreProcess.Wpf.Services.Process
{
    // 定义 ProcessRunState 类型，集中封装与该领域对象相关的状态和行为。
    public enum ProcessRunState { Idle, Preparing, Running, Stopping, Completed, Failed, Cancelled }

    public sealed class ProcessRunRequest
    // 说明当前文件承载的类型职责，便于维护者快速定位功能边界。
    {
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string Executable { get; set; }
        public IList<string> Arguments { get; set; } = new List<string>();
        public string WorkingDirectory { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public IDictionary<string, string> EnvironmentVariables { get; set; } = new Dictionary<string, string>();
        public TimeSpan? Timeout { get; set; }
    }
}
