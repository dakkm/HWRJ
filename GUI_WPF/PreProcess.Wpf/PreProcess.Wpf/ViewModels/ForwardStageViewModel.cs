namespace PreProcess.Wpf.ViewModels
{
    /// <summary>
    /// 04/05 正向计算内部阶段的只读状态页，不拥有独立执行入口。
    /// </summary>
    public sealed class ForwardStageViewModel : ObservableObject
    {
        private readonly ForwardRunViewModel execution;
        public string StageTitle { get; private set; }
        public string StageRole { get; private set; }
        public ForwardRunViewModel Execution { get { return execution; } }
        public string Status { get { return execution.ForwardResultBrowser.RunStatus; } }
        public string ResultLocation { get { return execution.ForwardResultBrowser.OutputDirectory ?? "尚无正向运行目录"; } }
        public bool IsBusy { get { return execution.IsForward && execution.IsBusy; } }
        public string TimelineSummary
        {
            get
            {
                var timeline = execution.ForwardResultBrowser.Timeline;
                if (StageTitle.Contains("外热流")) return timeline == null ? "暂无正向结果；外热流暂无独立输出文件。" : timeline.HeatFluxStatus;
                return timeline == null ? execution.ForwardResultBrowser.TimelineText : "温度历史：" + timeline.TemperatureStatus + "；" + timeline.Message;
            }
        }

        public ForwardStageViewModel(string title, string role, ForwardRunViewModel execution)
        {
            StageTitle = title;
            StageRole = role;
            this.execution = execution;
            execution.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(ForwardRunViewModel.Status)) Notify(nameof(Status));
                if (args.PropertyName == nameof(ForwardRunViewModel.ResultLocation)) Notify(nameof(ResultLocation));
                if (args.PropertyName == nameof(ForwardRunViewModel.IsBusy)) Notify(nameof(IsBusy));
                if (args.PropertyName == nameof(ForwardRunViewModel.ForwardResultBrowser)) { Notify(nameof(TimelineSummary)); Notify(nameof(Status)); Notify(nameof(ResultLocation)); }
            };
        }
    }
}
