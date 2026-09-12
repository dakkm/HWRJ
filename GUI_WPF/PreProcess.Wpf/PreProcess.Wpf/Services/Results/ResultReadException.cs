using System;
using System.IO;

namespace PreProcess.Wpf.Services.Results
{
    public sealed class ResultReadException : IOException
    {
        public ResultReadException(string path, string message)
            : base("结果文件格式异常：" + path + "。" + message)
        {
            SourcePath = path;
        }

        public ResultReadException(string path, string message, Exception innerException)
            : base("结果文件格式异常：" + path + "。" + message, innerException)
        {
            SourcePath = path;
        }

        public string SourcePath { get; private set; }
    }
}
