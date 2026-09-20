using System.Collections.ObjectModel;
using System.Windows.Input;

namespace PreProcess.Wpf.ViewModels
{
    // 定义 MainWindowViewModel 类型，集中封装与该领域对象相关的状态和行为。
    public sealed class MainWindowViewModel : ObservableObject
    {
        public ObservableCollection<NavigationItemViewModel> Navigation { get; }
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            = new ObservableCollection<NavigationItemViewModel>();
        public TaskEditorViewModel Editor { get; } = new TaskEditorViewModel();
        // 保存该组件运行所需的配置或中间状态。
        private readonly System.Collections.Generic.Dictionary<NavigationItemViewModel, object> pages
            = new System.Collections.Generic.Dictionary<NavigationItemViewModel, object>();
        public ForwardRunViewModel Execution { get; }
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public object CurrentContent => selectedPage != null && pages.ContainsKey(selectedPage)
            ? (object)pages[selectedPage] : selectedPage;
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
                selectedPage = target;
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
            }
        }
        // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
        private string status = "就绪";
        public string Status { get => status; private set { status = value; Notify(); } }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public ICommand NavigateCommand { get; }
        public ICommand PlaceholderCommand { get; }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public MainWindowViewModel()
        {
            Execution = new ForwardRunViewModel(Editor); Editor.Execution = Execution;
            Execution.PropertyChanged += (sender, args) => { if (args.PropertyName == nameof(ForwardRunViewModel.Status)) Status = Execution.Status; };
            // 通知界面绑定层刷新相关状态，确保显示内容与模型一致。
            Editor.PropertyChanged += (sender, args) => { if (args.PropertyName == nameof(TaskEditorViewModel.Message)) Status = Editor.Message; };
            var task = AddGroup("场景任务", "在这里组织场景任务的设置与数据。");
            // 将当前结果加入集合，供后续汇总或界面展示。
            Add(task, "任务设置", "任务基本信息的编辑区域。");
            Add(task, "目标参数", "目标自身参数的编辑区域。");
            // 将当前结果加入集合，供后续汇总或界面展示。
            Add(task, "场景与运动", "目标群及单目标空间状态的编辑区域。");
            Add(task, "环境与观测", "环境条件与观测设置的编辑区域。");
            var modules = AddGroup("功能模块", "选择要使用的功能模块。");
            // 将当前结果加入集合，供后续汇总或界面展示。
            Add(modules, "正向计算", "正向计算工作区域。");
            Add(modules, "智能预测", "智能预测工作区域。");
            // 将当前结果加入集合，供后续汇总或界面展示。
            Add(modules, "轨迹生成", "读取当前任务的正向计算轨迹并执行后处理。");
            Add(modules, "相似度评估", "相似度评估工作区域。");
            // 将当前结果加入集合，供后续汇总或界面展示。
            Add(modules, "红外场景构建", "红外场景构建工作区域。");
            var runs = AddGroup("运行管理", "任务运行及输出的管理区域。");
            Add(runs, "当前任务", "当前任务概览区域。");
            // 将当前结果加入集合，供后续汇总或界面展示。
            Add(runs, "历史任务", "历史任务列表区域。");
            Add(runs, "输出结果", "输出结果浏览区域。");
            // 将当前结果加入集合，供后续汇总或界面展示。
            pages.Add(task.Children[0], new TaskSettingsViewModel(Editor));
            pages.Add(task.Children[1], new TargetSettingsViewModel(Editor));
            // 将当前结果加入集合，供后续汇总或界面展示。
            pages.Add(task.Children[2], new SceneMotionViewModel(Editor));
            pages.Add(task.Children[3], new EnvironmentObservationViewModel(Editor));
            // 遍历当前数据集合，逐项完成必要的转换或状态更新。
            foreach (var module in modules.Children) pages.Add(module, Execution);
            pages.Add(runs.Children[0], new CurrentTaskViewModel(Editor));
            pages.Add(runs.Children[1], new RunHistoryViewModel(Editor));
            // 将当前结果加入集合，供后续汇总或界面展示。
            pages.Add(runs.Children[2], new OutputResultsViewModel(Editor));
            SelectedPage = task.Children[0];
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            NavigateCommand = new RelayCommand(item => SelectedPage = item as NavigationItemViewModel);
            PlaceholderCommand = new RelayCommand(action => Status = "红外场景工作台：场景任务配置、模块计算与结果分析。");
        }
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private NavigationItemViewModel AddGroup(string title, string description)
        {
            var group = new NavigationItemViewModel(title, description);
            Navigation.Add(group);
            // 返回当前步骤生成的结果，并结束本次调用。
            return group;
        }
        private static void Add(NavigationItemViewModel group, string title, string description)
        {
            // 将当前结果加入集合，供后续汇总或界面展示。
            group.Children.Add(new NavigationItemViewModel(title, description));
        }
        private static NavigationItemViewModel ResolveNavigationTarget(NavigationItemViewModel item)
        {
            // Group headings organize the tree; they are not standalone pages.
            // 返回当前步骤生成的结果，并结束本次调用。
            return item != null && item.Children.Count > 0 ? item.Children[0] : item;
        }
    }
}

