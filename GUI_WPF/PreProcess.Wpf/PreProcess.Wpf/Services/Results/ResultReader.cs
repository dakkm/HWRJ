using System;
using PreProcess.Wpf.Models.Results;
using PreProcess.Wpf.Services.Execution;

namespace PreProcess.Wpf.Services.Results
{
    public sealed class ResultReader
    {
        public RunResult Read(RunRecord record, ResultModuleType? moduleOverride = null)
        {
            return Read(new ResultDirectoryManager().Create(record, moduleOverride));
        }

        public RunResult Read(RunResult result)
        {
            if (result == null) throw new ArgumentNullException("result");
            switch (result.ModuleType)
            {
                case ResultModuleType.Forward:
                case ResultModuleType.Trajectory: return new ForwardResultReader().Read(result);
                case ResultModuleType.Prediction: return new PredictionResultReader().Read(result);
                case ResultModuleType.Similarity: return new SimilarityResultReader().Read(result);
                case ResultModuleType.Scene: return new SceneResultReader().Read(result);
                default: throw new ArgumentOutOfRangeException("result");
            }
        }
    }
}
