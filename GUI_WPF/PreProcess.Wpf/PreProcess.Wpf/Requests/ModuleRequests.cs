namespace PreProcess.Requests
{
    // 定义 PredictionRequest 类型，集中封装与该领域对象相关的状态和行为。
    public class PredictionRequest
    {
        public ForwardSimulationRequest SourceRequest { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public double InternalPower { get; set; }
        public double EmissivityIr { get; set; }
        public double SolarAbsorption { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string Mode { get; set; }
    }

    public class TrajectoryRequest
    {
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string TrajectoryCsv { get; set; }
        public bool IncludePrerelease { get; set; }
    }

    // 定义 SimilarityEvaluationRequest 类型，集中封装与该领域对象相关的状态和行为。
    public class SimilarityEvaluationRequest
    {
        public ResultReference Reference { get; set; }
        public ResultReference Candidate { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public EvaluationConfig Config { get; set; }
    }

    public class SceneBuildRequest
    {
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string SourceRequestFile { get; set; }
        public double RequiredSimilarityPercent { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public int RequiredCandidateCount { get; set; }
    }

    public class ResultReference
    {
        public string RunDirectory { get; set; }
    }

    // 定义 EvaluationConfig 类型，集中封装与该领域对象相关的状态和行为。
    public class EvaluationConfig
    {
        public int TargetObjectId { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string Metric { get; set; }
    }
}
