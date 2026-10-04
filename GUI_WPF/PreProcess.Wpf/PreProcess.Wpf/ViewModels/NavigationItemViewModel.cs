using System.Collections.ObjectModel;

namespace PreProcess.Wpf.ViewModels
{
    // Presentation state only; no business task or backend contract model.
    // 说明当前文件承载的类型职责，便于维护者快速定位功能边界。
    public sealed class NavigationItemViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public string Title { get; }
        private bool isExpanded;
        public bool IsExpanded { get { return isExpanded; } set { if (isExpanded == value) return; isExpanded = value; Notify(nameof(IsExpanded)); } }
        private bool isSelected;
        public bool IsSelected { get { return isSelected; } set { if (isSelected == value) return; isSelected = value; Notify(nameof(IsSelected)); } }
        private string status;
        public string Status { get { return status; } private set { if (status == value) return; status = value; Notify("Status"); Notify("DisplayTitle"); } }
        public string DisplayTitle { get { return Status == "已保存" || Status == "待保存" || Status == "结果可用" || Status == "运行中" || Status == "失败" ? Title + "  [" + Status + "]" : Title; } }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string Description { get; }
        public ObservableCollection<NavigationItemViewModel> Children { get; }
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            = new ObservableCollection<NavigationItemViewModel>();

        public NavigationItemViewModel(string title, string description, string status = null)
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            Title = title;
            // 本文件只处理所属模块的数据或界面逻辑，不在此处引入额外副作用。
            Description = description;
            this.status = status ?? InferStatus(title);
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        public void UpdateStatus(string value) { Status = value; }
        private void Notify(string name)
        {
            if (PropertyChanged != null) PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        }

        private static string InferStatus(string title)
        {
            if (title == null) return null;
            if (title.IndexOf("正向计算内部阶段", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                title.IndexOf("空间外热流", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                title.IndexOf("温度场", System.StringComparison.OrdinalIgnoreCase) >= 0) return "正向计算阶段";
            if (title.IndexOf("复用", System.StringComparison.OrdinalIgnoreCase) >= 0) return "复用入口";
            if (title.IndexOf("后处理任务", System.StringComparison.OrdinalIgnoreCase) >= 0) return "结果存在后可用";
            if (title.StartsWith("02 ", System.StringComparison.OrdinalIgnoreCase)) return "配置与展示";
            if (title.StartsWith("03 ", System.StringComparison.OrdinalIgnoreCase)) return "配置与展示";
            if (title.StartsWith("04 ", System.StringComparison.OrdinalIgnoreCase) ||
                title.StartsWith("05 ", System.StringComparison.OrdinalIgnoreCase) ||
                title.StartsWith("06 ", System.StringComparison.OrdinalIgnoreCase)) return "正向计算阶段";
            if (title.StartsWith("07 ", System.StringComparison.OrdinalIgnoreCase) ||
                title.StartsWith("08 ", System.StringComparison.OrdinalIgnoreCase) ||
                title.StartsWith("09 ", System.StringComparison.OrdinalIgnoreCase)) return "已有调用入口";
            if (title.StartsWith("01 ", System.StringComparison.OrdinalIgnoreCase)) return "任务配置";
            return null;
        }
    }
}
