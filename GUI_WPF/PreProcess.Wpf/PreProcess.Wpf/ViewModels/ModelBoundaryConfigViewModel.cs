using System;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using PreProcess.Wpf.Models;
using PreProcess.Wpf.Services.Mesh;

namespace PreProcess.Wpf.ViewModels
{
    /// <summary>
    /// 02 模型、光热物性和边界信息的独立显示模型。
    /// 当前只映射已有任务字段，不新增后端契约。
    /// </summary>
    public sealed class ModelBoundaryConfigViewModel : BindableModel
    {
        public TaskEditorViewModel Editor { get; private set; }
        private SphericalShellMesh mesh;
        private int radialLayers = 1;
        private string meshStatus = "尚无网格";
        private string meshSummary = "";
        private string meshIssues = "";
        private string meshGeometrySummary = "尚未绑定几何参数。";
        public ICommand GenerateMeshCommand { get; private set; }
        public ICommand SaveMeshCommand { get; private set; }
        public ICommand OpenMeshCommand { get; private set; }
        public ICommand RunMeshRegressionCommand { get; private set; }
        public ICommand ExportConfigCommand { get; private set; }
        public ICommand ImportConfigCommand { get; private set; }
        public ICommand ValidateConfigCommand { get; private set; }
        public ICommand SelectModelCommand { get; private set; }
        public ICommand ApplyImportedConfigCommand { get; private set; }
        private string meshGeometryKey;
        private readonly DispatcherTimer meshStateTimer;
        private string meshSurfacePreview = "生成网格后显示内外表面摘要。";
        private string meshRegressionSummary = "";
        private string configStatus = "";
        private string modelFileStatus = "尚未选择独立模型文件。";
        private string surfaceBoundaryType = "太阳吸收 + 红外辐射（当前后端）";
        private ModelBoundaryConfigPackage pendingConfig;
        private TaskModel pendingTask;
        private string importedConfigPreview = "";
        public string ModelStatus { get { return "前端功能：模型文件检查与球壳网格预览，不参与后端求解"; } }
        public string MaterialStatus { get { return "已有：复用目标物性参数"; } }
        public string BoundaryStatus { get { return "太阳与环境参数参与任务；扩展边界仅作前端配置"; } }
        public string MaterialSummary
        {
            get
            {
                var p = Editor.Task.Targets.Uniform;
                return string.Format("密度 {0:g6} kg/m³；热容 {1:g6} J/(kg·K)；发射率 {2:g4}；太阳吸收率 {3:g4}",
                    p.Density, p.HeatCapacity, p.Emissivity, p.SolarAbsorption);
            }
        }

        public int RadialLayers { get { return radialLayers; } set { Set(ref radialLayers, value); } }
        public string MeshStatus { get { return meshStatus; } }
        public string MeshSummary { get { return meshSummary; } }
        public string MeshIssues { get { return meshIssues; } }
        public string MeshGeometrySummary { get { return meshGeometrySummary; } }
        public string MeshSurfacePreview { get { return meshSurfacePreview; } }
        public string MeshRegressionSummary { get { return meshRegressionSummary; } }
        public SphericalShellMesh PreviewMesh { get { return mesh; } }
        public string ConfigStatus { get { return configStatus; } }
        public string ModelFileStatus { get { return modelFileStatus; } }
        public bool HasPendingConfig { get { return pendingConfig != null; } }
        public string ImportedConfigPreview { get { return importedConfigPreview; } }
        public string SurfaceBoundaryType { get { return surfaceBoundaryType; } set { Set(ref surfaceBoundaryType, value); Notify(nameof(BoundaryEditStatus)); } }
        public string[] BoundaryTypes { get { return new[] { "太阳吸收 + 红外辐射（当前后端）", "绝热（仅前端配置）", "固定温度（仅前端配置）" }; } }
        public string MaterialEditStatus { get { return "物性编辑直接写入当前目标参数，仍沿用现有任务字段。"; } }
        public string BoundaryEditStatus { get { return SurfaceBoundaryType == "太阳吸收 + 红外辐射（当前后端）" ? "此项描述后端已有物理过程；边界类型选择本身不提交后端。" : "该边界类型仅保存为前端配置，当前后端不会执行。"; } }

