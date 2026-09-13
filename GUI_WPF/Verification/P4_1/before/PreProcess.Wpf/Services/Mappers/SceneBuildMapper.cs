using PreProcess.Requests;
using PreProcess.Wpf.Models;

namespace PreProcess.Wpf.Services.Mappers
{
    public sealed class SceneBuildMapper
    {
        public SceneBuildRequest Map(
            TaskModel task,
            string sourceRequestFile,
            int requiredCandidateCount)
        {
            return new SceneBuildRequest
            {
                SourceRequestFile = sourceRequestFile,
                RequiredSimilarityPercent = task.Settings.SimilarityIndex,
                RequiredCandidateCount = requiredCandidateCount
            };
        }
    }
}
