namespace PreProcess.Wpf.Models { public sealed class Vector3 : BindableModel {
private double x = 0;
public double X { get => x; set { Finite(value); Set(ref x, value);  } }
private double y = 0;
public double Y { get => y; set { Finite(value); Set(ref y, value);  } }
private double z = 0;
public double Z { get => z; set { Finite(value); Set(ref z, value);  } }
public Vector3(double x=0, double y=0, double z=0) { X=x; Y=y; Z=z; } } }