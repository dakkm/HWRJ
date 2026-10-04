using System;
using System.Collections.Generic;

namespace PreProcess.Wpf.Services.Mesh
{
    public sealed class MeshRegressionReport
    {
        public bool Passed { get; set; }
        public string Summary { get; set; }
        public IList<string> Cases { get; set; }
    }

    public sealed class MeshRegressionRunner
    {
        public MeshRegressionReport Run()
        {
            var cases = new List<string>();
            bool passed = true;
            var inputs = new[] { new { Radius = 0.2, Layers = 1 }, new { Radius = 0.5, Layers = 2 }, new { Radius = 1.0, Layers = 4 } };
            foreach (var input in inputs)
            {
                try
                {
                    var mesh = new SphericalShellMeshGenerator().Generate(input.Radius, 5, new MeshGenerationSettings { RadialLayers = input.Layers, SurfaceSubdivisions = 1 });
                    int inner = 0, outer = 0;
                    foreach (var face in mesh.SurfaceFaces)
                    {
                        if (face.Region == "Inner") inner++;
                        if (face.Region == "Outer") outer++;
                    }
                    // 当前范围是前端预览网格：检查几何、表面标记和退化单元，
                    // 不把求解级共形边界面要求误当作预览回归条件。
                    bool ok = mesh.Quality.IsValid && mesh.Quality.ZeroVolumeElementCount == 0 && inner > 0 && outer > 0;
                    passed = passed && ok;
                    cases.Add(string.Format("R={0:G3}m/L={1}：{2}（节点{3}，四面体{4}）", input.Radius, input.Layers, ok ? "通过" : "失败", mesh.Quality.NodeCount, mesh.Quality.TetrahedronCount));
                }
                catch (Exception ex) { passed = false; cases.Add(string.Format("R={0:G3}m/L={1}：失败（{2}）", input.Radius, input.Layers, ex.Message)); }
            }
            return new MeshRegressionReport { Passed = passed, Summary = passed ? "三组预览网格回归通过。" : "预览网格回归存在失败项，请查看明细。", Cases = cases };
        }
    }
}
