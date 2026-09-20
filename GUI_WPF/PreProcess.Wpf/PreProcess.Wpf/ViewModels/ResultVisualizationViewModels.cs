using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PreProcess.Wpf.Models.Results;

namespace PreProcess.Wpf.ViewModels
{
    public sealed class ChartPointViewModel
    {
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public double X { get; set; }
        public double Y { get; set; }
    }

    // 定义 ChartSeriesViewModel 类型，集中封装与该领域对象相关的状态和行为。
    public sealed class ChartSeriesViewModel
    {
        public string Name { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public Color Color { get; set; }
        public IList<ChartPointViewModel> Points { get; set; }
    }

    public sealed class ChartViewModel
    {
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public ChartViewModel() { Series = new List<ChartSeriesViewModel>(); }
        public string XAxisTitle { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string YAxisTitle { get; set; }
        public IList<ChartSeriesViewModel> Series { get; private set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public bool HasData { get { return Series.Any(x => x.Points != null && x.Points.Count > 0); } }

        public static ChartViewModel From(IEnumerable<TemperatureResult> results)
        {
            var chart = new ChartViewModel { XAxisTitle = "Time / s", YAxisTitle = "Temperature / K" };
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            Color[] colors = { Colors.DodgerBlue, Colors.OrangeRed, Colors.SeaGreen, Colors.MediumPurple, Colors.Goldenrod, Colors.DeepPink, Colors.Teal, Colors.SlateBlue };
            foreach (TemperatureResult result in results ?? Enumerable.Empty<TemperatureResult>())
            // 遍历当前数据集合，逐项完成必要的转换或状态更新。
            foreach (ResultTable table in result.Tables)
            {
                if (!table.Columns.Any(c => c.Key == "time_s")) continue;
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                IEnumerable<ResultRow> rows = table.Rows;
                if (table.Columns.Any(c => c.Key == "solution_index") && table.Rows.Count > 0)
                {
                    double first = table.Rows.Min(r => Number(r, "solution_index"));
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    rows = rows.Where(r => Number(r, "solution_index") == first);
                }
                // The result panel is a single-target view. Keep the primary
                // target (T_1) for forward-history tables; prediction and
                // comparison tables use their explicitly named temperature
                // columns and are retained when present.
                foreach (ResultColumn column in table.Columns.Where(c => IsDisplayedTemperature(c.Key)))
                {
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    var points = new List<ChartPointViewModel>();
                    foreach (ResultRow row in rows)
                    {
                        // 继续处理当前业务步骤，保持上下文状态一致。
                        double x, y;
                        if (TryNumber(row, "time_s", out x) && TryNumber(row, column.Key, out y)) points.Add(new ChartPointViewModel { X = x, Y = y });
                    }
                    // 校验当前条件，仅在满足业务约束时进入该处理分支。
                    if (points.Count == 0) continue;
                    string name = SeriesName(result.Name, column.Key);
                    chart.Series.Add(new ChartSeriesViewModel { Name = name, Color = colors[chart.Series.Count % colors.Length], Points = points });
                }
            }
            // 返回当前步骤生成的结果，并结束本次调用。
            return chart;
        }

        private static bool IsDisplayedTemperature(string key)
        {
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (String.Equals(key, "T_1", StringComparison.Ordinal)) return true;
            if (key.StartsWith("T_", StringComparison.Ordinal)) return false;
            // 返回当前步骤生成的结果，并结束本次调用。
            return key.IndexOf("temperature", StringComparison.OrdinalIgnoreCase) >= 0 && key.EndsWith("_K", StringComparison.Ordinal);
        }
        private static string SeriesName(string artifact, string key)
        {
            string label = key == "temperature_prediction_K" ? "Prediction" : key == "temperature_reference_K" || key == "reference_temperature_K" ? "Reference" : key == "temperature_K" ? artifact :
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                key == "target_temperature_K" ? "Target" : key == "m2_temperature_K" ? "Proxy" :
                key == "forward_temperature_K" ? "Forward" : key;
            // 返回当前步骤生成的结果，并结束本次调用。
            return String.IsNullOrWhiteSpace(artifact) || label == artifact ? label : artifact + " · " + label;
        }
        private static bool TryNumber(ResultRow row, string key, out double value)
        {
            // 继续处理当前业务步骤，保持上下文状态一致。
            object raw;
            value = 0;
            return row.Values.TryGetValue(key, out raw) && raw != null && Double.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static double Number(ResultRow row, string key) { double value; return TryNumber(row, key, out value) ? value : 0; }
    }

    public sealed class PointImageViewModel
    {
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string Name { get; private set; }
        public string Description { get; private set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public ImageSource Image { get; private set; }
        public string MinimumLabel { get; private set; }
        public string MaximumLabel { get; private set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public System.Windows.Visibility ScaleVisibility { get; private set; }

        public static PointImageViewModel From(PointImageResult result)
        {
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (result == null || result.Values == null || result.Values.Length != result.Width * result.Height || result.Width <= 0 || result.Height <= 0) return null;
            double minimum = result.Values.Min(), maximum = result.Values.Max();
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            byte[] pixels = new byte[result.Width * result.Height * 4];
            for (int index = 0; index < result.Values.Length; index++)
            {
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                double ratio = maximum > minimum ? (result.Values[index] - minimum) / (maximum - minimum) : 0;
                Color color = Heat(ratio);
                int offset = index * 4;
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                pixels[offset] = color.B; pixels[offset + 1] = color.G; pixels[offset + 2] = color.R; pixels[offset + 3] = 255;
            }
            var bitmap = BitmapSource.Create(result.Width, result.Height, 96, 96, PixelFormats.Bgra32, null, pixels, result.Width * 4);
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            bitmap.Freeze();
            string unit = String.IsNullOrWhiteSpace(result.ValueUnit) ? String.Empty : " " + result.ValueUnit;
            // 返回当前步骤生成的结果，并结束本次调用。
            return new PointImageViewModel
            {
                Name = result.Name,
                Description = "Frame " + result.FrameId.ToString(CultureInfo.InvariantCulture) + " · " + result.TimeSeconds.ToString("G6", CultureInfo.InvariantCulture) + " s · " + result.Width + " × " + result.Height,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                Image = bitmap,
                MinimumLabel = minimum.ToString("G4", CultureInfo.InvariantCulture) + unit,
                // 将外部数据转换为目标类型，并保持约定的表示格式。
                MaximumLabel = maximum.ToString("G4", CultureInfo.InvariantCulture) + unit,
                ScaleVisibility = System.Windows.Visibility.Visible
            };
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public static PointImageViewModel FromFile(string path, string name)
        {
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            var bitmap = new BitmapImage();
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            bitmap.UriSource = new Uri(Path.GetFullPath(path), UriKind.Absolute);
            bitmap.EndInit();
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            bitmap.Freeze();
            return new PointImageViewModel
            {
                Name = name,
                // 处理文件系统路径及数据，并在使用前确认目标有效。
                Description = bitmap.PixelWidth + " × " + bitmap.PixelHeight + " · " + Path.GetFileName(path),
                Image = bitmap,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                MinimumLabel = String.Empty,
                MaximumLabel = String.Empty,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                ScaleVisibility = System.Windows.Visibility.Collapsed
            };
        }

        private static Color Heat(double value)
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            value = Math.Max(0, Math.Min(1, value));
            if (value < .25) return Color.FromRgb(0, (byte)(value * 4 * 180), (byte)(80 + value * 4 * 175));
            if (value < .5) return Color.FromRgb(0, (byte)(180 + (value - .25) * 4 * 75), (byte)(255 - (value - .25) * 4 * 255));
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (value < .75) return Color.FromRgb((byte)((value - .5) * 4 * 255), 255, 0);
            return Color.FromRgb(255, (byte)(255 - (value - .75) * 4 * 255), 0);
        }
    }
}
