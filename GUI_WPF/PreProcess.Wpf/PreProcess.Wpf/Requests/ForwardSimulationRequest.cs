using System.Collections.Generic;

namespace PreProcess.Requests
{
    public class ForwardSimulationRequest
    {
        public CaseRequest Case { get; set; }
        public EnvironmentRequest Environment { get; set; }
        public GroupStateRequest GroupState { get; set; }
        public ObservationRequest Observation { get; set; }
        public List<TargetPhysicsRequest> TargetPhysics { get; set; }
        public List<TargetSceneRequest> TargetScene { get; set; }
    }

    public class CaseRequest
    {
        public double TotalTimeSeconds { get; set; }
        public int TargetCount { get; set; }
    }

    public class EnvironmentRequest
    {
        public double SolarFlux { get; set; }
        public double EnvironmentTemperature { get; set; }
        public double[] SolarDirection { get; set; }
    }

    public class GroupStateRequest
    {
        public double[] Center { get; set; }
        public double[] Direction { get; set; }
        public double[] Velocity { get; set; }
        public double[] AngularVelocity { get; set; }
        public string CompanionType { get; set; }
        public string AttitudeMotionType { get; set; }
        public double[] MicroMotionParameters { get; set; }
        public string SimilarityLevel { get; set; }
    }

    public class ObservationRequest
    {
        public double[] ApertureCenter { get; set; }
        public double[] DetectorNormal { get; set; }
        public double FocalLength { get; set; }
    }

    public class TargetPhysicsRequest
    {
        public int Id { get; set; }
        public double Radius { get; set; }
        public double Density { get; set; }
        public double HeatCapacity { get; set; }
        public double InternalPower { get; set; }
        public double EmissivityIr { get; set; }
        public double SolarAbsorption { get; set; }
        public double InitialTemperature { get; set; }
    }

    public class TargetSceneRequest
    {
        public int Id { get; set; }
        public double[] Position { get; set; }
        public double[] Velocity { get; set; }
        public double ReleaseTime { get; set; }
        public bool Active { get; set; }
    }
}
