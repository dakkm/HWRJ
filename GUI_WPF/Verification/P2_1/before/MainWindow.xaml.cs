using System.Windows;
using PreProcess.Wpf.ViewModels;

namespace PreProcess.Wpf
{
    public partial class MainWindow : Window
    {
        public MainWindow() { InitializeComponent(); }

        // WPF TreeView.SelectedItem is read-only: bridge the view event to a VM command.
        private void OnNavigationSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var viewModel = DataContext as MainWindowViewModel;
            if (viewModel?.NavigateCommand.CanExecute(e.NewValue) == true)
                viewModel.NavigateCommand.Execute(e.NewValue);
        }
    }
}
