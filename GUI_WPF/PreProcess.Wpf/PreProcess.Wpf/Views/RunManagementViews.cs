using System.Windows.Controls;

namespace PreProcess.Wpf.Views
{
    // 定义 CurrentTaskView 类型，集中封装与该领域对象相关的状态和行为。
    public partial class CurrentTaskView : UserControl
    { public CurrentTaskView() { InitializeComponent(); } }

    // 定义 RunHistoryView 类型，集中封装与该领域对象相关的状态和行为。
    public partial class RunHistoryView : UserControl
    { public RunHistoryView() { InitializeComponent(); } }

    public partial class OutputResultsView : UserControl
    // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
    { public OutputResultsView() { InitializeComponent(); } }

}
