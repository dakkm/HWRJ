using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using PreProcess.Wpf.Models.Results;
using PreProcess.Wpf.Services.Execution;
using PreProcess.Wpf.Services.Process;
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
            TestTrajectoryImport(root);
            TestTrajectoryInputModes(root);
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
        File.WriteAllText(Path.Combine(output, "trajectory_history.csv"), "case_id,frame_id,time_s,object_id,active_flag,released_flag,motion_stage,release_time_s,x_m,y_m,z_m,vx_m_s,vy_m_s,vz_m_s,speed_m_s,range_to_detector_m\nc,1,1,1,1,1,1,0,0,0,0,0,0,0,0,1\n");
        File.WriteAllText(Path.Combine(output, "infrared_response_history.csv"), "case_id,frame_id,time_s,object_id,active_flag,released_flag,radiation_power_W,radiant_intensity_W_sr,detector_received_power_W,detector_irradiance_W_m2,screen_x_m,screen_y_m,in_screen_flag,range_to_detector_m\nc,1,1,1,1,1,1,1,1,2,-1,-1,1,1\nc,1,1,2,1,1,1,1,1,4,1,1,1,1\n");
        RunResult result = new ResultDirectoryLoader().Load(run);
        Check(result.ModuleType == ResultModuleType.Forward, "run directory recognition");
        Check(result.Summary["reader"] == "ForwardResultReader", "ResultReader dispatch");
        Check(result.Temperatures.Count == 1 && result.Temperatures[0].Tables[0].Rows.Count == 2, "TemperatureResult conversion");
        Check(result.PointImages.Count == 1 && result.PointImages[0].Values.Count(v => v > 0) == 2, "forward PointImageResult conversion");
        ResultBrowserViewModel view = ResultBrowserViewModel.FromResult(result, "正向计算");
        Check(view.HasTemperatureChart && view.TemperatureChart.Series.Count == 2, "01 temperature chart");
        Check(view.HasTrajectoryDiagram && view.TrajectoryDiagram.Series.Count == 1, "01 trajectory diagram");
        Check(view.TrajectoryDiagram.Series[0].Name == "目标 1", "trajectory grouped by object");
        Check(view.HasPointImages && view.SelectedPointImage.Image != null, "01 infrared image");
    }

    private static void TestTrajectoryImport(string root)
    {
        string run = Path.Combine(root, "run_trajectory_fixture"), output = Path.Combine(run, "output"); Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "trajectory_history.csv"),
            "case_id,frame_id,time_s,object_id,active_flag,released_flag,motion_stage,release_time_s,x_m,y_m,z_m,vx_m_s,vy_m_s,vz_m_s,speed_m_s,range_to_detector_m\n" +
            "c,0,0,1,1,1,1,0,0,0,0,1,2,0,2.236,100\n" +
            "c,1,1,1,1,1,1,0,1,2,0,1,2,0,2.236,98\n");
        RunResult result = new ResultDirectoryLoader().Load(run);
        ResultBrowserViewModel view = ResultBrowserViewModel.FromResult(result, "轨迹生成");
        Check(result.ModuleType == ResultModuleType.Trajectory, "standalone trajectory directory recognition");
        Check(view.HasTrajectoryDiagram && view.TrajectoryDiagram.Series.Single().Points.Count == 2, "standalone trajectory diagram");
    }

    private static void TestTrajectoryInputModes(string root)
    {
        string output = Path.Combine(root, "completed_forward", "output"); Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "trajectory_history.csv"), "fixture");
        using (var view = new ForwardRunViewModel(new TaskEditorViewModel()))
        {
            view.SelectedModule = "轨迹";
            Check(view.IsTrajectory && !view.IsForward && view.ModuleTitle == "轨迹生成", "trajectory is independent from forward module");
            Check(view.UseForwardTrajectory && view.TrajectoryInputModes.Length == 2, "latest forward trajectory is default input mode");
            view.RunHistory.Add(new RunRecord { Module = "01", State = ProcessRunState.Completed, ResultDirectory = output });
            Check(view.DefaultTrajectoryPath == Path.Combine(output, "trajectory_history.csv"), "latest forward trajectory path resolved");
            view.TrajectoryInputMode = "读取外部轨迹文件";
            Check(view.UseExternalTrajectory && !view.UseForwardTrajectory, "external trajectory input mode selectable");
        }
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
        Check(view.TemperatureChart.Series.Count == 2 && view.TemperatureChart.Series.Any(x => x.Name == "Reference") && view.TemperatureChart.Series.Any(x => x.Name == "Candidate"), "03 dual temperature chart");
        Check(!view.HasPointImages, "03 image interface fallback");
        Check(!view.HasTrajectoryDiagram && !String.IsNullOrWhiteSpace(view.TrajectoryEmptyMessage), "03 trajectory empty state");
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
        Check(view.TemperatureChart.Series.Count == 3 && view.TemperatureChart.Series.Any(x => x.Name.Contains("Target")) && view.TemperatureChart.Series.Any(x => x.Name.Contains("Proxy")) && view.TemperatureChart.Series.Any(x => x.Name.Contains("Forward")), "04 target/proxy/forward chart");
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
