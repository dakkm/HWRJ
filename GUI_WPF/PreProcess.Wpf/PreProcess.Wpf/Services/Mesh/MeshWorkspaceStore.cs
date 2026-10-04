using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace PreProcess.Wpf.Services.Mesh
{
    /// <summary>独立网格包读写器；文件不属于任务文件，也不会被求解器读取。</summary>
    public sealed class MeshWorkspaceStore
    {
        private sealed class Package
        {
            public string Format { get; set; }
            public SphericalShellMesh Mesh { get; set; }
        }

        public void Save(string path, SphericalShellMesh mesh)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("网格文件路径不能为空。", "path");
            if (mesh == null) throw new ArgumentNullException("mesh");
            string json = new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 }.Serialize(new Package { Format = "independent-spherical-shell-mesh-v1", Mesh = mesh });
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }

        public SphericalShellMesh Load(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("找不到独立网格文件。", path);
            var package = new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 }.Deserialize<Package>(File.ReadAllText(path, Encoding.UTF8));
            if (package == null || package.Mesh == null || package.Format != "independent-spherical-shell-mesh-v1") throw new InvalidDataException("不是受支持的独立球壳网格包。");
            if (package.Mesh.Nodes == null || package.Mesh.Elements == null || package.Mesh.SurfaceFaces == null) throw new InvalidDataException("网格包缺少节点、单元或表面数据。");
            if (package.Mesh.OuterRadius <= 0 || package.Mesh.InnerRadius <= 0 || package.Mesh.InnerRadius >= package.Mesh.OuterRadius) throw new InvalidDataException("网格包几何范围无效。");
            // 不信任包内序列化的质量结论：文件可能被编辑或来自旧版生成器。
            package.Mesh.Quality = new MeshValidator().Validate(package.Mesh);
            return package.Mesh;
        }
    }
}
