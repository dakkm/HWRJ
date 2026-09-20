# 预处理仿真平台完整离线安装包

安装包包含 WPF 图形界面、`coreprogram` 后端、Python 3.13 基础解释器、完整科学计算环境、TensorFlow/Keras 模型运行环境，以及 Microsoft .NET Framework 4.8 离线运行库。

默认安装位置：

```text
%LOCALAPPDATA%\Programs\PreProcess.Wpf
```

运行结果默认写入安装目录下的 `output` 文件夹。安装程序不会在卸载时主动删除用户生成的结果文件。

目标系统要求：Windows 10 1809 或更高版本、x64 架构。若目标机没有 .NET Framework 4.8，安装程序会自动调用随包附带的微软离线安装程序，过程中可能出现系统权限确认。

当前完整安装包：

```text
output\installer\PreProcess-Windows-x64-Full-Setup.exe
大小：360853910 字节（约 344.14 MB）
SHA-256：D466DA92E589BC835E1E118E327EFC63348DB6666C7685D090AE02013B63F9A0
```

安装后占用空间约 1.78 GB。安装包尚未使用商业代码签名证书签名，因此复制到其他机器后 Windows SmartScreen 可能显示未知发布者提示。

已完成静默试装验证：Python 3.13.5 路径重定位成功，NumPy、pandas、joblib、scikit-learn、TensorFlow 均可加载，四个后端标准入口均正常解析，GUI 可以启动。

重新构建安装包前，应先以 Release 配置编译 `PreProcess.Wpf.csproj`，随后用 Inno Setup 6 编译 `PreProcessFull.iss`。
