using System;

namespace PreProcess.Wpf.Services.Process
{
    // 定义 ProcessRunResult 类型，集中封装与该领域对象相关的状态和行为。
    public sealed class ProcessRunResult
    {
        public ProcessRunState State { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public int? ExitCode { get; set; }
        public int? ProcessId { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public DateTimeOffset EndedAt { get; set; }
        public string RunId { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string ResultDirectory { get; set; }
        public string Error { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public bool TimedOut { get; set; }
    }
}
