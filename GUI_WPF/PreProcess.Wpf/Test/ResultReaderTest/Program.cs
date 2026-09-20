using System;
using System.IO;
using System.Linq;
using PreProcess.Wpf.Models.Results;
using PreProcess.Wpf.Services.Execution;
using PreProcess.Wpf.Services.Process;
using PreProcess.Wpf.Services.Results;

namespace ResultReaderTest
{
    // 定义 Program 类型，集中封装与该领域对象相关的状态和行为。
    internal static class Program
    {
        private static int passed;

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static int Main(string[] args)
        {
            if (args.Length != 1) { Console.Error.WriteLine("Usage: ResultReaderTest <project-root>"); return 2; }
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string root = Path.GetFullPath(args[0]);
            string temporary = Path.Combine(Path.GetTempPath(), "PreProcess-P5_2-" + Guid.NewGuid().ToString("N"));
            try
            {
                // 处理文件系统路径及数据，并在使用前确认目标有效。
                Directory.CreateDirectory(temporary);
                TestForward(root);
                // 调用对应组件完成当前步骤，并保留产生的处理结果。
                TestPrediction(root);
                TestSimilarity(root);
                // 调用对应组件完成当前步骤，并保留产生的处理结果。
                TestScene(root);
                TestMissingAndMalformed(temporary);
                Console.WriteLine("P5.2 ResultReaderTest: " + passed + " assertions passed.");
                // 返回当前步骤生成的结果，并结束本次调用。
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
            // 无论执行成功与否都释放资源并恢复组件的可用状态。
            finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
        }

        private static void TestForward(string root)
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            string output = FindRunFile(root, "run_20260911_131206_54dd28bb", "temperature_history.csv").DirectoryName;
            string runDirectory = Directory.GetParent(output).FullName;
            string request = Path.Combine(runDirectory, "request.json");
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var record = new RunRecord { Module = "01", RunId = "run_20260911_131206_54dd28bb", RequestPath = request,
                RunDirectory = runDirectory, ResultDirectory = output, State = ProcessRunState.Completed };
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            RunResult result = new ResultReader().Read(record);
            Check(result.ModuleType == ResultModuleType.Forward && result.InputRequestPath == Path.GetFullPath(request), "P4 forward record association");
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(result.Temperatures.Count == 1 && result.Temperatures[0].Tables[0].Rows.Count == 78, "real 01 temperature rows");
            Check(result.Trajectories.Count == 1 && result.Trajectories[0].Tables[0].Rows.Count == 1248, "real 01 trajectory rows");
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(result.InfraredResponses.Count == 1 && result.InfraredResponses[0].Tables[0].Rows.Count == 128, "real 01 infrared rows");
            Check((long)result.Temperatures[0].Tables[0].Rows[0].Values["frame"] == 0L, "real 01 typed frame");
            Check((double)result.Temperatures[0].Tables[0].Rows[0].Values["time_s"] == 0.0, "real 01 typed time");
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(result.Temperatures[0].Tables[0].Columns.First(x => x.Key == "T_1").Unit == "K", "01 unit metadata");

            RunResult trajectory = new ForwardResultReader().Read(record, ResultModuleType.Trajectory);
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(trajectory.ModuleType == ResultModuleType.Trajectory && trajectory.Trajectories.Count == 1, "01 record trajectory classification");
        }

