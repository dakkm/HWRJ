using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace PreProcess.Wpf.Services
{
    /// <summary>03 模块独立配置包；只保存求解控制定义，不接入当前后端请求。</summary>
    public sealed class SolverControlConfigPackage
    {
        public string Format { get; set; }
        public string CreatedAtUtc { get; set; }
        public double TimeStep { get; set; }
        public double OutputInterval { get; set; }
        public double ConvergenceTolerance { get; set; }
        public int MaxIterations { get; set; }
        public int TimeoutSeconds { get; set; }
        public string SolverOption { get; set; }
    }

    public sealed class SolverControlConfigStore
    {
        public void Save(string path, SolverControlConfigPackage package)
        {
            Validate(package);
            File.WriteAllText(path, new JavaScriptSerializer().Serialize(package), new UTF8Encoding(false));
        }

        public SolverControlConfigPackage LoadAndValidate(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("找不到 03 求解控制配置文件。", path);
            var package = new JavaScriptSerializer().Deserialize<SolverControlConfigPackage>(File.ReadAllText(path, Encoding.UTF8));
            Validate(package);
            return package;
        }

        private static void Validate(SolverControlConfigPackage package)
        {
            if (package == null || package.Format != "solver-control-config-v1") throw new InvalidDataException("03 求解控制配置格式无效或版本不受支持。");
            if (double.IsNaN(package.TimeStep) || double.IsInfinity(package.TimeStep) ||
                double.IsNaN(package.OutputInterval) || double.IsInfinity(package.OutputInterval) ||
                double.IsNaN(package.ConvergenceTolerance) || double.IsInfinity(package.ConvergenceTolerance))
                throw new InvalidDataException("求解控制数值必须为有限数值。");
            if (package.TimeStep <= 0 || package.OutputInterval <= 0 || package.ConvergenceTolerance <= 0 || package.MaxIterations <= 0 || package.TimeoutSeconds <= 0) throw new InvalidDataException("求解控制数值必须为正数。");
            if (package.OutputInterval < package.TimeStep) throw new InvalidDataException("输出频率不能小于时间步长。");
            if (string.IsNullOrWhiteSpace(package.SolverOption)) throw new InvalidDataException("求解器选项不能为空。");
        }
    }
}
