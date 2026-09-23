using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PreProcess.Wpf.Models.Results;

namespace PreProcess.Wpf.Services.Results
{
    internal static class PointImageBuilder
    {
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public static PointImageResult FromPrediction(ResultTable tokens)
        {
            if (tokens == null || tokens.Rows.Count == 0) return null;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            long frame = tokens.Rows.Max(r => Integer(r, "frame_id"));
            List<ResultRow> rows = tokens.Rows.Where(r => Integer(r, "frame_id") == frame && Boolean(r, "in_bounds")).ToList();
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (rows.Count == 0) return null;
            int width = Clamp((int)Number(rows[0], "input_GRID_NX"), 1, 2048);
            int height = Clamp((int)Number(rows[0], "input_GRID_NY"), 1, 2048);
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var values = new double[width * height];
            foreach (ResultRow row in rows)
            {
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                int x = (int)Math.Round(Number(row, "pixel_x")) - 1;
                int y = (int)Math.Round(Number(row, "pixel_y")) - 1;
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (x >= 0 && x < width && y >= 0 && y < height) values[y * width + x] += PredictionIrradiance(row);
            }
            return new PointImageResult { Name = "预测红外点图像", SourcePath = tokens.SourcePath, Width = width, Height = height,
                Values = values, FrameId = frame, TimeSeconds = Number(rows[0], "time_s"), ValueName = "Detector irradiance", ValueUnit = "W/m²" };
        }

        internal static double PredictionPower(ResultRow row)
        {
            object value;
            if (row.Values.TryGetValue("pred_spot_power_W", out value) && value != null)
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return Math.Max(Math.Pow(10.0, Number(row, "pred_log_spot_intensity")) - 1e-18, 0.0);
        }

        internal static double PredictionIrradiance(ResultRow row)
        {
            object value;
            if (row.Values.TryGetValue("pred_detector_irradiance_W_m2", out value) && value != null)
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            double nx = Number(row, "input_GRID_NX"), ny = Number(row, "input_GRID_NY");
            double size = Number(row, "input_SPOT_PLANE_SIZE");
            if (nx <= 0 || ny <= 0 || size <= 0 || nx != Math.Floor(nx) || ny != Math.Floor(ny))
                throw new InvalidOperationException("预测点图像的物理网格合同无效。");
            return PredictionPower(row) / ((size / nx) * (size / ny));
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public static PointImageResult FromForward(ResultTable infrared)
        {
            if (infrared == null || infrared.Rows.Count == 0) return null;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            long frame = infrared.Rows.Max(r => Integer(r, "frame_id"));
            List<ResultRow> rows = infrared.Rows.Where(r => Integer(r, "frame_id") == frame && Boolean(r, "in_screen_flag")).ToList();
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (rows.Count == 0) return null;
            const int size = 64;
            var values = new double[size * size];
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            double minX = rows.Min(r => Number(r, "screen_x_m")), maxX = rows.Max(r => Number(r, "screen_x_m"));
            double minY = rows.Min(r => Number(r, "screen_y_m")), maxY = rows.Max(r => Number(r, "screen_y_m"));
            // 遍历当前数据集合，逐项完成必要的转换或状态更新。
            foreach (ResultRow row in rows)
            {
                int x = Coordinate(Number(row, "screen_x_m"), minX, maxX, size);
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                int y = Coordinate(Number(row, "screen_y_m"), minY, maxY, size);
                values[y * size + x] += Number(row, "detector_irradiance_W_m2");
            }
            return new PointImageResult { Name = "正向红外响应图", SourcePath = infrared.SourcePath, Width = size, Height = size,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                Values = values, FrameId = frame, TimeSeconds = Number(rows[0], "time_s"), ValueName = "Detector irradiance", ValueUnit = "W/m²" };
        }

        private static int Coordinate(double value, double minimum, double maximum, int size)
        {
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (maximum <= minimum) return size / 2;
            return Clamp((int)Math.Round((value - minimum) / (maximum - minimum) * (size - 1)), 0, size - 1);
        }
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static int Clamp(int value, int min, int max) { return Math.Max(min, Math.Min(max, value)); }
        private static long Integer(ResultRow row, string key) { return Convert.ToInt64(row.Values[key], CultureInfo.InvariantCulture); }
        // 将外部数据转换为目标类型，并保持约定的表示格式。
        private static double Number(ResultRow row, string key) { return Convert.ToDouble(row.Values[key], CultureInfo.InvariantCulture); }
        private static bool Boolean(ResultRow row, string key)
        {
            object value = row.Values[key];
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (value is bool) return (bool)value;
            return Convert.ToDouble(value, CultureInfo.InvariantCulture) != 0;
        }
    }
}
