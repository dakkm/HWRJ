using System;
using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using PreProcess.Wpf.Models;
using PreProcess.Wpf.Services;

namespace PreProcess.Wpf.ViewModels
{
    /// <summary>
    /// 03 求解定义与控制的职责页面模型，只展示当前已有配置和缺失边界。
    /// </summary>
    public sealed class SolverControlViewModel : BindableModel
    {
        public TaskEditorViewModel Editor { get; private set; }
        public string DefinitionStatus { get { return "部分已有：仿真时长复用任务设置"; } }
        public string TimeStepStatus { get { return "前端配置：时间步长与输出间隔可保存，不提交后端"; } }
        public string ConvergenceStatus { get { return "前端配置：收敛阈值与迭代上限可保存，不提交后端"; } }
        public string ResourceStatus { get { return "实际运行使用计算页的运行、停止与日志功能"; } }
        public string DurationSummary { get { return Editor.Task.Settings.Duration.ToString("g6") + " s"; } }
        private double timeStep = 1.0;
        private double outputInterval = 10.0;
        private double convergenceTolerance = 1e-6;
        private int maxIterations = 1000;
        private int timeoutSeconds = 3600;
        private string solverOption = "现有正向求解器";
        private string controlStatus = "未校验";
        public double TimeStep { get { return timeStep; } set { Set(ref timeStep, value); } }
        public double OutputInterval { get { return outputInterval; } set { Set(ref outputInterval, value); } }
        public double ConvergenceTolerance { get { return convergenceTolerance; } set { Set(ref convergenceTolerance, value); } }
        public int MaxIterations { get { return maxIterations; } set { Set(ref maxIterations, value); } }
        public int TimeoutSeconds { get { return timeoutSeconds; } set { Set(ref timeoutSeconds, value); } }
        public string SolverOption { get { return solverOption; } set { Set(ref solverOption, value); } }
        public string ControlStatus { get { return controlStatus; } }
        public ICommand ValidateControlCommand { get; private set; }
        public ICommand SaveControlCommand { get; private set; }
        public ICommand OpenControlCommand { get; private set; }

        public SolverControlViewModel(TaskEditorViewModel editor)
        {
            Editor = editor;
            Editor.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(TaskEditorViewModel.Task)) { ObserveDuration(); Notify(nameof(DurationSummary)); } };
            ObserveDuration();
            ValidateControlCommand = new RelayCommand(_ => ValidateControl());
            SaveControlCommand = new RelayCommand(_ => SaveControl());
            OpenControlCommand = new RelayCommand(_ => OpenControl());
        }

        private PreProcess.Wpf.Models.TaskSettings observedSettings;
        private void ObserveDuration()
        {
            if (observedSettings != null) observedSettings.PropertyChanged -= DurationChanged;
            observedSettings = Editor.Task.Settings;
            if (observedSettings != null) observedSettings.PropertyChanged += DurationChanged;
        }
        private void DurationChanged(object sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            if (args.PropertyName == "Duration") Notify(nameof(DurationSummary));
        }

        private void ValidateControl()
        {
            if (double.IsNaN(TimeStep) || double.IsInfinity(TimeStep) || double.IsNaN(OutputInterval) || double.IsInfinity(OutputInterval) ||
                double.IsNaN(ConvergenceTolerance) || double.IsInfinity(ConvergenceTolerance) ||
                TimeStep <= 0 || OutputInterval <= 0 || ConvergenceTolerance <= 0 || MaxIterations <= 0 || TimeoutSeconds <= 0)
                controlStatus = "求解控制检查失败：时间步长、输出频率、收敛阈值、迭代次数和超时必须为正数。";
            else if (OutputInterval < TimeStep)
                controlStatus = "求解控制检查失败：输出频率不能小于时间步长。";
            else controlStatus = "配置校验通过。";
            Notify(nameof(ControlStatus));
        }

        private SolverControlConfigPackage Package()
        {
            return new SolverControlConfigPackage { Format = "solver-control-config-v1", CreatedAtUtc = DateTime.UtcNow.ToString("o"), TimeStep = TimeStep, OutputInterval = OutputInterval, ConvergenceTolerance = ConvergenceTolerance, MaxIterations = MaxIterations, TimeoutSeconds = TimeoutSeconds, SolverOption = SolverOption };
        }

        private void SaveControl()
        {
            var dialog = new SaveFileDialog { Filter = "03求解控制配置 (*.solver-control.json)|*.solver-control.json|JSON 文件 (*.json)|*.json", DefaultExt = ".solver-control.json", AddExtension = true };
            if (dialog.ShowDialog() != true) return;
            try { new SolverControlConfigStore().Save(dialog.FileName, Package()); controlStatus = "配置已保存。"; }
            catch (Exception ex) { controlStatus = "保存失败：" + ex.Message; }
            Notify(nameof(ControlStatus));
        }

        private void OpenControl()
        {
            var dialog = new OpenFileDialog { Filter = "03求解控制配置 (*.solver-control.json)|*.solver-control.json|JSON 文件 (*.json)|*.json", CheckFileExists = true };
            if (dialog.ShowDialog() != true) return;
            try
            {
                var package = new SolverControlConfigStore().LoadAndValidate(dialog.FileName);
                TimeStep = package.TimeStep; OutputInterval = package.OutputInterval; ConvergenceTolerance = package.ConvergenceTolerance; MaxIterations = package.MaxIterations; TimeoutSeconds = package.TimeoutSeconds; SolverOption = package.SolverOption;
                controlStatus = "配置已加载。";
            }
            catch (Exception ex) { controlStatus = "打开失败：" + ex.Message; }
            Notify(nameof(ControlStatus));
        }
    }
}
