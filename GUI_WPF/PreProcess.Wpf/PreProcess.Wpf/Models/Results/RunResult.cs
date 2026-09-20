using System;
using System.Collections.Generic;

namespace PreProcess.Wpf.Models.Results
{
    public sealed class RunResult
    {
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public RunResult()
        {
            Summary = new Dictionary<string, string>();
            Issues = new List<string>();
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            Temperatures = new List<TemperatureResult>();
            Trajectories = new List<TrajectoryResult>();
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            InfraredResponses = new List<InfraredResult>();
            PointImages = new List<PointImageResult>();
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            Similarities = new List<SimilarityResult>();
            Scenes = new List<SceneResult>();
        }

        public string RunId { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public ResultModuleType ModuleType { get; set; }
        public string ModuleCode { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string InputRequestPath { get; set; }
        public string RunDirectory { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string OutputDirectory { get; set; }
        public bool ResultExists { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string AvailabilityMessage { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? EndedAt { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string RunState { get; set; }
        public IDictionary<string, string> Summary { get; private set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public IList<string> Issues { get; private set; }

        public IList<TemperatureResult> Temperatures { get; private set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public IList<TrajectoryResult> Trajectories { get; private set; }
        public IList<InfraredResult> InfraredResponses { get; private set; }
        public IList<PointImageResult> PointImages { get; private set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public IList<SimilarityResult> Similarities { get; private set; }
        public IList<SceneResult> Scenes { get; private set; }
    }
}
