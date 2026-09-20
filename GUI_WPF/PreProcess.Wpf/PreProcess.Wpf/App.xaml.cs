using System;
using System.Collections.Generic;
// 说明当前文件承载的类型职责，便于维护者快速定位功能边界。
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
// 本文件只处理所属模块的数据或界面逻辑，不在此处引入额外副作用。
using System.Windows;

namespace PreProcess.Wpf
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    // 调用方应通过公开成员访问功能，内部状态由当前类型统一维护。
    {
    }
}
