using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Web.Script.Serialization;

namespace PreProcess.Wpf.Services.Execution
{
    public sealed class BackendPaths
    {
        public string PackageRoot { get; set; }
        public string Entry { get; set; }
        public string Python { get; set; }
        public string RuntimeRoot { get; set; }
    }
    public sealed class BackendPathResolver
    {
        public BackendPaths Resolve()
        {
            string app = AppDomain.CurrentDomain.BaseDirectory;
            string package = ConfigurationManager.AppSettings["BackendRoot"];
            if (!String.IsNullOrWhiteSpace(package)) package = Absolute(app, package);
            else
            {
                for (var directory = new DirectoryInfo(app); directory != null; directory = directory.Parent)
                {
                    string candidate = Path.Combine(directory.FullName, "coreprogram");
                    if (File.Exists(Path.Combine(candidate, "config.json"))) { package = candidate; break; }
                }
            }
            if (String.IsNullOrWhiteSpace(package) || !File.Exists(Path.Combine(package, "config.json")))
                throw new FileNotFoundException("未找到后端程序包，请配置 BackendRoot。");
            var config = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(package, "config.json")));
            var entries = (Dictionary<string, object>)config["standard_entries"];
            string entry = Absolute(package, (string)entries["01_forward"]);
            if (!File.Exists(entry)) throw new FileNotFoundException("正向计算入口文件不存在。", entry);
            string python = ConfigurationManager.AppSettings["PythonExecutable"];
            python = FindExecutable(String.IsNullOrWhiteSpace(python) ? "python.exe" : python, app);
            string runtime = ConfigurationManager.AppSettings["RuntimeRoot"];
            if (String.IsNullOrWhiteSpace(runtime)) runtime = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PreProcess", "Runtime");
            else runtime = Absolute(app, runtime);
            // Respect the backend's official --run-root option; never use a protected package as writable storage.
            if (IsWithin(runtime, package)) throw new ArgumentException("运行目录不能位于冻结的后端程序包内。");
            return new BackendPaths { PackageRoot = package, Entry = entry, Python = python, RuntimeRoot = runtime };
        }
        public static string FindExecutable(string name, string baseDirectory)
        {
            if (Path.IsPathRooted(name) || name.IndexOfAny(new[] { '\\', '/' }) >= 0)
            {
                string path = Absolute(baseDirectory, name);
                if (File.Exists(path)) return path;
            }
            else foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                if (String.IsNullOrWhiteSpace(directory)) continue;
                string path = Path.Combine(directory.Trim('"'), name);
                if (File.Exists(path) && path.IndexOf("WindowsApps", StringComparison.OrdinalIgnoreCase) < 0) return Path.GetFullPath(path);
            }
            throw new FileNotFoundException("未找到可用Python环境，请配置 PythonExecutable。", name);
        }
        public static bool IsWithin(string path, string directory)
        {
            string root = Path.GetFullPath(directory).TrimEnd('\\', '/');
            string full = Path.GetFullPath(path).TrimEnd('\\', '/');
            return String.Equals(root, full, StringComparison.OrdinalIgnoreCase) || full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        private static string Absolute(string root, string path) { return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(root, path)); }
    }
}
