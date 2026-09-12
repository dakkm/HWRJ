using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PreProcess.Wpf.Models.Results;

namespace PreProcess.Wpf.Services.Results
{
    internal static class PointImageBuilder
    {
        public static PointImageResult FromPrediction(ResultTable tokens)
        {
            if (tokens == null || tokens.Rows.Count == 0) return null;
            long frame = tokens.Rows.Max(r => Integer(r, "frame_id"));
            List<ResultRow> rows = tokens.Rows.Where(r => Integer(r, "frame_id") == frame && Boolean(r, "in_bounds")).ToList();
            if (rows.Count == 0) return null;
            int width = Clamp((int)Number(rows[0], "input_GRID_NX"), 1, 2048);
            int height = Clamp((int)Number(rows[0], "input_GRID_NY"), 1, 2048);
            var values = new double[width * height];
            foreach (ResultRow row in rows)
            {
                int x = (int)Math.Round(Number(row, "pixel_x"));
                int y = (int)Math.Round(Number(row, "pixel_y"));
                if (x >= 0 && x < width && y >= 0 && y < height) values[y * width + x] += Number(row, "pred_spot_intensity");
            }
            return new PointImageResult { Name = "预测红外点图像", SourcePath = tokens.SourcePath, Width = width, Height = height,
                Values = values, FrameId = frame, TimeSeconds = Number(rows[0], "time_s"), ValueName = "Radiant intensity", ValueUnit = "W/sr" };
        }

        public static PointImageResult FromForward(ResultTable infrared)
        {
            if (infrared == null || infrared.Rows.Count == 0) return null;
            long frame = infrared.Rows.Max(r => Integer(r, "frame_id"));
            List<ResultRow> rows = infrared.Rows.Where(r => Integer(r, "frame_id") == frame && Boolean(r, "in_screen_flag")).ToList();
            if (rows.Count == 0) return null;
            const int size = 64;
            var values = new double[size * size];
            double minX = rows.Min(r => Number(r, "screen_x_m")), maxX = rows.Max(r => Number(r, "screen_x_m"));
            double minY = rows.Min(r => Number(r, "screen_y_m")), maxY = rows.Max(r => Number(r, "screen_y_m"));
            foreach (ResultRow row in rows)
            {
                int x = Coordinate(Number(row, "screen_x_m"), minX, maxX, size);
                int y = Coordinate(Number(row, "screen_y_m"), minY, maxY, size);
                values[y * size + x] += Number(row, "detector_irradiance_W_m2");
            }
            return new PointImageResult { Name = "正向红外响应图", SourcePath = infrared.SourcePath, Width = size, Height = size,
                Values = values, FrameId = frame, TimeSeconds = Number(rows[0], "time_s"), ValueName = "Detector irradiance", ValueUnit = "W/m²" };
        }

        private static int Coordinate(double value, double minimum, double maximum, int size)
        {
            if (maximum <= minimum) return size / 2;
            return Clamp((int)Math.Round((value - minimum) / (maximum - minimum) * (size - 1)), 0, size - 1);
        }
        private static int Clamp(int value, int min, int max) { return Math.Max(min, Math.Min(max, value)); }
        private static long Integer(ResultRow row, string key) { return Convert.ToInt64(row.Values[key], CultureInfo.InvariantCulture); }
        private static double Number(ResultRow row, string key) { return Convert.ToDouble(row.Values[key], CultureInfo.InvariantCulture); }
        private static bool Boolean(ResultRow row, string key)
        {
            object value = row.Values[key];
            if (value is bool) return (bool)value;
            return Convert.ToDouble(value, CultureInfo.InvariantCulture) != 0;
        }
    }
}
