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
    internal static class Program
    {
        private static int Main()
        {
            var tests = new Action[]
            {
                DefaultSphericalShellMapsToForwardRequest,
                SingleTargetChangesMapIndependently,
                SimilarityIndexMapsToSceneBuildRequest,
                TaskFileRoundTripPreservesEditableState,
                DefaultTaskIsSelectedAtStartup,
                PredictionAdjustmentPreservesLearnedInputs,
                InvalidParametersAreRejectedByValidator,
                ResultBasedMappersDoNotRequireTaskPhysics
            };

            var failures = new List<string>();
            foreach (var test in tests)
            {
                try
                {
                    test();
                    Console.WriteLine("PASS " + test.Method.Name);
                }
                catch (Exception exception)
                {
                    failures.Add(test.Method.Name + ": " + exception.Message);
                    Console.WriteLine("FAIL " + failures[failures.Count - 1]);
                }
            }

            Console.WriteLine("MapperTest: {0} passed, {1} failed.",
                tests.Length - failures.Count, failures.Count);
            return failures.Count == 0 ? 0 : 1;
        }

        private static void DefaultSphericalShellMapsToForwardRequest()
        {
            var task = new TaskModel();
            Equal("默认", task.Scene.AttitudeMotionType, "Chinese default attitude motion type");
            task.Scene.AttitudeMotionType = "NONE";
            Equal("默认", task.Scene.AttitudeMotionType, "legacy NONE is normalized for display");
            var request = new ForwardSimulationMapper().Map(task);

            Equal(16, request.Case.TargetCount, "default target count");
            Equal(1000.0, request.Case.TotalTimeSeconds, "default duration");
            True(request.TargetPhysics != null, "TargetPhysics must exist");
            Equal(16, request.TargetPhysics.Count, "TargetPhysics count");
            Equal(0.2, request.TargetPhysics[0].Radius, "outer radius");
            Equal(2700.0, request.TargetPhysics[0].Density, "density");
            Equal(1, request.TargetPhysics[0].Id, "first target id");
            Equal("SPHERE", request.GroupState.CompanionType, "default companion type");
            Equal("NONE", request.GroupState.AttitudeMotionType, "default attitude motion type");
            Equal("90", request.GroupState.SimilarityLevel, "default similarity level");
        }

        private static void SingleTargetChangesMapIndependently()
        {
            var task = new TaskModel();
            var target = task.IndividualTargets[1];
            target.UseOverride = true;
            target.EffectivePhysics.InternalPower = 123.0;
            target.Motion.Position.X = 42.0;
            target.Motion.Velocity.Y = -7.0;

            var request = new ForwardSimulationMapper().Map(task);

            Equal(300.0, request.TargetPhysics[0].InternalPower, "uniform target physics");
            Equal(123.0, request.TargetPhysics[1].InternalPower, "overridden target physics");
            Equal(42.0, request.TargetScene[1].Position[0], "target scene position");
            Equal(-7.0, request.TargetScene[1].Velocity[1], "target scene velocity");
            Equal(0.0, request.TargetScene[0].Position[0], "other target scene remains unchanged");
        }

        private static void SimilarityIndexMapsToSceneBuildRequest()
        {
            var task = new TaskModel();
            task.Settings.SimilarityIndex = 90.0;

            var request = new SceneBuildMapper().Map(task, "requests/source.json", 10);

            Equal(90.0, request.RequiredSimilarityPercent, "similarity percentage points");
            Equal(10, request.RequiredCandidateCount, "candidate count");
            Equal("requests/source.json", request.SourceRequestFile, "source request reference");
        }

        private static void InvalidParametersAreRejectedByValidator()
        {
            var task = new TaskModel();
            task.Targets.Uniform.InternalPower = -1.0;

            var request = new PredictionMapper().Map(task, "temperature");
            var errors = new CapabilityValidator().ValidatePrediction(request);

            True(errors.Count > 0, "validator must reject unsupported proxy parameters");

            var invalidSceneBuild = new SceneBuildMapper().Map(new TaskModel(), "source.json", 0);
            errors = new CapabilityValidator().ValidateSceneBuild(invalidSceneBuild);
            True(errors.Count > 0, "validator must reject non-positive candidate count");
        }

        private static void TaskFileRoundTripPreservesEditableState()
        {
            var task = new TaskModel();
            task.Settings.Metadata.Name = "任务文件测试"; task.Settings.Metadata.Description = "完整往返";
            task.Settings.Duration = 321; task.Settings.TargetCount = 3; task.Settings.SimilarityIndex = 92.5;
            task.Targets.Uniform.Radius = 0.3; task.Targets.Uniform.InternalPower = 456;
            task.IndividualTargets[1].UseOverride = true; task.IndividualTargets[1].EffectivePhysics.Density = 1234;
            task.IndividualTargets[2].Motion.Position.X = 88; task.IndividualTargets[2].Motion.ReleaseTime = 12;
            task.Scene.AngularVelocity.Z = 0.25; task.Environment.SolarFlux = 1400;
            task.Scene.CompanionType = "SPHERE"; task.Scene.AttitudeMotionType = "SPIN";
            task.Scene.MicroMotionParameters.X = 1.5;
            task.Environment.ObserverPosition.Y = -456; task.Environment.DetectorTracking = false;
            string path = Path.Combine(Path.GetTempPath(), "preprocess-task-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var files = new TaskFileService(); files.Save(task, path);
                TaskModel restored = files.Load(path);
                Equal("任务文件测试", restored.Settings.Metadata.Name, "task name");
                Equal(92.5, restored.Settings.SimilarityIndex, "similarity setting");
                Equal(3, restored.IndividualTargets.Count, "target count");
                True(restored.IndividualTargets[1].UseOverride, "target override state");
                Equal(1234, restored.IndividualTargets[1].EffectivePhysics.Density, "target override physics");
                Equal(88, restored.IndividualTargets[2].Motion.Position.X, "target motion");
                Equal(0.25, restored.Scene.AngularVelocity.Z, "scene vector");
                Equal("球壳", restored.Scene.CompanionType, "companion type");
                Equal("SPIN", restored.Scene.AttitudeMotionType, "attitude motion type");
                Equal(1.5, restored.Scene.MicroMotionParameters.X, "micro motion parameters");
                Equal(-456, restored.Environment.ObserverPosition.Y, "environment vector");
                True(!restored.Environment.DetectorTracking, "tracking flag");
                Equal(new RequestGenerator().Generate(task), new RequestGenerator().Generate(restored), "backend request after task roundtrip");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        private static void DefaultTaskIsSelectedAtStartup()
        {
            var editor = new TaskEditorViewModel();
            True(!String.IsNullOrWhiteSpace(editor.CurrentTaskPath), "startup must select a task file");
            True(editor.CurrentTaskPath.EndsWith(Path.Combine("output", "default.task.json"), StringComparison.OrdinalIgnoreCase),
                "startup task must be output/default.task.json");
            True(File.Exists(editor.CurrentTaskPath), "startup must create the default task file when missing");
            Equal("preprocess-task-v1", new TaskFileService().Load(editor.CurrentTaskPath) == null ? null : TaskFileService.SchemaVersion,
                "default task schema");
        }

        private static void PredictionAdjustmentPreservesLearnedInputs()
        {
            var source = new TaskModel();
            source.Settings.Metadata.Name = "自动调整测试";
            source.Targets.Uniform.InternalPower = 123;
            source.Targets.Uniform.Emissivity = 0.7;
            source.Targets.Uniform.SolarAbsorption = 0.6;
            source.IndividualTargets[1].Motion.Position.X = 99;
            var adjusted = PreProcess.Wpf.Services.Execution.ReferenceTaskLoader.AdjustForPrediction(
                source, new PreProcess.Wpf.Services.Execution.BackendPathResolver().Resolve().PackageRoot);
            Equal("自动调整测试", adjusted.Settings.Metadata.Name, "prediction adjustment task name");
            Equal(123, adjusted.Targets.Uniform.InternalPower, "prediction internal power");
            Equal(0.7, adjusted.Targets.Uniform.Emissivity, "prediction emissivity");
            Equal(0.6, adjusted.Targets.Uniform.SolarAbsorption, "prediction solar absorption");
            Equal(0.3, adjusted.Targets.Uniform.IrReflection, "prediction complementary reflection");
            Equal(0, adjusted.IndividualTargets[1].Motion.Position.X, "prediction fixed scene adjustment");
        }

        private static void ResultBasedMappersDoNotRequireTaskPhysics()
        {
            var similarity = new SimilarityEvaluationMapper().Map(
                new ResultReference { RunDirectory = "reference/output" },
                new ResultReference { RunDirectory = "candidate/output" },
                new EvaluationConfig { TargetObjectId = 1, Metric = "temperature_similarity" });
            Equal("reference/output", similarity.Reference.RunDirectory, "reference result");
            Equal("candidate/output", similarity.Candidate.RunDirectory, "candidate result");

            var trajectory = new TrajectoryMapper().Map("run/output/trajectory_history.csv", true);
            Equal("run/output/trajectory_history.csv", trajectory.TrajectoryCsv, "trajectory result");
            True(trajectory.IncludePrerelease, "trajectory prerelease option");
        }

        private static void True(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Equal(int expected, int actual, string message)
        {
            if (expected != actual)
                throw new InvalidOperationException(message + ": expected " + expected + ", actual " + actual);
        }

        private static void Equal(double expected, double actual, string message)
        {
            if (Math.Abs(expected - actual) > 1e-12)
                throw new InvalidOperationException(message + ": expected " + expected + ", actual " + actual);
        }

        private static void Equal(string expected, string actual, string message)
        {
            if (!String.Equals(expected, actual, StringComparison.Ordinal))
                throw new InvalidOperationException(message + ": expected " + expected + ", actual " + actual);
        }
    }
}
