using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
    private static int passed;
    private static readonly string Self = System.Reflection.Assembly.GetExecutingAssembly().Location;
    private static string artifacts;
    private static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        if (args.Length > 0 && args[0] == "--fixture") return Fixture(args.Skip(1).ToArray());
        if (args.Length > 0 && args[0] == "-B") return EntryFixture(args);
        try { Run(args).GetAwaiter().GetResult(); Console.WriteLine("TOTAL " + passed + " passed"); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static int EntryFixture(string[] args)
    {
        if (args.Contains("-c")) { Console.WriteLine("fixture environment ready"); return Path.GetFileName(Self).StartsWith("bad-environment") ? 8 : 0; }
        string mode = File.ReadAllText(args[2]);
        string root = args[Array.IndexOf(args, "--run-root") + 1];
        string directory = Path.Combine(root, "run_fixture_" + Guid.NewGuid().ToString("N"));
        Console.WriteLine("GUI_PROGRESS " + new JavaScriptSerializer().Serialize(new { module = "01", state = "input_ready", timestamp = 123.5, run_id = Path.GetFileName(directory), run_dir = directory }));
        Console.Out.Flush();
        if (mode == "missing-directory") return 0;
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "result.json"), new JavaScriptSerializer().Serialize(new { status = "prepared", return_code = 0, run_id = Path.GetFileName(directory), output_dir = Path.Combine(directory, "output"), request_json = "unused" }));
        if (mode == "missing-output") return 0;
        return Fixture(new[] { "tree" });
    }
    private static int Fixture(string[] args)
    {
        if (args[0] == "sleep") { Thread.Sleep(60000); return 0; }
        if (args[0] == "tree" || args[0] == "orphan")
        {
            using (var child = Process.Start(new ProcessStartInfo(Self, "--fixture sleep") { UseShellExecute = false, CreateNoWindow = true }))
            { Console.WriteLine("CHILD=" + child.Id); Console.Out.Flush(); if (args[0] == "tree") Thread.Sleep(60000); }
            return 0;
        }
        if (args[0] == "echo") { Console.WriteLine(new JavaScriptSerializer().Serialize(args.Skip(1).ToArray())); return 0; }
        Console.WriteLine("stdout 中文"); Console.Error.WriteLine("stderr 告警");
        Console.WriteLine("GUI_PROGRESS {\"module\":\"test\",\"state\":\"unrecognized_future_state\",\"timestamp\":123.5,\"run_id\":\"sample\",\"run_dir\":\"sample/output\",\"extra\":42}");
        Console.WriteLine("GUI_PROGRESS {broken}");
        if (args[0] == "flood") for (int i = 0; i < 2000; i++) { Console.WriteLine("out" + i); Console.Error.WriteLine("err" + i); }
        return args[0] == "fail" ? 7 : 0;
    }
    private static ProcessRunRequest Request(string mode)
    { return new ProcessRunRequest { Executable = Self, Arguments = new List<string> { "--fixture", mode }, WorkingDirectory = artifacts, Timeout = TimeSpan.FromSeconds(15) }; }
    private static void Check(bool value, string text) { if (!value) throw new Exception(text); passed++; Console.WriteLine("PASS " + text); }
    private static bool Alive(int id) { try { using (var p = Process.GetProcessById(id)) return !p.HasExited; } catch (ArgumentException) { return false; } }
    private static async Task WaitUntil(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!condition()) { if (clock.ElapsedMilliseconds > 10000) throw new TimeoutException("test condition"); await Task.Delay(25); }
    }
    private static async Task Run(string[] args)
    {
        artifacts = Path.GetFullPath(args[0]); Directory.CreateDirectory(artifacts);
        string package = Path.GetFullPath(args[1]); string python = Path.GetFullPath(args[2]);
        var manager = new ProcessManager(); var logs = new ConcurrentQueue<ProcessLogEvent>(); var progress = new ConcurrentQueue<ProcessProgressEvent>();
        manager.Log += logs.Enqueue; manager.Progress += progress.Enqueue;
        var normal = await manager.RunAsync(Request("normal"));
        Check(normal.State == ProcessRunState.Completed && normal.ExitCode == 0, "Normal start and exit");
        Check(logs.Any(x => !x.IsError && x.Text == "stdout 中文"), "Unicode stdout");
        Check(logs.Any(x => x.IsError && x.Text == "stderr 告警"), "Unicode stderr does not force failure");
        Check(progress.Count == 1 && progress.First().State == "unrecognized_future_state" && progress.First().Timestamp == 123.5, "Structured progress and unknown state");
        Check(normal.RunId == "sample" && normal.ResultDirectory == "sample/output", "Generic run location");
        ProcessProgressEvent parsed;
        Check(!GuiProgressParser.TryParse("GUI_PROGRESS {broken}", out parsed), "Malformed progress ignored");
        Check(!GuiProgressParser.TryParse("GUI_PROGRESS []", out parsed), "Non-object progress ignored");
        Check(!GuiProgressParser.TryParse("ordinary log", out parsed), "Ordinary stdout not a lifecycle signal");
        var bad = await manager.RunAsync(Request("fail"));
        Check(bad.State == ProcessRunState.Failed && bad.ExitCode == 7, "Nonzero exit");
        logs = new ConcurrentQueue<ProcessLogEvent>(); manager.Log += logs.Enqueue;
        var flood = await manager.RunAsync(Request("flood"));
        Check(flood.State == ProcessRunState.Completed && logs.Count == 4004, "Simultaneous stdout stderr drain without deadlock");
        var quoting = Request("echo");
        string[] values = { "with space", "中文路径", "ends\\", "embedded\"quote", "", "a&b|c%PATH%" };
        foreach (string value in values) quoting.Arguments.Add(value);
        string echo = null; Action<ProcessLogEvent> capture = item => { if (item.Text.StartsWith("[")) echo = item.Text; };
        manager.Log += capture; await manager.RunAsync(quoting); manager.Log -= capture;
        Check(new JavaScriptSerializer().Deserialize<string[]>(echo).SequenceEqual(values), "Argument quoting no shell expansion");
        int childId = 0;
        Action<ProcessLogEvent> capturePid = item => { if (item.Text.StartsWith("CHILD=")) childId = Int32.Parse(item.Text.Substring(6)); };
        manager.Log += capturePid;
        var running = manager.RunAsync(Request("tree"));
        await WaitUntil(() => childId > 0);
        Check(!running.IsCompleted && Alive(childId), "Live child observed while async call remains pending");
        bool duplicate = false; try { await manager.RunAsync(Request("normal")); } catch (InvalidOperationException) { duplicate = true; }
        Check(duplicate, "Concurrent run rejected");
        manager.Stop(); var stopped = await running;
        await WaitUntil(() => !Alive(childId));
        Check(stopped.State == ProcessRunState.Cancelled && !Alive(stopped.ProcessId.Value) && !Alive(childId), "Stop terminates root and child");
        childId = 0;
        var orphan = await manager.RunAsync(Request("orphan"));
        await WaitUntil(() => childId > 0 && !Alive(childId));
        Check(orphan.State == ProcessRunState.Completed && !Alive(childId), "Normal root exit cleans residual child");
        manager.Log -= capturePid;
        var timeout = Request("sleep"); timeout.Timeout = TimeSpan.FromMilliseconds(300);
        var timed = await manager.RunAsync(timeout);
        Check(timed.State == ProcessRunState.Failed && timed.TimedOut && !Alive(timed.ProcessId.Value), "Timeout cleans process");
        using (var cancelled = new CancellationTokenSource())
        { cancelled.Cancel(); var result = await manager.RunAsync(Request("normal"), cancelled.Token); Check(result.State == ProcessRunState.Cancelled && !result.ProcessId.HasValue, "Cancellation before launch"); }
        var missing = Request("normal"); missing.Executable = Path.Combine(artifacts, "missing.exe");
        Check((await manager.RunAsync(missing)).State == ProcessRunState.Failed, "Missing executable handled");
        var invalidExecutable = Path.Combine(artifacts, "not-executable.exe"); File.WriteAllText(invalidExecutable, "not PE");
        missing.Executable = invalidExecutable;
        Check((await manager.RunAsync(missing)).State == ProcessRunState.Failed, "Native start failure handled");
        var badDirectory = Request("normal"); badDirectory.WorkingDirectory = Path.Combine(artifacts, "absent");
        Check((await manager.RunAsync(badDirectory)).State == ProcessRunState.Failed, "Missing working directory handled");
        Check(BackendPathResolver.FindExecutable(Self, artifacts) == Self, "Explicit executable resolution");
        var resolved = new BackendPathResolver().Resolve();
        Check(resolved.PackageRoot == package && File.Exists(resolved.Entry) && !BackendPathResolver.IsWithin(resolved.RuntimeRoot, package), "App-relative backend discovery and external runtime root");
        var service = new ForwardSimulationService(); var serviceLogs = new ConcurrentQueue<ProcessLogEvent>();
        service.Log += serviceLogs.Enqueue;
        BackendPaths Paths(string root) => new BackendPaths { PackageRoot = package, Entry = Path.Combine(package, "01-正向仿真", "02-程序", "forward_simulation_runner.py"), Python = python, RuntimeRoot = root };
        var paths = Paths(Path.Combine(artifacts, "真实联调 with spaces"));
        var prepared = await service.RunAsync(new TaskModel(), true, paths);
        File.WriteAllText(Path.Combine(artifacts, "prepare-record.json"), new JavaScriptSerializer().Serialize(prepared));
        Check(prepared.State == ProcessRunState.Completed && prepared.ExitCode == 0, "Real 01 prepare-only through RequestGenerator and ProcessManager: " + prepared.Message);
        Check(Directory.Exists(prepared.ResultDirectory) && File.Exists(Path.Combine(prepared.RunDirectory, "input.dat")), "Backend-created run_id and output directory");
        Check(Directory.Exists(prepared.TaskDirectory) && BackendPathResolver.IsWithin(prepared.RequestPath, prepared.TaskDirectory) &&
            BackendPathResolver.IsWithin(prepared.RunDirectory, prepared.TaskDirectory) &&
            File.Exists(Path.Combine(prepared.ExecutionDirectory, "stdout.log")) && File.Exists(Path.Combine(prepared.ExecutionDirectory, "stderr.log")),
            "Request logs and results grouped in one task directory");
        Check(serviceLogs.Any(x => x.Text.StartsWith("GUI_PROGRESS ") && x.Text.Contains("prepared")), "Real GUI_PROGRESS captured");
        Check(File.Exists(Path.Combine(Path.GetDirectoryName(prepared.RequestPath), "run-location.json")), "Run metadata persisted");
        Check(!File.Exists(Path.Combine(prepared.RunDirectory, "stdout.txt")), "Prepare-only did not execute solver");
        Check((await service.RunAsync(null, true, paths)).State == ProcessRunState.Failed, "Request generation failure handled");
        var noPython = Paths(paths.RuntimeRoot); noPython.Python = Path.Combine(artifacts, "absent-python.exe");
        Check((await service.RunAsync(new TaskModel(), true, noPython)).Message.Contains("Python"), "Missing Python business message");
        var noEntry = Paths(paths.RuntimeRoot); noEntry.Entry += ".absent";
        Check((await service.RunAsync(new TaskModel(), true, noEntry)).State == ProcessRunState.Failed, "Missing entry handled");
        Check((await service.RunAsync(new TaskModel(), true, Paths(package))).State == ProcessRunState.Failed, "Frozen package cannot be runtime root");
        // Actual backend rejection, using its real CLI; no Python source edits.
        string invalidJson = Path.Combine(artifacts, "invalid-request.json"); File.WriteAllText(invalidJson, "{}");
        var reject = new ProcessRunRequest { Executable = python, WorkingDirectory = package, Arguments = new List<string>
            { "-B", "-u", paths.Entry, "--params-json", invalidJson, "--run-root", paths.RuntimeRoot, "--prepare-only" },
            EnvironmentVariables = new Dictionary<string, string> { { "PYTHONUTF8", "1" }, { "PYTHONDONTWRITEBYTECODE", "1" } } };
        var rejected = await manager.RunAsync(reject);
        Check(rejected.ExitCode == 2 && rejected.State == ProcessRunState.Failed, "Actual backend request rejection exit code 2");
        var simulated = Paths(Path.Combine(artifacts, "simulated")); simulated.Python = Self;
        simulated.Entry = Path.Combine(artifacts, "simulated-entry.txt");
        File.WriteAllText(simulated.Entry, "missing-directory");
        Check((await service.RunAsync(new TaskModel(), true, simulated)).State == ProcessRunState.Failed, "Exit zero without run directory rejected");
        File.WriteAllText(simulated.Entry, "missing-output");
        Check((await service.RunAsync(new TaskModel(), true, simulated)).State == ProcessRunState.Failed, "Missing result directory rejected");
        File.WriteAllText(simulated.Entry, "tree");
        int serviceChild = 0;
        service.Log += item => { if (item.Text.StartsWith("CHILD=")) serviceChild = Int32.Parse(item.Text.Substring(6)); };
        var longRun = service.RunAsync(new TaskModel(), true, simulated);
        await WaitUntil(() => serviceChild > 0);
        bool serviceDuplicate = false;
        try { await service.RunAsync(new TaskModel(), true, simulated); } catch (InvalidOperationException) { serviceDuplicate = true; }
        Check(serviceDuplicate, "Service duplicate request rejected");
        service.Stop(); var cancelledService = await longRun;
        await WaitUntil(() => !Alive(serviceChild));
        Check(cancelledService.State == ProcessRunState.Cancelled && !Alive(serviceChild), "Service stop reaches entire child tree");
        string badEnvironment = Path.Combine(Path.GetDirectoryName(Self), "bad-environment.exe");
        File.Copy(Self, badEnvironment, true); simulated.Python = badEnvironment;
        Check((await service.RunAsync(new TaskModel(), true, simulated)).Message.Contains("Python"), "Python dependency probe failure surfaced");
    }
}
