// 说明当前文件承载的类型职责，便于维护者快速定位功能边界。
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace PreProcess.Wpf.Views
{
    /// <summary>兼容中英文小数分隔符，避免编辑小数时被即时绑定吞掉。</summary>
    public sealed class FlexibleDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is double number
                ? number.ToString("G", CultureInfo.InvariantCulture)
                : value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = System.Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
            if (String.IsNullOrEmpty(text)) return DependencyProperty.UnsetValue;
            text = text.Replace(',', '.');
            return Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                ? number
                : DependencyProperty.UnsetValue;
        }
    }

    public partial class TargetPhysicsEditor : UserControl
    {
        public TargetPhysicsEditor() { InitializeComponent(); }
    }
}
