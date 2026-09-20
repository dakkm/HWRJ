using System;
using System.Windows.Input;

// 说明当前文件承载的类型职责，便于维护者快速定位功能边界。
namespace PreProcess.Wpf.ViewModels
{
    public sealed class RelayCommand : ICommand
    {
        // 保存该组件运行所需的配置或中间状态。
        private readonly Action<object> execute;
        public RelayCommand(Action<object> execute) { this.execute = execute; }
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => execute(parameter);
        // 本文件只处理所属模块的数据或界面逻辑，不在此处引入额外副作用。
        public event EventHandler CanExecuteChanged { add { } remove { } }
    }
}
