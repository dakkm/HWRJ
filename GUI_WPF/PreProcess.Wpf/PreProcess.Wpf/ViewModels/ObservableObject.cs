using System.ComponentModel;
using System.Runtime.CompilerServices;

// 说明当前文件承载的类型职责，便于维护者快速定位功能边界。
namespace PreProcess.Wpf.ViewModels
{
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        // 通知界面绑定层刷新相关状态，确保显示内容与模型一致。
        public event PropertyChangedEventHandler PropertyChanged;
        protected void Notify([CallerMemberName] string name = null)
        {
            // 通知界面绑定层刷新相关状态，确保显示内容与模型一致。
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        // 本文件只处理所属模块的数据或界面逻辑，不在此处引入额外副作用。
        }
    }
}
