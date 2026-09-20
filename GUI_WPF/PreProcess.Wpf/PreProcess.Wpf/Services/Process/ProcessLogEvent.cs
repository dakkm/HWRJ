using System;

namespace PreProcess.Wpf.Services.Process
// 说明当前文件承载的类型职责，便于维护者快速定位功能边界。
{
    public sealed class ProcessLogEvent
    {
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public DateTimeOffset Timestamp { get; } = DateTimeOffset.Now;
        public string Text { get; set; }
        // 本文件只处理所属模块的数据或界面逻辑，不在此处引入额外副作用。
        public bool IsError { get; set; }
    }
}
