using System.Collections.ObjectModel;

namespace PreProcess.Wpf.ViewModels
{
    // Presentation state only; no business task or backend contract model.
    // 说明当前文件承载的类型职责，便于维护者快速定位功能边界。
    public sealed class NavigationItemViewModel
    {
        public string Title { get; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string Description { get; }
        public ObservableCollection<NavigationItemViewModel> Children { get; }
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            = new ObservableCollection<NavigationItemViewModel>();

        public NavigationItemViewModel(string title, string description)
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            Title = title;
            // 本文件只处理所属模块的数据或界面逻辑，不在此处引入额外副作用。
            Description = description;
        }
    }
}
