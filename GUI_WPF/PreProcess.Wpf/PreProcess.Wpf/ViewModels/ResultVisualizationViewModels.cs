using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PreProcess.Wpf.Models.Results;

namespace PreProcess.Wpf.ViewModels
{
    public sealed class ChartPointViewModel
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public sealed class ChartSeriesViewModel
    {
        public string Name { get; set; }
        public Color Color { get; set; }
        public IList<ChartPointViewModel> Points { get; set; }
    }

    public sealed class ChartViewModel
    {
        public ChartViewModel() { Series = new List<ChartSeriesViewModel>(); }
        public string XAxisTitle { get; set; }
        public string YAxisTitle { get; set; }
        public IList<ChartSeriesViewModel> Series { get; private set; }
        public bool HasData { get { return Series.Any(x => x.Points != null && x.Points.Count > 0); } }

        public static ChartViewModel From(IEnumerable<TemperatureResult> results)
        {
            var chart = new ChartViewModel { XAxisTitle = "Time / s", YAxisTitle = "Temperature / K" };
            Color[] colors = { Colors.DodgerBlue, Colors.OrangeRed, Colors.SeaGreen, Colors.MediumPurple, Colors.Goldenrod, Colors.DeepPink, Colors.Teal, Colors.SlateBlue };
            foreach (TemperatureResult result in results ?? Enumerable.Empty<TemperatureResult>())
            foreach (ResultTable table in result.Tables)
            {
                if (!table.Columns.Any(c => c.Key == "time_s")) continue;
                IEnumerable<ResultRow> rows = table.Rows;
                if (table.Columns.Any(c => c.Key == "solution_index") && table.Rows.Count > 0)
                {
                    double first = table.Rows.Min(r => Number(r, "solution_index"));
                    rows = rows.Where(r => Number(r, "solution_index") == first);
                }
                foreach (ResultColumn column in table.Columns.Where(c => IsTemperature(c.Key)))
                {
                    var points = new List<ChartPointViewModel>();
                    foreach (ResultRow row in rows)
                    {
                        double x, y;
                        if (TryNumber(row, "time_s", out x) && TryNumber(row, column.Key, out y)) points.Add(new ChartPointViewModel { X = x, Y = y });
                    }
                    if (points.Count == 0) continue;
                    string name = SeriesName(result.Name, column.Key);
                    chart.Series.Add(new ChartSeriesViewModel { Name = name, Color = colors[chart.Series.Count % colors.Length], Points = points });
                }
            }
            return chart;
        }

        private static bool IsTemperature(string key)
        {
            return key.StartsWith("T_", StringComparison.Ordinal) || key.IndexOf("temperature", StringComparison.OrdinalIgnoreCase) >= 0 && key.EndsWith("_K", StringComparison.Ordinal);
        }
        private static string SeriesName(string artifact, string key)
        {
            string label = key == "temperature_prediction_K" ? "Prediction" : key == "temperature_reference_K" || key == "reference_temperature_K" ? "Reference" : key == "temperature_K" ? artifact :
                key == "target_temperature_K" ? "Target" : key == "m2_temperature_K" ? "Proxy" :
                key == "forward_temperature_K" ? "Forward" : key;
            return String.IsNullOrWhiteSpace(artifact) || label == artifact ? label : artifact + " · " + label;
        }
        private static bool TryNumber(ResultRow row, string key, out double value)
        {
            object raw;
            value = 0;
            return row.Values.TryGetValue(key, out raw) && raw != null && Double.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
        private static double Number(ResultRow row, string key) { double value; return TryNumber(row, key, out value) ? value : 0; }
    }

    public sealed class PointImageViewModel
    {
        public string Name { get; private set; }
        public string Description { get; private set; }
        public ImageSource Image { get; private set; }
        public string MinimumLabel { get; private set; }
        public string MaximumLabel { get; private set; }

        public static PointImageViewModel From(PointImageResult result)
        {
            if (result == null || result.Values == null || result.Values.Length != result.Width * result.Height || result.Width <= 0 || result.Height <= 0) return null;
            double minimum = result.Values.Min(), maximum = result.Values.Max();
            byte[] pixels = new byte[result.Width * result.Height * 4];
            for (int index = 0; index < result.Values.Length; index++)
            {
                double ratio = maximum > minimum ? (result.Values[index] - minimum) / (maximum - minimum) : 0;
                Color color = Heat(ratio);
                int offset = index * 4;
                pixels[offset] = color.B; pixels[offset + 1] = color.G; pixels[offset + 2] = color.R; pixels[offset + 3] = 255;
            }
            var bitmap = BitmapSource.Create(result.Width, result.Height, 96, 96, PixelFormats.Bgra32, null, pixels, result.Width * 4);
            bitmap.Freeze();
            string unit = String.IsNullOrWhiteSpace(result.ValueUnit) ? String.Empty : " " + result.ValueUnit;
            return new PointImageViewModel
            {
                Name = result.Name,
                Description = "Frame " + result.FrameId.ToString(CultureInfo.InvariantCulture) + " · " + result.TimeSeconds.ToString("G6", CultureInfo.InvariantCulture) + " s · " + result.Width + " × " + result.Height,
                Image = bitmap,
                MinimumLabel = minimum.ToString("G4", CultureInfo.InvariantCulture) + unit,
                MaximumLabel = maximum.ToString("G4", CultureInfo.InvariantCulture) + unit
            };
        }

        private static Color Heat(double value)
        {
            value = Math.Max(0, Math.Min(1, value));
            if (value < .25) return Color.FromRgb(0, (byte)(value * 4 * 180), (byte)(80 + value * 4 * 175));
            if (value < .5) return Color.FromRgb(0, (byte)(180 + (value - .25) * 4 * 75), (byte)(255 - (value - .25) * 4 * 255));
            if (value < .75) return Color.FromRgb((byte)((value - .5) * 4 * 255), 255, 0);
            return Color.FromRgb(255, (byte)(255 - (value - .75) * 4 * 255), 0);
        }
    }
}