        private static void TestPrediction(string root)
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            string tempRun = FindRunFile(root, "run_20260910_205207_f10e224c", "prediction_summary.json").DirectoryName;
            RunResult temperature = new ResultReader().Read(new RunRecord { Module = "02", RunId = "run_20260910_205207_f10e224c",
                RunDirectory = tempRun, ResultDirectory = tempRun, State = ProcessRunState.Completed });
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(temperature.Temperatures.Count == 1 && temperature.Temperatures[0].Tables[0].Rows.Count == 101, "real 02 temperature rows");
            Check(temperature.InfraredResponses.Count == 0 && temperature.Issues.Count == 0, "02 temperature mode optional files");
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(temperature.Summary["prediction.mode"] == "temperature", "02 summary mode");
            Check(temperature.ModuleType == ResultModuleType.Prediction && temperature.RunState == "Completed", "P4 prediction record association");

            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            string bothRun = FindRunFile(root, "run_20260910_205238_9608c9ba", "prediction_summary.json").DirectoryName;
            RunResult both = new PredictionResultReader().Read(new ResultDirectoryManager().Create(ResultModuleType.Prediction,
                "run_20260910_205238_9608c9ba", null, bothRun));
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(both.Temperatures[0].Tables[0].Rows.Count == 101, "real 02 both temperature rows");
            Check(both.Temperatures[0].Tables[0].Columns.Select(x => x.Key).SequenceEqual(new[] { "frame", "time_s", "T_1" }), "02 forward-format temperature columns");
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(both.InfraredResponses.Count == 1 && both.InfraredResponses[0].Tables.Count == 1, "real 02 forward-format infrared table");
            Check(both.InfraredResponses[0].Tables[0].Rows.Count == 1616, "real 02 infrared row count");
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(both.InfraredResponses[0].Tables[0].Columns.Any(x => x.Key == "detector_irradiance_W_m2"), "02 forward-format infrared columns");
            Check(both.InfraredResponses[0].Summary["reconstruction.grid"] == "256x256", "02 reconstruction contract");
        }

        private static void TestSimilarity(string root)
        {
            // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
            string featureRun = FindRunFile(root, "run_20260911_151825_9403cacb", "evaluation_status.json").DirectoryName;
            RunResult features = new SimilarityResultReader().Read(new ResultDirectoryManager().Create(ResultModuleType.Similarity,
                // 继续处理当前业务步骤，保持上下文状态一致。
                "run_20260911_151825_9403cacb", null, featureRun));
            Check(features.Similarities.Count == 1 && features.Similarities[0].Tables.Count == 3, "real 03 features tables");
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(features.Similarities[0].Tables[0].Rows.Count == 8 && features.Similarities[0].Tables[1].Rows.Count == 128, "real 03 features row counts");
            Check(features.Summary["similarity.status.mode"] == "features", "03 features status");

            // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
            string similarityRun = FindRunFile(root, "run_20260912_044041_2f565756", "evaluation_status.json").DirectoryName;
            RunResult similarity = new ResultReader().Read(new RunRecord { Module = "03", RunId = "run_20260912_044041_2f565756",
                RunDirectory = similarityRun, ResultDirectory = similarityRun, State = ProcessRunState.Completed });
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(similarity.Similarities.Count == 1 && similarity.Similarities[0].Tables.Count == 8, "real P4 03 similarity overview and feature tables");
            Check(similarity.Temperatures.Count == 2, "real P4 03 reference and candidate curves");
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(similarity.Similarities[0].Tables[0].Name == "相似度结果" && similarity.Similarities[0].Tables[0].Rows.Count == 3,
                "real P4 03 percentage overview");
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(similarity.Similarities[0].Tables[1].Rows.Count > 0, "real P4 03 component rows");
            Check(Math.Abs(Double.Parse(similarity.Summary["similarity.summary.temperature_similarity_percent"], System.Globalization.CultureInfo.InvariantCulture) - 100.0) < 1e-12, "real P4 03 summary");
            Check(similarity.ModuleType == ResultModuleType.Similarity && similarity.RunState == "Completed", "P4 similarity record association");
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static void TestScene(string root)
        {
            string sceneRun = FindRunFile(root, "run_20260911_151927_cfafd7eb", "scene_search_summary.json").DirectoryName;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            RunResult scene = new ResultReader().Read(new RunRecord { Module = "04", RunId = "run_20260911_151927_cfafd7eb",
                RunDirectory = sceneRun, ResultDirectory = sceneRun, State = ProcessRunState.Completed });
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(scene.Scenes.Count == 1 && scene.Scenes[0].Tables.Count == 4, "real 04 summary and detailed tables");
            Check(scene.Scenes[0].Tables[0].Name == "候选相似度结果" && scene.Scenes[0].Tables[0].Rows.Count == 1, "04 candidate similarity summary");
            Check(scene.Scenes[0].Tables[1].Rows.Count == 0 && scene.Scenes[0].Tables[2].Rows.Count == 0, "04 empty candidate tables are valid");
            // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
            Check(scene.Scenes[0].Tables[3].Rows.Count == 5, "real 04 search log rows");
            Check(scene.Summary["scene.summary.status"] == "ProxyOnlyCompleted", "real 04 status");
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(scene.ModuleType == ResultModuleType.Scene && scene.RunState == "Completed", "P4 scene record association");
        }

        private static void TestMissingAndMalformed(string root)
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var manager = new ResultDirectoryManager();
            RunResult absent = new ResultReader().Read(manager.Create(ResultModuleType.Forward, "run_absent", null, Path.Combine(root, "absent")));
            Check(!absent.ResultExists && absent.Issues.Count == 1, "missing result directory handled");

            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string empty = Path.Combine(root, "empty"); Directory.CreateDirectory(empty);
            RunResult missingFiles = new PredictionResultReader().Read(manager.Create(ResultModuleType.Prediction, "run_empty", null, empty));
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(missingFiles.Issues.Any(x => x.Contains("prediction_summary.json")), "missing result file reported");

            string malformedCsv = Path.Combine(root, "bad-csv"); Directory.CreateDirectory(malformedCsv);
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            File.WriteAllText(Path.Combine(malformedCsv, "temperature_history.csv"), "frame,time_s,T_1\n0,not-a-number,300\n");
            Expect<ResultReadException>(() => new ForwardResultReader().Read(manager.Create(ResultModuleType.Forward, "run_bad_csv", null, malformedCsv)), "malformed numeric CSV rejected");

            string malformedJson = Path.Combine(root, "bad-json"); Directory.CreateDirectory(malformedJson);
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            File.WriteAllText(Path.Combine(malformedJson, "prediction_summary.json"), "{broken");
            Expect<ResultReadException>(() => new PredictionResultReader().Read(manager.Create(ResultModuleType.Prediction, "run_bad_json", null, malformedJson)), "malformed JSON rejected");

            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string mismatch = Path.Combine(root, "mismatch"); Directory.CreateDirectory(mismatch);
            File.WriteAllText(Path.Combine(mismatch, "evaluation_status.json"), "{\"module\":\"03\",\"status\":\"success\",\"run_id\":\"another_run\",\"mode\":\"similarity\"}");
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            Expect<ResultReadException>(() => new SimilarityResultReader().Read(manager.Create(ResultModuleType.Similarity, "run_expected", null, mismatch)), "mismatched run_id rejected");
        }

