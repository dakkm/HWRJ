namespace PreProcess.Wpf.Models { public sealed class EnvironmentObservationSettings : BindableModel {
// 保存该组件运行所需的配置或中间状态。
private double solarFlux = 1361;
public double SolarFlux { get => solarFlux; set { Finite(value); Set(ref solarFlux, value);  } }
private double radiationTemperature = 3;
// 通过属性封装状态访问，并在变更时执行必要的同步操作。
public double RadiationTemperature { get => radiationTemperature; set { Finite(value); Set(ref radiationTemperature, value);  } }
private double apertureSize = 0.27;
// 通过属性封装状态访问，并在变更时执行必要的同步操作。
public double ApertureSize { get => apertureSize; set { Finite(value); Set(ref apertureSize, value);  } }
private double focalLength = 1.34;
// 通过属性封装状态访问，并在变更时执行必要的同步操作。
public double FocalLength { get => focalLength; set { Finite(value); Set(ref focalLength, value);  } }
private double planeSize = 0.0128;
public double PlaneSize { get => planeSize; set { Finite(value); Set(ref planeSize, value);  } }
// 通过属性封装状态访问，并在变更时执行必要的同步操作。
public Vector3 SunDirection { get; } = new Vector3(1,0,0);
public Vector3 ObserverPosition { get; } = new Vector3(100000,-100000,6500000);
// 通过属性封装状态访问，并在变更时执行必要的同步操作。
public Vector3 ObserverDirection { get; } = new Vector3(-0.447213595,0.894427191,0);
public Vector3 ObserverUp { get; } = new Vector3(0,0,1);
// 通过属性封装状态访问，并在变更时执行必要的同步操作。
public Vector3 ObserverVelocity { get; } = new Vector3(-2100,2100,69);
public Vector3 ObserverAngularVelocity { get; } = new Vector3();
public Vector3 ObserverAngularAcceleration { get; } = new Vector3();
// 通过属性封装状态访问，并在变更时执行必要的同步操作。
public Vector3 DetectorDirection { get; } = new Vector3(0,0,1);
public Vector3 DetectorUp { get; } = new Vector3(-0.447213595,0.894427191,0);
// 通过属性封装状态访问，并在变更时执行必要的同步操作。
public Vector3 DetectorVelocity { get; } = new Vector3(0,0,1);
public Vector3 DetectorAngularVelocity { get; } = new Vector3();
// 通过属性封装状态访问，并在变更时执行必要的同步操作。
public Vector3 DetectorAngularAcceleration { get; } = new Vector3();
private bool apertureTracking=true, detectorTracking=true; public bool ApertureTracking { get => apertureTracking; set => Set(ref apertureTracking,value); } public bool DetectorTracking { get => detectorTracking; set => Set(ref detectorTracking,value); } } }
