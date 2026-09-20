using System.Collections.Generic;
using PreProcess.Requests;
using PreProcess.Wpf.Models;

namespace PreProcess.Wpf.Services.Mappers
{
    public sealed class ForwardSimulationMapper
    {
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public ForwardSimulationRequest Map(TaskModel task)
        {
            var targetPhysics = new List<TargetPhysicsRequest>(task.IndividualTargets.Count);
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var targetScene = new List<TargetSceneRequest>(task.IndividualTargets.Count);

            foreach (var target in task.IndividualTargets)
            {
                // 将当前结果加入集合，供后续汇总或界面展示。
                targetPhysics.Add(MapPhysics(target));
                targetScene.Add(MapScene(target));
            }

            return new ForwardSimulationRequest
            {
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                Case = new CaseRequest
                {
                    TotalTimeSeconds = task.Settings.Duration,
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    TargetCount = task.Settings.TargetCount
                },
                Environment = new EnvironmentRequest
                {
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    SolarFlux = task.Environment.SolarFlux,
                    EnvironmentTemperature = task.Environment.RadiationTemperature,
                    SolarDirection = MapVector(task.Environment.SunDirection)
                },
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                GroupState = new GroupStateRequest
                {
                    Center = MapVector(task.Scene.Position),
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    Direction = MapVector(task.Scene.Direction),
                    Velocity = MapVector(task.Scene.Velocity),
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    AngularVelocity = MapVector(task.Scene.AngularVelocity),
                    CompanionType = task.Scene.CompanionType == "球壳" ? "SPHERE" : task.Scene.CompanionType,
                    AttitudeMotionType = task.Scene.AttitudeMotionType == "默认" ? "NONE" : task.Scene.AttitudeMotionType,
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    MicroMotionParameters = MapVector(task.Scene.MicroMotionParameters),
                    // Backend scene-contract token; module 04 maps SimilarityIndex separately
                    // to its numeric required_percent acceptance threshold.
                    SimilarityLevel = "DATASET_BASIC"
                },
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                Observation = new ObservationRequest
                // 说明当前文件承载的类型职责，便于维护者快速定位功能边界。
                {
                    ApertureCenter = MapVector(task.Environment.ObserverPosition),
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    DetectorNormal = MapVector(task.Environment.DetectorDirection),
                    FocalLength = task.Environment.FocalLength
                },
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                TargetPhysics = targetPhysics,
                TargetScene = targetScene
            };
        }

        private static TargetPhysicsRequest MapPhysics(TargetInstance target)
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var physics = target.EffectivePhysics;
            return new TargetPhysicsRequest
            {
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                Id = target.Id,
                Radius = physics.Radius,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                Density = physics.Density,
                HeatCapacity = physics.HeatCapacity,
                InternalPower = physics.InternalPower,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                EmissivityIr = physics.Emissivity,
                SolarAbsorption = physics.SolarAbsorption,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                InitialTemperature = physics.InitialTemperature
            };
        }

        private static TargetSceneRequest MapScene(TargetInstance target)
        {
            // 返回当前步骤生成的结果，并结束本次调用。
            return new TargetSceneRequest
            {
                Id = target.Id,
                Position = MapVector(target.Motion.Position),
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                Velocity = MapVector(target.Motion.Velocity),
                ReleaseTime = target.Motion.ReleaseTime,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                Active = target.Motion.Active
            };
        }

        internal static double[] MapVector(Vector3 value)
        {
            // 返回当前步骤生成的结果，并结束本次调用。
            return new[] { value.X, value.Y, value.Z };
        }
    }
}
