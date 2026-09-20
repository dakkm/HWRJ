using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using PreProcess.Wpf.Models;
using PreProcess.Wpf.Services;
using PreProcess.Wpf.Services.Execution;
using PreProcess.Wpf.Services.Process;

internal static class Program
{
    // 继续处理当前业务步骤，保持上下文状态一致。
    static int passed;
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 33554432 };
    // 更新当前流程使用的数据，为下一处理步骤做好准备。
    static readonly string Self = System.Reflection.Assembly.GetExecutingAssembly().Location;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS " + message); passed++; }
    // 调用对应组件完成当前步骤，并保留产生的处理结果。
    static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        if (args.Length > 0 && args[0] == "-B") return Fixture(args);
        // 进入可能失败的操作区间，并由后续异常分支统一处理故障。
        try { Run(args).GetAwaiter().GetResult(); Console.WriteLine("TOTAL " + passed + " passed"); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    // 处理文件系统路径及数据，并在使用前确认目标有效。
    static void Write(string path, object value) { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, Json.Serialize(value), new UTF8Encoding(false)); }
    static Dictionary<string, object> Read(string path) => Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
    // 调用对应组件完成当前步骤，并保留产生的处理结果。
    static async Task Run(string[] args)
    {
        string artifacts = Path.GetFullPath(args[0]); Directory.CreateDirectory(artifacts);
        var paths = new BackendPaths { PackageRoot = Path.GetFullPath(args[1]), Python = args[2], RuntimeRoot = Path.Combine(artifacts, "runtime") };
        // 处理文件系统路径及数据，并在使用前确认目标有效。
        string groupedTask = Path.Combine(paths.RuntimeRoot, "results", "same-task"); Directory.CreateDirectory(groupedTask);
        var original = Directory.GetFiles(paths.PackageRoot, "*", SearchOption.AllDirectories).ToDictionary(x => x, RuntimePackage.Hash);
        // 处理文件系统路径及数据，并在使用前确认目标有效。
        string forwardRun = Path.Combine(groupedTask, "01_001");
        string forwardOutput = Path.Combine(forwardRun, "results", "source_output"); Directory.CreateDirectory(forwardOutput);
        // 处理文件系统路径及数据，并在使用前确认目标有效。
        File.WriteAllText(Path.Combine(forwardOutput, "trajectory_history.csv"),
            "case_id,frame_id,time_s,object_id,active_flag,released_flag,motion_stage,release_time_s,x_m,y_m,z_m,vx_m_s,vy_m_s,vz_m_s,speed_m_s,range_to_detector_m\n" +
            "c,0,0,1,1,1,1,0,0,0,0,1,0,0,1,10\n" +
            // 继续处理当前业务步骤，保持上下文状态一致。
            "c,1,1,1,1,1,1,0,1,0,0,1,0,0,1,9\n");
        Write(Path.Combine(forwardRun, "run-location.json"), new RunRecord { Module = "01", RunId = "source",
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            TaskDirectory = groupedTask, ExecutionDirectory = forwardRun, ResultDirectory = forwardOutput,
            State = ProcessRunState.Completed, StartedAt = DateTimeOffset.Now.AddMinutes(-1), EndedAt = DateTimeOffset.Now });
        // 异步等待耗时任务完成，期间保持界面线程可响应。
        var trajectory = await new TrajectoryPostprocessService().RunAsync(groupedTask, false, paths);
        Check(trajectory.State == ProcessRunState.Completed && File.Exists(Path.Combine(trajectory.TrajectoryResultDirectory, "trajectory_metrics.csv")),
            // 继续处理当前业务步骤，保持上下文状态一致。
            "Trajectory module reuses latest forward trajectory and completes postprocess");
        Check(Directory.GetDirectories(groupedTask, "01_*", SearchOption.TopDirectoryOnly).Length == 1 && Path.GetFileName(trajectory.ExecutionDirectory).StartsWith("05_"),
            "Trajectory module does not launch another 01 run and uses the unified flat layout");
        // 更新当前流程使用的数据，为下一处理步骤做好准备。
        var task = ReferenceTaskLoader.Load(paths.PackageRoot);
        string generated = new RequestGenerator().Generate(task);
        // 检查输入及依赖是否满足要求，提前阻止无效操作。
        new RequestGenerator().ValidateJson(generated);
        var reference = Read(Path.Combine(paths.PackageRoot, "02-智能预测", "03-模型文件", "surrogate_reference_request.json"));
        // 在持久化格式与内存对象之间转换，供后续流程继续使用。
        Check(Canonical(Json.DeserializeObject(generated)) == Canonical(reference), "Reference preset roundtrip equals all backend fields");
        var logs = new List<string>(); var progress = new List<string>();
        var prediction = new PredictionService();
        // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
        prediction.Log += x => { lock (logs) logs.Add(x.Text); }; prediction.Progress += x => progress.Add(x.State);
        var result = await prediction.RunAsync(task, "temperature", paths, default(CancellationToken), groupedTask);
        // 处理文件系统路径及数据，并在使用前确认目标有效。
        Console.WriteLine(Json.Serialize(result)); Write(Path.Combine(artifacts, "real02.json"), result);
        Check(result.State == ProcessRunState.Completed && result.ExitCode == 0, "Real02 temperature entry succeeds");
        // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
        Check(progress.Contains("temperature_completed") && progress.Contains("success"), "Real02 progress events read");
        Check(logs.Any(x => x.Contains("GUI_PROGRESS")), "Real02 stdout streamed");
        Check(Directory.Exists(result.ResultDirectory) && File.Exists(Path.Combine(result.ResultDirectory, "prediction_summary.json")), "Real02 result directory and envelope");
        // 检查输入及依赖是否满足要求，提前阻止无效操作。
        Check(BackendPathResolver.IsWithin(result.RequestPath, result.TaskDirectory) && BackendPathResolver.IsWithin(result.ResultDirectory, result.TaskDirectory), "Real02 request and result grouped by task");
        Check(File.Exists(Path.Combine(Path.GetDirectoryName(result.RequestPath), "run-location.json")) && result.EndedAt >= result.StartedAt && result.Module == "02", "Unified run record persisted");
        // 更新当前流程使用的数据，为下一处理步骤做好准备。
        string predictionDir = result.ResultDirectory;
        var second = await prediction.RunAsync(task, "temperature", paths, default(CancellationToken), groupedTask);
        // 检查输入及依赖是否满足要求，提前阻止无效操作。
        Check(second.State == ProcessRunState.Completed && second.RunId != result.RunId, "Repeated run permits mutable latest pointer and unique run ID");
        Check(second.TaskDirectory == result.TaskDirectory && Path.GetFileName(result.ExecutionDirectory).StartsWith("02_"), "Repeated module runs share selected task and use flat module-prefixed folders");
        var bad = await prediction.RunAsync(new TaskModel(), "temperature", paths);
        // 检查输入及依赖是否满足要求，提前阻止无效操作。
        Check(bad.State == ProcessRunState.Failed && bad.ExitCode != 0 && bad.ResultDirectory == null, "Real02 rejects incompatible default without adopting stale result");
        bad = await prediction.RunAsync(task, "unknown", paths);
        // 检查输入及依赖是否满足要求，提前阻止无效操作。
        Check(bad.State == ProcessRunState.Failed && bad.ExitCode == null, "Invalid prediction mode rejected before launch");
        string refDir = Path.Combine(artifacts, "SYNTHETIC_CONTRACT_FIXTURE", "reference");
        // 处理文件系统路径及数据，并在使用前确认目标有效。
        string candDir = Path.Combine(artifacts, "SYNTHETIC_CONTRACT_FIXTURE", "candidate");
        GenerateFixture(refDir); GenerateFixture(candDir);
        // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
        var similarity = new SimilarityEvaluationService(); int events = 0; similarity.Progress += x => events++;
        result = await similarity.RunAsync(refDir, candDir, null, paths, default(CancellationToken), groupedTask);
        Console.WriteLine(Json.Serialize(result)); Write(Path.Combine(artifacts, "real03.json"), result);
        // 检查输入及依赖是否满足要求，提前阻止无效操作。
        Check(result.State == ProcessRunState.Completed && result.ExitCode == 0, "Real03 evaluates explicitly synthetic contract fixtures");
        Check(File.Exists(Path.Combine(result.ResultDirectory, "similarity_components.csv")) && File.Exists(Path.Combine(result.ResultDirectory, "similarity_summary.json")), "Real03 output artifacts exist (no GUI metric parsing)");
        // 检查输入及依赖是否满足要求，提前阻止无效操作。
        Check(BackendPathResolver.IsWithin(result.RequestPath, result.TaskDirectory) && BackendPathResolver.IsWithin(result.ResultDirectory, result.TaskDirectory), "Real03 request and result grouped by task");
        Check(result.TaskDirectory == groupedTask && Path.GetFileName(result.ExecutionDirectory).StartsWith("03_"), "Different modules share one task result directory with module-prefixed runs");
        // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
        Check(events == 0, "Real03 has no GUI_PROGRESS; no invented percentage");
        Check(Read(result.RequestPath).ContainsKey("reference_result") && !Read(result.RequestPath).ContainsKey("CASE"), "03 records result references rather than physical request");
        bad = await similarity.RunAsync(refDir, predictionDir, null, paths);
        // 检查输入及依赖是否满足要求，提前阻止无效操作。
        Check(bad.State == ProcessRunState.Failed && bad.ExitCode == null && bad.Message.Contains("02"), "Raw02 prediction input rejected with contract explanation");
        bad = await similarity.RunAsync("missing", candDir, null, paths);
        // 检查输入及依赖是否满足要求，提前阻止无效操作。
        Check(bad.State == ProcessRunState.Failed && bad.ExitCode == null, "Missing input directory rejected");
        bad = await similarity.RunAsync(refDir, candDir, "missing-config.json", paths);
        // 检查输入及依赖是否满足要求，提前阻止无效操作。
        Check(bad.State == ProcessRunState.Failed && bad.ExitCode == null, "Missing evaluation config rejected");
        File.WriteAllText(Path.Combine(candDir, "infrared_response_history.csv"), "wrong\n1\n");
        bad = await similarity.RunAsync(refDir, candDir, null, paths);
        // 检查输入及依赖是否满足要求，提前阻止无效操作。
        Check(bad.State == ProcessRunState.Failed && bad.ExitCode == 2 && bad.ResultDirectory != null, "Authoritative03 column validation failure retains diagnostic run");
        var scene = new SceneBuildService();
        // 异步等待耗时任务完成，期间保持界面线程可响应。
        bad = await scene.RunAsync(new TaskModel(), 1, paths);
        Write(Path.Combine(artifacts, "real04-rejection.json"), bad);
        // 检查输入及依赖是否满足要求，提前阻止无效操作。
        Check(bad.State == ProcessRunState.Failed && bad.ExitCode == 2, "Real04 rejects incompatible physical task");
        bool reached = false;
        using (var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
        {
            scene.Progress += x => { if (x.State == "checking_reference_cache") { reached = true; cancel.Cancel(); } };
            // 异步等待耗时任务完成，期间保持界面线程可响应。
            result = await scene.RunAsync(task, 1, paths, cancel.Token);
        }
        Write(Path.Combine(artifacts, "real04-cancelled.json"), result);
        // 检查输入及依赖是否满足要求，提前阻止无效操作。
        Check(reached && result.State == ProcessRunState.Cancelled, "Real04 compatible request reaches internal chain and cancels via existing ProcessManager");
        var request = Read(result.RequestPath);
        // 处理文件系统路径及数据，并在使用前确认目标有效。
        Check((string)request["schema_version"] == "scene-search-request-v2" && File.Exists((string)request["input_file"]), "04 writes existing v2 request referencing shared forward input");
        Check(Convert.ToDouble(((Dictionary<string, object>)request["similarity_requirement"])["required_percent"]) == task.Settings.SimilarityIndex, "04 uses task acceptance threshold");
        // 遍历当前数据集合，逐项完成必要的转换或状态更新。
        foreach (string mode in new[] { "success", "incomplete", "missing", "mismatch", "proxy", "sleep" })
        {
            var fake = MakePackage(artifacts, mode);
            if (mode == "sleep")
            {
                // 异步等待耗时任务完成，期间保持界面线程可响应。
                var running = new SceneBuildService(); var active = running.RunAsync(task, 1, fake);
                bool rejected = false;
                // 进入可能失败的操作区间，并由后续异常分支统一处理故障。
                try { await new PredictionService().RunAsync(task, "temperature", fake); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "Global lease rejects parallel module execution"); running.Stop();
                // 异步等待耗时任务完成，期间保持界面线程可响应。
                Check((await active).State == ProcessRunState.Cancelled, "Stop during module preparation cancels");
                continue;
            }
            result = await new SceneBuildService().RunAsync(task, 1, fake);
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(mode == "success" ? result.State == ProcessRunState.Completed : result.State == ProcessRunState.Failed, "04 fixture completion policy: " + mode);
            if (result.ResultDirectory != null) Check(BackendPathResolver.IsWithin(result.ResultDirectory, result.TaskDirectory), "04 retained result grouped by task: " + mode);
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (mode == "incomplete") Check(result.ExitCode == 3 && result.ResultDirectory != null && result.Message.Contains("足量"), "Exit3 incomplete retains output without false success");
        }
        Check(original.Count == Directory.GetFiles(paths.PackageRoot, "*", SearchOption.AllDirectories).Length && original.All(x => RuntimePackage.Hash(x.Key) == x.Value), "Frozen backend all file hashes unchanged");
    }
    // 调用对应组件完成当前步骤，并保留产生的处理结果。
    static string Canonical(object value)
    {
        var dict = value as IDictionary<string, object>;
        if (dict != null) return "{" + String.Join(",", dict.OrderBy(x => x.Key).Select(x => x.Key + ":" + Canonical(x.Value))) + "}";
        // 校验当前条件，仅在满足业务约束时进入该处理分支。
        if (value is System.Collections.IEnumerable && !(value is string)) return "[" + String.Join(",", ((System.Collections.IEnumerable)value).Cast<object>().Select(Canonical)) + "]";
        if (value is decimal || value is double || value is int || value is long) return Convert.ToDouble(value).ToString("G17", CultureInfo.InvariantCulture);
        // 返回当前步骤生成的结果，并结束本次调用。
        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }
    static void GenerateFixture(string directory)
    {
        // 处理文件系统路径及数据，并在使用前确认目标有效。
        Directory.CreateDirectory(directory);
        var temp = new StringBuilder("time_s,T_1\n");
        var ir = new StringBuilder("case_id,frame_id,time_s,object_id,active_flag,released_flag,radiation_power_W,radiant_intensity_W_sr,detector_received_power_W,detector_irradiance_W_m2,screen_x_m,screen_y_m,in_screen_flag,range_to_detector_m\n");
        // 遍历当前数据集合，逐项完成必要的转换或状态更新。
        for (int i = 0; i <= 100; i++)
        { temp.AppendFormat(CultureInfo.InvariantCulture, "{0},{1}\n", i * 10, 300 + i * 0.1 + Math.Sin(i)); ir.AppendFormat(CultureInfo.InvariantCulture, "SYNTHETIC_TEST_ONLY,{0},{1},1,1,1,10,1,0.1,0.2,0,0,1,100\n", i, i * 10); }
        // 处理文件系统路径及数据，并在使用前确认目标有效。
        File.WriteAllText(Path.Combine(directory, "temperature_history.csv"), temp.ToString()); File.WriteAllText(Path.Combine(directory, "infrared_response_history.csv"), ir.ToString());
    }
    static BackendPaths MakePackage(string artifacts, string mode)
    {
        // 处理文件系统路径及数据，并在使用前确认目标有效。
        string root = Path.Combine(artifacts, "fixture-packages", mode); Directory.CreateDirectory(Path.Combine(root, "04", "program"));
        Write(Path.Combine(root, "config.json"), new { standard_entries = new Dictionary<string, string> { { "04_scene_search", "04/program/entry.txt" } } });
        // 处理文件系统路径及数据，并在使用前确认目标有效。
        File.WriteAllText(Path.Combine(root, "04", "program", "entry.txt"), mode);
        return new BackendPaths { PackageRoot = root, Python = Self, RuntimeRoot = Path.Combine(artifacts, "fixture-runtime", mode) };
    }
    static int Fixture(string[] args)
    {
        // 处理文件系统路径及数据，并在使用前确认目标有效。
        string mode = File.ReadAllText(args[2]); if (mode == "sleep") { Thread.Sleep(30000); return 0; }
        if (mode == "missing") return 0;
        // 处理文件系统路径及数据，并在使用前确认目标有效。
        string root = Path.Combine(Directory.GetParent(Path.GetDirectoryName(args[2])).FullName, "03-输出文件");
        string id = "run_fixture_" + Guid.NewGuid().ToString("N"), dir = Path.Combine(root, "runs", id);
        // 处理文件系统路径及数据，并在使用前确认目标有效。
        Write(Path.Combine(root, "latest_run.json"), new { run_id = id, run_dir = dir, status = mode == "incomplete" ? "incomplete" : "success" });
        Write(Path.Combine(dir, "scene_search_summary.json"), new { run_id = mode == "mismatch" ? "other" : id, status = mode == "proxy" ? "ProxyOnlyCompleted" : "Completed" });
        Console.WriteLine("GUI_PROGRESS {\"state\":\"finished\"}"); return mode == "incomplete" ? 3 : 0;
    }
}
