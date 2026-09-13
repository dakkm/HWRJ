$ErrorActionPreference='Stop'
$project=Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'PreProcess.Wpf/PreProcess.Wpf'
$utf8=New-Object Text.UTF8Encoding($true)
function WriteSource($name,$text){[IO.File]::WriteAllText((Join-Path $project $name),$text.Replace("`r`n","`n"),$utf8)}
function NumberProperty($name,$initial,$validation='Finite(value);',$notify='') {
 $field=$name.Substring(0,1).ToLower()+$name.Substring(1)
 return "private double $field = $initial;`npublic double $name { get => $field; set { $validation Set(ref $field, value); $notify } }`n"
}
$source="using System;`nnamespace PreProcess.Wpf.Models {`npublic sealed class TargetPhysics : BindableModel {`n"
$fields=@(@('Radius','0.2','Finite(value); if(value <= 0.005) throw new ArgumentException("球体半径必须大于 0.005 m。");','Notify(nameof(Mass));'),@('Density','2700','Positive(value);','Notify(nameof(Mass));'),@('HeatCapacity','900','Positive(value);',''),@('InitialTemperature','300','Positive(value);',''),@('InternalPower','300','Finite(value);',''),@('Emissivity','0.95','Finite(value);',''),@('SolarAbsorption','0.95','Finite(value);',''),@('IrReflection','0.05','Finite(value);',''))
foreach($f in $fields){$source+=NumberProperty $f[0] $f[1] $f[2] $f[3]}
$source+='public double Mass => 4.0 / 3.0 * Math.PI * Radius * Radius * Radius * Density;'+"`n"
$source+='public TargetPhysics Copy() => new TargetPhysics { '+(($fields | ForEach-Object {$_.Item(0)+' = '+$_.Item(0)}) -join ', ')+' };'+"`n}}"
WriteSource 'Models/TargetPhysics.cs' $source
$source="namespace PreProcess.Wpf.Models { public sealed class Vector3 : BindableModel {`n"
foreach($axis in 'X','Y','Z'){$source+=NumberProperty $axis '0'}
$source+='public Vector3(double x=0, double y=0, double z=0) { X=x; Y=y; Z=z; } } }'
WriteSource 'Models/Vector3.cs' $source
$source=@'
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
'@
WriteSource 'Models/SceneMotionSettings.cs' $source
$source="namespace PreProcess.Wpf.Models { public sealed class EnvironmentObservationSettings : BindableModel {`n"
foreach($f in @(@('SolarFlux','1361'),@('RadiationTemperature','3'),@('ApertureSize','0.27'),@('FocalLength','1.34'),@('PlaneSize','0.0128'))){$source+=NumberProperty $f[0] $f[1]}
foreach($f in @(@('SunDirection','1,0,0'),@('ObserverPosition','100000,-100000,6500000'),@('ObserverDirection','-0.447213595,0.894427191,0'),@('ObserverUp','0,0,1'),@('ObserverVelocity','-2100,2100,69'),@('ObserverAngularVelocity',''),@('ObserverAngularAcceleration',''),@('DetectorDirection','0,0,1'),@('DetectorUp','-0.447213595,0.894427191,0'),@('DetectorVelocity','0,0,1'),@('DetectorAngularVelocity',''),@('DetectorAngularAcceleration',''))){$source+='public Vector3 '+$f[0]+' { get; } = new Vector3('+$f[1]+');'+"`n"}
$source+='private bool apertureTracking=true, detectorTracking=true; public bool ApertureTracking { get => apertureTracking; set => Set(ref apertureTracking,value); } public bool DetectorTracking { get => detectorTracking; set => Set(ref detectorTracking,value); } } }'
WriteSource 'Models/EnvironmentObservationSettings.cs' $source