        public ModelBoundaryConfigViewModel(TaskEditorViewModel editor)
        {
            Editor = editor;
            GenerateMeshCommand = new RelayCommand(_ => GenerateMesh());
            SaveMeshCommand = new RelayCommand(_ => SaveMesh());
            OpenMeshCommand = new RelayCommand(_ => OpenMesh());
            RunMeshRegressionCommand = new RelayCommand(_ => RunMeshRegression());
            ExportConfigCommand = new RelayCommand(_ => ExportConfig());
            ImportConfigCommand = new RelayCommand(_ => ImportConfig());
            ValidateConfigCommand = new RelayCommand(_ => ValidateConfig());
            SelectModelCommand = new RelayCommand(_ => SelectModelFile());
            ApplyImportedConfigCommand = new RelayCommand(_ => ApplyImportedConfig());
            meshStateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            meshStateTimer.Tick += (s, e) => RefreshMeshStaleness();
            meshStateTimer.Start();
        }

        private void GenerateMesh()
        {
            try
            {
                var geometry = Editor.Task.Targets.Uniform.Geometry;
                meshGeometryKey = GeometryKey(geometry.Radius, geometry.ShellThickness);
                meshGeometrySummary = string.Format("外半径 {0:G6} m；壳厚 {1:G4} mm（对应内半径 {2:G6} m）",
                    geometry.Radius, geometry.ShellThickness, geometry.Radius - geometry.ShellThickness / 1000.0);
                mesh = new SphericalShellMeshGenerator().Generate(geometry.Radius, geometry.ShellThickness, new MeshGenerationSettings
                {
                    RadialLayers = RadialLayers,
                    SurfaceSubdivisions = 1
                });
                meshStatus = mesh.Quality.IsValid ? "网格生成完成" : "网格已生成，但质量检查未通过";
                meshSummary = string.Format("节点 {0}；四面体 {1}；表面三角形 {2}；最小体积 {3:G4} m³；{4}",
                    mesh.Quality.NodeCount, mesh.Quality.TetrahedronCount, mesh.Quality.SurfaceFaceCount,
                    mesh.Quality.MinimumVolume, mesh.Quality.IsValid ? "质量检查通过" : "请检查参数");
                meshIssues = mesh.Quality.Issues.Count == 0 ? "质量检查明细：未发现问题。" : "质量检查明细：" + string.Join("；", mesh.Quality.Issues);
                meshSurfacePreview = SurfacePreview(mesh);
            }
            catch (Exception ex)
            {
                mesh = null;
                meshStatus = "网格生成失败：" + ex.Message;
                meshSummary = "未修改当前任务参数，也未向后端提交网格。";
                meshIssues = "质量检查未执行。";
            }
            Notify(nameof(MeshStatus));
            Notify(nameof(MeshSummary));
            Notify(nameof(MeshIssues));
            Notify(nameof(MeshGeometrySummary));
            Notify(nameof(MeshSurfacePreview));
            Notify(nameof(PreviewMesh));
        }

        private void SaveMesh()
        {
            if (mesh == null) { meshStatus = "请先生成网格，再保存独立网格包。"; Notify(nameof(MeshStatus)); return; }
            var dialog = new SaveFileDialog { Filter = "独立球壳网格 (*.mesh.json)|*.mesh.json|JSON 文件 (*.json)|*.json", DefaultExt = ".mesh.json", AddExtension = true };
            if (dialog.ShowDialog() != true) return;
            try { new MeshWorkspaceStore().Save(dialog.FileName, mesh); meshStatus = "网格已保存。"; }
            catch (Exception ex) { meshStatus = "保存失败：" + ex.Message; }
            Notify(nameof(MeshStatus));
        }

