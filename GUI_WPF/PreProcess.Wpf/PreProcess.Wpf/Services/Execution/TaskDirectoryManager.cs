using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace PreProcess.Wpf.Services.Execution
{
    internal static class TaskDirectoryManager
    {
        public static string SelectTask(string runtimeRoot, string taskFile, string taskName)
        {
            if (String.IsNullOrWhiteSpace(runtimeRoot)) throw new ArgumentException("运行根目录不能为空。", "runtimeRoot");
            string tasksRoot = Path.Combine(Path.GetFullPath(runtimeRoot), "tasks");
            Directory.CreateDirectory(tasksRoot);
            string identity = String.IsNullOrWhiteSpace(taskFile) ? taskName ?? "default" : Path.GetFullPath(taskFile);
            string label = String.IsNullOrWhiteSpace(taskFile) ? taskName : Path.GetFileNameWithoutExtension(taskFile);
            if (!String.IsNullOrEmpty(label) && label.EndsWith(".task", StringComparison.OrdinalIgnoreCase)) label = label.Substring(0, label.Length - 5);
            label = SafeName(label, "default", 24);
            string directory = Path.Combine(tasksRoot, label + "_" + ShortHash(identity));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "task-info.json"), new JavaScriptSerializer().Serialize(new
            {
                schema_version = "preprocess-result-task-v1", task_name = taskName ?? String.Empty,
                task_file = taskFile == null ? null : Path.GetFullPath(taskFile)
            }), new UTF8Encoding(false));
            return directory;
        }

        public static string Create(string runtimeRoot, string module, string selectedTask = null)
        {
            string tasksRoot = Path.Combine(Path.GetFullPath(runtimeRoot), "tasks");
            Directory.CreateDirectory(tasksRoot);
            string taskDirectory = selectedTask;
            if (String.IsNullOrWhiteSpace(taskDirectory))
            {
                taskDirectory = Path.Combine(tasksRoot, "task_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 4));
                Directory.CreateDirectory(taskDirectory);
            }
            if (!BackendPathResolver.IsWithin(taskDirectory, tasksRoot)) throw new ArgumentException("任务结果目录必须位于 output/tasks 内。", "selectedTask");
            string moduleDirectory = Path.Combine(Path.GetFullPath(taskDirectory), ModuleName(module));
            Directory.CreateDirectory(moduleDirectory);
            for (int index = 1; index <= 999999; index++)
            {
                string run = Path.Combine(moduleDirectory, "run_" + index.ToString("D3"));
                if (Directory.Exists(run)) continue;
                Directory.CreateDirectory(run);
                return run;
            }
            throw new IOException("当前模块的运行目录数量已达到上限。");
        }

        private static string ModuleName(string module)
        {
            return module == "01" ? "01-正向计算" : module == "02" ? "02-智能预测" : module == "轨迹" ? "轨迹生成" :
                module == "03" ? "03-相似度评估" : module == "04" ? "04-红外场景构建" : SafeName(module, "module", 24);
        }
        private static string SafeName(string value, string fallback, int maximum)
        {
            string name = String.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
            return name.Length <= maximum ? name : name.Substring(0, maximum);
        }
        private static string ShortHash(string value)
        {
            using (var hash = SHA256.Create())
            {
                byte[] bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(value.ToUpperInvariant()));
                return BitConverter.ToString(bytes, 0, 3).Replace("-", String.Empty).ToLowerInvariant();
            }
        }
    }
}
