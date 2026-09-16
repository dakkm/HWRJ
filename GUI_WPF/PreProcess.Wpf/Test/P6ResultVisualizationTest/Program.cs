using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using PreProcess.Wpf.Models.Results;
using PreProcess.Wpf.Services.Results;
using PreProcess.Wpf.ViewModels;

internal static class Program
{
    private static int passed, total;
    private static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "preprocess-p6-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            TestForward(root);
            TestPrediction(root);
            TestSimilarity(root);
            TestScene(root);
            TestErrors(root);
            Console.WriteLine("P6.1 tests: " + passed + " / " + total + " passed");
            return passed == total ? 0 : 1;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 2; }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    private static void TestForward(string root)
    {
        string run = Path.Combine(root, "run_forward_fixture"), output = Path.Combine(run, "output"); Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "temperature_history.csv"), "frame,time_s,T_1,T_2\n0,0,300,301\n1,1,302,303\n");
        File.WriteAllText(Path.Combine(output, "trajectory_history.csv"),
            "case_id,frame_id,time_s,object_id,active_flag,released_flag,motion_stage,release_time_s,x_m,y_m,z_m,vx_m_s,vy_m_s,vz_m_s,speed_m_s,range_to_detector_m\n" +
            "c,1,1,1,1,1,1,0,6371008.8,0,0,100,0,0,100,1\n" +
            "c,2,2,1,1,1,1,0,6371108.8,100,10,100,10,1,100.503731,1\n");
        File.WriteAllText(Path.Combine(output, "infrared_response_history.csv"), "case_id,frame_id,time_s,object_id,active_flag,released_flag,radiation_power_W,radiant_intensity_W_sr,detector_received_power_W,detector_irradiance_W_m2,screen_x_m,screen_y_m,in_screen_flag,range_to_detector_m\nc,1,1,1,1,1,1,1,1,2,-1,-1,1,1\nc,1,1,2,1,1,1,1,1,4,1,1,1,1\n");
        string features = Path.Combine(output, "features", "run_features_fixture"); Directory.CreateDirectory(features);
        File.WriteAllText(Path.Combine(features, "evaluation_status.json"), "{\"module\":\"03\",\"status\":\"success\",\"run_id\":\"run_features_fixture\",\"mode\":\"features\"}");
        File.WriteAllText(Path.Combine(features, "feature_summary.json"), "{}");
        File.WriteAllText(Path.Combine(features, "feature_timeseries.csv"),
            "time_s,total_gray,gray_valid,gray_invalid_reason,total_radiant_intensity_W_sr,total_radiant_intensity_rate_W_sr_s,temperature_object_id,temperature_K,temperature_rate_K_s\n" +
            "0,100,1,,20,,1,300,\n1,110,1,,22,2,1,302,2\n");
        File.WriteAllText(Path.Combine(features, "object_feature_timeseries.csv"), "case_id,frame_id,time_s,object_id\nc,0,0,1\nc,1,1,1\n");
        File.WriteAllText(Path.Combine(features, "periodic_features.csv"), "signal,periodic_valid,sample_count\ntemperature,0,2\n");
        RunResult result = new ResultDirectoryLoader().Load(run);
        Check(result.ModuleType == ResultModuleType.Forward, "run directory recognition");
        Check(result.Summary["reader"] == "ForwardResultReader", "ResultReader dispatch");
        Check(result.Temperatures.Count == 2 && result.Temperatures[0].Tables[0].Rows.Count == 2, "01 and 03 TemperatureResult aggregation");
        Check(result.Similarities.Count == 1 && result.Similarities[0].Tables.Count == 3, "03 feature result aggregation");
        Check(result.PointImages.Count == 1 && result.PointImages[0].Values.Count(v => v > 0) == 2, "forward PointImageResult conversion");
        ResultBrowserViewModel view = ResultBrowserViewModel.FromResult(result, "正向计算");
        Check(view.DataSets.Select(x => x.Name).SequenceEqual(new[] { "总灰度", "辐射强度", "辐射强度变化率", "温度", "温度变化率", "周期调制特征" }),
            "forward feature selector exposes the six business features in order");
        Check(view.DataSets[4].Rows.Table.Columns.Cast<System.Data.DataColumn>().Select(x => x.ColumnName)
            .SequenceEqual(new[] { "时间（s）", "温度目标编号", "温度变化率（K·s⁻¹）" }), "feature table headers are localized");
        Check(view.DataSets.Take(5).SelectMany(x => x.Rows.Table.Columns.Cast<System.Data.DataColumn>())
            .All(x => !x.ColumnName.Contains("/")), "feature table headers avoid WPF binding path separators");
        Check(view.DataSets[5].Rows.Table.Columns.Cast<System.Data.DataColumn>().Select(x => x.ColumnName)
            .SequenceEqual(new[] { "特征信号", "周期有效标志", "样本数" }), "periodic feature headers are localized");
        Check(view.HasTemperatureChart && view.TemperatureChart.Series.Count == 2, "primary 01 plus 03 feature temperature chart");
        Check(view.HasPointImages && view.SelectedPointImage.Image != null, "01 infrared image");
        string trajectoryPostprocess = Path.Combine(output, "trajectory_postprocess"); Directory.CreateDirectory(trajectoryPostprocess);
        File.WriteAllText(Path.Combine(trajectoryPostprocess, "trajectory_metrics.csv"),
            "case_id,object_id,sample_count,release_time_s,trajectory_start_time_s,trajectory_end_time_s,trajectory_duration_s,path_length_m,net_displacement_m,average_path_speed_m_s,min_speed_m_s,max_speed_m_s,min_range_to_detector_m,max_range_to_detector_m,start_x_m,start_y_m,start_z_m,end_x_m,end_y_m,end_z_m\n" +
            "c,1,1,0,1,1,0,0,0,0,0,0,1,1,0,0,0,0,0,0\n");
        var trajectoryResult = new RunResult { RunId = "run_trajectory_fixture", ModuleType = ResultModuleType.Trajectory,
            ModuleCode = "轨迹", RunDirectory = run, OutputDirectory = output, ResultExists = true };
        new ResultReader().Read(trajectoryResult);
        Check(trajectoryResult.Trajectories.Count == 1 && trajectoryResult.Trajectories[0].Tables.Count == 2 &&
            trajectoryResult.Temperatures.Count == 0 && trajectoryResult.InfraredResponses.Count == 0 && trajectoryResult.Similarities.Count == 0,
            "trajectory page reads only history and postprocess metrics");
        ResultBrowserViewModel trajectoryView = ResultBrowserViewModel.FromResult(trajectoryResult, "轨迹生成");
        string[] trajectoryHeaders =
        {
            "时间（s）", "位置X（m）", "位置Y（m）", "位置Z（m）", "速度VX（m·s⁻¹）", "速度VY（m·s⁻¹）", "速度VZ（m·s⁻¹）",
            "偏航角（度）", "俯仰角（度）", "滚转角（度）", "偏航角速度（度·s⁻¹）", "俯仰角速度（度·s⁻¹）", "滚转角速度（度·s⁻¹）",
            "高度（m）", "全速度（m·s⁻¹）", "经度（度）", "纬度（度）"
        };
        Check(trajectoryView.DataSets[0].Rows.Table.Columns.Cast<System.Data.DataColumn>().Select(x => x.ColumnName).SequenceEqual(trajectoryHeaders),
            "trajectory table exposes the requested 17 headers in order");
        Check(!String.IsNullOrWhiteSpace(Convert.ToString(trajectoryView.DataSets[0].Rows[0][10]))
            && !String.IsNullOrWhiteSpace(Convert.ToString(trajectoryView.DataSets[0].Rows[1][10]))
            && !String.IsNullOrWhiteSpace(Convert.ToString(trajectoryView.DataSets[0].Rows[1][16])), "derived trajectory columns contain values");
    }

    private static void TestPrediction(string root)
    {
        string run = Path.Combine(root, "run_prediction_fixture"); Directory.CreateDirectory(run);
        File.WriteAllText(Path.Combine(run, "prediction_summary.json"), "{\"module\":\"02\",\"run_id\":\"run_prediction_fixture\",\"mode\":\"both\"}");
        File.WriteAllText(Path.Combine(run, "temperature_prediction.csv"), "time_s,temperature_prediction_K,temperature_reference_K\n0,300,301\n1,310,309\n");
        File.WriteAllText(Path.Combine(run, "point_image_frame_metrics.csv"), "frame_id,time_s,released_count,in_bounds_count,total_power,peak_power,total_intensity,centroid_x_pixel,centroid_y_pixel\n1,1,1,2,3,2,3,1,1\n");
        File.WriteAllText(Path.Combine(run, "point_image_reconstruction_contract.json"), "{}");
        WriteGzip(Path.Combine(run, "point_token_predictions.csv.gz"), "frame_id,sphere_id,time_s,sphere_released_flag,input_GRID_NX,input_GRID_NY,input_SPOT_PLANE_SIZE,pred_screen_x,pred_screen_y,pred_log_spot_power,pred_log_spot_intensity,pred_spot_power,pred_spot_intensity,pixel_x,pixel_y,in_bounds\n1,1,1,1,4,3,1,0,0,0,0,1,2,1,1,true\n1,2,1,1,4,3,1,0,0,0,0,1,3,2,1,true\n");
        RunResult result = new ResultDirectoryLoader().Load(run);
        Check(result.ModuleType == ResultModuleType.Prediction, "output directory recognition");
        Check(result.Temperatures.Count == 1, "02 TemperatureResult conversion");
        Check(result.PointImages.Single().Width == 4 && result.PointImages.Single().Height == 3, "prediction PointImageResult dimensions");
        ResultBrowserViewModel view = ResultBrowserViewModel.FromResult(result, "智能预测");
        Check(view.HasTemperatureChart && view.TemperatureChart.Series.Count == 2 && view.TemperatureChart.Series.Any(x => x.Name.Contains("Reference")), "02 prediction/reference chart");
        Check(view.HasPointImages, "02 reconstructed image");
    }

    private static void TestSimilarity(string root)
    {
        string run = Path.Combine(root, "run_similarity_fixture"); Directory.CreateDirectory(run);
        File.WriteAllText(Path.Combine(run, "evaluation_status.json"), "{\"module\":\"03\",\"run_id\":\"run_similarity_fixture\",\"mode\":\"similarity\"}");
        File.WriteAllText(Path.Combine(run, "similarity_summary.json"), "{}");
        File.WriteAllText(Path.Combine(run, "similarity_components.csv"), "feature_name,category,raw_difference,raw_unit,similarity_percent,valid_flag,invalid_reason,valid_sample_count\ntemperature,temp,1,K,99,1,,2\n");
        WriteFeature(Path.Combine(run, "reference_features"), 300, 301);
        WriteFeature(Path.Combine(run, "candidate_features"), 302, 303);
        RunResult result = new ResultDirectoryLoader().Load(run);
        ResultBrowserViewModel view = ResultBrowserViewModel.FromResult(result, "相似度评估");
        Check(result.Temperatures.Count == 2, "03 reference/candidate TemperatureResult conversion");
        Check(view.HasTemperatureChart && view.HidePhysicalVisualizations, "03 temperature data retained but physical visualizations hidden");
        Check(!view.HasPointImages, "03 image interface fallback");
    }

    private static void TestScene(string root)
    {
        string run = Path.Combine(root, "run_scene_fixture"); Directory.CreateDirectory(run);
        File.WriteAllText(Path.Combine(run, "scene_search_summary.json"), "{\"module\":\"04\",\"run_id\":\"run_scene_fixture\"}");
        File.WriteAllText(Path.Combine(run, "scene_search_status.json"), "{\"module\":\"04\",\"run_id\":\"run_scene_fixture\"}");
        File.WriteAllText(Path.Combine(run, "candidate_parameters.csv"), "solution_index,sequence_index,target_id,source_case_id,candidate_id,q_int,emissivity_ir,absorptivity_solar\n0,0,1,c,x,1,.8,.5\n");
        File.WriteAllText(Path.Combine(run, "candidate_temperature_curves.csv"), "solution_index,target_id,source_case_id,candidate_id,time_s,target_temperature_K,m2_temperature_K,forward_temperature_K\n0,1,c,x,0,300,301,302\n0,1,c,x,1,303,304,305\n");
        File.WriteAllText(Path.Combine(run, "scene_search_log.csv"), "sequence_index,target_id,source_case_id,candidate_id,q_int,emissivity_ir,absorptivity_solar,decision\n0,1,c,x,1,.8,.5,accepted\n");
        RunResult result = new ResultDirectoryLoader().Load(run);
        ResultBrowserViewModel view = ResultBrowserViewModel.FromResult(result, "红外场景构建");
        Check(result.Temperatures.Count == 1, "04 TemperatureResult conversion");
        Check(view.HasTemperatureChart && view.HidePhysicalVisualizations, "04 temperature data retained but physical visualizations hidden");
        Check(!view.HasPointImages, "04 image interface fallback");
    }

    private static void WriteFeature(string directory, double first, double second)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "feature_timeseries.csv"), "time_s,temperature_object_id,temperature_K\n0,1," + first.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n1,1," + second.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n");
    }

    private static void TestErrors(string root)
    {
        string empty = Path.Combine(root, "unknown"); Directory.CreateDirectory(empty);
        bool clear = false; try { new ResultDirectoryLoader().Load(empty); } catch (InvalidDataException ex) { clear = ex.Message.Contains("无法识别模块类型"); }
        Check(clear, "unknown module user message");
    }

    private static void WriteGzip(string path, string text)
    {
        using (FileStream file = File.Create(path)) using (var gzip = new GZipStream(file, CompressionMode.Compress))
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text); gzip.Write(bytes, 0, bytes.Length);
        }
    }
    private static void Check(bool condition, string name) { total++; if (!condition) throw new InvalidOperationException("FAILED: " + name); passed++; Console.WriteLine("PASS " + name); }
}
