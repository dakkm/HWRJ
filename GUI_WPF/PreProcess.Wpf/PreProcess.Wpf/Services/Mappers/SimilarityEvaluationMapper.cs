using PreProcess.Requests;

namespace PreProcess.Wpf.Services.Mappers
{
    public sealed class SimilarityEvaluationMapper
    {
        public SimilarityEvaluationRequest Map(
            ResultReference reference,
            ResultReference candidate,
            EvaluationConfig config)
        {
            return new SimilarityEvaluationRequest
            {
                Reference = new ResultReference { RunDirectory = reference.RunDirectory },
                Candidate = new ResultReference { RunDirectory = candidate.RunDirectory },
                Config = new EvaluationConfig
                {
                    TargetObjectId = config.TargetObjectId,
                    Metric = config.Metric
                }
            };
        }
    }
}
