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
        // 保存该组件运行所需的配置或中间状态。
        private static int passed;

        private static int Main()
        {
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string root = Path.Combine(Path.GetTempPath(), "PreProcess-P5_1-" + Guid.NewGuid().ToString("N"));
            try
            {
                // 处理文件系统路径及数据，并在使用前确认目标有效。
                Directory.CreateDirectory(root);
                TestGivenDirectory(root);
                // 调用对应组件完成当前步骤，并保留产生的处理结果。
                TestMissingDirectory(root);
                TestModuleDistinction(root);
                TestP4RunRecordAssociation(root);
                // 调用对应组件完成当前步骤，并保留产生的处理结果。
                TestModelContainers();
                TestValidation(root);
                // 调用对应组件完成当前步骤，并保留产生的处理结果。
                Console.WriteLine("P5.1 ResultModelTest: " + passed + " assertions passed.");
                return 0;
            }
            // 捕获本步骤产生的异常，将故障转换为可追踪的运行状态。
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
            // 无论执行成功与否都释放资源并恢复组件的可用状态。
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static void TestGivenDirectory(string root)
        {
            var manager = new ResultDirectoryManager();
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string run = Path.Combine(root, "runs", "run_forward");
            string output = Path.Combine(run, "output");
            string request = Path.Combine(root, "requests", "request.json");
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            Directory.CreateDirectory(output);
            Directory.CreateDirectory(Path.GetDirectoryName(request));
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            File.WriteAllText(request, "{}");

            RunResult result = manager.Create(ResultModuleType.Forward, "run_forward", request, output, run);
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(result.RunId == "run_forward", "given directory keeps run_id");
            Check(result.ModuleType == ResultModuleType.Forward && result.ModuleCode == "01", "given directory keeps module");
            Check(result.InputRequestPath == Path.GetFullPath(request), "given directory keeps request path");
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            Check(result.OutputDirectory == Path.GetFullPath(output), "given directory keeps output path");
            Check(result.ResultExists && manager.ResultExists(result), "given directory is available");
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static void TestMissingDirectory(string root)
        {
            var manager = new ResultDirectoryManager();
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            RunResult result = manager.Create(ResultModuleType.Prediction, "run_missing", null,
                Path.Combine(root, "missing", "run_missing"));
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(!result.ResultExists, "missing directory is unavailable");
            Check(result.AvailabilityMessage.Contains("结果目录不存在"), "missing directory has user-facing message");
            Check(!manager.ResultExists(result), "missing directory live check is false");
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static void TestModuleDistinction(string root)
        {
            var manager = new ResultDirectoryManager();
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string runsRoot = Path.Combine(root, "module-runs");
            Directory.CreateDirectory(Path.Combine(runsRoot, "run_01", "output"));
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            Directory.CreateDirectory(Path.Combine(runsRoot, "run_02"));
            Directory.CreateDirectory(Path.Combine(runsRoot, "run_03"));
            Directory.CreateDirectory(Path.Combine(runsRoot, "run_04"));
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            Directory.CreateDirectory(Path.Combine(runsRoot, "run_trajectory", "output"));

            RunResult forward = manager.Locate(ResultModuleType.Forward, "run_01", null, runsRoot);
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            RunResult prediction = manager.Locate(ResultModuleType.Prediction, "run_02", null, runsRoot);
            RunResult similarity = manager.Locate(ResultModuleType.Similarity, "run_03", null, runsRoot);
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            RunResult scene = manager.Locate(ResultModuleType.Scene, "run_04", null, runsRoot);
            RunResult trajectory = manager.Locate(ResultModuleType.Trajectory, "run_trajectory", null, runsRoot);

            Check(forward.ModuleCode == "01" && prediction.ModuleCode == "02" && similarity.ModuleCode == "03" && scene.ModuleCode == "04", "backend modules keep distinct codes");
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(trajectory.ModuleType == ResultModuleType.Trajectory && trajectory.ModuleCode == "01", "trajectory remains a distinct GUI type using backend 01");
            Check(Path.GetFileName(forward.OutputDirectory) == "output" && Path.GetFileName(trajectory.OutputDirectory) == "output", "01 result uses nested output directory");
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(prediction.OutputDirectory == prediction.RunDirectory && similarity.OutputDirectory == similarity.RunDirectory && scene.OutputDirectory == scene.RunDirectory, "02/03/04 result uses run directory");
            Check(forward.ResultExists && prediction.ResultExists && similarity.ResultExists && scene.ResultExists && trajectory.ResultExists, "all module fixtures are located");
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static void TestP4RunRecordAssociation(string root)
        {
            var manager = new ResultDirectoryManager();
            string request = Path.Combine(root, "requests", "p4.json");
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string run = Path.Combine(root, "p4", "run_02");
            Directory.CreateDirectory(run);
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var record = new RunRecord
            {
                Module = "02", RunId = "run_02", RequestPath = request,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                RunDirectory = run, ResultDirectory = run,
                StartedAt = DateTimeOffset.Now.AddSeconds(-1), EndedAt = DateTimeOffset.Now,
                State = ProcessRunState.Completed
            };
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            RunResult result = manager.Create(record);
            Check(result.ModuleType == ResultModuleType.Prediction, "P4 module maps to prediction");
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            Check(result.RunId == record.RunId && result.OutputDirectory == Path.GetFullPath(record.ResultDirectory), "P4 paths are associated");
            Check(result.RunState == "Completed" && result.StartedAt == record.StartedAt && result.EndedAt == record.EndedAt, "P4 execution metadata is retained");

            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string forwardRun = Path.Combine(root, "p4", "run_trajectory");
            Directory.CreateDirectory(Path.Combine(forwardRun, "output"));
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            RunResult trajectory = manager.Create(new RunRecord { Module = "01", RunId = "run_trajectory", RunDirectory = forwardRun }, ResultModuleType.Trajectory);
            Check(trajectory.ModuleType == ResultModuleType.Trajectory && trajectory.ResultExists, "P4 backend 01 can be classified as trajectory");
        }

        private static void TestModelContainers()
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var run = new RunResult();
            var temperature = new TemperatureResult { Name = "temperature" };
            // 将外部数据转换为目标类型，并保持约定的表示格式。
            var table = new ResultTable { Name = "samples", SourcePath = "unparsed-source" };
            table.Columns.Add(new ResultColumn { Key = "confirmed_later", DisplayName = "P5.2确认字段" });
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var row = new ResultRow();
            row.Values["confirmed_later"] = 1.0;
            table.Rows.Add(row);
            // 将当前结果加入集合，供后续汇总或界面展示。
            temperature.Tables.Add(table);
            run.Temperatures.Add(temperature);
            // 将当前结果加入集合，供后续汇总或界面展示。
            run.Trajectories.Add(new TrajectoryResult());
            run.InfraredResponses.Add(new InfraredResult());
            // 将当前结果加入集合，供后续汇总或界面展示。
            run.Similarities.Add(new SimilarityResult());
            run.Scenes.Add(new SceneResult());
            Check(run.Temperatures.Count == 1 && run.Trajectories.Count == 1 && run.InfraredResponses.Count == 1, "physical result collections are initialized");
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(run.Similarities.Count == 1 && run.Scenes.Count == 1, "module result collections are initialized");
            Check(run.Temperatures[0].Tables[0].Rows[0].Values.ContainsKey("confirmed_later"), "format-neutral table holds reader data");
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static void TestValidation(string root)
        {
            var manager = new ResultDirectoryManager();
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            Expect<ArgumentException>(() => manager.Locate(ResultModuleType.Forward, "..", null, root), "path traversal run_id rejected");
            Expect<ArgumentException>(() => manager.ParseModule("05"), "unknown module rejected");
            Expect<ArgumentException>(() => manager.Create(new RunRecord { Module = "02", RunId = "run_x", ResultDirectory = root }, ResultModuleType.Scene), "incompatible P4 module override rejected");
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(manager.ParseModule("轨迹") == ResultModuleType.Trajectory, "trajectory alias parsed");
        }

        private static void Check(bool condition, string name)
        {
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (!condition) throw new InvalidOperationException("FAILED: " + name);
            passed++;
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            Console.WriteLine("PASS " + name);
        }

        private static void Expect<T>(Action action, string name) where T : Exception
        {
            // 进入可能失败的操作区间，并由后续异常分支统一处理故障。
            try { action(); }
            catch (T) { Check(true, name); return; }
            throw new InvalidOperationException("FAILED: " + name);
        }
    }
}
