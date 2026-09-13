using System;
namespace PreProcess.Wpf.Models {
public sealed class TargetPhysics : BindableModel {
private double radius = 0.2;
public double Radius { get => radius; set { Finite(value); if(value <= 0.005) throw new ArgumentException("球体半径必须大于 0.005 m。"); Set(ref radius, value); Notify(nameof(Mass)); } }
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
public double Mass => 4.0 / 3.0 * Math.PI * Radius * Radius * Radius * Density;
public TargetPhysics Copy() => new TargetPhysics { Radius = Radius, Density = Density, HeatCapacity = HeatCapacity, InitialTemperature = InitialTemperature, InternalPower = InternalPower, Emissivity = Emissivity, SolarAbsorption = SolarAbsorption, IrReflection = IrReflection };
}}