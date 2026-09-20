using System;
using System.Collections.Generic;
using System.IO;
using PreProcess.Wpf.Models.Results;

namespace PreProcess.Wpf.Services.Results
{
    // 定义 PredictionResultReader 类型，集中封装与该领域对象相关的状态和行为。
    public sealed class PredictionResultReader : ResultReaderBase
    {
        public override RunResult Read(RunResult result)
        {
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (!Prepare(result, ResultModuleType.Prediction)) return result;
            IDictionary<string, object> summary = ReadJson(result, "prediction_summary.json", true);
            ValidateIdentity(summary, result, "02", "prediction_summary.json");
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            AddJsonSummary(result.Summary, summary, "prediction.");
            string mode = OptionalString(summary, "mode");

            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            bool temperatureExpected = mode == "temperature" || mode == "both";
            ResultTable temperature = ReadCsv(result, "temperature_prediction.csv", "温度预测",
                // 继续处理当前业务步骤，保持上下文状态一致。
                new[] { "time_s", "temperature_prediction_K" }, new[] { "time_s", "temperature_prediction_K" }, temperatureExpected);
            if (temperature != null)
            {
                ResultTable display = ToForwardTemperatureTable(temperature);
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                // 温度已改由模块 01 的正式求解内核生成，名称中明确来源，避免与旧代理模型混淆。
                var model = new TemperatureResult { Name = "可信温度历史（正向内核）", SourcePath = display.SourcePath };
                model.Tables.Add(display); result.Temperatures.Add(model);
            }

            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            bool pointExpected = mode == "point-image" || mode == "both";
            ResultTable frames = ReadCsv(result, "point_image_frame_metrics.csv", "点图像帧汇总",
                // 继续处理当前业务步骤，保持上下文状态一致。
                new[] { "frame_id", "time_s", "released_count", "in_bounds_count", "total_power", "peak_power", "total_intensity", "centroid_x_pixel", "centroid_y_pixel" },
                new[] { "frame_id", "time_s", "released_count", "in_bounds_count", "total_power", "peak_power", "total_intensity" }, pointExpected);
            ResultTable tokens = ReadCsv(result, "point_token_predictions.csv.gz", "点图像逐目标预测",
                // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
                new[] { "frame_id", "sphere_id", "time_s", "sphere_released_flag", "input_GRID_NX", "input_GRID_NY", "input_SPOT_PLANE_SIZE", "pred_screen_x", "pred_screen_y", "pred_log_spot_power", "pred_log_spot_intensity", "pred_spot_power", "pred_spot_intensity", "pixel_x", "pixel_y", "in_bounds" },
                new[] { "frame_id", "sphere_id", "time_s", "sphere_released_flag", "input_GRID_NX", "input_GRID_NY", "input_SPOT_PLANE_SIZE", "pred_screen_x", "pred_screen_y", "pred_log_spot_power", "pred_log_spot_intensity", "pred_spot_power", "pred_spot_intensity", "pixel_x", "pixel_y" }, pointExpected);
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (frames != null || tokens != null)
            {
                ResultTable display = tokens == null ? null : ToForwardInfraredTable(tokens);
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                var model = new InfraredResult { Name = "智能预测红外响应", SourcePath = tokens != null ? tokens.SourcePath : frames.SourcePath };
                if (display != null) model.Tables.Add(display);
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                IDictionary<string, object> contract = ReadJson(result, "point_image_reconstruction_contract.json", pointExpected);
                AddJsonSummary(model.Summary, contract, "reconstruction.");
                result.InfraredResponses.Add(model);
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                PointImageResult image = PointImageBuilder.FromForward(display);
                if (image != null)
                {
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    image.Name = "智能预测红外响应图";
                    result.PointImages.Add(image);
                }
            }
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            result.Summary["reader"] = "PredictionResultReader";
            result.Summary["display.format"] = "forward-compatible";
            result.Summary["display.unavailable_fields"] = "radiation_power_W, radiant_intensity_W_sr, range_to_detector_m";
            // 返回当前步骤生成的结果，并结束本次调用。
            return result;
        }

        private static ResultTable ToForwardTemperatureTable(ResultTable source)
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var target = new ResultTable { Name = "温度历史", SourcePath = source.SourcePath };
            AddColumn(target, "frame", null);
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            AddColumn(target, "time_s", "s");
            AddColumn(target, "T_1", "K");
            for (int index = 0; index < source.Rows.Count; index++)
            {
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                var row = new ResultRow();
                row.Values["frame"] = (long)index;
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                row.Values["time_s"] = Value(source.Rows[index], "time_s");
                row.Values["T_1"] = Value(source.Rows[index], "temperature_prediction_K");
                // 将当前结果加入集合，供后续汇总或界面展示。
                target.Rows.Add(row);
            }
            return target;
        }

        private static ResultTable ToForwardInfraredTable(ResultTable source)
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var target = new ResultTable { Name = "红外响应历史", SourcePath = source.SourcePath };
            AddColumn(target, "frame_id", null);
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            AddColumn(target, "time_s", "s");
            AddColumn(target, "object_id", null);
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            AddColumn(target, "active_flag", null);
            AddColumn(target, "released_flag", null);
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            AddColumn(target, "radiation_power_W", "W");
            AddColumn(target, "radiant_intensity_W_sr", "W/sr");
            AddColumn(target, "detector_received_power_W", "W");
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            AddColumn(target, "detector_irradiance_W_m2", "W/m²");
            AddColumn(target, "screen_x_m", "m");
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            AddColumn(target, "screen_y_m", "m");
            AddColumn(target, "in_screen_flag", null);
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            AddColumn(target, "range_to_detector_m", "m");
            foreach (ResultRow sourceRow in source.Rows)
            {
                var row = new ResultRow();
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                row.Values["frame_id"] = Value(sourceRow, "frame_id");
                row.Values["time_s"] = Value(sourceRow, "time_s");
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                row.Values["object_id"] = Value(sourceRow, "sphere_id");
                row.Values["active_flag"] = null;
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                row.Values["released_flag"] = Value(sourceRow, "sphere_released_flag");
                row.Values["radiation_power_W"] = null;
                row.Values["radiant_intensity_W_sr"] = null;
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                row.Values["detector_received_power_W"] = Value(sourceRow, "pred_spot_power");
                row.Values["detector_irradiance_W_m2"] = Value(sourceRow, "pred_spot_intensity");
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                row.Values["screen_x_m"] = Value(sourceRow, "pred_screen_x");
                row.Values["screen_y_m"] = Value(sourceRow, "pred_screen_y");
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                row.Values["in_screen_flag"] = Value(sourceRow, "in_bounds");
                row.Values["range_to_detector_m"] = null;
                target.Rows.Add(row);
            }
            // 返回当前步骤生成的结果，并结束本次调用。
            return target;
        }

        private static void AddColumn(ResultTable table, string key, string unit)
        {
            // 将当前结果加入集合，供后续汇总或界面展示。
            table.Columns.Add(new ResultColumn { Key = key, DisplayName = key, Unit = unit });
        }

        private static object Value(ResultRow row, string key)
        {
            // 继续处理当前业务步骤，保持上下文状态一致。
            object value;
            return row.Values.TryGetValue(key, out value) ? value : null;
        }
    }
}
