using System;
namespace PreProcess.Wpf.Models {
// 定义 SceneMotionSettings 类型，集中封装与该领域对象相关的状态和行为。
public sealed class SceneMotionSettings : BindableModel {
public Vector3 Position { get; } = new Vector3(0,100000,6500000);
 public Vector3 Direction { get; } = new Vector3(0,-0.996193717,0.0871669503);
 // 通过属性封装状态访问，并在变更时执行必要的同步操作。
 public Vector3 Up { get; } = new Vector3(0,0.0871669503,0.996193717);
 public Vector3 Velocity { get; } = new Vector3(0,-4000,350);
 // 通过属性封装状态访问，并在变更时执行必要的同步操作。
 public Vector3 AngularVelocity { get; } = new Vector3();
public Vector3 AngularAcceleration { get; } = new Vector3();
// 通过属性封装状态访问，并在变更时执行必要的同步操作。
public Vector3 MicroMotionParameters { get; } = new Vector3();
private string companionType = "球壳";
private string attitudeMotionType = "默认";
// 保存该组件运行所需的配置或中间状态。
public string CompanionType {
 get => companionType;
 // 通过属性封装状态访问，并在变更时执行必要的同步操作。
 set {
  var text=(value??String.Empty).Trim();
  // 校验当前条件，仅在满足业务约束时进入该处理分支。
  if(String.Equals(text,"SPHERE",StringComparison.OrdinalIgnoreCase)) text="球壳";
  if(text!="球壳") throw new ArgumentException("当前红外伴飞物类型仅支持球壳。");
  Set(ref companionType,text);
 }
}
// 保存该组件运行所需的配置或中间状态。
public string AttitudeMotionType {
 get => attitudeMotionType;
 // 通过属性封装状态访问，并在变更时执行必要的同步操作。
 set {
  var text=(value??String.Empty).Trim();
  // 校验当前条件，仅在满足业务约束时进入该处理分支。
  if(String.Equals(text,"NONE",StringComparison.OrdinalIgnoreCase)) text="默认";
  Set(ref attitudeMotionType, text=="默认" ? text : Token(text, "姿态运动类型"));
 }
}
private static string Token(string value, string name) {
 // 更新当前流程使用的数据，为下一处理步骤做好准备。
 var text=(value??String.Empty).Trim();
 if(text.Length==0) throw new ArgumentException(name+"不能为空。");
 // 校验当前条件，仅在满足业务约束时进入该处理分支。
 if(text.IndexOfAny(new[]{' ','\t','#','=','[',']'})>=0) throw new ArgumentException(name+"不能包含空格或输入格式保留字符。");
 return text.ToUpperInvariant();
}
}
// 定义 TargetMotionSettings 类型，集中封装与该领域对象相关的状态和行为。
public sealed class TargetMotionSettings : BindableModel {
 public bool CanChangeActivation { get; }
 // 通过属性封装状态访问，并在变更时执行必要的同步操作。
 public Vector3 Position { get; } = new Vector3();
 public Vector3 Velocity { get; } = new Vector3();
 public Vector3 Acceleration { get; } = new Vector3();
 // 保存该组件运行所需的配置或中间状态。
 private double releaseTime;
 public double ReleaseTime { get => releaseTime; set { Finite(value); if(value<0 || (!CanChangeActivation && value!=0)) throw new ArgumentException("释放时间必须非负，1号目标必须为零。"); Set(ref releaseTime,value); } }
 // 保存该组件运行所需的配置或中间状态。
 private bool active=true;
 public bool Active { get => active; set { if(!CanChangeActivation && !value) throw new ArgumentException("1号目标必须启用。"); Set(ref active,value); } }
 // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
 public TargetMotionSettings(bool primary) { CanChangeActivation=!primary; }
}
}
