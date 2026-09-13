using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using PreProcess.Wpf.Services;
using PreProcess.Wpf.ViewModels;

namespace PreProcess.Wpf.Views
{
    public partial class CalculationOutputView : UserControl
    {
        public CalculationOutputView() { InitializeComponent(); }
        private void GenerateRequest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (HasErrors(Window.GetWindow(this)))
                    throw new ArgumentException("请先修正页面中标红的输入，再生成请求。");
                var task = ((CalculationOutputViewModel)DataContext).Editor.Task;
                var generator = new RequestGenerator();
                generator.Generate(task);
                var dialog = new SaveFileDialog { FileName = "request.json", Filter = "JSON 请求 (*.json)|*.json", DefaultExt = ".json", AddExtension = true, OverwritePrompt = false };
                if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
                bool overwrite = File.Exists(dialog.FileName);
                if (overwrite && MessageBox.Show(Window.GetWindow(this), "请求文件已存在，是否覆盖？\n" + dialog.FileName,
                    "确认覆盖", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                string path = generator.Save(task, dialog.FileName, overwrite);
                RequestStatus.Text = "请求已生成并通过合同验证：" + path;
            }
            catch (Exception exception) { RequestStatus.Text = "生成失败：" + exception.Message; }
        }
        private static bool HasErrors(DependencyObject node)
        {
            if (node == null) return false;
            if (System.Windows.Controls.Validation.GetHasError(node)) return true;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
                if (HasErrors(VisualTreeHelper.GetChild(node, i))) return true;
            return false;
        }
    }
}
