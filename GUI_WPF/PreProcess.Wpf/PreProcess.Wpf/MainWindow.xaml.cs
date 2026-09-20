using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PreProcess.Wpf.ViewModels;

namespace PreProcess.Wpf
{
    // 定义 MainWindow 类型，集中封装与该领域对象相关的状态和行为。
    public partial class MainWindow : Window
    {
        private bool closingAfterStop;
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public MainWindow()
        {
            InitializeComponent();
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var vm = (MainWindowViewModel)DataContext;
            vm.Execution.HasInputErrors = () => HasErrors(EditorContent);
            vm.Execution.PropertyChanged += (s, e) => UpdateEditing();
            // 通知界面绑定层刷新相关状态，确保显示内容与模型一致。
            vm.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(MainWindowViewModel.CurrentContent)) UpdateEditing(); };
            Closing += OnClosing;
            // 结束当前资源的使用，避免残留句柄或后台任务。
            Closed += (s, e) => vm.Execution.Dispose();
        }
        private void UpdateEditing()
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var vm = (MainWindowViewModel)DataContext;
            EditorContent.IsEnabled = !vm.Execution.IsBusy || ReferenceEquals(vm.CurrentContent, vm.Execution);
        }
        private async void OnClosing(object sender, CancelEventArgs e)
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var execution = ((MainWindowViewModel)DataContext).Execution;
            if (closingAfterStop || !execution.IsBusy) return;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            e.Cancel = true;
            await execution.StopAndWaitAsync();
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            closingAfterStop = true;
            Close();
        }
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static bool HasErrors(DependencyObject node)
        {
            if (System.Windows.Controls.Validation.GetHasError(node)) return true;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (HasErrors(VisualTreeHelper.GetChild(node, i))) return true;
            return false;
        }
        // 通知界面绑定层刷新相关状态，确保显示内容与模型一致。
        private void OnNavigationSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var viewModel = DataContext as MainWindowViewModel;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var item = e.NewValue as NavigationItemViewModel;
            if (viewModel == null || item == null) return;

            if (item.Children.Count > 0)
            {
                // Selecting a group heading expands it and selects its first real page.
                // 将界面状态变更调度到用户界面线程，避免跨线程访问。
                Dispatcher.BeginInvoke(new Action(() => SelectFirstChild(item)), DispatcherPriority.Input);
                return;
            }

            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (viewModel.NavigateCommand.CanExecute(item))
                viewModel.NavigateCommand.Execute(item);
        }
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private void SelectFirstChild(NavigationItemViewModel group)
        {
            var groupContainer = NavigationTree.ItemContainerGenerator.ContainerFromItem(group) as TreeViewItem;
            if (groupContainer == null || group.Children.Count == 0) return;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            groupContainer.IsExpanded = true;
            groupContainer.UpdateLayout();
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var childContainer = groupContainer.ItemContainerGenerator.ContainerFromItem(group.Children[0]) as TreeViewItem;
            if (childContainer == null) return;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            childContainer.IsSelected = true;
            childContainer.BringIntoView();
        }
    }
}