        private static FileInfo FindRunFile(string root, string runId, string fileName)
        {
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            FileInfo match = new DirectoryInfo(Path.Combine(root, "coreprogram")).EnumerateFiles(fileName, SearchOption.AllDirectories)
                .FirstOrDefault(x => x.FullName.IndexOf(runId, StringComparison.Ordinal) >= 0);
            if (match == null) match = new DirectoryInfo(Path.Combine(root, "GUI_WPF", "Verification")).EnumerateFiles(fileName, SearchOption.AllDirectories)
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                .FirstOrDefault(x => x.FullName.IndexOf(runId, StringComparison.Ordinal) >= 0);
            if (match == null) throw new FileNotFoundException("Fixture not found: " + runId + "/" + fileName);
            // 返回当前步骤生成的结果，并结束本次调用。
            return match;
        }

        private static void Check(bool condition, string name)
        {
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (!condition) throw new InvalidOperationException("FAILED: " + name);
            passed++; Console.WriteLine("PASS " + name);
        }

        private static void Expect<T>(Action action, string name) where T : Exception
        {
            // 进入可能失败的操作区间，并由后续异常分支统一处理故障。
            try { action(); }
            catch (T) { Check(true, name); return; }
            // 发现无效输入或运行状态后立即终止，防止错误继续传播。
            throw new InvalidOperationException("FAILED: " + name);
        }
    }
}
