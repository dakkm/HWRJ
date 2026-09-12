using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PreProcess.Wpf.ViewModels;

namespace PreProcess.Wpf
{
    public partial class MainWindow : Window
    {
        private bool closingAfterStop;
        public MainWindow()
        {
            InitializeComponent();
            var vm = (MainWindowViewModel)DataContext;
            vm.Execution.HasInputErrors = () => HasErrors(EditorContent);
            vm.Execution.PropertyChanged += (s, e) => UpdateEditing();
            vm.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(MainWindowViewModel.CurrentContent)) UpdateEditing(); };
            Closing += OnClosing;
            Closed += (s, e) => vm.Execution.Dispose();
        }
        private void UpdateEditing()
        {
            var vm = (MainWindowViewModel)DataContext;
            EditorContent.IsEnabled = !vm.Execution.IsBusy || ReferenceEquals(vm.CurrentContent, vm.Execution) || vm.CurrentContent is CalculationOutputViewModel;
        }
        private async void OnClosing(object sender, CancelEventArgs e)
        {
            var execution = ((MainWindowViewModel)DataContext).Execution;
            if (closingAfterStop || !execution.IsBusy) return;
            e.Cancel = true;
            await execution.StopAndWaitAsync();
            closingAfterStop = true;
            Close();
        }
        private static bool HasErrors(DependencyObject node)
        {
            if (System.Windows.Controls.Validation.GetHasError(node)) return true;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
                if (HasErrors(VisualTreeHelper.GetChild(node, i))) return true;
            return false;
        }
        private void OnNavigationSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var viewModel = DataContext as MainWindowViewModel;
            var item = e.NewValue as NavigationItemViewModel;
            if (viewModel == null || item == null) return;

            if (item.Children.Count > 0)
            {
                // Selecting a group heading expands it and selects its first real page.
                Dispatcher.BeginInvoke(new Action(() => SelectFirstChild(item)), DispatcherPriority.Input);
                return;
            }

            if (viewModel.NavigateCommand.CanExecute(item))
                viewModel.NavigateCommand.Execute(item);
        }
        private void SelectFirstChild(NavigationItemViewModel group)
        {
            var groupContainer = NavigationTree.ItemContainerGenerator.ContainerFromItem(group) as TreeViewItem;
            if (groupContainer == null || group.Children.Count == 0) return;
            groupContainer.IsExpanded = true;
            groupContainer.UpdateLayout();
            var childContainer = groupContainer.ItemContainerGenerator.ContainerFromItem(group.Children[0]) as TreeViewItem;
            if (childContainer == null) return;
            childContainer.IsSelected = true;
            childContainer.BringIntoView();
        }
    }
}