        private void OpenMesh()
        {
            var dialog = new OpenFileDialog { Filter = "独立球壳网格 (*.mesh.json)|*.mesh.json|JSON 文件 (*.json)|*.json", CheckFileExists = true };
            if (dialog.ShowDialog() != true) return;
            try
            {
                mesh = new MeshWorkspaceStore().Load(dialog.FileName);
                var geometry = Editor.Task.Targets.Uniform.Geometry;
                meshGeometryKey = mesh.OuterRadius.ToString("R") + "|" + (mesh.ShellThicknessMeters * 1000.0).ToString("R");
                bool current = meshGeometryKey == GeometryKey(geometry.Radius, geometry.ShellThickness);
                meshStatus = current ? "独立网格包已打开，几何参数匹配。" : "独立网格包已打开，但几何参数已变化，网格已过期。";
                meshSummary = string.Format("节点 {0}；四面体 {1}；表面三角形 {2}；{3}", mesh.Quality.NodeCount, mesh.Quality.TetrahedronCount, mesh.Quality.SurfaceFaceCount, current ? "可继续预览" : "请重新生成");
                meshIssues = mesh.Quality.Issues.Count == 0 ? "质量检查明细：未发现问题。" : "质量检查明细：" + string.Join("；", mesh.Quality.Issues);
                meshSurfacePreview = SurfacePreview(mesh);
            }
            catch (Exception ex) { meshStatus = "打开失败：" + ex.Message; }
            Notify(nameof(MeshStatus)); Notify(nameof(MeshSummary)); Notify(nameof(MeshIssues)); Notify(nameof(MeshSurfacePreview)); Notify(nameof(PreviewMesh));
        }

        private void RunMeshRegression()
        {
            var report = new MeshRegressionRunner().Run();
            meshRegressionSummary = report.Summary + " " + string.Join("；", report.Cases);
            Notify(nameof(MeshRegressionSummary));
        }

        private static string SurfacePreview(SphericalShellMesh value)
        {
            if (value.SurfaceFaces.Count == 0) return "表面预览：没有可显示的表面三角形。";
            var face = value.SurfaceFaces[0];
            return string.Format("表面预览：内表面 {0} 个三角形、外表面 {1} 个三角形；首个 {2} 面节点 [{3},{4},{5}]。",
                CountRegion(value, "Inner"), CountRegion(value, "Outer"), face.Region, face.A, face.B, face.C);
        }

        private static int CountRegion(SphericalShellMesh value, string region)
        {
            int count = 0; foreach (var face in value.SurfaceFaces) if (face.Region == region) count++; return count;
        }

        private static string GeometryKey(double radius, double thicknessMm) { return radius.ToString("R") + "|" + thicknessMm.ToString("R"); }

        private void ExportConfig()
        {
            var dialog = new SaveFileDialog { Filter = "02模块配置 (*.model-boundary.json)|*.model-boundary.json|JSON 文件 (*.json)|*.json", DefaultExt = ".model-boundary.json", AddExtension = true };
            if (dialog.ShowDialog() != true) return;
            try { new ModelBoundaryConfigStore().Save(dialog.FileName, Editor.Task, mesh, null, SurfaceBoundaryType); configStatus = "02 模型、网格摘要、物性和边界配置已导出。"; }
            catch (Exception ex) { configStatus = "配置导出失败：" + ex.Message; }
            Notify(nameof(ConfigStatus));
        }

        private void ImportConfig()
        {
            var dialog = new OpenFileDialog { Filter = "02模块配置 (*.model-boundary.json)|*.model-boundary.json|JSON 文件 (*.json)|*.json", CheckFileExists = true };
            if (dialog.ShowDialog() != true) return;
            try
            {
                var p = new ModelBoundaryConfigStore().LoadAndValidate(dialog.FileName);
                pendingConfig = p;
                pendingTask = Editor.Task;
                importedConfigPreview = string.Format("待应用差异：半径 {0:G6}→{1:G6} m；密度 {2:G6}→{3:G6} kg/m³；热容 {4:G6}→{5:G6} J/(kg·K)；太阳通量 {6:G6}→{7:G6} W/m²。边界类型“{8}”仅保存在 02 前端配置。网格摘要不会自动打开或写入任务。",
                    Editor.Task.Targets.Uniform.Geometry.Radius, p.Model.OuterRadiusMeters,
                    Editor.Task.Targets.Uniform.Density, p.Material.Density,
                    Editor.Task.Targets.Uniform.HeatCapacity, p.Material.HeatCapacity,
                    Editor.Task.Environment.SolarFlux, p.Boundary.SolarFlux, p.Boundary.SurfaceBoundaryType);
                configStatus = "配置校验通过，尚未应用到当前任务。请检查差异后点击“应用导入配置”。";
            }
            catch (Exception ex) { pendingConfig = null; pendingTask = null; importedConfigPreview = "没有可应用的配置。"; configStatus = "配置导入失败：" + ex.Message; }
            Notify(nameof(ConfigStatus)); Notify(nameof(HasPendingConfig)); Notify(nameof(ImportedConfigPreview));
        }

