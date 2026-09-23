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
    // 定义 TaskEditorViewModel 类型，集中封装与该领域对象相关的状态和行为。
    public sealed class TaskEditorViewModel : ObservableObject
    {
        public ForwardRunViewModel Execution { get; internal set; }
        // 保存该组件运行所需的配置或中间状态。
        private TaskModel task;
        public TaskModel Task { get => task; private set { task = value; Notify(); } }
        private TargetInstance selectedTarget;
        // 保存该组件运行所需的配置或中间状态。
        public TargetInstance SelectedTarget
        {
            get => selectedTarget;
            // 通知界面绑定层刷新相关状态，确保显示内容与模型一致。
            set { if (value == null || selectedTarget == value) return; selectedTarget = value; Notify(); }
        }
        private string message = "正在加载默认任务。";
        // 通知界面绑定层刷新相关状态，确保显示内容与模型一致。
        public string Message { get => message; private set { message = value; Notify(); } }
        private string currentTaskPath;
        public string CurrentTaskPath { get => currentTaskPath; private set { currentTaskPath = value; Notify(); } }
        // 保存该组件运行所需的配置或中间状态。
        private string resultTaskDirectory;
        public string ResultTaskDirectory { get => resultTaskDirectory; private set { resultTaskDirectory = value; Notify(); } }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public ICommand OpenCommand { get; }
        public ICommand SaveCommand { get; }
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public TaskEditorViewModel()
        {
            LoadDefaultTask();
            OpenCommand = new RelayCommand(_ => OpenTask());
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            SaveCommand = new RelayCommand(_ => SaveTask());
        }
        private void Reset()
        // 调用对应组件完成当前步骤，并保留产生的处理结果。
        { LoadTask(new TaskModel()); }
        private void LoadDefaultTask()
        {
            // 进入可能失败的操作区间，并由后续异常分支统一处理故障。
            try
            {
                string runtimeRoot = new BackendPathResolver().ResolveRuntimeRoot();
                // 处理文件系统路径及数据，并在使用前确认目标有效。
                Directory.CreateDirectory(runtimeRoot);
                string defaultPath = Path.Combine(runtimeRoot, "default.task.json");
                string path = ReadLastTaskPath(runtimeRoot);
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (String.IsNullOrWhiteSpace(path) || !File.Exists(path)) path = defaultPath;
                var files = new TaskFileService();
                // 处理文件系统路径及数据，并在使用前确认目标有效。
                // The startup template is generated from the packaged surrogate
                // reference scene. Rebuild it even when an older template exists,
                // otherwise module 02 can receive a scene outside its contract.
                TaskModel selected;
                if (String.Equals(Path.GetFullPath(path), Path.GetFullPath(defaultPath), StringComparison.OrdinalIgnoreCase))
                {
                    string package = new BackendPathResolver().Resolve().PackageRoot;
                    selected = ReferenceTaskLoader.Load(package);
                    files.Save(selected, defaultPath);
                }
                else
                {
                    selected = files.Load(path);
                    // Older releases persisted the demo task as 001.task.json
                    // with targets laid out at x=0..15 and zero velocity. Treat
                    // that specific legacy template as the startup template so
                    // it cannot mask the packaged intelligent-prediction scene.
                    if (LooksLikeLegacyDefaultScene(selected))
                    {
                        double duration = selected.Settings.Duration;
                        string package = new BackendPathResolver().Resolve().PackageRoot;
                        selected = ReferenceTaskLoader.Load(package);
                        // Scene migration must not silently change the user's
                        // simulation horizon (e.g. 200 s -> reference 1000 s).
                        selected.Settings.Duration = duration;
                        files.Save(selected, defaultPath);
                        path = defaultPath;
                    }
                }
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (!File.Exists(path)) path = defaultPath;
                string taskResults = TaskDirectoryManager.SelectTask(runtimeRoot, path, selected.Settings.Metadata.Name);
                LoadTask(selected); CurrentTaskPath = Path.GetFullPath(path); ResultTaskDirectory = taskResults;
                // 处理文件系统路径及数据，并在使用前确认目标有效。
                Message = String.Equals(CurrentTaskPath, Path.GetFullPath(defaultPath), StringComparison.OrdinalIgnoreCase)
                    ? "已自动加载默认任务：" + CurrentTaskPath
                    // 继续处理当前业务步骤，保持上下文状态一致。
                    : "已自动加载上次任务：" + CurrentTaskPath;
            }
            catch (Exception ex)
            {
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                Reset(); CurrentTaskPath = null; ResultTaskDirectory = null;
                Message = "默认任务加载失败，已使用内存默认参数：" + ex.Message;
            }
        }

        private static bool LooksLikeLegacyDefaultScene(TaskModel value)
        {
            if (value == null || value.IndividualTargets.Count < 2 ||
                !String.Equals(value.Settings.Metadata.Name, "001", StringComparison.OrdinalIgnoreCase)) return false;
            for (int i = 0; i < value.IndividualTargets.Count; i++)
            {
                var motion = value.IndividualTargets[i].Motion;
                if (Math.Abs(motion.Position.X - i) > 1e-9 ||
                    Math.Abs(motion.Position.Y) > 1e-9 || Math.Abs(motion.Position.Z) > 1e-9 ||
                    Math.Abs(motion.Velocity.X) > 1e-9 || Math.Abs(motion.Velocity.Y) > 1e-9 ||
                    Math.Abs(motion.Velocity.Z) > 1e-9 || Math.Abs(motion.ReleaseTime) > 1e-9)
                    return false;
            }
            return true;
        }
        private void OpenTask()
        {
            // 进入可能失败的操作区间，并由后续异常分支统一处理故障。
            try
            {
                var dialog = new OpenFileDialog { Filter = "红外场景任务 (*.task.json)|*.task.json|JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*", CheckFileExists = true, Multiselect = false };
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (dialog.ShowDialog() != true) return;
                TaskModel loaded = new TaskFileService().Load(dialog.FileName);
                // 处理文件系统路径及数据，并在使用前确认目标有效。
                string fullPath = Path.GetFullPath(dialog.FileName);
                string taskResults = TaskDirectoryManager.SelectTask(new BackendPathResolver().ResolveRuntimeRoot(), fullPath, loaded.Settings.Metadata.Name);
                LoadTask(loaded); CurrentTaskPath = fullPath; ResultTaskDirectory = taskResults;
                // 调用对应组件完成当前步骤，并保留产生的处理结果。
                RememberLastTask(fullPath);
                Message = "任务已打开：" + CurrentTaskPath;
            }
            // 捕获本步骤产生的异常，将故障转换为可追踪的运行状态。
            catch (Exception ex) { Message = "打开任务失败：" + ex.Message; }
        }
        private void SaveTask()
        {
            // 进入可能失败的操作区间，并由后续异常分支统一处理故障。
            try
            {
                string path = CurrentTaskPath;
                // The startup task is a template. Never overwrite it from the
                // normal Save command; require the user to choose a task file
                // first. Tasks opened explicitly can still be saved in place.
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (IsDefaultTaskPath(path)) path = null;
                if (String.IsNullOrWhiteSpace(path))
                {
                    var dialog = new SaveFileDialog { Filter = "红外场景任务 (*.task.json)|*.task.json|JSON 文件 (*.json)|*.json", DefaultExt = ".task.json", AddExtension = true,
                        // 更新当前流程使用的数据，为下一处理步骤做好准备。
                        FileName = SafeFileName(Task.Settings.Metadata.Name) + ".task.json", OverwritePrompt = true };
                    if (dialog.ShowDialog() != true) return;
                    // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
                    path = dialog.FileName;
                }
                new TaskFileService().Save(Task, path);
                // 处理文件系统路径及数据，并在使用前确认目标有效。
                CurrentTaskPath = Path.GetFullPath(path);
                RememberLastTask(CurrentTaskPath);
                ResultTaskDirectory = TaskDirectoryManager.SelectTask(new BackendPathResolver().ResolveRuntimeRoot(), CurrentTaskPath, Task.Settings.Metadata.Name);
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                Message = "任务已保存：" + CurrentTaskPath;
            }
            catch (Exception ex) { Message = "保存任务失败：" + ex.Message; }
        }
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static bool IsDefaultTaskPath(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return false;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            string runtimeRoot = new BackendPathResolver().ResolveRuntimeRoot();
            string defaultPath = Path.Combine(runtimeRoot, "default.task.json");
            return String.Equals(Path.GetFullPath(path), Path.GetFullPath(defaultPath), StringComparison.OrdinalIgnoreCase);
        }
        // 处理文件系统路径及数据，并在使用前确认目标有效。
        private static string LastTaskPointer(string runtimeRoot) => Path.Combine(runtimeRoot, "last-task.path");
        private static string ReadLastTaskPath(string runtimeRoot)
        {
            // 进入可能失败的操作区间，并由后续异常分支统一处理故障。
            try
            {
                string pointer = LastTaskPointer(runtimeRoot);
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (!File.Exists(pointer)) return null;
                string path = File.ReadAllText(pointer).Trim();
                return String.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
            }
            // 捕获本步骤产生的异常，将故障转换为可追踪的运行状态。
            catch (Exception) { return null; }
        }
        private static void RememberLastTask(string path)
        {
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (String.IsNullOrWhiteSpace(path)) return;
            try
            {
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                string runtimeRoot = new BackendPathResolver().ResolveRuntimeRoot();
                Directory.CreateDirectory(runtimeRoot);
                File.WriteAllText(LastTaskPointer(runtimeRoot), Path.GetFullPath(path) + Environment.NewLine, new UTF8Encoding(false));
            }
            // 捕获本步骤产生的异常，将故障转换为可追踪的运行状态。
            catch (Exception) { /* task save/open must not fail because the pointer cannot be written */ }
        }
        private static string SafeFileName(string value)
        {
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            string name = String.IsNullOrWhiteSpace(value) ? "未命名任务" : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
            // 返回当前步骤生成的结果，并结束本次调用。
            return String.IsNullOrWhiteSpace(name) ? "未命名任务" : name;
        }
        public void LoadTask(TaskModel value)
        {
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (Task != null) ((INotifyCollectionChanged)Task.IndividualTargets).CollectionChanged -= TargetsChanged;
            Task = value ?? throw new ArgumentNullException(nameof(value));
            // 通知界面绑定层刷新相关状态，确保显示内容与模型一致。
            ((INotifyCollectionChanged)Task.IndividualTargets).CollectionChanged += TargetsChanged;
            SelectedTarget = Task.IndividualTargets[0];
        }
        // 通知界面绑定层刷新相关状态，确保显示内容与模型一致。
        private void TargetsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (SelectedTarget == null || !Task.IndividualTargets.Contains(SelectedTarget))
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                SelectedTarget = Task.IndividualTargets[0];
        }
    }
    public abstract class TaskPageViewModel
    {
        public TaskEditorViewModel Editor { get; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string Title { get; }
        protected TaskPageViewModel(TaskEditorViewModel editor, string title) { Editor = editor; Title = title; }
    }
    // 定义 TaskSettingsViewModel 类型，集中封装与该领域对象相关的状态和行为。
    public sealed class TaskSettingsViewModel : TaskPageViewModel
    { public TaskSettingsViewModel(TaskEditorViewModel editor) : base(editor, "任务设置") { } }
    // 定义 TargetSettingsViewModel 类型，集中封装与该领域对象相关的状态和行为。
    public sealed class TargetSettingsViewModel : TaskPageViewModel
    { public TargetSettingsViewModel(TaskEditorViewModel editor) : base(editor, "目标参数") { } }
    public sealed class SceneMotionViewModel : TaskPageViewModel
    // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
    { public SceneMotionViewModel(TaskEditorViewModel editor) : base(editor, "场景与运动") { } }
    public sealed class EnvironmentObservationViewModel : TaskPageViewModel
    // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
    { public EnvironmentObservationViewModel(TaskEditorViewModel editor) : base(editor, "环境与观测") { } }
    public abstract class RunManagementPageViewModel
    {
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public TaskEditorViewModel Editor { get; }
        public ForwardRunViewModel Execution => Editor.Execution;
        protected RunManagementPageViewModel(TaskEditorViewModel editor) { Editor = editor; }
    }
    // 定义 CurrentTaskViewModel 类型，集中封装与该领域对象相关的状态和行为。
    public sealed class CurrentTaskViewModel : RunManagementPageViewModel
    { public CurrentTaskViewModel(TaskEditorViewModel editor) : base(editor) { } }
    // 定义 RunHistoryViewModel 类型，集中封装与该领域对象相关的状态和行为。
    public sealed class RunHistoryViewModel : RunManagementPageViewModel
    { public RunHistoryViewModel(TaskEditorViewModel editor) : base(editor) { } }
    // 定义 OutputResultsViewModel 类型，集中封装与该领域对象相关的状态和行为。
    public sealed class OutputResultsViewModel : RunManagementPageViewModel
    { public OutputResultsViewModel(TaskEditorViewModel editor) : base(editor) { } }
}

