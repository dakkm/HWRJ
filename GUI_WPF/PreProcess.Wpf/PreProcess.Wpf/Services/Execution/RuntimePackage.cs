using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        {
            if (BackendPathResolver.IsWithin(paths.RuntimeRoot, paths.PackageRoot)) throw new ArgumentException("运行区不能位于冻结后端内。");
            string source = Path.GetFullPath(paths.PackageRoot).TrimEnd('\\');
            var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                token.ThrowIfCancellationRequested();
                hashes.Add(file.Substring(source.Length + 1), Hash(file));
            }
            string manifest = new JavaScriptSerializer().Serialize(hashes);
            string key;
            using (var sha = SHA256.Create()) key = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(manifest))).Replace("-", "");
            string root = Path.Combine(paths.RuntimeRoot, "backend", key, "package");
            foreach (var item in hashes)
            {
                token.ThrowIfCancellationRequested();
                string target = Path.Combine(root, item.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                if (!File.Exists(target))
                {
                    using (var input = new FileStream(Path.Combine(source, item.Key), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write)) input.CopyTo(output);
                }
                // These three existing pointer files are documented mutable runtime outputs.
                bool pointer = item.Key == @"02-智能预测\04-输出文件\latest_run.json" ||
                    item.Key == @"03-相似度评估\03-输出文件\latest_run.json" || item.Key == @"04-红外场景构建\03-输出文件\latest_run.json";
                if (!pointer && Hash(target) != item.Value) throw new IOException("运行副本校验失败，请使用新的运行目录：" + item.Key);
            }
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(root), "source-sha256.json"), manifest, new UTF8Encoding(false));
            // Reject absolute resource paths, which could redirect copied programs back into frozen storage.
            var cfg = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(root, "config.json")));
            foreach (var item in cfg.Where(x => x.Value is string))
                if (Path.IsPathRooted((string)item.Value)) throw new ArgumentException("后端配置含绝对资源路径，无法保证副本隔离：" + item.Key);
            return root;
        }
        public static string Hash(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }
    }
}
