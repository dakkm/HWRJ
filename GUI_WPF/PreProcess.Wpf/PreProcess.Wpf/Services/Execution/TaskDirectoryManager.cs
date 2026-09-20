using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace PreProcess.Wpf.Services.Execution
{
    internal static class TaskDirectoryManager
    {
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public static string SelectTask(string runtimeRoot, string taskFile, string taskName)
        {
            if (String.IsNullOrWhiteSpace(runtimeRoot)) throw new ArgumentException("运行根目录不能为空。", "runtimeRoot");
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string tasksRoot = Path.Combine(Path.GetFullPath(runtimeRoot), "results");
            Directory.CreateDirectory(tasksRoot);
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string identity = String.IsNullOrWhiteSpace(taskFile) ? taskName ?? "default" : Path.GetFullPath(taskFile);
            string label = String.IsNullOrWhiteSpace(taskFile) ? taskName : Path.GetFileNameWithoutExtension(taskFile);
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (!String.IsNullOrEmpty(label) && label.EndsWith(".task", StringComparison.OrdinalIgnoreCase)) label = label.Substring(0, label.Length - 5);
            label = SafeName(label, "default", 24);
            string directory = Path.Combine(tasksRoot, label + "_" + ShortHash(identity));
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "task-info.json"), new JavaScriptSerializer().Serialize(new
            {
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                schema_version = "preprocess-result-task-v1", task_name = taskName ?? String.Empty,
                task_file = taskFile == null ? null : Path.GetFullPath(taskFile)
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            }), new UTF8Encoding(false));
            return directory;
        }

        public static string Create(string runtimeRoot, string module, string selectedTask = null)
        {
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string tasksRoot = Path.Combine(Path.GetFullPath(runtimeRoot), "results");
            Directory.CreateDirectory(tasksRoot);
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            string taskDirectory = selectedTask;
            if (String.IsNullOrWhiteSpace(taskDirectory))
            {
                // 处理文件系统路径及数据，并在使用前确认目标有效。
                taskDirectory = Path.Combine(tasksRoot, "task_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 4));
                // 说明当前文件承载的类型职责，便于维护者快速定位功能边界。
                Directory.CreateDirectory(taskDirectory);
            }
            if (!BackendPathResolver.IsWithin(taskDirectory, tasksRoot)) throw new ArgumentException("任务结果目录必须位于 output/results 内。", "selectedTask");
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            string moduleCode = ModuleCode(module);
            for (int index = 1; index <= 999999; index++)
            {
                // 处理文件系统路径及数据，并在使用前确认目标有效。
                string run = Path.Combine(Path.GetFullPath(taskDirectory), moduleCode + "_" + index.ToString("D3"));
                if (Directory.Exists(run)) continue;
                // 处理文件系统路径及数据，并在使用前确认目标有效。
                Directory.CreateDirectory(run);
                return run;
            }
            throw new IOException("当前模块的运行目录数量已达到上限。");
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static string ModuleCode(string module)
        {
            return module == "01" ? "01" : module == "02" ? "02" : module == "轨迹" ? "05" :
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                module == "03" ? "03" : module == "04" ? "04" : SafeName(module, "M", 8);
        }
        private static string SafeName(string value, string fallback, int maximum)
        {
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            string name = String.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
            // 返回当前步骤生成的结果，并结束本次调用。
            return name.Length <= maximum ? name : name.Substring(0, maximum);
        }
        private static string ShortHash(string value)
        {
            using (var hash = SHA256.Create())
            {
                byte[] bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(value.ToUpperInvariant()));
                // 返回当前步骤生成的结果，并结束本次调用。
                return BitConverter.ToString(bytes, 0, 3).Replace("-", String.Empty).ToLowerInvariant();
            }
        }
    }
}
