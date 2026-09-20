using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Web.Script.Serialization;

namespace PreProcess.Wpf.Services.Execution
{
    // 定义 BackendPaths 类型，集中封装与该领域对象相关的状态和行为。
    public sealed class BackendPaths
    {
        public string PackageRoot { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string Entry { get; set; }
        public string Python { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string RuntimeRoot { get; set; }
    }
    public sealed class BackendPathResolver
    {
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public BackendPaths Resolve()
        {
            string app = AppDomain.CurrentDomain.BaseDirectory;
            string package = ResolvePackageRoot(app);
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            var config = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(package, "config.json")));
            var entries = (Dictionary<string, object>)config["standard_entries"];
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            string entry = Absolute(package, (string)entries["01_forward"]);
            if (!File.Exists(entry)) throw new FileNotFoundException("正向计算入口文件不存在。", entry);
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            string configuredPython = ConfigurationManager.AppSettings["PythonExecutable"];
            string defaultPython = Path.Combine(
                Directory.GetParent(Path.GetFullPath(package).TrimEnd('\\', '/')).FullName,
                // 继续处理当前业务步骤，保持上下文状态一致。
                ".python-runtime", "Scripts", "python.exe");
            string python = (String.IsNullOrWhiteSpace(configuredPython)
                    // 调用对应组件完成当前步骤，并保留产生的处理结果。
                    || String.Equals(configuredPython, "python.exe", StringComparison.OrdinalIgnoreCase))
                && File.Exists(defaultPython)
                // 继续处理当前业务步骤，保持上下文状态一致。
                ? defaultPython
                : FindExecutable(String.IsNullOrWhiteSpace(configuredPython) ? "python.exe" : configuredPython, app);
            string runtime = ResolveRuntimeRoot(app, package);
            // 返回当前步骤生成的结果，并结束本次调用。
            return new BackendPaths { PackageRoot = package, Entry = entry, Python = python, RuntimeRoot = runtime };
        }
        public string ResolveRuntimeRoot()
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            string app = AppDomain.CurrentDomain.BaseDirectory;
            return ResolveRuntimeRoot(app, ResolvePackageRoot(app));
        }
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public static string FindExecutable(string name, string baseDirectory)
        {
            if (Path.IsPathRooted(name) || name.IndexOfAny(new[] { '\\', '/' }) >= 0)
            {
                string path = Absolute(baseDirectory, name);
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (File.Exists(path)) return path;
            }
            else foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (String.IsNullOrWhiteSpace(directory)) continue;
                string path = Path.Combine(directory.Trim('"'), name);
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (File.Exists(path) && path.IndexOf("WindowsApps", StringComparison.OrdinalIgnoreCase) < 0) return Path.GetFullPath(path);
            }
            throw new FileNotFoundException("未找到可用Python环境，请配置 PythonExecutable。", name);
        }
        public static bool IsWithin(string path, string directory)
        {
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string root = Path.GetFullPath(directory).TrimEnd('\\', '/');
            string full = Path.GetFullPath(path).TrimEnd('\\', '/');
            // 返回当前步骤生成的结果，并结束本次调用。
            return String.Equals(root, full, StringComparison.OrdinalIgnoreCase) || full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        private static string ResolvePackageRoot(string app)
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            string package = ConfigurationManager.AppSettings["BackendRoot"];
            if (!String.IsNullOrWhiteSpace(package)) package = Absolute(app, package);
            // 当前置条件不成立时执行备用路径，保持处理结果完整。
            else
            {
                for (var directory = new DirectoryInfo(app); directory != null; directory = directory.Parent)
                {
                    string candidate = Path.Combine(directory.FullName, "coreprogram");
                    // 校验当前条件，仅在满足业务约束时进入该处理分支。
                    if (File.Exists(Path.Combine(candidate, "config.json"))) { package = candidate; break; }
                }
            }
            if (String.IsNullOrWhiteSpace(package) || !File.Exists(Path.Combine(package, "config.json")))
                // 发现无效输入或运行状态后立即终止，防止错误继续传播。
                throw new FileNotFoundException("未找到后端程序包，请配置 BackendRoot。");
            return Path.GetFullPath(package);
        }
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static string ResolveRuntimeRoot(string app, string package)
        {
            string runtime = ConfigurationManager.AppSettings["RuntimeRoot"];
            if (String.IsNullOrWhiteSpace(runtime))
            {
                // 处理文件系统路径及数据，并在使用前确认目标有效。
                string projectRoot = Directory.GetParent(Path.GetFullPath(package).TrimEnd('\\', '/')).FullName;
                runtime = Path.Combine(projectRoot, "output");
            }
            // 当前置条件不成立时执行备用路径，保持处理结果完整。
            else runtime = Absolute(app, runtime);
            if (IsWithin(runtime, package)) throw new ArgumentException("运行目录不能位于冻结的后端程序包内。");
            // 返回当前步骤生成的结果，并结束本次调用。
            return Path.GetFullPath(runtime);
        }
        private static string Absolute(string root, string path) { return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(root, path)); }
    }
}
