using System;
using System.Globalization;
using System.IO;

namespace PreProcess.Wpf.Services.Mesh
{
    /// <summary>只做 STL/OBJ 几何结构检查，不导入任务或生成求解网格。</summary>
    public sealed class GeometryFileInspector
    {
        public string Inspect(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("找不到几何文件。", path);
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension == ".obj") return InspectObj(path);
            if (extension == ".stl") return InspectStl(path);
            throw new InvalidDataException("仅支持 STL 和 OBJ 几何检查。");
        }

        private static string InspectObj(string path)
        {
            int vertices = 0, faces = 0, lineNumber = 0;
            foreach (string raw in File.ReadLines(path))
            {
                lineNumber++;
                string line = raw.Trim();
                if (line.StartsWith("v ", StringComparison.Ordinal))
                {
                    string[] parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 4) throw new InvalidDataException("OBJ 顶点坐标不完整，行 " + lineNumber);
                    for (int i = 1; i <= 3; i++) CheckFinite(parts[i], "OBJ 顶点", lineNumber);
                    vertices++;
                }
                else if (line.StartsWith("f ", StringComparison.Ordinal))
                {
                    string[] parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 4) throw new InvalidDataException("OBJ 面至少需要三个顶点，行 " + lineNumber);
                    for (int i = 1; i < parts.Length; i++)
                    {
                        string token = parts[i].Split('/')[0];
                        int index;
                        if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out index) || index == 0 || (index > 0 && index > vertices) || (index < 0 && -index > vertices))
                            throw new InvalidDataException("OBJ 面索引无效，行 " + lineNumber);
                    }
                    faces++;
                }
            }
            if (vertices < 3 || faces < 1) throw new InvalidDataException("OBJ 缺少可用顶点或面。");
            return string.Format("OBJ 结构检查通过：{0} 顶点，{1} 面。尚未导入当前球壳模型或求解器。", vertices, faces);
        }

        private static string InspectStl(string path)
        {
            var info = new FileInfo(path);
            if (info.Length < 15) throw new InvalidDataException("STL 文件过短。");
            using (var stream = File.OpenRead(path))
            {
                if (stream.Length >= 84)
                {
                    stream.Position = 80;
                    var reader = new BinaryReader(stream);
                    uint count = reader.ReadUInt32();
                    if (count > 0 && 84L + 50L * count == stream.Length)
                    {
                        for (uint i = 0; i < count; i++)
                        {
                            for (int value = 0; value < 12; value++)
                            {
                                float coordinate = reader.ReadSingle();
                                if (float.IsNaN(coordinate) || float.IsInfinity(coordinate)) throw new InvalidDataException("二进制 STL 含非有限坐标。");
                            }
                            reader.ReadUInt16();
                        }
                        return "二进制 STL 结构检查通过：" + count + " 个三角面。尚未导入当前球壳模型或求解器。";
                    }
                }
            }
            int facets = 0, vertices = 0;
            foreach (string raw in File.ReadLines(path))
            {
                string line = raw.Trim();
                if (line.StartsWith("facet normal", StringComparison.OrdinalIgnoreCase)) facets++;
                if (line.StartsWith("vertex ", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 4) throw new InvalidDataException("ASCII STL 顶点格式无效。");
                    for (int i = 1; i <= 3; i++) CheckFinite(parts[i], "STL 顶点", 0);
                    vertices++;
                }
            }
            if (facets < 1 || vertices != facets * 3) throw new InvalidDataException("ASCII STL 三角面或顶点记录不完整。");
            return "ASCII STL 结构检查通过：" + facets + " 个三角面。尚未导入当前球壳模型或求解器。";
        }

        private static void CheckFinite(string text, string label, int line)
        {
            double value;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || double.IsNaN(value) || double.IsInfinity(value))
                throw new InvalidDataException(label + "坐标无效" + (line > 0 ? "，行 " + line : "") + "。");
        }
    }
}
