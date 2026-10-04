namespace PreProcess.Wpf.ViewModels
{
    /// <summary>
    /// 06 红外场景仿真结果摘要，只组织现有结果浏览器数据。
    /// </summary>
    public sealed class SceneSimulationSummaryViewModel : ObservableObject
    {
        private readonly ForwardRunViewModel execution;
        public ForwardRunViewModel Execution { get { return execution; } }
        public ResultBrowserViewModel ResultBrowser { get { return execution.ForwardResultBrowser; } }
        public string Status { get { return ResultBrowser.RunStatus; } }
        public string ResultLocation { get { return ResultBrowser.OutputDirectory ?? "尚无正向运行目录"; } }
        public bool HasResult { get { return ResultBrowser != null && ResultBrowser.HasResult; } }
        public string Availability { get { return HasResult ? "结果已生成，可进入后处理任务" : "尚无可用红外场景结果"; } }
        public string TimelineSummary { get { return ResultBrowser.Timeline == null ? ResultBrowser.TimelineText : "红外响应：" + ResultBrowser.Timeline.InfraredStatus + "；" + ResultBrowser.Timeline.Message; } }

        public SceneSimulationSummaryViewModel(ForwardRunViewModel execution)
        {
            this.execution = execution;
            execution.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(ForwardRunViewModel.ForwardResultBrowser) ||
                    args.PropertyName == nameof(ForwardRunViewModel.Status) ||
                    args.PropertyName == nameof(ForwardRunViewModel.ResultLocation))
                {
                    Notify(nameof(ResultBrowser));
                    Notify(nameof(Status));
                    Notify(nameof(ResultLocation));
                    Notify(nameof(HasResult));
                    Notify(nameof(Availability));
                    Notify(nameof(TimelineSummary));
                }
            };
        }
    }
}
