using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
        // 保存该组件运行所需的配置或中间状态。
        private readonly TaskEditorViewModel editor;
        private readonly ForwardSimulationService service = new ForwardSimulationService();
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private readonly TrajectoryPostprocessService trajectory = new TrajectoryPostprocessService();
        private readonly PredictionService prediction = new PredictionService();
        private readonly SimilarityEvaluationService similarity = new SimilarityEvaluationService();
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private readonly SceneBuildService scene = new SceneBuildService();
        private readonly Dispatcher dispatcher;
        // 将界面状态变更调度到用户界面线程，避免跨线程访问。
        private readonly DispatcherTimer timer;
        private readonly ConcurrentQueue<ProcessLogEvent> pending = new ConcurrentQueue<ProcessLogEvent>();
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private readonly ResultBrowserStore resultBrowsers = new ResultBrowserStore();
        private Task<RunRecord> current;
        private string selectedModule = "01";
        // 保存该组件运行所需的配置或中间状态。
        public string[] Modules => new[] { "01", "02", "轨迹", "03", "04" };
        public string SelectedModule { get => selectedModule; set { if (IsBusy || Array.IndexOf(Modules, value) < 0) return; selectedModule = value; Notify(); Notify(nameof(IsForward)); Notify(nameof(IsTrajectory)); Notify(nameof(IsPrediction)); Notify(nameof(IsSimilarity)); Notify(nameof(IsScene)); Notify(nameof(ModuleTitle)); ShowSelectedModuleResult(); Notify(nameof(ForwardResultBrowser)); } }
        // 保存该组件运行所需的配置或中间状态。
        public bool IsForward => SelectedModule == "01";
        public bool IsTrajectory => SelectedModule == "轨迹";
        // 保存该组件运行所需的配置或中间状态。
        public bool IsPrediction => SelectedModule == "02";
        public bool IsSimilarity => SelectedModule == "03";
        public bool IsScene => SelectedModule == "04";
        // 保存该组件运行所需的配置或中间状态。
        public string ModuleTitle => IsPrediction ? "智能预测" : IsSimilarity ? "相似度评估" : IsScene ? "红外场景构建" : IsTrajectory ? "轨迹生成" : "正向计算";
        private string referenceDirectory, candidateDirectory;
        // 通知界面绑定层刷新相关状态，确保显示内容与模型一致。
        public string ReferenceDirectory { get => referenceDirectory; private set { referenceDirectory = value; Notify(); } }
        public string CandidateDirectory { get => candidateDirectory; private set { candidateDirectory = value; Notify(); } }
        // 保存该组件运行所需的配置或中间状态。
        private int requiredCandidates = 10;
        public int RequiredCandidates { get => requiredCandidates; set { if (value <= 0) throw new ArgumentException("候选数量必须为正整数。"); requiredCandidates = value; Notify(); } }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public ICommand ImportResultCommand { get; }
        public ICommand SelectReferenceDirectoryCommand { get; }
        public ICommand SelectCandidateDirectoryCommand { get; }
        // 保存该组件运行所需的配置或中间状态。
        private bool busy, stopping, prepareOnly;
        private bool trajectoryIncludePrerelease;
        // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
        private string status = "就绪", log = "", warnings = "", location = "尚无运行目录";
        public bool IsBusy { get => busy; private set { busy = value; Notify(); Notify(nameof(CanEdit)); CommandManager.InvalidateRequerySuggested(); } }
        // 保存该组件运行所需的配置或中间状态。
        public bool CanEdit => !IsBusy;
        public bool PrepareOnly { get => prepareOnly; set { if (IsBusy) return; prepareOnly = value; Notify(); } }
        public bool TrajectoryIncludePrerelease { get => trajectoryIncludePrerelease; set { if (IsBusy) return; trajectoryIncludePrerelease = value; Notify(); } }
        // 通知界面绑定层刷新相关状态，确保显示内容与模型一致。
        public string Status { get => status; private set { status = value; Notify(); } }
        public string LogText { get => log; private set { log = value; Notify(); } }
        // 通知界面绑定层刷新相关状态，确保显示内容与模型一致。
        public string WarningText { get => warnings; private set { warnings = value; Notify(); } }
        public string ResultLocation { get => location; private set { location = value; Notify(); } }
        // 保存该组件运行所需的配置或中间状态。
        private ResultBrowserViewModel resultBrowser;
        public ResultBrowserViewModel ResultBrowser { get => resultBrowser; private set { resultBrowser = value; Notify(); if (IsForward) Notify(nameof(ForwardResultBrowser)); } }
        public ResultBrowserViewModel ForwardResultBrowser { get { return IsForward ? ResultBrowser : resultBrowsers.Select("01", "正向计算"); } }
        private RunRecord lastRun;
        // 通知界面绑定层刷新相关状态，确保显示内容与模型一致。
        public RunRecord LastRun { get => lastRun; private set { lastRun = value; Notify(); } }
        public ObservableCollection<RunRecord> RunHistory { get; } = new ObservableCollection<RunRecord>();
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public ICommand RunCommand { get; }
        public ICommand StopCommand { get; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public Func<bool> HasInputErrors { get; set; }

        public ForwardRunViewModel(TaskEditorViewModel editor)
        {
            this.editor = editor;
            // 将界面状态变更调度到用户界面线程，避免跨线程访问。
            dispatcher = Dispatcher.CurrentDispatcher;
            ResultBrowser = ResultBrowserViewModel.Empty(ModuleTitle);
            // 异步等待耗时任务完成，期间保持界面线程可响应。
            RunCommand = new RunCommandImpl(async () => await StartAsync(), () => !IsBusy);
            StopCommand = new RunCommandImpl(Stop, () => IsBusy && !stopping);
            // 异步等待耗时任务完成，期间保持界面线程可响应。
            ImportResultCommand = new RunCommandImpl(async () => await ImportResultAsync(), () => !IsBusy);
            SelectReferenceDirectoryCommand = new RunCommandImpl(() => SelectSimilarityDirectory(true), () => !IsBusy);
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            SelectCandidateDirectoryCommand = new RunCommandImpl(() => SelectSimilarityDirectory(false), () => !IsBusy);
            foreach (var module in new ModuleExecutionService[] { prediction, similarity, scene })
            {
                module.Log += item => { pending.Enqueue(item); while (pending.Count > 2000) { ProcessLogEvent ignored; pending.TryDequeue(out ignored); } };
                // 将界面状态变更调度到用户界面线程，避免跨线程访问。
                module.Progress += item => OnUi(() => { if (IsBusy && !stopping) Status = ModuleTitle + "：" + item.State; });
                module.StateChanged += state => OnUi(() => { if (IsBusy && !stopping && (state == ProcessRunState.Preparing || state == ProcessRunState.Running)) Status = ModuleTitle + "：" + state; });
            }
            // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
            service.Log += item => { pending.Enqueue(item); while (pending.Count > 2000) { ProcessLogEvent ignored; pending.TryDequeue(out ignored); } };
            service.Progress += item => OnUi(() =>
            {
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (stopping) return;
                switch (item.State)
                {
                    case "input_ready": Status = IsTrajectory ? "轨迹计算输入已准备" : "输入已准备"; break;
                    // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                    case "running_forward": Status = IsTrajectory ? "正在生成轨迹历史" : "正向计算运行中"; break;
                    case "prepared": Status = "输入准备完成（未求解）"; break;
                    // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                    case "success": Status = IsTrajectory ? "轨迹数据已生成，正在准备后处理" : "正在确认完成状态"; break;
                    case "forward_failed": Status = IsTrajectory ? "轨迹基础求解失败" : "后端计算失败"; break;
                    // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                    default: pending.Enqueue(new ProcessLogEvent { Text = "未知进度状态：" + item.State }); break;
                }
                if (!String.IsNullOrEmpty(item.RunDirectory)) ResultLocation = item.RunDirectory;
            });
            // 将界面状态变更调度到用户界面线程，避免跨线程访问。
            service.StateChanged += state => OnUi(() =>
            {
                if (state == ProcessRunState.Preparing) Status = "准备" + ModuleTitle;
                // 当前置条件不成立时执行备用路径，保持处理结果完整。
                else if (state == ProcessRunState.Running && !stopping) Status = IsTrajectory ? "轨迹生成或后处理运行中" : "正向计算运行中";
                else if (state == ProcessRunState.Stopping) Status = "正在停止进程树";
            // 继续处理当前业务步骤，保持上下文状态一致。
            });
            trajectory.Log += item => { pending.Enqueue(item); while (pending.Count > 2000) { ProcessLogEvent ignored; pending.TryDequeue(out ignored); } };
            trajectory.StateChanged += state => OnUi(() =>
            {
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (state == ProcessRunState.Preparing) Status = "正在查找当前任务的正向计算结果";
                else if (state == ProcessRunState.Running && !stopping) Status = "正在执行轨迹后处理";
                // 当前置条件不成立时执行备用路径，保持处理结果完整。
                else if (state == ProcessRunState.Stopping) Status = "正在停止轨迹后处理";
            });
            // 将界面状态变更调度到用户界面线程，避免跨线程访问。
            timer = new DispatcherTimer(TimeSpan.FromMilliseconds(150), DispatcherPriority.Background, (s, e) => Flush(), dispatcher);
        }
        private async Task ImportResultAsync()
        {
            // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择结果索引或任一结果文件",
                Filter = "结果索引 (result-index.json;run-location.json)|result-index.json;run-location.json|结果标志文件 (*.json;*.csv;*.gz)|*.json;*.csv;*.gz|所有文件 (*.*)|*.*",
                // 检查输入及依赖是否满足要求，提前阻止无效操作。
                CheckFileExists = true,
                Multiselect = false,
                // 处理文件系统路径及数据，并在使用前确认目标有效。
                InitialDirectory = Directory.Exists(editor.ResultTaskDirectory) ? editor.ResultTaskDirectory : null
            };
            if (dialog.ShowDialog() == true)
            {
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                IsBusy = true;
                Status = "正在导入已有结果…";
                try
                {
                    // 异步等待耗时任务完成，期间保持界面线程可响应。
                    RunResult result = await Task.Run(() => new ResultDirectoryLoader().Load(dialog.FileName));
                    string key = result.ModuleCode;
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    string title = result.ModuleType == ResultModuleType.Prediction ? "智能预测" : result.ModuleType == ResultModuleType.Similarity ? "相似度评估" : result.ModuleType == ResultModuleType.Scene ? "红外场景构建" : "正向计算";
                    ResultBrowserViewModel browser = ResultBrowserViewModel.FromResult(result, title, "已有计算结果已导入。");
                    // 将当前结果加入集合，供后续汇总或界面展示。
                    resultBrowsers.Remember(key, browser);
                    selectedModule = key;
                    Notify(nameof(SelectedModule)); Notify(nameof(IsForward)); Notify(nameof(IsTrajectory)); Notify(nameof(IsPrediction)); Notify(nameof(IsSimilarity)); Notify(nameof(IsScene)); Notify(nameof(ModuleTitle));
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    ResultBrowser = browser;
                    ResultLocation = result.OutputDirectory;
                    // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
                    Status = title + "结果导入完成。";
                    LastRun = new RunRecord
                    {
                        // 更新当前流程使用的数据，为下一处理步骤做好准备。
                        Module = key,
                        RunId = result.RunId,
                        RunDirectory = result.RunDirectory,
                        // 更新当前流程使用的数据，为下一处理步骤做好准备。
                        ResultDirectory = result.OutputDirectory,
                        StartedAt = result.StartedAt ?? DateTimeOffset.Now,
                        // 更新当前流程使用的数据，为下一处理步骤做好准备。
                        EndedAt = result.EndedAt ?? DateTimeOffset.Now,
                        State = ProcessRunState.Completed,
                        // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
                        Message = Status
                    };
                    RememberRun(LastRun);
                }
                // 捕获本步骤产生的异常，将故障转换为可追踪的运行状态。
                catch (Exception ex)
                {
                    Status = "结果导入失败：" + ex.Message;
                    System.Windows.MessageBox.Show(Status, "导入结果", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
                // 无论执行成功与否都释放资源并恢复组件的可用状态。
                finally { IsBusy = false; }
            }
        }
        private void SelectSimilarityDirectory(bool reference)
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            string current = reference ? ReferenceDirectory : CandidateDirectory;
            if (String.IsNullOrWhiteSpace(current) || !Directory.Exists(current))
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                current = FindLatestForwardOutput(editor.ResultTaskDirectory);
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = reference ? "选择参考正向计算或智能预测结果目录" : "选择候选正向计算或智能预测结果目录",
                ShowNewFolderButton = false,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                SelectedPath = current ?? String.Empty
            })
            {
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                try
                {
                    // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
                    SimilarityEvaluationService.CheckDirectory(dialog.SelectedPath);
                    if (reference) ReferenceDirectory = System.IO.Path.GetFullPath(dialog.SelectedPath);
                    else CandidateDirectory = System.IO.Path.GetFullPath(dialog.SelectedPath);
                    // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
                    Status = reference ? "已选择参考结果目录。" : "已选择候选结果目录。";
                }
                catch (Exception ex)
                {
                    // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
                    Status = "结果目录不可用：" + ex.Message;
                    System.Windows.MessageBox.Show(Status, "选择相似度评估结果", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
            }
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static string FindLatestForwardOutput(string taskDirectory)
        {
            if (String.IsNullOrWhiteSpace(taskDirectory) || !Directory.Exists(taskDirectory)) return String.Empty;
            try
            {
                // 处理文件系统路径及数据，并在使用前确认目标有效。
                string latest = Directory.GetDirectories(taskDirectory, "*", SearchOption.AllDirectories)
                    .Where(path => (File.Exists(Path.Combine(path, "temperature_history.csv"))
                        && File.Exists(Path.Combine(path, "infrared_response_history.csv")))
                        || (File.Exists(Path.Combine(path, "temperature_prediction.csv"))
                        && File.Exists(Path.Combine(path, "point_token_predictions.csv.gz"))))
                    .OrderByDescending(path => Directory.GetLastWriteTimeUtc(path))
                    // 调用对应组件完成当前步骤，并保留产生的处理结果。
                    .FirstOrDefault();
                return latest ?? taskDirectory;
            }
            catch (IOException) { return taskDirectory; }
            // 捕获本步骤产生的异常，将故障转换为可追踪的运行状态。
            catch (UnauthorizedAccessException) { return taskDirectory; }
        }
        public async Task StartAsync(BackendPaths paths = null)
        {
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (IsBusy) return;
            if (IsPrediction || IsScene)
            {
                // 进入可能失败的操作区间，并由后续异常分支统一处理故障。
                try
                {
                    paths = paths ?? new BackendPathResolver().Resolve();
                    // 调用对应组件完成当前步骤，并保留产生的处理结果。
                    if (IsPrediction)
                    {
                        // Keep the user's task intact. The backend performs an
                        // applicability check and reports a warning when the
                        // frozen model contract is not satisfied.
                        pending.Enqueue(new ProcessLogEvent { Text = "智能预测保留当前任务输入；若超出模型适用域，将提示但不自动替换场景。" });
                    }
                    else editor.LoadTask(ReferenceTaskLoader.AdjustForScene(editor.Task, paths.PackageRoot));
                    // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
                    if (IsScene) pending.Enqueue(new ProcessLogEvent { Text = "已使用软件默认场景作为场景构建参考。" });
                }
                catch (Exception ex) { Status = "模型输入准备失败：" + ex.Message; return; }
            }
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (HasInputErrors?.Invoke() == true) { Status = "请先修正标红的输入。"; return; }
            IsBusy = true; stopping = false; Status = "准备" + ModuleTitle; ResultLocation = "正在准备本次运行";
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            ResultBrowser = ResultBrowserViewModel.Loading(ModuleTitle);
            LastRun = null; LogText = ""; WarningText = "";
            try
            {
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                current = RunSelectedAsync(paths);
                LastRun = await current;
                // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
                Status = LastRun.Message;
                ResultLocation = LastRun.TaskDirectory ?? LastRun.ResultDirectory ?? LastRun.RunDirectory ?? "本次未产生结果目录";
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (LastRun.State == ProcessRunState.Failed) pending.Enqueue(new ProcessLogEvent { IsError = true, Text = LastRun.Message });
                await LoadResultAsync(LastRun, SelectedModule, ModuleTitle);
                RememberRun(LastRun);
            }
            // 捕获本步骤产生的异常，将故障转换为可追踪的运行状态。
            catch (Exception ex)
            {
                Status = ModuleTitle + "无法启动，请查看警告。";
                // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
                ResultBrowser = ResultBrowserViewModel.ReadFailed(LastRun ?? new RunRecord { Module = SelectedModule, State = ProcessRunState.Failed, Message = Status }, ModuleTitle, ex);
                resultBrowsers.Remember(SelectedModule, ResultBrowser);
                // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
                pending.Enqueue(new ProcessLogEvent { IsError = true, Text = ex.ToString() });
            }
            finally { current = null; IsBusy = false; stopping = false; Flush(); CommandManager.InvalidateRequerySuggested(); }
        }
        private async Task LoadResultAsync(RunRecord record, string moduleKey, string moduleTitle)
        {
            // 继续处理当前业务步骤，保持上下文状态一致。
            ResultBrowserViewModel browser;
            if (record.State != ProcessRunState.Completed || String.IsNullOrWhiteSpace(record.ResultDirectory))
            {
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                browser = ResultBrowserViewModel.FromRecord(record, moduleTitle);
            }
            else
            {
                // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
                Status = record.Message + " 正在读取结果…";
                try
                {
                    // 异步等待耗时任务完成，期间保持界面线程可响应。
                    browser = await Task.Run(() => ResultBrowserViewModel.FromResult(
                        new ResultReader().Read(record, moduleKey == "轨迹" ? ResultModuleType.Trajectory : (ResultModuleType?)null),
                        moduleTitle, record.Message));
                    // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
                    Status = record.Message;
                }
                catch (Exception ex)
                {
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    browser = ResultBrowserViewModel.ReadFailed(record, moduleTitle, ex);
                    pending.Enqueue(new ProcessLogEvent { IsError = true, Text = "结果读取失败：" + ex });
                    // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
                    Status = record.Message + " 结果读取失败，请查看警告。";
                }
            }
            resultBrowsers.Remember(moduleKey, browser);
            ResultBrowser = browser;
            if (moduleKey == "01") Notify(nameof(ForwardResultBrowser));
        }
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private void ShowSelectedModuleResult()
        {
            ResultBrowser = resultBrowsers.Select(SelectedModule, ModuleTitle);
        }
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private void RememberRun(RunRecord record)
        {
            if (record == null || RunHistory.Contains(record)) return;
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            RunHistory.Insert(0, record);
            while (RunHistory.Count > 100) RunHistory.RemoveAt(RunHistory.Count - 1);
        }
        private async Task<RunRecord> RunSelectedAsync(BackendPaths paths)
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            string taskDirectory = editor.ResultTaskDirectory;
            if (IsTrajectory) return await trajectory.RunAsync(taskDirectory, TrajectoryIncludePrerelease, paths);
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (IsPrediction) return await prediction.RunAsync(editor.Task, "both", paths, default(System.Threading.CancellationToken), taskDirectory);
            if (IsSimilarity) return await similarity.RunAsync(ReferenceDirectory, CandidateDirectory, null, paths, default(System.Threading.CancellationToken), taskDirectory);
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (IsScene) return await scene.RunAsync(editor.Task, RequiredCandidates, paths, default(System.Threading.CancellationToken), taskDirectory);
            return await service.RunAsync(editor.Task, PrepareOnly, paths, 1800,
                default(System.Threading.CancellationToken), taskDirectory);
        }
        // 结束当前资源的使用，避免残留句柄或后台任务。
        public void Stop()
        {
            if (!IsBusy) return;
            // 通知界面绑定层刷新相关状态，确保显示内容与模型一致。
            stopping = true; Status = "正在停止进程树"; CommandManager.InvalidateRequerySuggested(); service.Stop(); trajectory.Stop(); prediction.Stop(); similarity.Stop(); scene.Stop();
        }
        public async Task StopAndWaitAsync()
        {
            // 结束当前资源的使用，避免残留句柄或后台任务。
            Stop(); var run = current;
            if (run != null) try { await run; } catch (Exception ex) { System.Diagnostics.Trace.WriteLine(ex); }
        }
        // 将界面状态变更调度到用户界面线程，避免跨线程访问。
        private void OnUi(Action action)
        { if (!dispatcher.HasShutdownStarted) dispatcher.BeginInvoke(action); }
        private void Flush()
        {
            // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
            var output = new System.Text.StringBuilder(); var errors = new System.Text.StringBuilder(); ProcessLogEvent item;
            for (int i = 0; i < 500 && pending.TryDequeue(out item); i++)
            {
                // 将外部数据转换为目标类型，并保持约定的表示格式。
                string text = item.Timestamp.ToString("HH:mm:ss") + " " + item.Text + Environment.NewLine;
                output.Append(text); if (item.IsError) errors.Append(text);
            }
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (output.Length > 0) LogText = Tail(LogText + output);
            if (errors.Length > 0) WarningText = Tail(WarningText + errors);
        }
        private static string Tail(string text) { return text.Length <= 64000 ? text : "（界面仅保留最近日志，完整记录见运行目录 requests 下日志）\n" + text.Substring(text.Length - 63000); }
        // 结束当前资源的使用，避免残留句柄或后台任务。
        public void Dispose() { timer.Stop(); service.Stop(); trajectory.Stop(); prediction.Stop(); similarity.Stop(); scene.Stop(); }

        private sealed class RunCommandImpl : ICommand
        {
            // 保存该组件运行所需的配置或中间状态。
            private readonly Action action; private readonly Func<bool> enabled;
            public RunCommandImpl(Action action, Func<bool> enabled) { this.action = action; this.enabled = enabled; }
            // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
            public bool CanExecute(object parameter) => enabled();
            public void Execute(object parameter) { if (enabled()) action(); }
            public event EventHandler CanExecuteChanged { add { CommandManager.RequerySuggested += value; } remove { CommandManager.RequerySuggested -= value; } }
        }
    }
}

