using System;
namespace PreProcess.Wpf.Models {
public sealed class TargetPhysics : BindableModel {
// 通过属性封装状态访问，并在变更时执行必要的同步操作。
public TargetGeometrySettings Geometry { get; }
// Compatibility accessor: dimensions have a single owner, Geometry.
public double Radius { get => Geometry.Radius; set => Geometry.Radius = value; }
// 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
public TargetPhysics() : this(new TargetGeometrySettings()) { }
private TargetPhysics(TargetGeometrySettings geometry)
{
    // 更新当前流程使用的数据，为下一处理步骤做好准备。
    Geometry = geometry;
    Geometry.PropertyChanged += (sender, args) => {
        if(args.PropertyName == nameof(TargetGeometrySettings.Radius)) Notify(nameof(Radius));
        // 通知界面绑定层刷新相关状态，确保显示内容与模型一致。
        Notify(nameof(Mass));
    };
}
private double density = 2700;
// 通知界面绑定层刷新相关状态，确保显示内容与模型一致。
public double Density { get => density; set { Positive(value); Set(ref density, value); Notify(nameof(Mass)); } }
private double heatCapacity = 900;
// 通过属性封装状态访问，并在变更时执行必要的同步操作。
public double HeatCapacity { get => heatCapacity; set { Positive(value); Set(ref heatCapacity, value);  } }
private double initialTemperature = 300;
// 通过属性封装状态访问，并在变更时执行必要的同步操作。
public double InitialTemperature { get => initialTemperature; set { Positive(value); Set(ref initialTemperature, value);  } }
private double internalPower = 300;
public double InternalPower { get => internalPower; set { Finite(value); Set(ref internalPower, value);  } }
// 保存该组件运行所需的配置或中间状态。
private double emissivity = 0.95;
public double Emissivity { get => emissivity; set { Finite(value); Set(ref emissivity, value);  } }
// 保存该组件运行所需的配置或中间状态。
private double solarAbsorption = 0.95;
public double SolarAbsorption { get => solarAbsorption; set { Finite(value); Set(ref solarAbsorption, value);  } }
// 保存该组件运行所需的配置或中间状态。
private double irReflection = 0.05;
public double IrReflection { get => irReflection; set { Finite(value); Set(ref irReflection, value);  } }
public double Mass => Geometry.Volume * Density;
// 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
public TargetPhysics Copy() => new TargetPhysics(Geometry.Copy()) { Density = Density, HeatCapacity = HeatCapacity, InitialTemperature = InitialTemperature, InternalPower = InternalPower, Emissivity = Emissivity, SolarAbsorption = SolarAbsorption, IrReflection = IrReflection };
}}
