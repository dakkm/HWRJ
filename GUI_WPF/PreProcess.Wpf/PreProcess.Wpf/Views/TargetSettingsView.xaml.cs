using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace PreProcess.Wpf.Views
// 说明当前文件承载的类型职责，便于维护者快速定位功能边界。
{
    public sealed class SimilarityPercentageValidationRule : ValidationRule
    {
        // 检查输入及依赖是否满足要求，提前阻止无效操作。
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            double number;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            CultureInfo culture = cultureInfo ?? CultureInfo.CurrentCulture;
            string text = Convert.ToString(value, culture);
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (!Double.TryParse(text, NumberStyles.Float, culture, out number))
                return new ValidationResult(false, "请输入数值。");
            return number >= 50.0 && number <= 100.0
                // 继续处理当前业务步骤，保持上下文状态一致。
                ? ValidationResult.ValidResult
                : new ValidationResult(false, "相似度必须在 50%～100% 之间。");
        }
    }

    // 定义 TargetSettingsView 类型，集中封装与该领域对象相关的状态和行为。
    public partial class TargetSettingsView : UserControl
    {
        public TargetSettingsView() { InitializeComponent(); }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private void SimilarityTextBox_KeyDown(object sender, KeyEventArgs e)
        // 本文件只处理所属模块的数据或界面逻辑，不在此处引入额外副作用。
        {
            if (e.Key != Key.Enter) return;
            var textBox = sender as TextBox;
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            textBox?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            e.Handled = true;
        }
    }
}
