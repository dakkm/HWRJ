using System;
using System.Windows.Input;

namespace PreProcess.Wpf.ViewModels
{
    public sealed class RelayCommand : ICommand
    {
        private readonly Action<object> execute;
        public RelayCommand(Action<object> execute) { this.execute = execute; }
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => execute(parameter);
        public event EventHandler CanExecuteChanged { add { } remove { } }
    }
}
