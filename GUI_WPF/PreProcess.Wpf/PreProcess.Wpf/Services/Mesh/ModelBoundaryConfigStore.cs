using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using PreProcess.Wpf.Models;

namespace PreProcess.Wpf.Services.Mesh
{
    /// <summary>02 模块独立配置包。只记录模型、网格摘要、物性和边界，不改变现有任务文件。</summary>
    public sealed class ModelBoundaryConfigPackage
    {
        public string Format { get; set; }
        public string CreatedAtUtc { get; set; }
        public ModelSection Model { get; set; }
        public MeshSection Mesh { get; set; }
        public MaterialSection Material { get; set; }
        public BoundarySection Boundary { get; set; }
    }
    public sealed class ModelSection { public string TargetType { get; set; } public double OuterRadiusMeters { get; set; } public double ShellThicknessMillimeters { get; set; } }
    public sealed class MeshSection { public string Status { get; set; } public int NodeCount { get; set; } public int TetrahedronCount { get; set; } public int SurfaceFaceCount { get; set; } public string WorkspaceFile { get; set; } }
    public sealed class MaterialSection { public double Density { get; set; } public double HeatCapacity { get; set; } public double InitialTemperature { get; set; } public double InternalPower { get; set; } public double Emissivity { get; set; } public double SolarAbsorption { get; set; } public double IrReflection { get; set; } }
    public sealed class BoundarySection { public string SurfaceBoundaryType { get; set; } public double SolarFlux { get; set; } public double RadiationTemperature { get; set; } public double ApertureSize { get; set; } public double FocalLength { get; set; } }

    public sealed class ModelBoundaryConfigStore
    {
        private static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        public void Save(string path, TaskModel task, SphericalShellMesh mesh, string workspaceFile, string surfaceBoundaryType = null)
        {
            if (task == null) throw new ArgumentNullException("task");
            var p = task.Targets.Uniform; var e = task.Environment;
            var package = new ModelBoundaryConfigPackage
            {
                Format = "model-boundary-config-v1", CreatedAtUtc = DateTime.UtcNow.ToString("o"),
                Model = new ModelSection { TargetType = p.Geometry.TargetType.ToString(), OuterRadiusMeters = p.Geometry.Radius, ShellThicknessMillimeters = p.Geometry.ShellThickness },
                Mesh = mesh == null ? new MeshSection { Status = "NotGenerated", WorkspaceFile = workspaceFile } : new MeshSection { Status = mesh.Quality.IsValid ? "PreviewReady" : "PreviewInvalid", NodeCount = mesh.Quality.NodeCount, TetrahedronCount = mesh.Quality.TetrahedronCount, SurfaceFaceCount = mesh.Quality.SurfaceFaceCount, WorkspaceFile = workspaceFile },
                Material = new MaterialSection { Density = p.Density, HeatCapacity = p.HeatCapacity, InitialTemperature = p.InitialTemperature, InternalPower = p.InternalPower, Emissivity = p.Emissivity, SolarAbsorption = p.SolarAbsorption, IrReflection = p.IrReflection },
                Boundary = new BoundarySection { SurfaceBoundaryType = surfaceBoundaryType ?? "太阳吸收 + 红外辐射（当前后端）", SolarFlux = e.SolarFlux, RadiationTemperature = e.RadiationTemperature, ApertureSize = e.ApertureSize, FocalLength = e.FocalLength }
            };
            File.WriteAllText(path, new JavaScriptSerializer().Serialize(package), new UTF8Encoding(false));
        }
        public ModelBoundaryConfigPackage LoadAndValidate(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("找不到 02 模块配置文件。", path);
            var p = new JavaScriptSerializer().Deserialize<ModelBoundaryConfigPackage>(File.ReadAllText(path, Encoding.UTF8));
            if (p == null || p.Format != "model-boundary-config-v1" || p.Model == null || p.Material == null || p.Boundary == null) throw new InvalidDataException("02 模块配置格式无效或版本不受支持。");
            if (p.Model.TargetType != "SphericalShell" || !Finite(p.Model.OuterRadiusMeters) || p.Model.OuterRadiusMeters <= 0.005 || p.Model.ShellThicknessMillimeters != 5) throw new InvalidDataException("当前任务只支持固定 5 mm 壳厚的球壳模型。");
            var m = p.Material;
            if (!Finite(m.Density) || m.Density <= 0 || !Finite(m.HeatCapacity) || m.HeatCapacity <= 0 || !Finite(m.InitialTemperature) || m.InitialTemperature <= 0 || !Finite(m.InternalPower) || !Finite(m.Emissivity) || m.Emissivity < 0 || m.Emissivity > 1 || !Finite(m.SolarAbsorption) || m.SolarAbsorption < 0 || m.SolarAbsorption > 1 || !Finite(m.IrReflection) || m.IrReflection < 0 || m.IrReflection > 1) throw new InvalidDataException("物性参数超出当前任务可应用范围。");
            var b = p.Boundary;
            if (!Finite(b.SolarFlux) || b.SolarFlux < 0 || !Finite(b.RadiationTemperature) || b.RadiationTemperature < 0 || !Finite(b.ApertureSize) || b.ApertureSize <= 0 || !Finite(b.FocalLength) || b.FocalLength <= 0) throw new InvalidDataException("边界参数无效。");
            if (p.Boundary.SurfaceBoundaryType != "太阳吸收 + 红外辐射（当前后端）" && p.Boundary.SurfaceBoundaryType != "绝热（仅前端配置）" && p.Boundary.SurfaceBoundaryType != "固定温度（仅前端配置）") throw new InvalidDataException("表面边界类型不受当前版本支持。");
            return p;
        }

