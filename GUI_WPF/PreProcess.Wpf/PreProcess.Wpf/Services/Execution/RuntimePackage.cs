using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
// 说明当前文件承载的类型职责，便于维护者快速定位功能边界。
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace PreProcess.Wpf.Services.Execution
{
    public static class RuntimePackage
    {
        // Some official entries always write package-local latest_run/cache files despite CLI output overrides.
        // Execute an exact, verified deployment copy instead of rewriting scripts or touching the frozen source.
        public static string Prepare(BackendPaths paths, CancellationToken token)
        // 本文件只处理所属模块的数据或界面逻辑，不在此处引入额外副作用。
        {
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (BackendPathResolver.IsWithin(paths.RuntimeRoot, paths.PackageRoot)) throw new ArgumentException("运行区不能位于冻结后端内。");
            string source = Path.GetFullPath(paths.PackageRoot).TrimEnd('\\');
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                // 调用对应组件完成当前步骤，并保留产生的处理结果。
                token.ThrowIfCancellationRequested();
                hashes.Add(file.Substring(source.Length + 1), Hash(file));
            }
            string manifest = new JavaScriptSerializer().Serialize(hashes);
            // 继续处理当前业务步骤，保持上下文状态一致。
            string key;
            using (var sha = SHA256.Create()) key = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(manifest))).Replace("-", "");
            string root = Path.Combine(paths.RuntimeRoot, "backend", key, "package");
            // 遍历当前数据集合，逐项完成必要的转换或状态更新。
            foreach (var item in hashes)
            // 调用方应通过公开成员访问功能，内部状态由当前类型统一维护。
            {
                token.ThrowIfCancellationRequested();
                // 处理文件系统路径及数据，并在使用前确认目标有效。
                string target = Path.Combine(root, item.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                if (!File.Exists(target))
                {
                    using (var input = new FileStream(Path.Combine(source, item.Key), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write)) input.CopyTo(output);
                }
                // These files are documented mutable runtime outputs. Module 04 appends
                // reference-run metadata to its cache manifest after a successful run.
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                bool mutableOutput = item.Key == @"02-智能预测\04-输出文件\latest_run.json" ||
                    // 相关输入在进入核心流程前完成校验，避免无效状态向下游传播。
                    item.Key == @"03-相似度评估\03-输出文件\latest_run.json" ||
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    item.Key == @"04-红外场景构建\03-输出文件\latest_run.json" ||
                    item.Key == @"04-红外场景构建\03-输出文件\reference_runs\reference_cache_manifest.json";
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (!mutableOutput && Hash(target) != item.Value) throw new IOException("运行副本校验失败，请使用新的运行目录：" + item.Key);
            }
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(root), "source-sha256.json"), manifest, new UTF8Encoding(false));
            // Reject absolute resource paths, which could redirect copied programs back into frozen storage.
            var cfg = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(root, "config.json")));
            // 遍历当前数据集合，逐项完成必要的转换或状态更新。
            foreach (var item in cfg.Where(x => x.Value is string))
                if (Path.IsPathRooted((string)item.Value)) throw new ArgumentException("后端配置含绝对资源路径，无法保证副本隔离：" + item.Key);
            // 返回当前步骤生成的结果，并结束本次调用。
            return root;
        }
        public static string Hash(string path)
        {
            // 保持该实现与界面绑定及服务层约定一致，修改时需同步检查调用方。
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }
    }
}
