using System;
namespace PreProcess.Wpf.Models {
public sealed class TargetPhysics : BindableModel {
public TargetGeometrySettings Geometry { get; }
// Compatibility accessor: dimensions have a single owner, Geometry.
public double Radius { get => Geometry.Radius; set => Geometry.Radius = value; }
public TargetPhysics() : this(new TargetGeometrySettings()) { }
private TargetPhysics(TargetGeometrySettings geometry)
{
    Geometry = geometry;
    Geometry.PropertyChanged += (sender, args) => {
        if(args.PropertyName == nameof(TargetGeometrySettings.Radius)) Notify(nameof(Radius));
        Notify(nameof(Mass));
    };
}
private double density = 2700;
public double Density { get => density; set { Positive(value); Set(ref density, value); Notify(nameof(Mass)); } }
private double heatCapacity = 900;
public double HeatCapacity { get => heatCapacity; set { Positive(value); Set(ref heatCapacity, value);  } }
private double initialTemperature = 300;
public double InitialTemperature { get => initialTemperature; set { Positive(value); Set(ref initialTemperature, value);  } }
private double internalPower = 300;
public double InternalPower { get => internalPower; set { Finite(value); Set(ref internalPower, value);  } }
private double emissivity = 0.95;
public double Emissivity { get => emissivity; set { Finite(value); Set(ref emissivity, value);  } }
private double solarAbsorption = 0.95;
public double SolarAbsorption { get => solarAbsorption; set { Finite(value); Set(ref solarAbsorption, value);  } }
private double irReflection = 0.05;
public double IrReflection { get => irReflection; set { Finite(value); Set(ref irReflection, value);  } }
public double Mass => Geometry.Volume * Density;
public TargetPhysics Copy() => new TargetPhysics(Geometry.Copy()) { Density = Density, HeatCapacity = HeatCapacity, InitialTemperature = InitialTemperature, InternalPower = InternalPower, Emissivity = Emissivity, SolarAbsorption = SolarAbsorption, IrReflection = IrReflection };
}}
