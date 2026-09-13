namespace PreProcess.Requests
{
    public class PredictionRequest
    {
        public ForwardSimulationRequest SourceRequest { get; set; }
        public double InternalPower { get; set; }
        public double EmissivityIr { get; set; }
        public double SolarAbsorption { get; set; }
        public string Mode { get; set; }
    }

    public class TrajectoryRequest
    {
        public string TrajectoryCsv { get; set; }
        public bool IncludePrerelease { get; set; }
    }

    public class SimilarityEvaluationRequest
    {
        public ResultReference Reference { get; set; }
        public ResultReference Candidate { get; set; }
        public EvaluationConfig Config { get; set; }
    }

    public class SceneBuildRequest
    {
        public string SourceRequestFile { get; set; }
        public double RequiredSimilarityPercent { get; set; }
        public int RequiredCandidateCount { get; set; }
    }

    public class ResultReference
    {
        public string RunDirectory { get; set; }
    }

    public class EvaluationConfig
    {
        public int TargetObjectId { get; set; }
        public string Metric { get; set; }
    }
}
