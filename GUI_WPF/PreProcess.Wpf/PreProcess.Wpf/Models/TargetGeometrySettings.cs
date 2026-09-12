using System;
using System.Collections.ObjectModel;

namespace PreProcess.Wpf.Models
{
    // Frozen backend supports only a spherical shell with 5 mm thickness.
    public enum TargetType { SphericalShell }

    public sealed class TargetTypeOption
    {
        public TargetType Value { get; }
        public string Label { get; }
        public TargetTypeOption(TargetType value, string label) { Value = value; Label = label; }
    }

    public sealed class TargetGeometrySettings : BindableModel
    {
        public ReadOnlyCollection<TargetTypeOption> AvailableTypes { get; } =
            new ReadOnlyCollection<TargetTypeOption>(new[] {
                new TargetTypeOption(TargetType.SphericalShell, "球壳（固定5mm）")
            });
        private TargetType targetType = TargetType.SphericalShell;
        private double radius;
        private double shellThickness;

        public TargetType TargetType
        {
            get => targetType;
            set
            {
                if (value != TargetType.SphericalShell) throw new ArgumentException("当前仅支持球壳。");
                Set(ref targetType, value);
            }
        }
        // Outer radius in metres; shell thickness in millimetres (also the UI unit).
        public double Radius
        {
            get => radius;
            set { Validate(value, shellThickness); Set(ref radius, value); Notify(nameof(Volume)); }
        }
        public double ShellThickness
        {
            get => shellThickness;
            set { Validate(radius, value); Set(ref shellThickness, value); Notify(nameof(Volume)); }
        }
        public double Volume
        {
            get
            {
                switch (TargetType)
                {
                    case TargetType.SphericalShell:
                        double t = ShellThickness / 1000.0;
                        double inner = Radius - t;
                        // R³-r³=(R-r)(R²+Rr+r²), avoiding cancellation for thin shells.
                        return 4.0 / 3.0 * Math.PI * t *
                            (Radius * Radius + Radius * inner + inner * inner);
                    default: throw new InvalidOperationException("尚未实现该目标类型的体积计算。");
                }
            }
        }
        public TargetGeometrySettings() : this(0.2, 5) { }
        private TargetGeometrySettings(double outerRadius, double thicknessMm)
        {
            Validate(outerRadius, thicknessMm);
            radius = outerRadius;
            shellThickness = thicknessMm;
        }
        private static void Validate(double outerRadius, double thicknessMm)
        {
            Finite(outerRadius); Finite(thicknessMm);
            if (thicknessMm != 5) throw new ArgumentException("后端仅支持球壳（固定5mm），不能修改壳厚。");
            if (outerRadius <= 0.005) throw new ArgumentException("外半径必须大于 0.005 m。");
            if (thicknessMm <= 0 || thicknessMm / 1000.0 >= outerRadius)
                throw new ArgumentException("壳厚必须大于零且小于外半径（注意 mm 与 m 的换算）。");
        }
        public TargetGeometrySettings Copy() => new TargetGeometrySettings(Radius, ShellThickness) { TargetType = TargetType };
    }
}

