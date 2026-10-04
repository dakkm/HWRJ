using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PreProcess.Wpf.Services.Mesh
{
    public sealed class MeshGenerationSettings
    {
        public int RadialLayers { get; set; }
        public int SurfaceSubdivisions { get; set; }
        public MeshGenerationSettings() { RadialLayers = 1; SurfaceSubdivisions = 1; }
    }

    public sealed class MeshNode
    {
        public int Id { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    public sealed class TetraElement
    {
        public int Id { get; set; }
        public int A { get; set; }
        public int B { get; set; }
        public int C { get; set; }
        public int D { get; set; }
        public int[] Nodes { get { return new[] { A, B, C, D }; } }
    }

    public sealed class MeshSurfaceFace
    {
        public int A { get; set; }
        public int B { get; set; }
        public int C { get; set; }
        public string Region { get; set; }
        public int OwnerElementId { get; set; }
    }

    public sealed class MeshQualitySummary
    {
        public int NodeCount { get; set; }
        public int TetrahedronCount { get; set; }
        public int SurfaceFaceCount { get; set; }
        public int BoundaryFaceCount { get; set; }
        public int InteriorFaceCount { get; set; }
        public int NonManifoldFaceCount { get; set; }
        public int ZeroVolumeElementCount { get; set; }
        public double MinimumVolume { get; set; }
        public bool IsValid { get; set; }
        public IList<string> Issues { get; set; }
    }

    public sealed class SphericalShellMesh
    {
        public double OuterRadius { get; set; }
        public double InnerRadius { get; set; }
        public double ShellThicknessMeters { get; set; }
        public MeshGenerationSettings Settings { get; set; }
        public IList<MeshNode> Nodes { get; set; }
        public IList<TetraElement> Elements { get; set; }
        public IList<MeshSurfaceFace> SurfaceFaces { get; set; }
        public MeshQualitySummary Quality { get; set; }

        public SphericalShellMesh()
        {
            Nodes = new List<MeshNode>();
            Elements = new List<TetraElement>();
            SurfaceFaces = new List<MeshSurfaceFace>();
        }
    }

    /// <summary>
    /// 当前球壳模型的独立网格生成器。采用八面体近似球面，生成同心壳层四面体；
    /// 结果仅供 GUI 预处理、检查和导出，不参与现有求解请求。
    /// </summary>
    public sealed class SphericalShellMeshGenerator
    {
        private static readonly int[,] Faces =
        {
            { 0, 2, 4 }, { 0, 4, 3 }, { 0, 3, 5 }, { 0, 5, 2 },
            { 1, 4, 2 }, { 1, 3, 4 }, { 1, 5, 3 }, { 1, 2, 5 }
        };

        public SphericalShellMesh Generate(double outerRadius, double shellThicknessMillimeters, MeshGenerationSettings settings)
        {
            if (outerRadius <= 0) throw new ArgumentOutOfRangeException("outerRadius");
            if (shellThicknessMillimeters <= 0) throw new ArgumentOutOfRangeException("shellThicknessMillimeters");
            if (settings == null) throw new ArgumentNullException("settings");
            if (settings.RadialLayers < 1 || settings.RadialLayers > 64) throw new ArgumentOutOfRangeException("settings.RadialLayers");
            if (settings.SurfaceSubdivisions != 1) throw new NotSupportedException("首版球壳网格仅支持 SurfaceSubdivisions=1。");

            double thickness = shellThicknessMillimeters / 1000.0;
            double innerRadius = outerRadius - thickness;
            if (innerRadius <= 0) throw new ArgumentException("壳厚必须小于外半径。", "shellThicknessMillimeters");

            var mesh = new SphericalShellMesh
            {
                OuterRadius = outerRadius,
                InnerRadius = innerRadius,
                ShellThicknessMeters = thickness,
                Settings = settings
            };

            int[][] layers = new int[settings.RadialLayers + 1][];
            for (int layer = 0; layer <= settings.RadialLayers; layer++)
            {
                double radius = innerRadius + thickness * layer / settings.RadialLayers;
                layers[layer] = AddOctahedronLayer(mesh.Nodes, radius);
            }

            int nextElement = 1;
            for (int layer = 0; layer < settings.RadialLayers; layer++)
            {
                for (int face = 0; face < Faces.GetLength(0); face++)
                {
                    int a = layers[layer][Faces[face, 0]];
                    int b = layers[layer][Faces[face, 1]];
                    int c = layers[layer][Faces[face, 2]];
                    int A = layers[layer + 1][Faces[face, 0]];
                    int B = layers[layer + 1][Faces[face, 1]];
                    int C = layers[layer + 1][Faces[face, 2]];
                    mesh.Elements.Add(new TetraElement { Id = nextElement++, A = A, B = B, C = C, D = a });
                    mesh.Elements.Add(new TetraElement { Id = nextElement++, A = a, B = B, C = C, D = b });
                    mesh.Elements.Add(new TetraElement { Id = nextElement++, A = a, B = b, C = C, D = c });
                    if (layer == 0) mesh.SurfaceFaces.Add(new MeshSurfaceFace { A = a, B = c, C = b, Region = "Inner" });
                    if (layer == settings.RadialLayers - 1) mesh.SurfaceFaces.Add(new MeshSurfaceFace { A = A, B = B, C = C, Region = "Outer" });
                }
            }
            mesh.Quality = new MeshValidator().Validate(mesh);
            return mesh;
        }

        private static int[] AddOctahedronLayer(IList<MeshNode> nodes, double radius)
        {
            var ids = new int[6];
            double[][] points =
            {
                new[] { radius, 0.0, 0.0 }, new[] { -radius, 0.0, 0.0 },
                new[] { 0.0, radius, 0.0 }, new[] { 0.0, -radius, 0.0 },
                new[] { 0.0, 0.0, radius }, new[] { 0.0, 0.0, -radius }
            };
            for (int i = 0; i < points.Length; i++)
            {
                ids[i] = nodes.Count + 1;
                nodes.Add(new MeshNode { Id = ids[i], X = points[i][0], Y = points[i][1], Z = points[i][2] });
            }
            return ids;
        }
    }

    public sealed class MeshValidator
    {
        public MeshQualitySummary Validate(SphericalShellMesh mesh)
        {
            if (mesh == null) throw new ArgumentNullException("mesh");
            var issues = new List<string>();
            var nodes = mesh.Nodes.ToDictionary(n => n.Id);
            var faceCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            double minimum = Double.MaxValue;
            int zero = 0;
            foreach (TetraElement element in mesh.Elements)
            {
                if (element.Nodes.Any(id => !nodes.ContainsKey(id))) { issues.Add("存在引用不存在节点的四面体。"); continue; }
                double volume = Volume(nodes[element.A], nodes[element.B], nodes[element.C], nodes[element.D]);
                minimum = Math.Min(minimum, volume);
                if (volume <= 1e-15) zero++;
                AddFace(faceCounts, element.A, element.B, element.C);
                AddFace(faceCounts, element.A, element.B, element.D);
                AddFace(faceCounts, element.A, element.C, element.D);
                AddFace(faceCounts, element.B, element.C, element.D);
            }
            int nonManifold = faceCounts.Count(p => p.Value > 2);
            if (zero > 0) issues.Add("存在零体积或退化四面体：" + zero.ToString(CultureInfo.InvariantCulture));
            if (nonManifold > 0) issues.Add("存在非流形内部面：" + nonManifold.ToString(CultureInfo.InvariantCulture));
            if (mesh.SurfaceFaces.Count == 0) issues.Add("没有生成内外表面标记。");
            return new MeshQualitySummary
            {
                NodeCount = mesh.Nodes.Count,
                TetrahedronCount = mesh.Elements.Count,
                SurfaceFaceCount = mesh.SurfaceFaces.Count,
                BoundaryFaceCount = faceCounts.Count(p => p.Value == 1),
                InteriorFaceCount = faceCounts.Count(p => p.Value == 2),
                NonManifoldFaceCount = nonManifold,
                ZeroVolumeElementCount = zero,
                MinimumVolume = minimum == Double.MaxValue ? 0 : minimum,
                IsValid = issues.Count == 0,
                Issues = issues
            };
        }

        private static void AddFace(IDictionary<string, int> faces, int a, int b, int c)
        {
            int[] values = { a, b, c };
            Array.Sort(values);
            string key = values[0].ToString(CultureInfo.InvariantCulture) + ":" + values[1] + ":" + values[2];
            faces[key] = faces.ContainsKey(key) ? faces[key] + 1 : 1;
        }

        private static double Volume(MeshNode a, MeshNode b, MeshNode c, MeshNode d)
        {
            double abx = b.X - a.X, aby = b.Y - a.Y, abz = b.Z - a.Z;
            double acx = c.X - a.X, acy = c.Y - a.Y, acz = c.Z - a.Z;
            double adx = d.X - a.X, ady = d.Y - a.Y, adz = d.Z - a.Z;
            double determinant = abx * (acy * adz - acz * ady) - aby * (acx * adz - acz * adx) + abz * (acx * ady - acy * adx);
            return Math.Abs(determinant) / 6.0;
        }
    }
}
