$ErrorActionPreference='Stop'
$project=Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'PreProcess.Wpf/PreProcess.Wpf'
$utf8=New-Object Text.UTF8Encoding($true)
function WriteSource($name,$text){[IO.File]::WriteAllText((Join-Path $project $name),$text.Replace("`r`n","`n"),$utf8)}
function Field($label,$path,$extra='') {
 return '<StackPanel Margin="0,5"><TextBlock Text="'+$label+'" TextWrapping="Wrap"/><TextBox Margin="0,4,0,0" Padding="6" '+$extra+' Text="{Binding '+$path+', Mode=TwoWay, UpdateSourceTrigger=PropertyChanged, ValidatesOnExceptions=True}" AutomationProperties.Name="'+$label+'"/></StackPanel>'
}
function Vector($label,$path) {return '<StackPanel Margin="0,8"><TextBlock Text="'+$label+'" TextWrapping="Wrap"/><views:Vector3Editor DataContext="{Binding '+$path+'}"/></StackPanel>'}
function Section($title,$content) { return '<GroupBox Header="'+$title+'" Margin="0,10,0,0" Padding="10"><StackPanel>'+$content+'</StackPanel></GroupBox>' }
function Hint($text) {return '<TextBlock Text="'+$text+'" TextWrapping="Wrap" Foreground="#64748B" Margin="0,8"/>'}
function Page($name,$title,$content) {
 $s='<UserControl x:Class="PreProcess.Wpf.Views.'+$name+'" xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:views="clr-namespace:PreProcess.Wpf.Views"><ScrollViewer VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled"><StackPanel Margin="18"><TextBlock Text="'+$title+'" FontSize="24" FontWeight="SemiBold" TextWrapping="Wrap"/>'+$content+'</StackPanel></ScrollViewer></UserControl>'
 WriteSource ('Views/'+$name+'.xaml') $s
 WriteSource ('Views/'+$name+'.xaml.cs') ('using System.Windows.Controls; namespace PreProcess.Wpf.Views { public partial class '+$name+' : UserControl { public '+$name+'() { InitializeComponent(); } } }')
}
$vector='<UserControl x:Class="PreProcess.Wpf.Views.Vector3Editor" xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"><UniformGrid Columns="3">'
foreach($axis in 'X','Y','Z'){$vector+='<StackPanel Margin="0,4,6,0"><TextBlock Text="'+$axis+'"/><TextBox Padding="5" MinWidth="35" Text="{Binding '+$axis+', Mode=TwoWay, UpdateSourceTrigger=PropertyChanged, ValidatesOnExceptions=True}" AutomationProperties.Name="'+$axis+' 分量"/></StackPanel>'}
$vector+='</UniformGrid></UserControl>'
WriteSource 'Views/Vector3Editor.xaml' $vector
WriteSource 'Views/Vector3Editor.xaml.cs' 'using System.Windows.Controls; namespace PreProcess.Wpf.Views { public partial class Vector3Editor : UserControl { public Vector3Editor() { InitializeComponent(); } } }'
$physical=''
foreach($f in @(@('球体半径 (m)','Radius'),@('密度 (kg/m³)','Density'),@('比热 (J/(kg·K))','HeatCapacity'),@('初始温度 (K)','InitialTemperature'),@('内部热源功率 (W)','InternalPower'),@('红外发射率','Emissivity'),@('太阳吸收率','SolarAbsorption'),@('红外反射率','IrReflection'))){$physical+=Field $f[0] $f[1]}
$physical+='<StackPanel Margin="0,8"><TextBlock Text="球体质量 (kg，按半径和密度计算)" TextWrapping="Wrap"/><TextBlock Text="{Binding Mass, StringFormat={}{0:G6}}" Margin="0,5"/></StackPanel>'
# Reusable editor inherits a TargetPhysics instance as its DataContext.
Page 'TargetPhysicsEditor' '物性参数' $physical
$content=Hint '有效输入自动保留在内存。新建会重置当前任务；本阶段不读写任务文件。'
$content+='<WrapPanel Margin="0,8"><Button Content="新建任务" Command="{Binding Editor.NewCommand}" Padding="10,5" Margin="0,0,6,6"/><Button Content="打开任务" Command="{Binding Editor.OpenCommand}" Padding="10,5" Margin="0,0,6,6"/><Button Content="保存任务" Command="{Binding Editor.SaveCommand}" Padding="10,5" Margin="0,0,6,6"/></WrapPanel><TextBlock Text="{Binding Editor.Message}" TextWrapping="Wrap" Foreground="#35617F" Margin="0,6"/>'
$content+=Field '任务名称' 'Editor.Task.Settings.Metadata.Name'
$content+=Field '任务说明' 'Editor.Task.Settings.Metadata.Description' 'AcceptsReturn="True" TextWrapping="Wrap" MinHeight="72" VerticalScrollBarVisibility="Auto"'
$content+=Field '仿真时间 (s)' 'Editor.Task.Settings.Duration'
$content+=Field '目标数量' 'Editor.Task.Settings.TargetCount'
$content+=Hint '减少目标数量会移除末尾目标及其独立设置。编辑器上限为10000个目标。红框表示输入无效，悬停可查看原因。'
Page 'TaskSettingsView' '任务设置' $content
$selector='<TextBlock Text="目标编号"/><ComboBox ItemsSource="{Binding Editor.Task.IndividualTargets}" SelectedItem="{Binding Editor.SelectedTarget}" DisplayMemberPath="DisplayName" Margin="0,6" AutomationProperties.Name="目标编号"/>'
$content=Hint '统一设置应用到所有未启用独立物性的目标。质量由半径和密度计算。'
$content+=Section '统一设置' '<views:TargetPhysicsEditor DataContext="{Binding Editor.Task.Targets.Uniform}"/>'
$content+='<Expander Header="高级单目标设置" Margin="0,14"><StackPanel Margin="8">'+$selector+'<CheckBox Content="启用独立物性覆盖" IsChecked="{Binding Editor.SelectedTarget.UseOverride}" Margin="0,8"/>'+(Hint '首次启用时复制统一参数，之后独立编辑。关闭覆盖恢复继承，重新启用保留上次独立值。')+'<views:TargetPhysicsEditor DataContext="{Binding Editor.SelectedTarget.EffectivePhysics}" IsEnabled="{Binding Editor.SelectedTarget.UseOverride}"/></StackPanel></Expander>'
Page 'TargetSettingsView' '目标参数' $content
$group=''
foreach($f in @(@('群中心位置 (m)','Position'),@('群方向','Direction'),@('群参考上方向','Up'),@('群速度 (m/s)','Velocity'))){$group+=Vector $f[0] ('Editor.Task.Scene.'+$f[1])}
$group+='<Expander Header="高级：群体角运动"><StackPanel>'+(Vector '角速度 (rad/s)' 'Editor.Task.Scene.AngularVelocity')+(Vector '角加速度 (rad/s²)' 'Editor.Task.Scene.AngularAcceleration')+'</StackPanel></Expander>'
$content=Section '目标群初始状态与运动' $group
$single=$selector
foreach($f in @(@('相对位置 (m)','Position'),@('相对速度 (m/s)','Velocity'),@('相对加速度 (m/s²)','Acceleration'))){$single+=Vector $f[0] ('Editor.SelectedTarget.Motion.'+$f[1])}
$single+=Field '释放时间 (s)' 'Editor.SelectedTarget.Motion.ReleaseTime' 'IsEnabled="{Binding Editor.SelectedTarget.Motion.CanChangeActivation}"'
$single+='<CheckBox Content="启用目标" IsChecked="{Binding Editor.SelectedTarget.Motion.Active}" IsEnabled="{Binding Editor.SelectedTarget.Motion.CanChangeActivation}" Margin="0,8"/>'+(Hint '1号目标固定启用且释放时间为零。相对速度和加速度用于释放后的分离运动。')
$content+=Section '单目标状态' $single
Page 'SceneMotionView' '场景与运动' $content
$env=(Field '太阳辐照度 (W/m²)' 'Editor.Task.Environment.SolarFlux')+(Vector '太阳方向' 'Editor.Task.Environment.SunDirection')+(Field '环境辐射温度 (K)' 'Editor.Task.Environment.RadiationTemperature')
$content=Section '辐射环境' $env
$observation=''
foreach($f in @(@('观测位置 (m)','ObserverPosition'),@('观测方向','ObserverDirection'),@('参考上方向','ObserverUp'))){$observation+=Vector $f[0] ('Editor.Task.Environment.'+$f[1])}
$observation+='<CheckBox Content="孔径跟踪目标群中心" IsChecked="{Binding Editor.Task.Environment.ApertureTracking}" Margin="0,8"/><CheckBox Content="探测器跟踪目标群中心" IsChecked="{Binding Editor.Task.Environment.DetectorTracking}" Margin="0,8"/>'
foreach($f in @(@('孔径尺寸 (m)','ApertureSize'),@('焦距 (m)','FocalLength'),@('像面尺寸 (m)','PlaneSize'))){$observation+=Field $f[0] ('Editor.Task.Environment.'+$f[1])}
$content+=Section '观测与光学' $observation
$advanced=''
foreach($f in @(@('孔径速度 (m/s)','ObserverVelocity'),@('孔径角速度 (rad/s)','ObserverAngularVelocity'),@('孔径角加速度 (rad/s²)','ObserverAngularAcceleration'),@('探测器方向','DetectorDirection'),@('探测器参考上方向','DetectorUp'),@('探测器速度 (m/s)','DetectorVelocity'),@('探测器角速度 (rad/s)','DetectorAngularVelocity'),@('探测器角加速度 (rad/s²)','DetectorAngularAcceleration'))){$advanced+=Vector $f[0] ('Editor.Task.Environment.'+$f[1])}
$content+='<Expander Header="高级：观测系统姿态与运动" Margin="0,14"><StackPanel Margin="8">'+$advanced+'</StackPanel></Expander>'
Page 'EnvironmentObservationView' '环境与观测' $content
$content=(Hint '以下为五个模块的说明与参数预留区域。本阶段不执行计算，也不读取结果。')+'<ItemsControl ItemsSource="{Binding Editor.Task.Calculation.Modules}"><ItemsControl.ItemTemplate><DataTemplate><GroupBox Header="{Binding Name}" Margin="0,10" Padding="10"><StackPanel><TextBlock Text="{Binding Description}" TextWrapping="Wrap"/><TextBlock Text="输入说明" FontWeight="SemiBold" Margin="0,12,0,4"/><TextBlock Text="{Binding Input}" TextWrapping="Wrap"/><TextBlock Text="输出说明" FontWeight="SemiBold" Margin="0,12,0,4"/><TextBlock Text="{Binding Output}" TextWrapping="Wrap"/><Border Background="#F5F8FC" Padding="12" Margin="0,12,0,0"><TextBlock Text="参数区域 · 后续完善" TextWrapping="Wrap"/></Border></StackPanel></GroupBox></DataTemplate></ItemsControl.ItemTemplate></ItemsControl>'
Page 'CalculationOutputView' '计算与输出' $content
