using System;
using System.Collections.Generic;
using PreProcess.Requests;
using PreProcess.Validation;
using PreProcess.Wpf.Models;
using PreProcess.Wpf.Services.Mappers;

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
            var request = new ForwardSimulationMapper().Map(task);

            Equal(16, request.Case.TargetCount, "default target count");
            Equal(1000.0, request.Case.TotalTimeSeconds, "default duration");
            True(request.TargetPhysics != null, "TargetPhysics must exist");
            Equal(16, request.TargetPhysics.Count, "TargetPhysics count");
            Equal(0.2, request.TargetPhysics[0].Radius, "outer radius");
            Equal(2700.0, request.TargetPhysics[0].Density, "density");
            Equal(1, request.TargetPhysics[0].Id, "first target id");
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
