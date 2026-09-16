using System.Collections.Generic;
using System.Globalization;
using PreProcess.Requests;
using PreProcess.Wpf.Models;

namespace PreProcess.Wpf.Services.Mappers
{
    public sealed class ForwardSimulationMapper
    {
        public ForwardSimulationRequest Map(TaskModel task)
        {
            var targetPhysics = new List<TargetPhysicsRequest>(task.IndividualTargets.Count);
            var targetScene = new List<TargetSceneRequest>(task.IndividualTargets.Count);

            foreach (var target in task.IndividualTargets)
            {
                targetPhysics.Add(MapPhysics(target));
                targetScene.Add(MapScene(target));
            }

            return new ForwardSimulationRequest
            {
                Case = new CaseRequest
                {
                    TotalTimeSeconds = task.Settings.Duration,
                    TargetCount = task.Settings.TargetCount
                },
                Environment = new EnvironmentRequest
                {
                    SolarFlux = task.Environment.SolarFlux,
                    EnvironmentTemperature = task.Environment.RadiationTemperature,
                    SolarDirection = MapVector(task.Environment.SunDirection)
                },
                GroupState = new GroupStateRequest
                {
                    Center = MapVector(task.Scene.Position),
                    Direction = MapVector(task.Scene.Direction),
                    Velocity = MapVector(task.Scene.Velocity),
                    AngularVelocity = MapVector(task.Scene.AngularVelocity),
                    CompanionType = task.Scene.CompanionType == "球壳" ? "SPHERE" : task.Scene.CompanionType,
                    AttitudeMotionType = task.Scene.AttitudeMotionType == "默认" ? "NONE" : task.Scene.AttitudeMotionType,
                    MicroMotionParameters = MapVector(task.Scene.MicroMotionParameters),
                    SimilarityLevel = task.Settings.SimilarityIndex.ToString("0.########", CultureInfo.InvariantCulture)
                },
                Observation = new ObservationRequest
                {
                    ApertureCenter = MapVector(task.Environment.ObserverPosition),
                    DetectorNormal = MapVector(task.Environment.DetectorDirection),
                    FocalLength = task.Environment.FocalLength
                },
                TargetPhysics = targetPhysics,
                TargetScene = targetScene
            };
        }

        private static TargetPhysicsRequest MapPhysics(TargetInstance target)
        {
            var physics = target.EffectivePhysics;
            return new TargetPhysicsRequest
            {
                Id = target.Id,
                Radius = physics.Radius,
                Density = physics.Density,
                HeatCapacity = physics.HeatCapacity,
                InternalPower = physics.InternalPower,
                EmissivityIr = physics.Emissivity,
                SolarAbsorption = physics.SolarAbsorption,
                InitialTemperature = physics.InitialTemperature
            };
        }

        private static TargetSceneRequest MapScene(TargetInstance target)
        {
            return new TargetSceneRequest
            {
                Id = target.Id,
                Position = MapVector(target.Motion.Position),
                Velocity = MapVector(target.Motion.Velocity),
                ReleaseTime = target.Motion.ReleaseTime,
                Active = target.Motion.Active
            };
        }

        internal static double[] MapVector(Vector3 value)
        {
            return new[] { value.X, value.Y, value.Z };
        }
    }
}