        private void ApplyImportedConfig()
        {
            if (pendingConfig == null) { configStatus = "请先导入并检查配置。"; Notify(nameof(ConfigStatus)); return; }
            if (!ReferenceEquals(pendingTask, Editor.Task)) { pendingConfig = null; pendingTask = null; configStatus = "当前任务已切换，请重新导入配置并核对差异。"; Notify(nameof(ConfigStatus)); Notify(nameof(HasPendingConfig)); return; }
            if (Editor.Execution != null && !Editor.Execution.CanEdit) { configStatus = "运行期间不能应用配置。"; Notify(nameof(ConfigStatus)); return; }
            try
            {
                new ModelBoundaryConfigStore().ApplyToTask(pendingConfig, Editor.Task);
                SurfaceBoundaryType = pendingConfig.Boundary.SurfaceBoundaryType;
                pendingConfig = null;
                pendingTask = null;
                importedConfigPreview = "已应用已有任务字段；独立边界类型仅保存在 02 页面。请保存任务以持久化任务字段。";
                configStatus = "配置已应用到当前任务；预览网格若与新半径不符会标记过期。";
                Notify(nameof(MaterialSummary)); Notify(nameof(HasPendingConfig)); Notify(nameof(ImportedConfigPreview));
            }
            catch (Exception ex) { configStatus = "应用失败，任务未保存：" + ex.Message; }
            Notify(nameof(ConfigStatus));
        }

        private void ValidateConfig()
        {
            string temp = null;
            try
            {
                temp = Path.GetTempFileName();
                new ModelBoundaryConfigStore().Save(temp, Editor.Task, mesh, null, SurfaceBoundaryType);
                new ModelBoundaryConfigStore().LoadAndValidate(temp);
                configStatus = "当前 02 配置已通过格式和字段检查。";
            }
            catch (Exception ex) { configStatus = "配置检查失败：" + ex.Message; }
            finally { if (temp != null && File.Exists(temp)) File.Delete(temp); }
            Notify(nameof(ConfigStatus));
        }

        private void SelectModelFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "模型/网格文件 (*.model-boundary.json;*.mesh.json;*.stl;*.obj)|*.model-boundary.json;*.mesh.json;*.stl;*.obj|配置文件 (*.model-boundary.json)|*.model-boundary.json|网格包 (*.mesh.json)|*.mesh.json|STL/OBJ (*.stl;*.obj)|*.stl;*.obj|所有文件 (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };
            if (dialog.ShowDialog() != true) return;
            string path = dialog.FileName;
            try
            {
                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (path.EndsWith(".model-boundary.json", StringComparison.OrdinalIgnoreCase))
                {
                    new ModelBoundaryConfigStore().LoadAndValidate(path);
                    modelFileStatus = "模型配置文件格式检查通过：" + path;
                }
                else if (path.EndsWith(".mesh.json", StringComparison.OrdinalIgnoreCase))
                {
                    new MeshWorkspaceStore().Load(path);
                    modelFileStatus = "独立网格包格式检查通过：" + path;
                }
                else if (extension == ".stl" || extension == ".obj")
                {
                    modelFileStatus = new GeometryFileInspector().Inspect(path) + " 文件：" + path;
                }
                else throw new InvalidDataException("当前不支持该模型文件扩展名。");
            }
            catch (Exception ex) { modelFileStatus = "模型文件检查失败：" + ex.Message; }
            Notify(nameof(ModelFileStatus));
        }

        private void RefreshMeshStaleness()
        {
            if (mesh == null || string.IsNullOrEmpty(meshGeometryKey)) return;
            var geometry = Editor.Task.Targets.Uniform.Geometry;
            bool current = meshGeometryKey == GeometryKey(geometry.Radius, geometry.ShellThickness);
            string expected = current ? "网格状态：与当前几何参数匹配。" : "网格状态：已过期（几何参数发生变化，请重新生成）。";
            if (meshStatus == expected) return;
            meshStatus = expected;
            Notify(nameof(MeshStatus));
        }
    }
}
