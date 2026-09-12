using System;
using System.IO;
using System.Linq;
using PreProcess.Wpf.Models.Results;
using PreProcess.Wpf.Services.Execution;
using PreProcess.Wpf.Services.Process;
using PreProcess.Wpf.Services.Results;

namespace ResultReaderTest
{
    internal static class Program
    {
        private static int passed;

        private static int Main(string[] args)
        {
            if (args.Length != 1) { Console.Error.WriteLine("Usage: ResultReaderTest <project-root>"); return 2; }
            string root = Path.GetFullPath(args[0]);
            string temporary = Path.Combine(Path.GetTempPath(), "PreProcess-P5_2-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(temporary);
                TestForward(root);
                TestPrediction(root);
                TestSimilarity(root);
                TestScene(root);
                TestMissingAndMalformed(temporary);
                Console.WriteLine("P5.2 ResultReaderTest: " + passed + " assertions passed.");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
            finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
        }

        private static void TestForward(string root)
        {
            string output = FindRunFile(root, "run_20260911_131206_54dd28bb", "temperature_history.csv").DirectoryName;
            string runDirectory = Directory.GetParent(output).FullName;
            string request = Path.Combine(runDirectory, "request.json");
            var record = new RunRecord { Module = "01", RunId = "run_20260911_131206_54dd28bb", RequestPath = request,
                RunDirectory = runDirectory, ResultDirectory = output, State = ProcessRunState.Completed };
            RunResult result = new ResultReader().Read(record);
            Check(result.ModuleType == ResultModuleType.Forward && result.InputRequestPath == Path.GetFullPath(request), "P4 forward record association");
            Check(result.Temperatures.Count == 1 && result.Temperatures[0].Tables[0].Rows.Count == 78, "real 01 temperature rows");
            Check(result.Trajectories.Count == 1 && result.Trajectories[0].Tables[0].Rows.Count == 1248, "real 01 trajectory rows");
            Check(result.InfraredResponses.Count == 1 && result.InfraredResponses[0].Tables[0].Rows.Count == 128, "real 01 infrared rows");
            Check((long)result.Temperatures[0].Tables[0].Rows[0].Values["frame"] == 0L, "real 01 typed frame");
            Check((double)result.Temperatures[0].Tables[0].Rows[0].Values["time_s"] == 0.0, "real 01 typed time");
            Check(result.Temperatures[0].Tables[0].Columns.First(x => x.Key == "T_1").Unit == "K", "01 unit metadata");

            RunResult trajectory = new ForwardResultReader().Read(record, ResultModuleType.Trajectory);
            Check(trajectory.ModuleType == ResultModuleType.Trajectory && trajectory.Trajectories.Count == 1, "01 record trajectory classification");
        }

        private static void TestPrediction(string root)
        {
            string tempRun = FindRunFile(root, "run_20260910_205207_f10e224c", "prediction_summary.json").DirectoryName;
            RunResult temperature = new ResultReader().Read(new RunRecord { Module = "02", RunId = "run_20260910_205207_f10e224c",
                RunDirectory = tempRun, ResultDirectory = tempRun, State = ProcessRunState.Completed });
            Check(temperature.Temperatures.Count == 1 && temperature.Temperatures[0].Tables[0].Rows.Count == 101, "real 02 temperature rows");
            Check(temperature.InfraredResponses.Count == 0 && temperature.Issues.Count == 0, "02 temperature mode optional files");
            Check(temperature.Summary["prediction.mode"] == "temperature", "02 summary mode");
            Check(temperature.ModuleType == ResultModuleType.Prediction && temperature.RunState == "Completed", "P4 prediction record association");

            string bothRun = FindRunFile(root, "run_20260910_205238_9608c9ba", "prediction_summary.json").DirectoryName;
            RunResult both = new PredictionResultReader().Read(new ResultDirectoryManager().Create(ResultModuleType.Prediction,
                "run_20260910_205238_9608c9ba", null, bothRun));
            Check(both.Temperatures[0].Tables[0].Rows.Count == 101, "real 02 both temperature rows");
            Check(both.InfraredResponses.Count == 1 && both.InfraredResponses[0].Tables.Count == 2, "real 02 point image tables");
            Check(both.InfraredResponses[0].Tables[0].Rows.Count == 101 && both.InfraredResponses[0].Tables[1].Rows.Count == 1616, "real 02 point image row counts");
            Check(both.InfraredResponses[0].Summary["reconstruction.grid"] == "256x256", "02 reconstruction contract");
        }

        private static void TestSimilarity(string root)
        {
            string featureRun = FindRunFile(root, "run_20260911_151825_9403cacb", "evaluation_status.json").DirectoryName;
            RunResult features = new SimilarityResultReader().Read(new ResultDirectoryManager().Create(ResultModuleType.Similarity,
                "run_20260911_151825_9403cacb", null, featureRun));
            Check(features.Similarities.Count == 1 && features.Similarities[0].Tables.Count == 3, "real 03 features tables");
            Check(features.Similarities[0].Tables[0].Rows.Count == 8 && features.Similarities[0].Tables[1].Rows.Count == 128, "real 03 features row counts");
            Check(features.Summary["similarity.status.mode"] == "features", "03 features status");

            string similarityRun = FindRunFile(root, "run_20260912_044041_2f565756", "evaluation_status.json").DirectoryName;
            RunResult similarity = new ResultReader().Read(new RunRecord { Module = "03", RunId = "run_20260912_044041_2f565756",
                RunDirectory = similarityRun, ResultDirectory = similarityRun, State = ProcessRunState.Completed });
            Check(similarity.Similarities.Count == 1 && similarity.Similarities[0].Tables.Count == 7, "real P4 03 similarity and feature tables");
            Check(similarity.Temperatures.Count == 2, "real P4 03 reference and candidate curves");
            Check(similarity.Similarities[0].Tables[0].Rows.Count > 0, "real P4 03 component rows");
            Check(Math.Abs(Double.Parse(similarity.Summary["similarity.summary.temperature_similarity_percent"], System.Globalization.CultureInfo.InvariantCulture) - 100.0) < 1e-12, "real P4 03 summary");
            Check(similarity.ModuleType == ResultModuleType.Similarity && similarity.RunState == "Completed", "P4 similarity record association");
        }

        private static void TestScene(string root)
        {
            string sceneRun = FindRunFile(root, "run_20260911_151927_cfafd7eb", "scene_search_summary.json").DirectoryName;
            RunResult scene = new ResultReader().Read(new RunRecord { Module = "04", RunId = "run_20260911_151927_cfafd7eb",
                RunDirectory = sceneRun, ResultDirectory = sceneRun, State = ProcessRunState.Completed });
            Check(scene.Scenes.Count == 1 && scene.Scenes[0].Tables.Count == 3, "real 04 tables");
            Check(scene.Scenes[0].Tables[0].Rows.Count == 0 && scene.Scenes[0].Tables[1].Rows.Count == 0, "04 empty candidate tables are valid");
            Check(scene.Scenes[0].Tables[2].Rows.Count == 5, "real 04 search log rows");
            Check(scene.Summary["scene.summary.status"] == "ProxyOnlyCompleted", "real 04 status");
            Check(scene.ModuleType == ResultModuleType.Scene && scene.RunState == "Completed", "P4 scene record association");
        }

        private static void TestMissingAndMalformed(string root)
        {
            var manager = new ResultDirectoryManager();
            RunResult absent = new ResultReader().Read(manager.Create(ResultModuleType.Forward, "run_absent", null, Path.Combine(root, "absent")));
            Check(!absent.ResultExists && absent.Issues.Count == 1, "missing result directory handled");

            string empty = Path.Combine(root, "empty"); Directory.CreateDirectory(empty);
            RunResult missingFiles = new PredictionResultReader().Read(manager.Create(ResultModuleType.Prediction, "run_empty", null, empty));
            Check(missingFiles.Issues.Any(x => x.Contains("prediction_summary.json")), "missing result file reported");

            string malformedCsv = Path.Combine(root, "bad-csv"); Directory.CreateDirectory(malformedCsv);
            File.WriteAllText(Path.Combine(malformedCsv, "temperature_history.csv"), "frame,time_s,T_1\n0,not-a-number,300\n");
            Expect<ResultReadException>(() => new ForwardResultReader().Read(manager.Create(ResultModuleType.Forward, "run_bad_csv", null, malformedCsv)), "malformed numeric CSV rejected");

            string malformedJson = Path.Combine(root, "bad-json"); Directory.CreateDirectory(malformedJson);
            File.WriteAllText(Path.Combine(malformedJson, "prediction_summary.json"), "{broken");
            Expect<ResultReadException>(() => new PredictionResultReader().Read(manager.Create(ResultModuleType.Prediction, "run_bad_json", null, malformedJson)), "malformed JSON rejected");

            string mismatch = Path.Combine(root, "mismatch"); Directory.CreateDirectory(mismatch);
            File.WriteAllText(Path.Combine(mismatch, "evaluation_status.json"), "{\"module\":\"03\",\"status\":\"success\",\"run_id\":\"another_run\",\"mode\":\"similarity\"}");
            Expect<ResultReadException>(() => new SimilarityResultReader().Read(manager.Create(ResultModuleType.Similarity, "run_expected", null, mismatch)), "mismatched run_id rejected");
        }

        private static FileInfo FindRunFile(string root, string runId, string fileName)
        {
            FileInfo match = new DirectoryInfo(Path.Combine(root, "coreprogram")).EnumerateFiles(fileName, SearchOption.AllDirectories)
                .FirstOrDefault(x => x.FullName.IndexOf(runId, StringComparison.Ordinal) >= 0);
            if (match == null) match = new DirectoryInfo(Path.Combine(root, "GUI_WPF", "Verification")).EnumerateFiles(fileName, SearchOption.AllDirectories)
                .FirstOrDefault(x => x.FullName.IndexOf(runId, StringComparison.Ordinal) >= 0);
            if (match == null) throw new FileNotFoundException("Fixture not found: " + runId + "/" + fileName);
            return match;
        }

        private static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FAILED: " + name);
            passed++; Console.WriteLine("PASS " + name);
        }

        private static void Expect<T>(Action action, string name) where T : Exception
        {
            try { action(); }
            catch (T) { Check(true, name); return; }
            throw new InvalidOperationException("FAILED: " + name);
        }
    }
}
