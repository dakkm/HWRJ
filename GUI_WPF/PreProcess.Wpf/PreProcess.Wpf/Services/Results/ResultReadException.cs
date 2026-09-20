using System;
using System.IO;

// 说明当前文件承载的类型职责，便于维护者快速定位功能边界。
namespace PreProcess.Wpf.Services.Results
{
    public sealed class ResultReadException : IOException
    {
        public ResultReadException(string path, string message)
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            : base("结果文件格式异常：" + path + "。" + message)
        {
            // 本文件只处理所属模块的数据或界面逻辑，不在此处引入额外副作用。
            SourcePath = path;
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public ResultReadException(string path, string message, Exception innerException)
            : base("结果文件格式异常：" + path + "。" + message, innerException)
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            SourcePath = path;
        }

        // 调用方应通过公开成员访问功能，内部状态由当前类型统一维护。
        public string SourcePath { get; private set; }
    }
}
