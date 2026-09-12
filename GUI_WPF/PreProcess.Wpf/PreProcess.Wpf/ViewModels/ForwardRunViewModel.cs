using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using PreProcess.Wpf.Models.Results;
using PreProcess.Wpf.Services.Execution;
using PreProcess.Wpf.Services.Process;
using PreProcess.Wpf.Services.Results;

namespace PreProcess.Wpf.ViewModels
{
    public sealed class ForwardRunViewModel : ObservableObject, IDisposable
    {
        private readonly TaskEditorViewModel editor;
        private readonly ForwardSimulationService service = new ForwardSimulationService();
        private readonly PredictionService prediction = new PredictionService();
        private readonly SimilarityEvaluationService similarity = new SimilarityEvaluationService();
        private readonly SceneBuildService scene = new SceneBuildService();
        private readonly Dispatcher dispatcher;
        private readonly DispatcherTimer timer;
        private readonly ConcurrentQueue<ProcessLogEvent> pending = new ConcurrentQueue<ProcessLogEvent>();
        private readonly ResultBrowserStore resultBrowsers = new ResultBrowserStore();
        private Task<RunRecord> current;
        private string selectedModule = "01";
        public string[] Modules => new[] { "01", "02", "轨迹", "03", "04" };
        public string SelectedModule { get => selectedModule; set { if (IsBusy || Array.IndexOf(Modules, value) < 0) return; selectedModule = value; Notify(); Notify(nameof(IsForward)); Notify(nameof(IsPrediction)); Notify(nameof(IsSimilarity)); Notify(nameof(IsScene)); Notify(nameof(ModuleTitle)); ShowSelectedModuleResult(); } }
        public bool IsForward => SelectedModule == "01" || SelectedModule == "轨迹";
        public bool IsPrediction => SelectedModule == "02";
        public bool IsSimilarity => SelectedModule == "03";
        public bool IsScene => SelectedModule == "04";
        public string ModuleTitle => IsPrediction ? "智能预测" : IsSimilarity ? "相似度评估" : IsScene ? "红外场景构建" : SelectedModule == "轨迹" ? "轨迹生成（调用正向计算）" : "正向计算";
        public string[] PredictionModes => new[] { "temperature", "point-image", "both" };
        public string PredictionMode { get; set; } = "temperature";
        public string ReferenceDirectory { get; set; }
        public string CandidateDirectory { get; set; }
        public string EvaluationConfig { get; set; }
        private int requiredCandidates = 10;
        public int RequiredCandidates { get => requiredCandidates; set { if (value <= 0) throw new ArgumentException("候选数量必须为正整数。"); requiredCandidates = value; Notify(); } }
        public ICommand LoadReferenceCommand { get; }
        public ICommand ImportResultCommand { get; }
        private bool busy, stopping, prepareOnly;
        private string status = "就绪", log = "", warnings = "", location = "尚无运行目录";
        public bool IsBusy { get => busy; private set { busy = value; Notify(); Notify(nameof(CanEdit)); CommandManager.InvalidateRequerySuggested(); } }
        public bool CanEdit => !IsBusy;
        public bool PrepareOnly { get => prepareOnly; set { if (IsBusy) return; prepareOnly = value; Notify(); } }
        public string Status { get => status; private set { status = value; Notify(); } }
        public string LogText { get => log; private set { log = value; Notify(); } }
        public string WarningText { get => warnings; private set { warnings = value; Notify(); } }
        public string ResultLocation { get => location; private set { location = value; Notify(); } }
        private ResultBrowserViewModel resultBrowser;
        public ResultBrowserViewModel ResultBrowser { get => resultBrowser; private set { resultBrowser = value; Notify(); } }
        private RunRecord lastRun;
        public RunRecord LastRun { get => lastRun; private set { lastRun = value; Notify(); } }
        public ObservableCollection<RunRecord> RunHistory { get; } = new ObservableCollection<RunRecord>();
        public ICommand RunCommand { get; }
        public ICommand StopCommand { get; }
        public Func<bool> HasInputErrors { get; set; }

