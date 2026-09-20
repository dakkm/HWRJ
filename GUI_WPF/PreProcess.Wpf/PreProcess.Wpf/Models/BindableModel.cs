using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PreProcess.Wpf.Models
// 说明当前文件承载的类型职责，便于维护者快速定位功能边界。
{
    public abstract class BindableModel : INotifyPropertyChanged
    {
        // 通知界面绑定层刷新相关状态，确保显示内容与模型一致。
        public event PropertyChangedEventHandler PropertyChanged;
        protected void Notify([CallerMemberName] string property = null)
            // 通知界面绑定层刷新相关状态，确保显示内容与模型一致。
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        protected void Set<T>(ref T field, T value, [CallerMemberName] string property = null)
        {
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            // 通知界面绑定层刷新相关状态，确保显示内容与模型一致。
            Notify(property);
        }
        protected static void Finite(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                // 发现无效输入或运行状态后立即终止，防止错误继续传播。
                throw new ArgumentException("请输入有限数值。");
        }
        // 本文件只处理所属模块的数据或界面逻辑，不在此处引入额外副作用。
        protected static void Positive(double value)
        {
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            Finite(value);
            if (value <= 0) throw new ArgumentException("数值必须大于零。");
        }
    }
}
