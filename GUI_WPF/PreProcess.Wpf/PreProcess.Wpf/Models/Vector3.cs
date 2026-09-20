namespace PreProcess.Wpf.Models { public sealed class Vector3 : BindableModel {
private double x = 0;
// 通过属性封装状态访问，并在变更时执行必要的同步操作。
public double X { get => x; set { Finite(value); Set(ref x, value);  } }
private double y = 0;
// 通过属性封装状态访问，并在变更时执行必要的同步操作。
public double Y { get => y; set { Finite(value); Set(ref y, value);  } }
private double z = 0;
public double Z { get => z; set { Finite(value); Set(ref z, value);  } }
// 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
public Vector3(double x=0, double y=0, double z=0) { X=x; Y=y; Z=z; } } }
