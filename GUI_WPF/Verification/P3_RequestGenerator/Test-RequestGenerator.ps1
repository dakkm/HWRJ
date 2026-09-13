$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Web.Extensions
$repo=Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
[void][Reflection.Assembly]::LoadFrom((Join-Path $repo 'GUI_WPF/PreProcess.Wpf/PreProcess.Wpf/bin/Release/PreProcess.Wpf.exe'))
$generator=New-Object PreProcess.Wpf.Services.RequestGenerator
$serializer=New-Object System.Web.Script.Serialization.JavaScriptSerializer
$serializer.MaxJsonLength=33554432
$results=New-Object 'System.Collections.Generic.List[string]'
function Assert($condition,$name) { if(!$condition){throw $name}; $results.Add('PASS '+$name) }
function Reject($action,$name) { $caught=$false; try {& $action} catch {$caught=$true}; Assert $caught $name }
$task=New-Object PreProcess.Wpf.Models.TaskModel
$json=$generator.Generate($task)
$generator.ValidateJson($json)
$d=$serializer.DeserializeObject($json)
Assert ($d.Count -eq 6 -and $d['TARGET_PHYSICS'].Count -eq 16 -and $d['TARGET_SCENE'].Count -eq 16) 'Default complete request'
$schema=Get-Content (Join-Path $repo 'coreprogram/01-正向仿真/01-输入文件/forward_request_schema.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach($section in $schema.top_level_sections) {
 $definition=$schema.fields.$section
 $rows=if($section -like 'TARGET_*') {@($d[$section])} else {,@($d[$section])}
 $names=if($section -like 'TARGET_*') {@($definition.row_fields)} else {@($definition.PSObject.Properties.Name)}
 foreach($row in $rows) { Assert (@(Compare-Object @($row.Keys | Sort-Object) @($names | Sort-Object)).Count -eq 0) ('Schema exact keys '+$section) }
}
$target=$task.IndividualTargets[1]; $target.UseOverride=$true
$target.EffectivePhysics.Radius=0.3
$target.EffectivePhysics.IrReflection=0.37
$target.Motion.Acceleration.Z=7
$task.Scene.Up.X=0.2
$task.Scene.AngularAcceleration.Y=0.4
$task.Environment.ObserverDirection.X=0.6
$task.Environment.ObserverAngularAcceleration.Z=0.8
$task.Environment.DetectorAngularAcceleration.X=0.9
$task.Environment.ApertureTracking=$false
$c=$serializer.DeserializeObject($generator.Generate($task))
Assert ($c['TARGET_PHYSICS'][1]['r'] -eq 0.3 -and $c['TARGET_PHYSICS'][0]['r'] -eq 0.2) 'Independent target override'
Assert ($c['TARGET_PHYSICS'][1]['rho_ir'] -eq 0.37 -and $c['TARGET_SCENE'][1]['az'] -eq 7) 'Explicit reflection and acceleration'
Assert ($c['GROUP_STATE']['GROUP_UP'][0] -eq 0.2 -and $c['GROUP_STATE']['GROUP_ANGULAR_ACCELERATION'][1] -eq 0.4) 'Complete group mapping'
Assert ($c['OBSERVATION']['APERTURE_NORMAL'][0] -eq 0.6 -and $c['OBSERVATION']['APERTURE_ANGULAR_ACCELERATION'][2] -eq 0.8 -and $c['OBSERVATION']['DETECTOR_ANGULAR_ACCELERATION'][0] -eq 0.9 -and !$c['OBSERVATION']['APERTURE_TRACK_TARGET']) 'Complete observation mapping'
Reject {$task.Targets.Geometry.ShellThickness=6} 'Reject variable shell thickness'
Assert ($task.Targets.Geometry.ShellThickness -eq 5) 'Fixed thickness remains 5mm'
Assert ($json -notmatch 'Mass|ShellThickness|TargetType|SimilarityIndex|schema_version|Metadata|UseOverride') 'No unsupported request fields'
Reject {$generator.Generate($null)} 'Missing task rejected'
foreach($section in $schema.top_level_sections) {
 $bad=$serializer.DeserializeObject($json); $null=$bad.Remove($section)
 Reject {$generator.ValidateJson($serializer.Serialize($bad))} ('Missing section '+$section)
}
foreach($section in @('CASE','ENVIRONMENT','GROUP_STATE','OBSERVATION')) {
 foreach($name in @($d[$section].Keys)) {
  $bad=$serializer.DeserializeObject($json); $null=$bad[$section].Remove($name)
  Reject {$generator.ValidateJson($serializer.Serialize($bad))} ('Missing field '+$section+'.'+$name)
 }
}
foreach($section in @('TARGET_PHYSICS','TARGET_SCENE')) {
 foreach($name in @($d[$section][0].Keys)) {
  $bad=$serializer.DeserializeObject($json); $null=$bad[$section][0].Remove($name)
  Reject {$generator.ValidateJson($serializer.Serialize($bad))} ('Missing row field '+$section+'.'+$name)
 }
}
$bad=$serializer.DeserializeObject($json); $bad['TARGET_PHYSICS'][0]['Mass']=1
Reject {$generator.ValidateJson($serializer.Serialize($bad))} 'Unknown field rejected'
$bad=$serializer.DeserializeObject($json); $bad['TARGET_SCENE'][1]['id']=1
Reject {$generator.ValidateJson($serializer.Serialize($bad))} 'Duplicate ID rejected'
$bad=$serializer.DeserializeObject($json); $bad['CASE']['NUM_SPHERES']=15
Reject {$generator.ValidateJson($serializer.Serialize($bad))} 'Row count rejected'
$bad=$serializer.DeserializeObject($json); $bad['TARGET_SCENE'][0]['active']=$false
Reject {$generator.ValidateJson($serializer.Serialize($bad))} 'Primary activation rejected'
$bad=$serializer.DeserializeObject($json); $bad['TARGET_PHYSICS'][0]['r']=0.005
Reject {$generator.ValidateJson($serializer.Serialize($bad))} 'Backend radius boundary rejected'
$bad=$serializer.DeserializeObject($json); $bad['OBSERVATION']['APERTURE_TRACK_TARGET']='T'
Reject {$generator.ValidateJson($serializer.Serialize($bad))} 'Boolean string rejected'
Reject {$generator.ValidateJson('{broken')} 'Malformed JSON rejected'
Reject {$generator.ValidateJson($json.Replace('1361','1e9999'))} 'Nonfinite value rejected'
$original=[Threading.Thread]::CurrentThread.CurrentCulture
try {[Threading.Thread]::CurrentThread.CurrentCulture='fr-FR'; Assert ($generator.Generate((New-Object PreProcess.Wpf.Models.TaskModel)) -eq $json) 'Culture independent serialization'} finally {[Threading.Thread]::CurrentThread.CurrentCulture=$original}
$path=Join-Path $PSScriptRoot 'request.json'
$null=$generator.Save((New-Object PreProcess.Wpf.Models.TaskModel),$path,$true)
Reject {$generator.Save($task,$path)} 'Overwrite rejected by default'
Assert ([IO.File]::ReadAllText($path).Trim() -eq $json) 'Rejected overwrite preserves file'
$alternate=Join-Path $PSScriptRoot 'changed-request.json'
$null=$generator.Save($task,$alternate,$true)
Assert ($serializer.DeserializeObject([IO.File]::ReadAllText($alternate))['TARGET_PHYSICS'][1]['r'] -eq 0.3) 'Specified path and explicit overwrite'
Reject {$generator.Save($null,$path,$true)} 'Invalid task cannot overwrite existing request'
Assert ([IO.File]::ReadAllText($path).Trim() -eq $json) 'Failed generation preserves destination'
Assert ([IO.File]::ReadAllBytes($path)[0] -eq 123) 'UTF8 without BOM'
$results.Add(('TOTAL '+$results.Count+' passed'))
$results | Set-Content (Join-Path $PSScriptRoot 'request-tests.log') -Encoding UTF8
$results | Select-Object -Last 15
