using System;
using System.Collections.Specialized;
using System.Windows.Input;
using PreProcess.Wpf.Models;

namespace PreProcess.Wpf.ViewModels
{
    public sealed class TaskEditorViewModel : ObservableObject
    {
        public ForwardRunViewModel Execution { get; internal set; }
        private TaskModel task;
        public TaskModel Task { get => task; private set { task = value; Notify(); } }
        private TargetInstance selectedTarget;
        public TargetInstance SelectedTarget
        {
            get => selectedTarget;
            set { if (value == null || selectedTarget == value) return; selectedTarget = value; Notify(); }
        }
        private string message = "有效输入自动保留在内存中；关闭程序后不保留。";
        public string Message { get => message; private set { message = value; Notify(); } }
        public ICommand NewCommand { get; }
        public ICommand OpenCommand { get; }
        public ICommand SaveCommand { get; }
        public TaskEditorViewModel()
        {
            Reset();
            NewCommand = new RelayCommand(_ => { Reset(); Message = "已新建内存任务。"; });
            OpenCommand = new RelayCommand(_ => Message = "打开任务尚未接入；当前内存任务保持不变。");
            SaveCommand = new RelayCommand(_ => Message = "有效输入已保留在内存；红框输入未写入，尚未保存到文件。");
        }
        private void Reset()
        { LoadTask(new TaskModel()); }
        public void LoadTask(TaskModel value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (Task != null) ((INotifyCollectionChanged)Task.IndividualTargets).CollectionChanged -= TargetsChanged;
            Task = value ?? throw new ArgumentNullException(nameof(value));
            ((INotifyCollectionChanged)Task.IndividualTargets).CollectionChanged += TargetsChanged;
            SelectedTarget = Task.IndividualTargets[0];
        }
        private void TargetsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (SelectedTarget == null || !Task.IndividualTargets.Contains(SelectedTarget))
                SelectedTarget = Task.IndividualTargets[0];
        }
    }
    public abstract class TaskPageViewModel
    {
        public TaskEditorViewModel Editor { get; }
        public string Title { get; }
        protected TaskPageViewModel(TaskEditorViewModel editor, string title) { Editor = editor; Title = title; }
    }
    public sealed class TaskSettingsViewModel : TaskPageViewModel
    { public TaskSettingsViewModel(TaskEditorViewModel editor) : base(editor, "任务设置") { } }
    public sealed class TargetSettingsViewModel : TaskPageViewModel
    { public TargetSettingsViewModel(TaskEditorViewModel editor) : base(editor, "目标参数") { } }
    public sealed class SceneMotionViewModel : TaskPageViewModel
    { public SceneMotionViewModel(TaskEditorViewModel editor) : base(editor, "场景与运动") { } }
    public sealed class EnvironmentObservationViewModel : TaskPageViewModel
    { public EnvironmentObservationViewModel(TaskEditorViewModel editor) : base(editor, "环境与观测") { } }
    public sealed class CalculationOutputViewModel : TaskPageViewModel
    { public CalculationOutputViewModel(TaskEditorViewModel editor) : base(editor, "计算与输出") { } }

    public abstract class RunManagementPageViewModel
    {
        public TaskEditorViewModel Editor { get; }
        public ForwardRunViewModel Execution => Editor.Execution;
        protected RunManagementPageViewModel(TaskEditorViewModel editor) { Editor = editor; }
    }
    public sealed class CurrentTaskViewModel : RunManagementPageViewModel
    { public CurrentTaskViewModel(TaskEditorViewModel editor) : base(editor) { } }
    public sealed class RunHistoryViewModel : RunManagementPageViewModel
    { public RunHistoryViewModel(TaskEditorViewModel editor) : base(editor) { } }
    public sealed class OutputResultsViewModel : RunManagementPageViewModel
    { public OutputResultsViewModel(TaskEditorViewModel editor) : base(editor) { } }
}

