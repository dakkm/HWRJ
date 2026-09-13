using System.Collections.ObjectModel;
using System.Windows.Input;

namespace PreProcess.Wpf.ViewModels
{
    public sealed class MainWindowViewModel : ObservableObject
    {
        public ObservableCollection<NavigationItemViewModel> Navigation { get; }
            = new ObservableCollection<NavigationItemViewModel>();
        private NavigationItemViewModel selectedPage;
        public NavigationItemViewModel SelectedPage
        {
            get => selectedPage;
            set { if (selectedPage == value || value == null) return; selectedPage = value; Notify(); }
        }
        private string status = "就绪 · 工作台预览";
        public string Status { get => status; private set { status = value; Notify(); } }
        public ICommand NavigateCommand { get; }
        public ICommand PlaceholderCommand { get; }

        public MainWindowViewModel()
        {
            var task = AddGroup("场景任务", "在这里组织场景任务的设置与数据。");
            Add(task, "任务设置", "任务基本信息的编辑区域。");
            Add(task, "目标参数", "目标自身参数的编辑区域。");
            Add(task, "场景与运动", "目标群及单目标空间状态的编辑区域。");
            Add(task, "环境与观测", "环境条件与观测设置的编辑区域。");
            Add(task, "计算与输出", "计算选项与输出设置的编辑区域。");
            var modules = AddGroup("功能模块", "选择要使用的功能模块。");
            Add(modules, "正向计算", "正向计算工作区域。");
            Add(modules, "智能预测", "智能预测工作区域。");
            Add(modules, "轨迹生成", "轨迹生成工作区域。");
            Add(modules, "相似度评估", "相似度评估工作区域。");
            Add(modules, "红外场景构建", "红外场景构建工作区域。");
            var runs = AddGroup("运行管理", "任务运行及输出的管理区域。");
            Add(runs, "当前任务", "当前任务概览区域。");
            Add(runs, "历史任务", "历史任务列表区域。");
            Add(runs, "输出结果", "输出结果浏览区域。");
            Add(runs, "日志与告警", "任务日志与告警浏览区域。");
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
    }
}
