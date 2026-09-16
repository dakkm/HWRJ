using System;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Windows.Input;
using Microsoft.Win32;
using PreProcess.Wpf.Models;
using PreProcess.Wpf.Services;
using PreProcess.Wpf.Services.Execution;

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
        private string message = "正在加载默认任务。";
        public string Message { get => message; private set { message = value; Notify(); } }
        private string currentTaskPath;
        public string CurrentTaskPath { get => currentTaskPath; private set { currentTaskPath = value; Notify(); } }
        private string resultTaskDirectory;
        public string ResultTaskDirectory { get => resultTaskDirectory; private set { resultTaskDirectory = value; Notify(); } }
        public ICommand OpenCommand { get; }
        public ICommand SaveCommand { get; }
        public TaskEditorViewModel()
        {
            LoadDefaultTask();
            OpenCommand = new RelayCommand(_ => OpenTask());
            SaveCommand = new RelayCommand(_ => SaveTask());
        }
        private void Reset()
        { LoadTask(new TaskModel()); }
        private void LoadDefaultTask()
        {
            try
            {
                string runtimeRoot = new BackendPathResolver().ResolveRuntimeRoot();
                Directory.CreateDirectory(runtimeRoot);
                string defaultPath = Path.Combine(runtimeRoot, "default.task.json");
                string path = ReadLastTaskPath(runtimeRoot);
                if (String.IsNullOrWhiteSpace(path) || !File.Exists(path)) path = defaultPath;
                var files = new TaskFileService();
                TaskModel selected = File.Exists(path) ? files.Load(path) : new TaskModel();
                if (!File.Exists(path)) files.Save(selected, defaultPath);
                if (!File.Exists(path)) path = defaultPath;
                string taskResults = TaskDirectoryManager.SelectTask(runtimeRoot, path, selected.Settings.Metadata.Name);
                LoadTask(selected); CurrentTaskPath = Path.GetFullPath(path); ResultTaskDirectory = taskResults;
                Message = String.Equals(CurrentTaskPath, Path.GetFullPath(defaultPath), StringComparison.OrdinalIgnoreCase)
                    ? "已自动加载默认任务：" + CurrentTaskPath
                    : "已自动加载上次任务：" + CurrentTaskPath;
            }
            catch (Exception ex)
            {
                Reset(); CurrentTaskPath = null; ResultTaskDirectory = null;
                Message = "默认任务加载失败，已使用内存默认参数：" + ex.Message;
            }
        }
        private void OpenTask()
        {
            try
            {
                var dialog = new OpenFileDialog { Filter = "红外场景任务 (*.task.json)|*.task.json|JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*", CheckFileExists = true, Multiselect = false };
                if (dialog.ShowDialog() != true) return;
                TaskModel loaded = new TaskFileService().Load(dialog.FileName);
                string fullPath = Path.GetFullPath(dialog.FileName);
                string taskResults = TaskDirectoryManager.SelectTask(new BackendPathResolver().ResolveRuntimeRoot(), fullPath, loaded.Settings.Metadata.Name);
                LoadTask(loaded); CurrentTaskPath = fullPath; ResultTaskDirectory = taskResults;
                RememberLastTask(fullPath);
                Message = "任务已打开：" + CurrentTaskPath;
            }
            catch (Exception ex) { Message = "打开任务失败：" + ex.Message; }
        }
        private void SaveTask()
        {
            try
            {
                string path = CurrentTaskPath;
                // The startup task is a template. Never overwrite it from the
                // normal Save command; require the user to choose a task file
                // first. Tasks opened explicitly can still be saved in place.
                if (IsDefaultTaskPath(path)) path = null;
                if (String.IsNullOrWhiteSpace(path))
                {
                    var dialog = new SaveFileDialog { Filter = "红外场景任务 (*.task.json)|*.task.json|JSON 文件 (*.json)|*.json", DefaultExt = ".task.json", AddExtension = true,
                        FileName = SafeFileName(Task.Settings.Metadata.Name) + ".task.json", OverwritePrompt = true };
                    if (dialog.ShowDialog() != true) return;
                    path = dialog.FileName;
                }
                new TaskFileService().Save(Task, path);
                CurrentTaskPath = Path.GetFullPath(path);
                RememberLastTask(CurrentTaskPath);
                ResultTaskDirectory = TaskDirectoryManager.SelectTask(new BackendPathResolver().ResolveRuntimeRoot(), CurrentTaskPath, Task.Settings.Metadata.Name);
                Message = "任务已保存：" + CurrentTaskPath;
            }
            catch (Exception ex) { Message = "保存任务失败：" + ex.Message; }
        }
        private static bool IsDefaultTaskPath(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return false;
            string runtimeRoot = new BackendPathResolver().ResolveRuntimeRoot();
            string defaultPath = Path.Combine(runtimeRoot, "default.task.json");
            return String.Equals(Path.GetFullPath(path), Path.GetFullPath(defaultPath), StringComparison.OrdinalIgnoreCase);
        }
        private static string LastTaskPointer(string runtimeRoot) => Path.Combine(runtimeRoot, "last-task.path");
        private static string ReadLastTaskPath(string runtimeRoot)
        {
            try
            {
                string pointer = LastTaskPointer(runtimeRoot);
                if (!File.Exists(pointer)) return null;
                string path = File.ReadAllText(pointer).Trim();
                return String.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
            }
            catch (Exception) { return null; }
        }
        private static void RememberLastTask(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return;
            try
            {
                string runtimeRoot = new BackendPathResolver().ResolveRuntimeRoot();
                Directory.CreateDirectory(runtimeRoot);
                File.WriteAllText(LastTaskPointer(runtimeRoot), Path.GetFullPath(path) + Environment.NewLine, new UTF8Encoding(false));
            }
            catch (Exception) { /* task save/open must not fail because the pointer cannot be written */ }
        }
        private static string SafeFileName(string value)
        {
            string name = String.IsNullOrWhiteSpace(value) ? "未命名任务" : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
            return String.IsNullOrWhiteSpace(name) ? "未命名任务" : name;
        }
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

