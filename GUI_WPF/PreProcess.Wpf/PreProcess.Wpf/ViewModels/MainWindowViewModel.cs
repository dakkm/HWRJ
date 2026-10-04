using System.Collections.ObjectModel;
using System.Windows.Input;

namespace PreProcess.Wpf.ViewModels
{
    // 定义 MainWindowViewModel 类型，集中封装与该领域对象相关的状态和行为。
    public sealed partial class MainWindowViewModel : ObservableObject
    {
        public ObservableCollection<NavigationItemViewModel> Navigation { get; }
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            = new ObservableCollection<NavigationItemViewModel>();
        public TaskEditorViewModel Editor { get; } = new TaskEditorViewModel();
        // 保存该组件运行所需的配置或中间状态。
        private readonly System.Collections.Generic.Dictionary<NavigationItemViewModel, object> pages
            = new System.Collections.Generic.Dictionary<NavigationItemViewModel, object>();
        public ForwardRunViewModel Execution { get; }
        public bool IsConfigurationPage => CurrentContent is CapabilityPageViewModel;
        public CapabilityPageViewModel ConfigurationPage => CurrentContent as CapabilityPageViewModel;
        public string SidePanelTitle => IsConfigurationPage ? "配置摘要" : "结果浏览器";
        public bool CanRunCurrentPage => ReferenceEquals(CurrentContent, Execution) && !Execution.IsBusy;
        public string PageNotice => CurrentContent is CapabilityPageViewModel
            ? ((CapabilityPageViewModel)CurrentContent).Notice
            : CurrentContent is ModelBoundaryConfigViewModel ? "模型与边界：已有物性参与任务；网格与扩展边界仅用于前端配置。"
            : CurrentContent is SolverControlViewModel ? "求解控制：任务时长参与计算；独立控制配置仅作前端展示。"
            : CurrentContent is ForwardStageViewModel ? "本页显示正向内部阶段；实际运行请进入 06 → 正向计算。"
            : ReferenceEquals(CurrentContent, Execution) ? "实际计算入口：" + Execution.ModuleTitle + "。运行结果以本次执行记录为准。"
            : "任务参数与实际结果工作区。展示草稿需在对应页面独立保存。";
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public object CurrentContent => selectedPage != null && pages.ContainsKey(selectedPage)
            ? (object)pages[selectedPage] : selectedPage;
        public ResultBrowserViewModel ActiveResultBrowser
        {
            get
            {
                if (CurrentContent is CapabilityPageViewModel) return displayResultBrowser;
                var post = CurrentContent as PostProcessingTaskViewModel;
                if (post != null) return post.ResultBrowser;
                if (CurrentContent is ForwardStageViewModel || CurrentContent is SceneSimulationSummaryViewModel) return Execution.ForwardResultBrowser;
                return Execution.ResultBrowser;
            }
        }
        private readonly ResultBrowserViewModel displayResultBrowser = ResultBrowserViewModel.Empty("功能说明 / 展示草稿（无计算结果）");
        // 保存该组件运行所需的配置或中间状态。
        private NavigationItemViewModel selectedPage;
        public NavigationItemViewModel SelectedPage
        {
            // 通过属性封装状态访问，并在变更时执行必要的同步操作。
            get => selectedPage;
            set
            {
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                NavigationItemViewModel target = ResolveNavigationTarget(value);
                if (selectedPage == target || target == null) return;
                if (Execution.IsBusy && pages.ContainsKey(target) && ReferenceEquals(pages[target], Execution))
                {
                    Status = "任务正在运行，请完成或停止后切换计算入口。";
                    return;
                }
                if (selectedPage != null) selectedPage.IsSelected = false;
                selectedPage = target;
                foreach (var group in Navigation)
                    if (group.Children.Contains(target)) group.IsExpanded = true;
                target.IsSelected = true;
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (pages.ContainsKey(target) && ReferenceEquals(pages[target], Execution))
                    Execution.SelectedModule = target.Title == "智能预测" ? "02"
                        // 更新当前流程使用的数据，为下一处理步骤做好准备。
                        : target.Title == "相似度评估" ? "03"
                        : target.Title == "红外场景构建" ? "04"
                        // 更新当前流程使用的数据，为下一处理步骤做好准备。
                        : target.Title == "轨迹生成" ? "轨迹" : "01";
                Notify();
                Notify(nameof(CurrentContent));
                Notify(nameof(ActiveResultBrowser));
                Notify(nameof(CanRunCurrentPage));
                Notify(nameof(PageNotice));
                Notify(nameof(IsConfigurationPage));
                Notify(nameof(ConfigurationPage));
                Notify(nameof(SidePanelTitle));
            }
        }
        // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
        private string status = "就绪";
        public string Status { get => status; private set { status = value; Notify(); } }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public ICommand NavigateCommand { get; }
        public ICommand PlaceholderCommand { get; }
        public ICommand RunCurrentPageCommand { get; }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public MainWindowViewModel()
        {
            Execution = new ForwardRunViewModel(Editor); Editor.Execution = Execution;
            Execution.PropertyChanged += (sender, args) => { if (args.PropertyName == nameof(ForwardRunViewModel.Status)) Status = Execution.Status; };
            Execution.PropertyChanged += (sender, args) => { if (args.PropertyName == nameof(ForwardRunViewModel.ForwardResultBrowser) || args.PropertyName == nameof(ForwardRunViewModel.ResultBrowser)) Notify(nameof(ActiveResultBrowser)); };
            // 通知界面绑定层刷新相关状态，确保显示内容与模型一致。
            Editor.PropertyChanged += (sender, args) => { if (args.PropertyName == nameof(TaskEditorViewModel.Message)) Status = Editor.Message; };
            var task = AddGroup("01 红外目标规划设计", "任务配置、目标参数、场景运动和环境观测的规划入口。");
            Add(task, "任务设置", "任务基本信息的编辑区域。");
            Add(task, "目标参数", "目标自身参数的编辑区域。");
            Add(task, "场景与运动", "目标群及单目标空间状态的编辑区域。");
            Add(task, "环境与观测", "环境条件与观测设置的编辑区域。");

            var model = AddGroup("02 模型导入与光热物性、边界信息配置", "已有任务物性与独立网格、边界展示配置。");
            Add(model, "模型与边界配置", "独立配置页；网格与扩展边界不参与后端求解。");
            var solver = AddGroup("03 求解定义与控制", "任务时长与独立求解控制展示配置。");
            Add(solver, "求解控制配置", "独立控制配置页；扩展控制参数不提交后端。");
            var heatFlux = AddGroup("04 空间外热流求解", "正向计算内部阶段，只读显示职责，不单独启动求解。");
            Add(heatFlux, "空间外热流概览", "正向计算内部阶段。");
            var temperature = AddGroup("05 温度场求解", "正向计算内部阶段，只读显示职责，不单独启动求解。");
            Add(temperature, "温度场概览", "正向计算内部阶段。");
            var scene = AddGroup("06 红外场景仿真", "正向计算结果形成的红外场景仿真职责。");
            Add(scene, "正向计算", "正向计算工作区域，作为 06 的现有复用入口。");
            Add(scene, "场景仿真摘要", "温度场、红外响应和轨迹结果摘要。");
            var ai = AddGroup("07 基于人工智能的红外目标仿真计算", "AI 红外目标仿真计算入口。");
            Add(ai, "智能预测", "智能预测工作区域。");
            var similarity = AddGroup("08 相似度评估", "相似度评估工作区域。");
            Add(similarity, "相似度评估", "相似度评估工作区域。");
            var sceneBuild = AddGroup("09 红外场景构建", "红外场景构建工作区域。");
            Add(sceneBuild, "红外场景构建", "红外场景构建工作区域。");
            var post = AddGroup("后处理任务", "统一查看已完成模块产生的结果。", "暂无结果");
            Add(post, "结果浏览", "查看正向、AI、轨迹及其他已完成结果。");
            Add(post, "轨迹生成", "读取当前任务的正向计算轨迹并执行后处理。");
            var runs = AddGroup("运行管理", "当前任务、历史任务和运行日志管理。");
            Add(runs, "当前任务", "当前任务概览区域。");
            // 将当前结果加入集合，供后续汇总或界面展示。
            Add(runs, "历史任务", "历史任务列表区域。");
            Add(runs, "输出结果", "输出结果浏览区域。");
            // 将当前结果加入集合，供后续汇总或界面展示。
            pages.Add(task.Children[0], new TaskSettingsViewModel(Editor));
            pages.Add(task.Children[1], new TargetSettingsViewModel(Editor));
            pages.Add(task.Children[2], new SceneMotionViewModel(Editor));
            pages.Add(task.Children[3], new EnvironmentObservationViewModel(Editor));
            var modelBoundary = new ModelBoundaryConfigViewModel(Editor);
            pages.Add(model.Children[0], modelBoundary);
            modelBoundary.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(ModelBoundaryConfigViewModel.PreviewMesh))
                    Execution.ResultBrowser.SetPreviewMesh(modelBoundary.PreviewMesh);
            };
            pages.Add(solver.Children[0], new SolverControlViewModel(Editor));
            pages.Add(heatFlux.Children[0], new ForwardStageViewModel("空间外热流求解", "正向计算内部阶段：外热流输入与空间分布计算。", Execution));
            pages.Add(temperature.Children[0], new ForwardStageViewModel("温度场求解", "正向计算内部阶段：温度场时间推进与结果输出。", Execution));
            pages.Add(scene.Children[0], Execution);
            pages.Add(scene.Children[1], new SceneSimulationSummaryViewModel(Execution));
            pages.Add(ai.Children[0], Execution);
            pages.Add(similarity.Children[0], Execution);
            pages.Add(sceneBuild.Children[0], Execution);
            pages.Add(post.Children[0], new PostProcessingTaskViewModel());
            pages.Add(post.Children[1], Execution);
            Execution.RunHistory.CollectionChanged += (sender, args) =>
            {
                var postView = (PostProcessingTaskViewModel)pages[post.Children[0]];
                postView.RefreshFromTaskDirectory(Editor.ResultTaskDirectory, Execution.RunHistory);
                post.UpdateStatus(postView.HasAvailableResults ? "结果可用" : "暂无结果");
                if (args.NewItems != null && args.NewItems.Count > 0 && postView.HasAvailableResults)
                    SelectedPage = post.Children[0];
            };
            Editor.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(TaskEditorViewModel.ResultTaskDirectory))
                {
                    var postView = (PostProcessingTaskViewModel)pages[post.Children[0]];
                    postView.RefreshFromTaskDirectory(Editor.ResultTaskDirectory, Execution.RunHistory);
                    post.UpdateStatus(postView.HasAvailableResults ? "结果可用" : "暂无结果");
                }
            };
            var initialPostView = (PostProcessingTaskViewModel)pages[post.Children[0]];
            initialPostView.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(PostProcessingTaskViewModel.ResultBrowser)) Notify(nameof(ActiveResultBrowser));
            };
            initialPostView.RefreshFromTaskDirectory(Editor.ResultTaskDirectory, Execution.RunHistory);
            post.UpdateStatus(initialPostView.HasAvailableResults ? "结果可用" : "暂无结果");
            System.Action refreshReadiness = () =>
            {
                bool saved = !System.String.IsNullOrWhiteSpace(Editor.CurrentTaskPath);
                bool targetReady = Editor.Task != null && Editor.Task.Targets.Uniform.Density > 0 && Editor.Task.Targets.Uniform.HeatCapacity > 0;
                bool environmentReady = Editor.Task != null && Editor.Task.Environment.SolarFlux > 0 && Editor.Task.Environment.ApertureSize > 0;
                bool similarityReady = !System.String.IsNullOrWhiteSpace(Execution.ReferenceDirectory) && !System.String.IsNullOrWhiteSpace(Execution.CandidateDirectory);
                task.UpdateStatus(saved ? "已保存" : "待保存");
                task.Children[0].UpdateStatus(saved ? "已保存" : "待保存");
                task.Children[1].UpdateStatus(targetReady ? "已有参数" : "待完善");
                task.Children[3].UpdateStatus(environmentReady ? "已有参数" : "待完善");
                ai.UpdateStatus(initialPostView.PredictionCount > 0 ? "结果可用" : "已有调用入口");
                similarity.UpdateStatus(similarityReady ? "已选择输入" : "待选择结果");
                sceneBuild.UpdateStatus(initialPostView.SceneCount > 0 ? "结果可用" : "已有调用入口");
            };
            refreshReadiness();
            initialPostView.PropertyChanged += (sender, args) => refreshReadiness();
            Editor.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(TaskEditorViewModel.CurrentTaskPath) ||
                    args.PropertyName == nameof(TaskEditorViewModel.Task) ||
                    args.PropertyName == nameof(TaskEditorViewModel.ResultTaskDirectory)) refreshReadiness();
            };
            Execution.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(ForwardRunViewModel.IsBusy)) Notify(nameof(CanRunCurrentPage));
                if (args.PropertyName == nameof(ForwardRunViewModel.SelectedModule)) Notify(nameof(PageNotice));
                if (args.PropertyName == nameof(ForwardRunViewModel.ReferenceDirectory) ||
                    args.PropertyName == nameof(ForwardRunViewModel.CandidateDirectory)) refreshReadiness();
            };
            pages.Add(runs.Children[0], new CurrentTaskViewModel(Editor));
            pages.Add(runs.Children[1], new RunHistoryViewModel(Editor));
            // 将当前结果加入集合，供后续汇总或界面展示。
            pages.Add(runs.Children[2], new OutputResultsViewModel(Editor));
            AddCapabilityPages();
            SelectedPage = task.Children[0];
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            NavigateCommand = new RelayCommand(item => SelectedPage = item as NavigationItemViewModel);
            RunCurrentPageCommand = new RelayCommand(_ =>
            {
                if (CanRunCurrentPage && Execution.RunCommand.CanExecute(null)) Execution.RunCommand.Execute(null);
                else Status = "请从实际计算入口运行；展示与配置页面不会启动后端。";
            });
            PlaceholderCommand = new RelayCommand(action => Status = "红外场景工作台：场景任务配置、模块计算与结果分析。");
        }
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private NavigationItemViewModel AddGroup(string title, string description, string status = null)
        {
            var group = new NavigationItemViewModel(title, description, status);
            Navigation.Add(group);
            // 返回当前步骤生成的结果，并结束本次调用。
            return group;
        }
        private static void Add(NavigationItemViewModel group, string title, string description)
        {
            // 将当前结果加入集合，供后续汇总或界面展示。
            group.Children.Add(new NavigationItemViewModel(title, description));
        }
        private NavigationItemViewModel ResolveNavigationTarget(NavigationItemViewModel item)
        {
            if (item != null && capabilityTargets.ContainsKey(item)) return capabilityTargets[item];
            // Group headings organize the tree; they are not standalone pages.
            // 返回当前步骤生成的结果，并结束本次调用。
            return item != null && item.Children.Count > 0 ? item.Children[0] : item;
        }
    }
}

