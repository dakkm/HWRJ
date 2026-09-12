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
    static int passed;
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 33554432 };
    static readonly string Self = System.Reflection.Assembly.GetExecutingAssembly().Location;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS " + message); passed++; }
    static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        if (args.Length > 0 && args[0] == "-B") return Fixture(args);
        try { Run(args).GetAwaiter().GetResult(); Console.WriteLine("TOTAL " + passed + " passed"); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static void Write(string path, object value) { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, Json.Serialize(value), new UTF8Encoding(false)); }
    static Dictionary<string, object> Read(string path) => Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
    static async Task Run(string[] args)
    {
        string artifacts = Path.GetFullPath(args[0]); Directory.CreateDirectory(artifacts);
        var paths = new BackendPaths { PackageRoot = Path.GetFullPath(args[1]), Python = args[2], RuntimeRoot = Path.Combine(artifacts, "runtime") };
        var original = Directory.GetFiles(paths.PackageRoot, "*", SearchOption.AllDirectories).ToDictionary(x => x, RuntimePackage.Hash);
        var task = ReferenceTaskLoader.Load(paths.PackageRoot);
        string generated = new RequestGenerator().Generate(task);
        new RequestGenerator().ValidateJson(generated);
        var reference = Read(Path.Combine(paths.PackageRoot, "02-智能预测", "03-模型文件", "surrogate_reference_request.json"));
        Check(Canonical(Json.DeserializeObject(generated)) == Canonical(reference), "Reference preset roundtrip equals all backend fields");
        var logs = new List<string>(); var progress = new List<string>();
        var prediction = new PredictionService();
        prediction.Log += x => { lock (logs) logs.Add(x.Text); }; prediction.Progress += x => progress.Add(x.State);
        var result = await prediction.RunAsync(task, "temperature", paths);
        Console.WriteLine(Json.Serialize(result)); Write(Path.Combine(artifacts, "real02.json"), result);
        Check(result.State == ProcessRunState.Completed && result.ExitCode == 0, "Real02 temperature entry succeeds");
        Check(progress.Contains("temperature_completed") && progress.Contains("success"), "Real02 progress events read");
        Check(logs.Any(x => x.Contains("GUI_PROGRESS")), "Real02 stdout streamed");
        Check(Directory.Exists(result.ResultDirectory) && File.Exists(Path.Combine(result.ResultDirectory, "prediction_summary.json")), "Real02 result directory and envelope");
        Check(File.Exists(Path.Combine(Path.GetDirectoryName(result.RequestPath), "run-location.json")) && result.EndedAt >= result.StartedAt && result.Module == "02", "Unified run record persisted");
        string predictionDir = result.ResultDirectory;
        var second = await prediction.RunAsync(task, "temperature", paths);
        Check(second.State == ProcessRunState.Completed && second.RunId != result.RunId, "Repeated run permits mutable latest pointer and unique run ID");
        var bad = await prediction.RunAsync(new TaskModel(), "temperature", paths);
        Check(bad.State == ProcessRunState.Failed && bad.ExitCode != 0 && bad.ResultDirectory == null, "Real02 rejects incompatible default without adopting stale result");
        bad = await prediction.RunAsync(task, "unknown", paths);
        Check(bad.State == ProcessRunState.Failed && bad.ExitCode == null, "Invalid prediction mode rejected before launch");
        string refDir = Path.Combine(artifacts, "SYNTHETIC_CONTRACT_FIXTURE", "reference");
        string candDir = Path.Combine(artifacts, "SYNTHETIC_CONTRACT_FIXTURE", "candidate");
        GenerateFixture(refDir); GenerateFixture(candDir);
        var similarity = new SimilarityEvaluationService(); int events = 0; similarity.Progress += x => events++;
        result = await similarity.RunAsync(refDir, candDir, null, paths);
        Console.WriteLine(Json.Serialize(result)); Write(Path.Combine(artifacts, "real03.json"), result);
        Check(result.State == ProcessRunState.Completed && result.ExitCode == 0, "Real03 evaluates explicitly synthetic contract fixtures");
        Check(File.Exists(Path.Combine(result.ResultDirectory, "similarity_components.csv")) && File.Exists(Path.Combine(result.ResultDirectory, "similarity_summary.json")), "Real03 output artifacts exist (no GUI metric parsing)");
        Check(events == 0, "Real03 has no GUI_PROGRESS; no invented percentage");
        Check(Read(result.RequestPath).ContainsKey("reference_result") && !Read(result.RequestPath).ContainsKey("CASE"), "03 records result references rather than physical request");
        bad = await similarity.RunAsync(refDir, predictionDir, null, paths);
        Check(bad.State == ProcessRunState.Failed && bad.ExitCode == null && bad.Message.Contains("02"), "Raw02 prediction input rejected with contract explanation");
        bad = await similarity.RunAsync("missing", candDir, null, paths);
        Check(bad.State == ProcessRunState.Failed && bad.ExitCode == null, "Missing input directory rejected");
        bad = await similarity.RunAsync(refDir, candDir, "missing-config.json", paths);
        Check(bad.State == ProcessRunState.Failed && bad.ExitCode == null, "Missing evaluation config rejected");
        File.WriteAllText(Path.Combine(candDir, "infrared_response_history.csv"), "wrong\n1\n");
        bad = await similarity.RunAsync(refDir, candDir, null, paths);
        Check(bad.State == ProcessRunState.Failed && bad.ExitCode == 2 && bad.ResultDirectory != null, "Authoritative03 column validation failure retains diagnostic run");
        var scene = new SceneBuildService();
        bad = await scene.RunAsync(new TaskModel(), 1, paths);
        Write(Path.Combine(artifacts, "real04-rejection.json"), bad);
        Check(bad.State == ProcessRunState.Failed && bad.ExitCode == 2, "Real04 rejects incompatible physical task");
        bool reached = false;
        using (var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
        {
            scene.Progress += x => { if (x.State == "checking_reference_cache") { reached = true; cancel.Cancel(); } };
            result = await scene.RunAsync(task, 1, paths, cancel.Token);
        }
        Write(Path.Combine(artifacts, "real04-cancelled.json"), result);
        Check(reached && result.State == ProcessRunState.Cancelled, "Real04 compatible request reaches internal chain and cancels via existing ProcessManager");
        var request = Read(result.RequestPath);
        Check((string)request["schema_version"] == "scene-search-request-v2" && File.Exists((string)request["input_file"]), "04 writes existing v2 request referencing shared forward input");
        Check(Convert.ToDouble(((Dictionary<string, object>)request["similarity_requirement"])["required_percent"]) == task.Settings.SimilarityIndex, "04 uses task acceptance threshold");
        foreach (string mode in new[] { "success", "incomplete", "missing", "mismatch", "proxy", "sleep" })
        {
            var fake = MakePackage(artifacts, mode);
            if (mode == "sleep")
            {
                var running = new SceneBuildService(); var active = running.RunAsync(task, 1, fake);
                bool rejected = false;
                try { await new PredictionService().RunAsync(task, "temperature", fake); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "Global lease rejects parallel module execution"); running.Stop();
                Check((await active).State == ProcessRunState.Cancelled, "Stop during module preparation cancels");
                continue;
            }
            result = await new SceneBuildService().RunAsync(task, 1, fake);
            Check(mode == "success" ? result.State == ProcessRunState.Completed : result.State == ProcessRunState.Failed, "04 fixture completion policy: " + mode);
            if (mode == "incomplete") Check(result.ExitCode == 3 && result.ResultDirectory != null && result.Message.Contains("足量"), "Exit3 incomplete retains output without false success");
        }
        Check(original.Count == Directory.GetFiles(paths.PackageRoot, "*", SearchOption.AllDirectories).Length && original.All(x => RuntimePackage.Hash(x.Key) == x.Value), "Frozen backend all file hashes unchanged");
    }
    static string Canonical(object value)
    {
        var dict = value as IDictionary<string, object>;
        if (dict != null) return "{" + String.Join(",", dict.OrderBy(x => x.Key).Select(x => x.Key + ":" + Canonical(x.Value))) + "}";
        if (value is System.Collections.IEnumerable && !(value is string)) return "[" + String.Join(",", ((System.Collections.IEnumerable)value).Cast<object>().Select(Canonical)) + "]";
        if (value is decimal || value is double || value is int || value is long) return Convert.ToDouble(value).ToString("G17", CultureInfo.InvariantCulture);
        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }
    static void GenerateFixture(string directory)
    {
        Directory.CreateDirectory(directory);
        var temp = new StringBuilder("time_s,T_1\n");
        var ir = new StringBuilder("case_id,frame_id,time_s,object_id,active_flag,released_flag,radiation_power_W,radiant_intensity_W_sr,detector_received_power_W,detector_irradiance_W_m2,screen_x_m,screen_y_m,in_screen_flag,range_to_detector_m\n");
        for (int i = 0; i <= 100; i++)
        { temp.AppendFormat(CultureInfo.InvariantCulture, "{0},{1}\n", i * 10, 300 + i * 0.1 + Math.Sin(i)); ir.AppendFormat(CultureInfo.InvariantCulture, "SYNTHETIC_TEST_ONLY,{0},{1},1,1,1,10,1,0.1,0.2,0,0,1,100\n", i, i * 10); }
        File.WriteAllText(Path.Combine(directory, "temperature_history.csv"), temp.ToString()); File.WriteAllText(Path.Combine(directory, "infrared_response_history.csv"), ir.ToString());
    }
    static BackendPaths MakePackage(string artifacts, string mode)
    {
        string root = Path.Combine(artifacts, "fixture-packages", mode); Directory.CreateDirectory(Path.Combine(root, "04", "program"));
        Write(Path.Combine(root, "config.json"), new { standard_entries = new Dictionary<string, string> { { "04_scene_search", "04/program/entry.txt" } } });
        File.WriteAllText(Path.Combine(root, "04", "program", "entry.txt"), mode);
        return new BackendPaths { PackageRoot = root, Python = Self, RuntimeRoot = Path.Combine(artifacts, "fixture-runtime", mode) };
    }
    static int Fixture(string[] args)
    {
        string mode = File.ReadAllText(args[2]); if (mode == "sleep") { Thread.Sleep(30000); return 0; }
        if (mode == "missing") return 0;
        string root = Path.Combine(Directory.GetParent(Path.GetDirectoryName(args[2])).FullName, "03-输出文件");
        string id = "run_fixture_" + Guid.NewGuid().ToString("N"), dir = Path.Combine(root, "runs", id);
        Write(Path.Combine(root, "latest_run.json"), new { run_id = id, run_dir = dir, status = mode == "incomplete" ? "incomplete" : "success" });
        Write(Path.Combine(dir, "scene_search_summary.json"), new { run_id = mode == "mismatch" ? "other" : id, status = mode == "proxy" ? "ProxyOnlyCompleted" : "Completed" });
        Console.WriteLine("GUI_PROGRESS {\"state\":\"finished\"}"); return mode == "incomplete" ? 3 : 0;
    }
}
