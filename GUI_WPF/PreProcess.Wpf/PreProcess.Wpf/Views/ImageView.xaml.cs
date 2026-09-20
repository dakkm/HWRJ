using System.Windows.Controls;

// 说明当前文件承载的类型职责，便于维护者快速定位功能边界。
namespace PreProcess.Wpf.Views
{
    public partial class ImageView : UserControl
    {
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public ImageView() { InitializeComponent(); }
    // 本文件只处理所属模块的数据或界面逻辑，不在此处引入额外副作用。
    }
}
