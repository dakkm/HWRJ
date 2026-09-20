using System;
using System.Collections.Generic;
using System.IO;
using PreProcess.Requests;
using PreProcess.Validation;
using PreProcess.Wpf.Models;
using PreProcess.Wpf.Services;
using PreProcess.Wpf.Services.Mappers;
using PreProcess.Wpf.ViewModels;

namespace MapperTest
{
    // 定义 Program 类型，集中封装与该领域对象相关的状态和行为。
    internal static class Program
    {
        private static int Main()
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var tests = new Action[]
            {
                DefaultSphericalShellMapsToForwardRequest,
                SingleTargetChangesMapIndependently,
                // 继续处理当前业务步骤，保持上下文状态一致。
                SimilarityIndexMapsToSceneBuildRequest,
                TaskFileRoundTripPreservesEditableState,
                // 继续处理当前业务步骤，保持上下文状态一致。
                DefaultTaskIsSelectedAtStartup,
                PredictionAdjustmentPreservesLearnedInputs,
                // 继续处理当前业务步骤，保持上下文状态一致。
                InvalidParametersAreRejectedByValidator,
                ResultBasedMappersDoNotRequireTaskPhysics
            };

            var failures = new List<string>();
            // 遍历当前数据集合，逐项完成必要的转换或状态更新。
            foreach (var test in tests)
            {
                try
                {
                    // 调用对应组件完成当前步骤，并保留产生的处理结果。
                    test();
                    Console.WriteLine("PASS " + test.Method.Name);
                }
                // 捕获本步骤产生的异常，将故障转换为可追踪的运行状态。
                catch (Exception exception)
                {
                    failures.Add(test.Method.Name + ": " + exception.Message);
                    // 调用对应组件完成当前步骤，并保留产生的处理结果。
                    Console.WriteLine("FAIL " + failures[failures.Count - 1]);
                }
            }

            Console.WriteLine("MapperTest: {0} passed, {1} failed.",
                tests.Length - failures.Count, failures.Count);
            // 返回当前步骤生成的结果，并结束本次调用。
            return failures.Count == 0 ? 0 : 1;
        }

