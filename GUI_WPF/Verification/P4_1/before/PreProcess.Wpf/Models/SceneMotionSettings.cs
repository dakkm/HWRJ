using System;
namespace PreProcess.Wpf.Models {
public sealed class SceneMotionSettings {
 public Vector3 Position { get; } = new Vector3(0,100000,6500000);
 public Vector3 Direction { get; } = new Vector3(0,-0.996193717,0.0871669503);
 public Vector3 Up { get; } = new Vector3(0,0.0871669503,0.996193717);
 public Vector3 Velocity { get; } = new Vector3(0,-4000,350);
 public Vector3 AngularVelocity { get; } = new Vector3();
 public Vector3 AngularAcceleration { get; } = new Vector3();
}
public sealed class TargetMotionSettings : BindableModel {
 public bool CanChangeActivation { get; }
 public Vector3 Position { get; } = new Vector3();
 public Vector3 Velocity { get; } = new Vector3();
 public Vector3 Acceleration { get; } = new Vector3();
 private double releaseTime;
 public double ReleaseTime { get => releaseTime; set { Finite(value); if(value<0 || (!CanChangeActivation && value!=0)) throw new ArgumentException("释放时间必须非负，1号目标必须为零。"); Set(ref releaseTime,value); } }
 private bool active=true;
 public bool Active { get => active; set { if(!CanChangeActivation && !value) throw new ArgumentException("1号目标必须启用。"); Set(ref active,value); } }
 public TargetMotionSettings(bool primary) { CanChangeActivation=!primary; }
}
}