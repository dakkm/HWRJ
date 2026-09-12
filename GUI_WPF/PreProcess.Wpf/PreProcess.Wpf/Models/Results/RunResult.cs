using System;
using System.Collections.Generic;

namespace PreProcess.Wpf.Models.Results
{
    public sealed class RunResult
    {
        public RunResult()
        {
            Summary = new Dictionary<string, string>();
            Issues = new List<string>();
            Temperatures = new List<TemperatureResult>();
            Trajectories = new List<TrajectoryResult>();
            InfraredResponses = new List<InfraredResult>();
            PointImages = new List<PointImageResult>();
            Similarities = new List<SimilarityResult>();
            Scenes = new List<SceneResult>();
        }

        public string RunId { get; set; }
        public ResultModuleType ModuleType { get; set; }
        public string ModuleCode { get; set; }
        public string InputRequestPath { get; set; }
        public string RunDirectory { get; set; }
        public string OutputDirectory { get; set; }
        public bool ResultExists { get; set; }
        public string AvailabilityMessage { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? EndedAt { get; set; }
        public string RunState { get; set; }
        public IDictionary<string, string> Summary { get; private set; }
        public IList<string> Issues { get; private set; }

        public IList<TemperatureResult> Temperatures { get; private set; }
        public IList<TrajectoryResult> Trajectories { get; private set; }
        public IList<InfraredResult> InfraredResponses { get; private set; }
        public IList<PointImageResult> PointImages { get; private set; }
        public IList<SimilarityResult> Similarities { get; private set; }
        public IList<SceneResult> Scenes { get; private set; }
    }
}
