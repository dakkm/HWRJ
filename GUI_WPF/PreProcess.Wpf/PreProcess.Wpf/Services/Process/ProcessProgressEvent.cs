using System.Collections.Generic;

namespace PreProcess.Wpf.Services.Process
{
    // 定义 ProcessProgressEvent 类型，集中封装与该领域对象相关的状态和行为。
    public sealed class ProcessProgressEvent
    {
        public string Module { get; set; }
        // 说明当前文件承载的类型职责，便于维护者快速定位功能边界。
        public string State { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public double? Timestamp { get; set; }
        public string RunId { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string RunDirectory { get; set; }
        public IReadOnlyDictionary<string, object> Fields { get; set; }
    }
}
