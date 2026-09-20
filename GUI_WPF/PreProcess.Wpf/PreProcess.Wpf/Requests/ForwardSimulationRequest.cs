using System.Collections.Generic;

namespace PreProcess.Requests
{
    public class ForwardSimulationRequest
    {
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public CaseRequest Case { get; set; }
        public EnvironmentRequest Environment { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public GroupStateRequest GroupState { get; set; }
        public ObservationRequest Observation { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public List<TargetPhysicsRequest> TargetPhysics { get; set; }
        public List<TargetSceneRequest> TargetScene { get; set; }
    }

    public class CaseRequest
    {
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public double TotalTimeSeconds { get; set; }
        public int TargetCount { get; set; }
    }

    // 定义 EnvironmentRequest 类型，集中封装与该领域对象相关的状态和行为。
    public class EnvironmentRequest
    {
        public double SolarFlux { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public double EnvironmentTemperature { get; set; }
        public double[] SolarDirection { get; set; }
    }

    // 定义 GroupStateRequest 类型，集中封装与该领域对象相关的状态和行为。
    public class GroupStateRequest
    {
        public double[] Center { get; set; }
        public double[] Direction { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public double[] Velocity { get; set; }
        public double[] AngularVelocity { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string CompanionType { get; set; }
        public string AttitudeMotionType { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public double[] MicroMotionParameters { get; set; }
        public string SimilarityLevel { get; set; }
    }

    public class ObservationRequest
    {
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public double[] ApertureCenter { get; set; }
        public double[] DetectorNormal { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public double FocalLength { get; set; }
    }

    public class TargetPhysicsRequest
    {
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public int Id { get; set; }
        public double Radius { get; set; }
        public double Density { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public double HeatCapacity { get; set; }
        public double InternalPower { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public double EmissivityIr { get; set; }
        public double SolarAbsorption { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public double InitialTemperature { get; set; }
    }

    public class TargetSceneRequest
    {
        public int Id { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public double[] Position { get; set; }
        public double[] Velocity { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public double ReleaseTime { get; set; }
        public bool Active { get; set; }
    }
}
