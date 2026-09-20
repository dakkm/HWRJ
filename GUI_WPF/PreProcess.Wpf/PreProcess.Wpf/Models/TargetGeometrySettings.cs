using System;
using System.Collections.ObjectModel;

namespace PreProcess.Wpf.Models
{
    // Frozen backend supports only a spherical shell with 5 mm thickness.
    public enum TargetType { SphericalShell }

    public sealed class TargetTypeOption
    {
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public TargetType Value { get; }
        public string Label { get; }
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public TargetTypeOption(TargetType value, string label) { Value = value; Label = label; }
    }

    public sealed class TargetGeometrySettings : BindableModel
    {
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public ReadOnlyCollection<TargetTypeOption> AvailableTypes { get; } =
            new ReadOnlyCollection<TargetTypeOption>(new[] {
                new TargetTypeOption(TargetType.SphericalShell, "球壳（固定5mm）")
            // 继续处理当前业务步骤，保持上下文状态一致。
            });
        private TargetType targetType = TargetType.SphericalShell;
        // 保存该组件运行所需的配置或中间状态。
        private double radius;
        private double shellThickness;

        // 保存该组件运行所需的配置或中间状态。
        public TargetType TargetType
        {
            get => targetType;
            set
            {
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (value != TargetType.SphericalShell) throw new ArgumentException("当前仅支持球壳。");
                Set(ref targetType, value);
            }
        }
        // Outer radius in metres; shell thickness in millimetres (also the UI unit).
        // 保存该组件运行所需的配置或中间状态。
        public double Radius
        {
            get => radius;
            // 通知界面绑定层刷新相关状态，确保显示内容与模型一致。
            set { Validate(value, shellThickness); Set(ref radius, value); Notify(nameof(Volume)); }
        }
        public double ShellThickness
        // 说明当前文件承载的类型职责，便于维护者快速定位功能边界。
        {
            // 通过属性封装状态访问，并在变更时执行必要的同步操作。
            get => shellThickness;
            set { Validate(radius, value); Set(ref shellThickness, value); Notify(nameof(Volume)); }
        }
        public double Volume
        {
            // 继续处理当前业务步骤，保持上下文状态一致。
            get
            {
                switch (TargetType)
                {
                    // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                    case TargetType.SphericalShell:
                        double t = ShellThickness / 1000.0;
                        // 更新当前流程使用的数据，为下一处理步骤做好准备。
                        double inner = Radius - t;
                        // R³-r³=(R-r)(R²+Rr+r²), avoiding cancellation for thin shells.
                        return 4.0 / 3.0 * Math.PI * t *
                            (Radius * Radius + Radius * inner + inner * inner);
                    // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                    default: throw new InvalidOperationException("尚未实现该目标类型的体积计算。");
                }
            }
        }
        public TargetGeometrySettings() : this(0.2, 5) { }
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private TargetGeometrySettings(double outerRadius, double thicknessMm)
        {
            Validate(outerRadius, thicknessMm);
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            radius = outerRadius;
            shellThickness = thicknessMm;
        }
        private static void Validate(double outerRadius, double thicknessMm)
        {
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            Finite(outerRadius); Finite(thicknessMm);
            if (thicknessMm != 5) throw new ArgumentException("后端仅支持球壳（固定5mm），不能修改壳厚。");
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (outerRadius <= 0.005) throw new ArgumentException("外半径必须大于 0.005 m。");
            if (thicknessMm <= 0 || thicknessMm / 1000.0 >= outerRadius)
                // 发现无效输入或运行状态后立即终止，防止错误继续传播。
                throw new ArgumentException("壳厚必须大于零且小于外半径（注意 mm 与 m 的换算）。");
        }
        public TargetGeometrySettings Copy() => new TargetGeometrySettings(Radius, ShellThickness) { TargetType = TargetType };
    }
}

