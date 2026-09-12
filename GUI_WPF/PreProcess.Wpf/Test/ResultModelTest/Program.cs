using System;
using System.IO;
using PreProcess.Wpf.Models.Results;
using PreProcess.Wpf.Services.Execution;
using PreProcess.Wpf.Services.Process;
using PreProcess.Wpf.Services.Results;

namespace ResultModelTest
{
    internal static class Program
    {
        private static int passed;

        private static int Main()
        {
            string root = Path.Combine(Path.GetTempPath(), "PreProcess-P5_1-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                TestGivenDirectory(root);
                TestMissingDirectory(root);
                TestModuleDistinction(root);
                TestP4RunRecordAssociation(root);
                TestModelContainers();
                TestValidation(root);
                Console.WriteLine("P5.1 ResultModelTest: " + passed + " assertions passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void TestGivenDirectory(string root)
        {
            var manager = new ResultDirectoryManager();
            string run = Path.Combine(root, "runs", "run_forward");
            string output = Path.Combine(run, "output");
            string request = Path.Combine(root, "requests", "request.json");
            Directory.CreateDirectory(output);
            Directory.CreateDirectory(Path.GetDirectoryName(request));
            File.WriteAllText(request, "{}");

            RunResult result = manager.Create(ResultModuleType.Forward, "run_forward", request, output, run);
            Check(result.RunId == "run_forward", "given directory keeps run_id");
            Check(result.ModuleType == ResultModuleType.Forward && result.ModuleCode == "01", "given directory keeps module");
            Check(result.InputRequestPath == Path.GetFullPath(request), "given directory keeps request path");
            Check(result.OutputDirectory == Path.GetFullPath(output), "given directory keeps output path");
            Check(result.ResultExists && manager.ResultExists(result), "given directory is available");
        }

        private static void TestMissingDirectory(string root)
        {
            var manager = new ResultDirectoryManager();
            RunResult result = manager.Create(ResultModuleType.Prediction, "run_missing", null,
                Path.Combine(root, "missing", "run_missing"));
            Check(!result.ResultExists, "missing directory is unavailable");
            Check(result.AvailabilityMessage.Contains("结果目录不存在"), "missing directory has user-facing message");
            Check(!manager.ResultExists(result), "missing directory live check is false");
        }

        private static void TestModuleDistinction(string root)
        {
            var manager = new ResultDirectoryManager();
            string runsRoot = Path.Combine(root, "module-runs");
            Directory.CreateDirectory(Path.Combine(runsRoot, "run_01", "output"));
            Directory.CreateDirectory(Path.Combine(runsRoot, "run_02"));
            Directory.CreateDirectory(Path.Combine(runsRoot, "run_03"));
            Directory.CreateDirectory(Path.Combine(runsRoot, "run_04"));
            Directory.CreateDirectory(Path.Combine(runsRoot, "run_trajectory", "output"));

            RunResult forward = manager.Locate(ResultModuleType.Forward, "run_01", null, runsRoot);
            RunResult prediction = manager.Locate(ResultModuleType.Prediction, "run_02", null, runsRoot);
            RunResult similarity = manager.Locate(ResultModuleType.Similarity, "run_03", null, runsRoot);
            RunResult scene = manager.Locate(ResultModuleType.Scene, "run_04", null, runsRoot);
            RunResult trajectory = manager.Locate(ResultModuleType.Trajectory, "run_trajectory", null, runsRoot);

            Check(forward.ModuleCode == "01" && prediction.ModuleCode == "02" && similarity.ModuleCode == "03" && scene.ModuleCode == "04", "backend modules keep distinct codes");
            Check(trajectory.ModuleType == ResultModuleType.Trajectory && trajectory.ModuleCode == "01", "trajectory remains a distinct GUI type using backend 01");
            Check(Path.GetFileName(forward.OutputDirectory) == "output" && Path.GetFileName(trajectory.OutputDirectory) == "output", "01 result uses nested output directory");
            Check(prediction.OutputDirectory == prediction.RunDirectory && similarity.OutputDirectory == similarity.RunDirectory && scene.OutputDirectory == scene.RunDirectory, "02/03/04 result uses run directory");
            Check(forward.ResultExists && prediction.ResultExists && similarity.ResultExists && scene.ResultExists && trajectory.ResultExists, "all module fixtures are located");
        }

        private static void TestP4RunRecordAssociation(string root)
        {
            var manager = new ResultDirectoryManager();
            string request = Path.Combine(root, "requests", "p4.json");
            string run = Path.Combine(root, "p4", "run_02");
            Directory.CreateDirectory(run);
            var record = new RunRecord
            {
                Module = "02", RunId = "run_02", RequestPath = request,
                RunDirectory = run, ResultDirectory = run,
                StartedAt = DateTimeOffset.Now.AddSeconds(-1), EndedAt = DateTimeOffset.Now,
                State = ProcessRunState.Completed
            };
            RunResult result = manager.Create(record);
            Check(result.ModuleType == ResultModuleType.Prediction, "P4 module maps to prediction");
            Check(result.RunId == record.RunId && result.OutputDirectory == Path.GetFullPath(record.ResultDirectory), "P4 paths are associated");
            Check(result.RunState == "Completed" && result.StartedAt == record.StartedAt && result.EndedAt == record.EndedAt, "P4 execution metadata is retained");

            string forwardRun = Path.Combine(root, "p4", "run_trajectory");
            Directory.CreateDirectory(Path.Combine(forwardRun, "output"));
            RunResult trajectory = manager.Create(new RunRecord { Module = "01", RunId = "run_trajectory", RunDirectory = forwardRun }, ResultModuleType.Trajectory);
            Check(trajectory.ModuleType == ResultModuleType.Trajectory && trajectory.ResultExists, "P4 backend 01 can be classified as trajectory");
        }

        private static void TestModelContainers()
        {
            var run = new RunResult();
            var temperature = new TemperatureResult { Name = "temperature" };
            var table = new ResultTable { Name = "samples", SourcePath = "unparsed-source" };
            table.Columns.Add(new ResultColumn { Key = "confirmed_later", DisplayName = "P5.2确认字段" });
            var row = new ResultRow();
            row.Values["confirmed_later"] = 1.0;
            table.Rows.Add(row);
            temperature.Tables.Add(table);
            run.Temperatures.Add(temperature);
            run.Trajectories.Add(new TrajectoryResult());
            run.InfraredResponses.Add(new InfraredResult());
            run.Similarities.Add(new SimilarityResult());
            run.Scenes.Add(new SceneResult());
            Check(run.Temperatures.Count == 1 && run.Trajectories.Count == 1 && run.InfraredResponses.Count == 1, "physical result collections are initialized");
            Check(run.Similarities.Count == 1 && run.Scenes.Count == 1, "module result collections are initialized");
            Check(run.Temperatures[0].Tables[0].Rows[0].Values.ContainsKey("confirmed_later"), "format-neutral table holds reader data");
        }

        private static void TestValidation(string root)
        {
            var manager = new ResultDirectoryManager();
            Expect<ArgumentException>(() => manager.Locate(ResultModuleType.Forward, "..", null, root), "path traversal run_id rejected");
            Expect<ArgumentException>(() => manager.ParseModule("05"), "unknown module rejected");
            Expect<ArgumentException>(() => manager.Create(new RunRecord { Module = "02", RunId = "run_x", ResultDirectory = root }, ResultModuleType.Scene), "incompatible P4 module override rejected");
            Check(manager.ParseModule("轨迹") == ResultModuleType.Trajectory, "trajectory alias parsed");
        }

        private static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FAILED: " + name);
            passed++;
            Console.WriteLine("PASS " + name);
        }

        private static void Expect<T>(Action action, string name) where T : Exception
        {
            try { action(); }
            catch (T) { Check(true, name); return; }
            throw new InvalidOperationException("FAILED: " + name);
        }
    }
}
