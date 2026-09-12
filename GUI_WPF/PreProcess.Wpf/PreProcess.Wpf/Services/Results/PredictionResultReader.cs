using System;
using System.Collections.Generic;
using System.IO;
using PreProcess.Wpf.Models.Results;

namespace PreProcess.Wpf.Services.Results
{
    public sealed class PredictionResultReader : ResultReaderBase
    {
        public override RunResult Read(RunResult result)
        {
            if (!Prepare(result, ResultModuleType.Prediction)) return result;
            IDictionary<string, object> summary = ReadJson(result, "prediction_summary.json", true);
            ValidateIdentity(summary, result, "02", "prediction_summary.json");
            AddJsonSummary(result.Summary, summary, "prediction.");
            string mode = OptionalString(summary, "mode");

            bool temperatureExpected = mode == "temperature" || mode == "both";
            ResultTable temperature = ReadCsv(result, "temperature_prediction.csv", "温度预测",
                new[] { "time_s", "temperature_prediction_K" }, new[] { "time_s", "temperature_prediction_K" }, temperatureExpected);
            if (temperature != null)
            {
                var model = new TemperatureResult { Name = "智能温度预测", SourcePath = temperature.SourcePath };
                model.Tables.Add(temperature); result.Temperatures.Add(model);
            }

            bool pointExpected = mode == "point-image" || mode == "both";
            ResultTable frames = ReadCsv(result, "point_image_frame_metrics.csv", "点图像帧汇总",
                new[] { "frame_id", "time_s", "released_count", "in_bounds_count", "total_power", "peak_power", "total_intensity", "centroid_x_pixel", "centroid_y_pixel" },
                new[] { "frame_id", "time_s", "released_count", "in_bounds_count", "total_power", "peak_power", "total_intensity" }, pointExpected);
            ResultTable tokens = ReadCsv(result, "point_token_predictions.csv.gz", "点图像逐目标预测",
                new[] { "frame_id", "sphere_id", "time_s", "sphere_released_flag", "input_GRID_NX", "input_GRID_NY", "input_SPOT_PLANE_SIZE", "pred_screen_x", "pred_screen_y", "pred_log_spot_power", "pred_log_spot_intensity", "pred_spot_power", "pred_spot_intensity", "pixel_x", "pixel_y", "in_bounds" },
                new[] { "frame_id", "sphere_id", "time_s", "sphere_released_flag", "input_GRID_NX", "input_GRID_NY", "input_SPOT_PLANE_SIZE", "pred_screen_x", "pred_screen_y", "pred_log_spot_power", "pred_log_spot_intensity", "pred_spot_power", "pred_spot_intensity", "pixel_x", "pixel_y" }, pointExpected);
            if (frames != null || tokens != null)
            {
                var model = new InfraredResult { Name = "点图像预测", SourcePath = frames != null ? frames.SourcePath : tokens.SourcePath };
                if (frames != null) model.Tables.Add(frames);
                if (tokens != null) model.Tables.Add(tokens);
                IDictionary<string, object> contract = ReadJson(result, "point_image_reconstruction_contract.json", pointExpected);
                AddJsonSummary(model.Summary, contract, "reconstruction.");
                result.InfraredResponses.Add(model);
                PointImageResult image = PointImageBuilder.FromPrediction(tokens);
                if (image != null) result.PointImages.Add(image);
            }
            result.Summary["reader"] = "PredictionResultReader";
            return result;
        }
    }
}
