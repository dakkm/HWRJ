using System.Collections.ObjectModel;
using System.Windows.Input;

namespace PreProcess.Wpf.ViewModels
{
    public sealed class MainWindowViewModel : ObservableObject
    {
        public ObservableCollection<NavigationItemViewModel> Navigation { get; }
            = new ObservableCollection<NavigationItemViewModel>();
        public TaskEditorViewModel Editor { get; } = new TaskEditorViewModel();
        private readonly System.Collections.Generic.Dictionary<NavigationItemViewModel, object> pages
            = new System.Collections.Generic.Dictionary<NavigationItemViewModel, object>();
        public ForwardRunViewModel Execution { get; }
        public object CurrentContent => selectedPage != null && pages.ContainsKey(selectedPage)
            ? (object)pages[selectedPage] : selectedPage;
        private NavigationItemViewModel selectedPage;
        public NavigationItemViewModel SelectedPage
        {
            get => selectedPage;
            set
            {
                NavigationItemViewModel target = ResolveNavigationTarget(value);
                if (selectedPage == target || target == null) return;
                selectedPage = target;
                if (pages.ContainsKey(target) && ReferenceEquals(pages[target], Execution))
                    Execution.SelectedModule = target.Title == "智能预测" ? "02"
                        : target.Title == "相似度评估" ? "03"
                        : target.Title == "红外场景构建" ? "04"
                        : target.Title == "轨迹生成" ? "轨迹" : "01";
                Notify();
                Notify(nameof(CurrentContent));
            }
        }
        private string status = "就绪 · 工作台预览";
        public string Status { get => status; private set { status = value; Notify(); } }
        public ICommand NavigateCommand { get; }
        public ICommand PlaceholderCommand { get; }

        public MainWindowViewModel()
        {
            Execution = new ForwardRunViewModel(Editor); Editor.Execution = Execution;
            Execution.PropertyChanged += (sender, args) => { if (args.PropertyName == nameof(ForwardRunViewModel.Status)) Status = Execution.Status; };
            Editor.PropertyChanged += (sender, args) => { if (args.PropertyName == nameof(TaskEditorViewModel.Message)) Status = Editor.Message; };
            var task = AddGroup("场景任务", "在这里组织场景任务的设置与数据。");
            Add(task, "任务设置", "任务基本信息的编辑区域。");
            Add(task, "目标参数", "目标自身参数的编辑区域。");
            Add(task, "场景与运动", "目标群及单目标空间状态的编辑区域。");
            Add(task, "环境与观测", "环境条件与观测设置的编辑区域。");
            Add(task, "计算与输出", "计算选项与输出设置的编辑区域。");
            var modules = AddGroup("功能模块", "选择要使用的功能模块。");
            Add(modules, "正向计算", "正向计算工作区域。");
            Add(modules, "智能预测", "智能预测工作区域。");
            Add(modules, "轨迹生成", "选择外部或01默认轨迹并调用轨迹专用程序。");
            Add(modules, "相似度评估", "相似度评估工作区域。");
            Add(modules, "红外场景构建", "红外场景构建工作区域。");
            var runs = AddGroup("运行管理", "任务运行及输出的管理区域。");
            Add(runs, "当前任务", "当前任务概览区域。");
            Add(runs, "历史任务", "历史任务列表区域。");
            Add(runs, "输出结果", "输出结果浏览区域。");
            pages.Add(task.Children[0], new TaskSettingsViewModel(Editor));
            pages.Add(task.Children[1], new TargetSettingsViewModel(Editor));
            pages.Add(task.Children[2], new SceneMotionViewModel(Editor));
            pages.Add(task.Children[3], new EnvironmentObservationViewModel(Editor));
            pages.Add(task.Children[4], new CalculationOutputViewModel(Editor));
            foreach (var module in modules.Children) pages.Add(module, Execution);
            pages.Add(runs.Children[0], new CurrentTaskViewModel(Editor));
            pages.Add(runs.Children[1], new RunHistoryViewModel(Editor));
            pages.Add(runs.Children[2], new OutputResultsViewModel(Editor));
            SelectedPage = task.Children[0];
            NavigateCommand = new RelayCommand(item => SelectedPage = item as NavigationItemViewModel);
            PlaceholderCommand = new RelayCommand(action => Status = action + "：当前为工作台骨架，此操作尚未接入。");
        }
        private NavigationItemViewModel AddGroup(string title, string description)
        {
            var group = new NavigationItemViewModel(title, description);
            Navigation.Add(group);
            return group;
        }
        private static void Add(NavigationItemViewModel group, string title, string description)
        {
            group.Children.Add(new NavigationItemViewModel(title, description));
        }
        private static NavigationItemViewModel ResolveNavigationTarget(NavigationItemViewModel item)
        {
            // Group headings organize the tree; they are not standalone pages.
            return item != null && item.Children.Count > 0 ? item.Children[0] : item;
        }
    }
}