        private static void DefaultSphericalShellMapsToForwardRequest()
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var task = new TaskModel();
            Equal("默认", task.Scene.AttitudeMotionType, "Chinese default attitude motion type");
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            task.Scene.AttitudeMotionType = "NONE";
            Equal("默认", task.Scene.AttitudeMotionType, "legacy NONE is normalized for display");
            var request = new ForwardSimulationMapper().Map(task);

            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            Equal(16, request.Case.TargetCount, "default target count");
            Equal(1000.0, request.Case.TotalTimeSeconds, "default duration");
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            True(request.TargetPhysics != null, "TargetPhysics must exist");
            Equal(16, request.TargetPhysics.Count, "TargetPhysics count");
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            Equal(0.2, request.TargetPhysics[0].Radius, "outer radius");
            Equal(2700.0, request.TargetPhysics[0].Density, "density");
            Equal(1, request.TargetPhysics[0].Id, "first target id");
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            Equal("SPHERE", request.GroupState.CompanionType, "default companion type");
            Equal("NONE", request.GroupState.AttitudeMotionType, "default attitude motion type");
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            Equal("DATASET_BASIC", request.GroupState.SimilarityLevel, "default dataset contract token");
        }

        private static void SingleTargetChangesMapIndependently()
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var task = new TaskModel();
            var target = task.IndividualTargets[1];
            target.UseOverride = true;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            target.EffectivePhysics.InternalPower = 123.0;
            target.Motion.Position.X = 42.0;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            target.Motion.Velocity.Y = -7.0;

            var request = new ForwardSimulationMapper().Map(task);

            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            Equal(300.0, request.TargetPhysics[0].InternalPower, "uniform target physics");
            Equal(123.0, request.TargetPhysics[1].InternalPower, "overridden target physics");
            Equal(42.0, request.TargetScene[1].Position[0], "target scene position");
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            Equal(-7.0, request.TargetScene[1].Velocity[1], "target scene velocity");
            Equal(0.0, request.TargetScene[0].Position[0], "other target scene remains unchanged");
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static void SimilarityIndexMapsToSceneBuildRequest()
        {
            var task = new TaskModel();
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            task.Settings.SimilarityIndex = 90.0;

            var request = new SceneBuildMapper().Map(task, "requests/source.json", 10);
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var forward = new ForwardSimulationMapper().Map(task);
            var generated = new RequestGenerator().Generate(task);

            Equal(90.0, request.RequiredSimilarityPercent, "similarity percentage points");
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            Equal("DATASET_BASIC", forward.GroupState.SimilarityLevel, "forward dataset contract token");
            True(generated.Contains("\"SIMILARITY_LEVEL\":\"DATASET_BASIC\""), "generated dataset contract token");
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            Equal(10, request.RequiredCandidateCount, "candidate count");
            Equal("requests/source.json", request.SourceRequestFile, "source request reference");
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static void InvalidParametersAreRejectedByValidator()
        {
            var task = new TaskModel();
            task.Targets.Uniform.InternalPower = -1.0;

            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var request = new PredictionMapper().Map(task, "temperature");
            var errors = new CapabilityValidator().ValidatePrediction(request);

            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            True(errors.Count > 0, "validator must reject unsupported proxy parameters");

            var invalidSceneBuild = new SceneBuildMapper().Map(new TaskModel(), "source.json", 0);
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            errors = new CapabilityValidator().ValidateSceneBuild(invalidSceneBuild);
            True(errors.Count > 0, "validator must reject non-positive candidate count");
        }

        private static void TaskFileRoundTripPreservesEditableState()
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var task = new TaskModel();
            task.Settings.Metadata.Name = "任务文件测试"; task.Settings.Metadata.Description = "完整往返";
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            task.Settings.Duration = 321; task.Settings.TargetCount = 3; task.Settings.SimilarityIndex = 92.5;
            task.Targets.Uniform.Radius = 0.3; task.Targets.Uniform.InternalPower = 456;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            task.IndividualTargets[1].UseOverride = true; task.IndividualTargets[1].EffectivePhysics.Density = 1234;
            task.IndividualTargets[2].Motion.Position.X = 88; task.IndividualTargets[2].Motion.ReleaseTime = 12;
            task.Scene.AngularVelocity.Z = 0.25; task.Environment.SolarFlux = 1400;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            task.Scene.CompanionType = "SPHERE"; task.Scene.AttitudeMotionType = "SPIN";
            task.Scene.MicroMotionParameters.X = 1.5;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            task.Environment.ObserverPosition.Y = -456; task.Environment.DetectorTracking = false;
            string path = Path.Combine(Path.GetTempPath(), "preprocess-task-" + Guid.NewGuid().ToString("N") + ".json");
            // 进入可能失败的操作区间，并由后续异常分支统一处理故障。
            try
            {
                var files = new TaskFileService(); files.Save(task, path);
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                TaskModel restored = files.Load(path);
                Equal("任务文件测试", restored.Settings.Metadata.Name, "task name");
                Equal(92.5, restored.Settings.SimilarityIndex, "similarity setting");
                // 调用对应组件完成当前步骤，并保留产生的处理结果。
                Equal(3, restored.IndividualTargets.Count, "target count");
                True(restored.IndividualTargets[1].UseOverride, "target override state");
                // 调用对应组件完成当前步骤，并保留产生的处理结果。
                Equal(1234, restored.IndividualTargets[1].EffectivePhysics.Density, "target override physics");
                Equal(88, restored.IndividualTargets[2].Motion.Position.X, "target motion");
                // 调用对应组件完成当前步骤，并保留产生的处理结果。
                Equal(0.25, restored.Scene.AngularVelocity.Z, "scene vector");
                Equal("球壳", restored.Scene.CompanionType, "companion type");
                Equal("SPIN", restored.Scene.AttitudeMotionType, "attitude motion type");
                // 调用对应组件完成当前步骤，并保留产生的处理结果。
                Equal(1.5, restored.Scene.MicroMotionParameters.X, "micro motion parameters");
                Equal(-456, restored.Environment.ObserverPosition.Y, "environment vector");
                // 调用对应组件完成当前步骤，并保留产生的处理结果。
                True(!restored.Environment.DetectorTracking, "tracking flag");
                Equal(new RequestGenerator().Generate(task), new RequestGenerator().Generate(restored), "backend request after task roundtrip");
            }
            // 无论执行成功与否都释放资源并恢复组件的可用状态。
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        private static void DefaultTaskIsSelectedAtStartup()
        {
            var editor = new TaskEditorViewModel();
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            True(!String.IsNullOrWhiteSpace(editor.CurrentTaskPath), "startup must select a task file");
            True(editor.CurrentTaskPath.EndsWith(Path.Combine("output", "default.task.json"), StringComparison.OrdinalIgnoreCase),
                // 继续处理当前业务步骤，保持上下文状态一致。
                "startup task must be output/default.task.json");
            True(File.Exists(editor.CurrentTaskPath), "startup must create the default task file when missing");
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            Equal("preprocess-task-v1", new TaskFileService().Load(editor.CurrentTaskPath) == null ? null : TaskFileService.SchemaVersion,
                "default task schema");
        }

        private static void PredictionAdjustmentPreservesLearnedInputs()
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            string package = new PreProcess.Wpf.Services.Execution.BackendPathResolver().Resolve().PackageRoot;
            var source = new TaskModel();
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            source.Settings.Metadata.Name = "自动调整测试";
            source.Settings.Duration = 375.5;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            source.Targets.Uniform.InternalPower = 123;
            source.Targets.Uniform.Emissivity = 0.7;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            source.Targets.Uniform.SolarAbsorption = 0.6;
            Equal(4.9956754, source.IndividualTargets[1].Motion.Velocity.X, "software default dispersal velocity");
            Equal(20, source.IndividualTargets[1].Motion.ReleaseTime, "software default release time");
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            source.IndividualTargets[1].Motion.Position.X = 99;
            var adjusted = PreProcess.Wpf.Services.Execution.ReferenceTaskLoader.AdjustForPrediction(
                // 继续处理当前业务步骤，保持上下文状态一致。
                source, package);
            Equal("自动调整测试", adjusted.Settings.Metadata.Name, "prediction adjustment task name");
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            Equal(375.5, adjusted.Settings.Duration, "prediction duration");
            Equal(123, adjusted.Targets.Uniform.InternalPower, "prediction internal power");
            Equal(0.7, adjusted.Targets.Uniform.Emissivity, "prediction emissivity");
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            Equal(0.6, adjusted.Targets.Uniform.SolarAbsorption, "prediction solar absorption");
            Equal(0.3, adjusted.Targets.Uniform.IrReflection, "prediction complementary reflection");
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            Equal(0, adjusted.IndividualTargets[1].Motion.Position.X, "prediction software default scene adjustment");
            Equal(4.9956754, adjusted.IndividualTargets[1].Motion.Velocity.X, "prediction default dispersal velocity");
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            Equal(20, adjusted.IndividualTargets[1].Motion.ReleaseTime, "prediction default release time");
            Equal(375.5, PreProcess.Wpf.Services.Execution.ReferenceTaskLoader.AdjustForScene(
                source, package).Settings.Duration,
                // 继续处理当前业务步骤，保持上下文状态一致。
                "scene build duration");
        }

        private static void ResultBasedMappersDoNotRequireTaskPhysics()
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var similarity = new SimilarityEvaluationMapper().Map(
                new ResultReference { RunDirectory = "reference/output" },
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                new ResultReference { RunDirectory = "candidate/output" },
                new EvaluationConfig { TargetObjectId = 1, Metric = "temperature_similarity" });
            Equal("reference/output", similarity.Reference.RunDirectory, "reference result");
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            Equal("candidate/output", similarity.Candidate.RunDirectory, "candidate result");

            var trajectory = new TrajectoryMapper().Map("run/output/trajectory_history.csv", true);
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            Equal("run/output/trajectory_history.csv", trajectory.TrajectoryCsv, "trajectory result");
            True(trajectory.IncludePrerelease, "trajectory prerelease option");
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static void True(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static void Equal(int expected, int actual, string message)
        {
            if (expected != actual)
                throw new InvalidOperationException(message + ": expected " + expected + ", actual " + actual);
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static void Equal(double expected, double actual, string message)
        {
            if (Math.Abs(expected - actual) > 1e-12)
                // 发现无效输入或运行状态后立即终止，防止错误继续传播。
                throw new InvalidOperationException(message + ": expected " + expected + ", actual " + actual);
        }

        private static void Equal(string expected, string actual, string message)
        {
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (!String.Equals(expected, actual, StringComparison.Ordinal))
                throw new InvalidOperationException(message + ": expected " + expected + ", actual " + actual);
        }
    }
}