        public void ApplyToTask(ModelBoundaryConfigPackage package, TaskModel task)
        {
            if (task == null) throw new ArgumentNullException("task");
            if (package == null || package.Format != "model-boundary-config-v1" || package.Model == null || package.Material == null || package.Boundary == null) throw new InvalidDataException("配置包不完整。");
            // 与读取时采用相同约束；下列赋值不会引发中途校验失败。
            if (package.Model.TargetType != "SphericalShell" || !Finite(package.Model.OuterRadiusMeters) || package.Model.OuterRadiusMeters <= 0.005 || package.Model.ShellThicknessMillimeters != 5) throw new InvalidDataException("模型参数不受当前任务支持。");
            var m = package.Material; var b = package.Boundary;
            if (!Finite(m.Density) || m.Density <= 0 || !Finite(m.HeatCapacity) || m.HeatCapacity <= 0 || !Finite(m.InitialTemperature) || m.InitialTemperature <= 0 || !Finite(m.InternalPower) || !Finite(m.Emissivity) || m.Emissivity < 0 || m.Emissivity > 1 || !Finite(m.SolarAbsorption) || m.SolarAbsorption < 0 || m.SolarAbsorption > 1 || !Finite(m.IrReflection) || m.IrReflection < 0 || m.IrReflection > 1 || !Finite(b.SolarFlux) || b.SolarFlux < 0 || !Finite(b.RadiationTemperature) || b.RadiationTemperature < 0 || !Finite(b.ApertureSize) || b.ApertureSize <= 0 || !Finite(b.FocalLength) || b.FocalLength <= 0) throw new InvalidDataException("配置数值无效，未应用到任务。");
            if (b.SurfaceBoundaryType != "太阳吸收 + 红外辐射（当前后端）" && b.SurfaceBoundaryType != "绝热（仅前端配置）" && b.SurfaceBoundaryType != "固定温度（仅前端配置）") throw new InvalidDataException("表面边界类型不受当前版本支持，未应用到任务。");
            var target = task.Targets.Uniform;
            target.Geometry.Radius = package.Model.OuterRadiusMeters;
            target.Density = m.Density; target.HeatCapacity = m.HeatCapacity; target.InitialTemperature = m.InitialTemperature; target.InternalPower = m.InternalPower;
            target.Emissivity = m.Emissivity; target.SolarAbsorption = m.SolarAbsorption; target.IrReflection = m.IrReflection;
            var environment = task.Environment;
            environment.SolarFlux = b.SolarFlux; environment.RadiationTemperature = b.RadiationTemperature;
            environment.ApertureSize = b.ApertureSize; environment.FocalLength = b.FocalLength;
        }
    }
}