        public ForwardRunViewModel(TaskEditorViewModel editor)
        {
            this.editor = editor;
            dispatcher = Dispatcher.CurrentDispatcher;
            ResultBrowser = ResultBrowserViewModel.Empty(ModuleTitle);
            RunCommand = new RunCommandImpl(async () => await StartAsync(), () => !IsBusy);
            StopCommand = new RunCommandImpl(Stop, () => IsBusy && !stopping);
            ImportResultCommand = new RunCommandImpl(async () => await ImportResultAsync(), () => !IsBusy);
            LoadReferenceCommand = new RunCommandImpl(() =>
            {
                if (System.Windows.MessageBox.Show("载入后端固定参考任务会替换当前内存任务。是否继续？", "载入参考任务", System.Windows.MessageBoxButton.YesNo) != System.Windows.MessageBoxResult.Yes) return;
                try { var task = ReferenceTaskLoader.Load(new BackendPathResolver().Resolve().PackageRoot); task.Settings.SimilarityIndex = editor.Task.Settings.SimilarityIndex; editor.LoadTask(task); Status = "已载入后端参考任务。"; }
                catch (Exception ex) { Status = ex.Message; }
            }, () => !IsBusy);
            foreach (var module in new ModuleExecutionService[] { prediction, similarity, scene })
            {
                module.Log += item => { pending.Enqueue(item); while (pending.Count > 2000) { ProcessLogEvent ignored; pending.TryDequeue(out ignored); } };
                module.Progress += item => OnUi(() => { if (IsBusy && !stopping) Status = ModuleTitle + "：" + item.State; });
                module.StateChanged += state => OnUi(() => { if (IsBusy && !stopping && (state == ProcessRunState.Preparing || state == ProcessRunState.Running)) Status = ModuleTitle + "：" + state; });
            }
            service.Log += item => { pending.Enqueue(item); while (pending.Count > 2000) { ProcessLogEvent ignored; pending.TryDequeue(out ignored); } };
            service.Progress += item => OnUi(() =>
            {
                if (stopping) return;
                switch (item.State)
                {
                    case "input_ready": Status = "输入已准备"; break;
                    case "running_forward": Status = "正向计算运行中"; break;
                    case "prepared": Status = "输入准备完成（未求解）"; break;
                    case "success": Status = "正在确认完成状态"; break;
                    case "forward_failed": Status = "后端计算失败"; break;
                    default: pending.Enqueue(new ProcessLogEvent { Text = "未知进度状态：" + item.State }); break;
                }
                if (!String.IsNullOrEmpty(item.RunDirectory)) ResultLocation = item.RunDirectory;
            });
            service.StateChanged += state => OnUi(() =>
            {
                if (state == ProcessRunState.Preparing) Status = "准备" + ModuleTitle;
                else if (state == ProcessRunState.Running && !stopping) Status = "正向计算运行中";
                else if (state == ProcessRunState.Stopping) Status = "正在停止进程树";
            });
            timer = new DispatcherTimer(TimeSpan.FromMilliseconds(150), DispatcherPriority.Background, (s, e) => Flush(), dispatcher);
        }
        private async Task ImportResultAsync()
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择 run_xxxxxx 目录或 output 目录",
                ShowNewFolderButton = false
            })
            {
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                IsBusy = true;
                Status = "正在导入已有结果…";
                try
                {
                    RunResult result = await Task.Run(() => new ResultDirectoryLoader().Load(dialog.SelectedPath));
                    string key = result.ModuleCode;
                    string title = result.ModuleType == ResultModuleType.Prediction ? "智能预测" : result.ModuleType == ResultModuleType.Similarity ? "相似度评估" : result.ModuleType == ResultModuleType.Scene ? "红外场景构建" : "正向计算";
                    ResultBrowserViewModel browser = ResultBrowserViewModel.FromResult(result, title, "已有计算结果已导入。");
                    resultBrowsers.Remember(key, browser);
                    selectedModule = key;
                    Notify(nameof(SelectedModule)); Notify(nameof(IsForward)); Notify(nameof(IsPrediction)); Notify(nameof(IsSimilarity)); Notify(nameof(IsScene)); Notify(nameof(ModuleTitle));
                    ResultBrowser = browser;
                    ResultLocation = result.OutputDirectory;
                    Status = title + "结果导入完成。";
                    LastRun = new RunRecord
                    {
                        Module = key,
                        RunId = result.RunId,
                        RunDirectory = result.RunDirectory,
                        ResultDirectory = result.OutputDirectory,
                        StartedAt = result.StartedAt ?? DateTimeOffset.Now,
                        EndedAt = result.EndedAt ?? DateTimeOffset.Now,
                        State = ProcessRunState.Completed,
                        Message = Status
                    };
                    RememberRun(LastRun);
                }
                catch (Exception ex)
                {
                    Status = "结果导入失败：" + ex.Message;
                    System.Windows.MessageBox.Show(Status, "导入结果", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
                finally { IsBusy = false; }
            }
        }
        public async Task StartAsync(BackendPaths paths = null)
        {
            if (IsBusy) return;
            if (HasInputErrors?.Invoke() == true) { Status = "请先修正标红的输入。"; return; }
            IsBusy = true; stopping = false; Status = "准备" + ModuleTitle; ResultLocation = "正在准备本次运行";
            ResultBrowser = ResultBrowserViewModel.Loading(ModuleTitle);
            LastRun = null; LogText = ""; WarningText = "";
            try
            {
                current = RunSelectedAsync(paths);
                LastRun = await current;
                Status = LastRun.Message;
                ResultLocation = LastRun.ResultDirectory ?? LastRun.RunDirectory ?? "本次未产生结果目录";
                if (LastRun.State == ProcessRunState.Failed) pending.Enqueue(new ProcessLogEvent { IsError = true, Text = LastRun.Message });
                await LoadResultAsync(LastRun, SelectedModule, ModuleTitle);
                RememberRun(LastRun);
            }
            catch (Exception ex)
            {
                Status = ModuleTitle + "无法启动，请查看警告。";
                ResultBrowser = ResultBrowserViewModel.ReadFailed(LastRun ?? new RunRecord { Module = SelectedModule, State = ProcessRunState.Failed, Message = Status }, ModuleTitle, ex);
                resultBrowsers.Remember(SelectedModule, ResultBrowser);
                pending.Enqueue(new ProcessLogEvent { IsError = true, Text = ex.ToString() });
            }
            finally { current = null; IsBusy = false; stopping = false; Flush(); CommandManager.InvalidateRequerySuggested(); }
        }
        private async Task LoadResultAsync(RunRecord record, string moduleKey, string moduleTitle)
        {
            ResultBrowserViewModel browser;
            if (record.State != ProcessRunState.Completed || String.IsNullOrWhiteSpace(record.ResultDirectory))
            {
                browser = ResultBrowserViewModel.FromRecord(record, moduleTitle);
            }
            else
            {
                Status = record.Message + " 正在读取结果…";
                try
                {
                    browser = await Task.Run(() => ResultBrowserViewModel.FromResult(
                        new ResultReader().Read(record, moduleKey == "轨迹" ? ResultModuleType.Trajectory : (ResultModuleType?)null),
                        moduleTitle, record.Message));
                    Status = record.Message;
                }
                catch (Exception ex)
                {
                    browser = ResultBrowserViewModel.ReadFailed(record, moduleTitle, ex);
                    pending.Enqueue(new ProcessLogEvent { IsError = true, Text = "结果读取失败：" + ex });
                    Status = record.Message + " 结果读取失败，请查看警告。";
                }
            }
            resultBrowsers.Remember(moduleKey, browser);
            ResultBrowser = browser;
        }
        private void ShowSelectedModuleResult()
        {
            ResultBrowser = resultBrowsers.Select(SelectedModule, ModuleTitle);
        }
        private void RememberRun(RunRecord record)
        {
            if (record == null || RunHistory.Contains(record)) return;
            RunHistory.Insert(0, record);
            while (RunHistory.Count > 100) RunHistory.RemoveAt(RunHistory.Count - 1);
        }
        private async Task<RunRecord> RunSelectedAsync(BackendPaths paths)
        {
            if (IsPrediction) return await prediction.RunAsync(editor.Task, PredictionMode, paths);
            if (IsSimilarity) return await similarity.RunAsync(ReferenceDirectory, CandidateDirectory, EvaluationConfig, paths);
            if (IsScene) return await scene.RunAsync(editor.Task, RequiredCandidates, paths);
            return await service.RunAsync(editor.Task, PrepareOnly, paths);
        }
        public void Stop()
        {
            if (!IsBusy) return;
            stopping = true; Status = "正在停止进程树"; CommandManager.InvalidateRequerySuggested(); service.Stop(); prediction.Stop(); similarity.Stop(); scene.Stop();
        }
        public async Task StopAndWaitAsync()
        {
            Stop(); var run = current;
            if (run != null) try { await run; } catch (Exception ex) { System.Diagnostics.Trace.WriteLine(ex); }
        }
        private void OnUi(Action action)
        { if (!dispatcher.HasShutdownStarted) dispatcher.BeginInvoke(action); }
        private void Flush()
        {
            var output = new System.Text.StringBuilder(); var errors = new System.Text.StringBuilder(); ProcessLogEvent item;
            for (int i = 0; i < 500 && pending.TryDequeue(out item); i++)
            {
                string text = item.Timestamp.ToString("HH:mm:ss") + " " + item.Text + Environment.NewLine;
                output.Append(text); if (item.IsError) errors.Append(text);
            }
            if (output.Length > 0) LogText = Tail(LogText + output);
            if (errors.Length > 0) WarningText = Tail(WarningText + errors);
        }
        private static string Tail(string text) { return text.Length <= 64000 ? text : "（界面仅保留最近日志，完整记录见运行目录 requests 下日志）\n" + text.Substring(text.Length - 63000); }
        public void Dispose() { timer.Stop(); service.Stop(); prediction.Stop(); similarity.Stop(); scene.Stop(); }

        private sealed class RunCommandImpl : ICommand
        {
            private readonly Action action; private readonly Func<bool> enabled;
            public RunCommandImpl(Action action, Func<bool> enabled) { this.action = action; this.enabled = enabled; }
            public bool CanExecute(object parameter) => enabled();
            public void Execute(object parameter) { if (enabled()) action(); }
            public event EventHandler CanExecuteChanged { add { CommandManager.RequerySuggested += value; } remove { CommandManager.RequerySuggested -= value; } }
        }
    }
}

