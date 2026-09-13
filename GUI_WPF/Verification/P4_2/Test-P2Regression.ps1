param([string]$BuildFolder="P4_2Release")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$exe=Join-Path $root ("PreProcess.Wpf/PreProcess.Wpf/bin/"+$BuildFolder+"/PreProcess.Wpf.exe")
[void][Reflection.Assembly]::LoadFrom($exe)
$app=New-Object PreProcess.Wpf.App
$app.InitializeComponent()
$window=New-Object PreProcess.Wpf.MainWindow
$window.ShowInTaskbar=$false
$window.ShowActivated=$false
$window.WindowStartupLocation='Manual'
$window.Left=-16000;$window.Top=-16000
$window.Show()
$results=New-Object 'System.Collections.Generic.List[string]'
function Pump { $window.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle);$window.UpdateLayout() }
function Assert($test,$name){if(!$test){throw $name};$results.Add($name+' PASS')}
function Descendants($node) {
 if($node -isnot [Windows.Media.Visual]) { return }
 for($i=0;$i -lt [Windows.Media.VisualTreeHelper]::GetChildrenCount($node);$i++){
  $child=[Windows.Media.VisualTreeHelper]::GetChild($node,$i)
  $child
  Descendants $child
 }
}
$vm=$window.DataContext
$content=$window.FindName('EditorContent')
function SelectPage($index){$vm.NavigateCommand.Execute($vm.Navigation[0].Children[$index]);Pump}
function FindInput($path){return @(Descendants $content | Where-Object {$_ -is [Windows.Controls.TextBox] -and $_.GetBindingExpression([Windows.Controls.TextBox]::TextProperty).ParentBinding.Path.Path -eq $path})[0]}
function Input($path,$text){$box=FindInput $path;if(!$box){throw ('Missing binding '+$path)};$box.SetCurrentValue([Windows.Controls.TextBox]::TextProperty,$text);Pump;return $box}
try {
 Pump
 foreach($i in 0..4){
  SelectPage $i
  $view=@(Descendants $content | Where-Object {$_.GetType().FullName -like 'PreProcess.Wpf.Views.*View'})
  Assert ($view.Count -eq 1) ('Concrete view '+$i)
  Assert ($view[0].DataContext.Editor -eq $vm.Editor) ('Shared editor '+$i)
 }
 SelectPage 0
 $null=Input 'Editor.Task.Settings.Metadata.Name' '切页保留测试'
 $null=Input 'Editor.Task.Settings.Metadata.Description' "第一行`n第二行"
 $null=Input 'Editor.Task.Settings.Duration' '2500'
 $null=Input 'Editor.Task.Settings.TargetCount' '3'
 Assert ($vm.Editor.Task.Settings.Metadata.Name -eq '切页保留测试') 'Name UI -> Model'
 Assert ($vm.Editor.Task.Settings.Duration -eq 2500) 'Duration UI -> Model'
 Assert ($vm.Editor.Task.IndividualTargets.Count -eq 3) 'Count synchronizes target collection'
 SelectPage 1
 $null=Input 'Radius' '0.4'
 Assert ($vm.Editor.Task.Targets.Uniform.Radius -eq 0.4) 'Uniform radius binding'
 Assert ([Math]::Abs($vm.Editor.Task.Targets.Uniform.Mass-(4.0/3*[Math]::PI*([Math]::Pow(0.4,3)-[Math]::Pow(0.395,3))*2700)) -lt 0.0001) 'Derived mass'
 $advanced=@(Descendants $content | Where-Object {$_ -is [Windows.Controls.Expander]})[0]
 $advanced.IsExpanded=$true;Pump
 $combo=@(Descendants $content | Where-Object {$_ -is [Windows.Controls.ComboBox] -and $_.DisplayMemberPath -eq "DisplayName"})[0]
 $combo.SelectedIndex=1;Pump
 Assert ($vm.Editor.SelectedTarget.Id -eq 2) 'Target selector UI -> VM'
 $override=@(Descendants $content | Where-Object {$_ -is [Windows.Controls.CheckBox]})[0]
 $override.SetCurrentValue([Windows.Controls.Primitives.ToggleButton]::IsCheckedProperty,$true);Pump
 $radiusBoxes=@(Descendants $content | Where-Object {$_ -is [Windows.Controls.TextBox] -and $_.GetBindingExpression([Windows.Controls.TextBox]::TextProperty).ParentBinding.Path.Path -eq 'Radius'})
 $radiusBoxes[1].SetCurrentValue([Windows.Controls.TextBox]::TextProperty,'0.7');Pump
 Assert ($vm.Editor.SelectedTarget.EffectivePhysics.Radius -eq 0.7 -and $vm.Editor.Task.Targets.Uniform.Radius -eq 0.4) 'Override isolated from uniform'
 $vm.Editor.Task.Targets.Uniform.Radius=0.5;Pump
 Assert ($vm.Editor.Task.IndividualTargets[0].EffectivePhysics.Radius -eq 0.5 -and $vm.Editor.SelectedTarget.EffectivePhysics.Radius -eq 0.7) 'Uniform inheritance and override'
 $vm.Editor.SelectedTarget.UseOverride=$false;Pump
 Assert ($vm.Editor.SelectedTarget.EffectivePhysics.Radius -eq 0.5) 'Disable override restores inheritance'
 $vm.Editor.SelectedTarget.UseOverride=$true;Pump
 Assert ($vm.Editor.SelectedTarget.EffectivePhysics.Radius -eq 0.7) 'Reenable retains independent values'
 SelectPage 2
 $null=Input 'Editor.SelectedTarget.Motion.ReleaseTime' '12'
 $vm.Editor.SelectedTarget.Motion.Position.Y=45
 Assert ($vm.Editor.Task.IndividualTargets[1].Motion.ReleaseTime -eq 12) 'Motion binding and shared target'
 $vm.Editor.SelectedTarget=$vm.Editor.Task.IndividualTargets[0];Pump
 Assert (!(FindInput 'Editor.SelectedTarget.Motion.ReleaseTime').IsEnabled) 'Primary release locked'
 Assert ($vm.Editor.SelectedTarget.Motion.Active) 'Primary remains active'
 SelectPage 3
 $null=Input 'Editor.Task.Environment.SolarFlux' '1400'
 Assert ($vm.Editor.Task.Environment.SolarFlux -eq 1400) 'Environment binding'
 $tracking=@(Descendants $content | Where-Object {$_ -is [Windows.Controls.CheckBox]})[0]
 $tracking.SetCurrentValue([Windows.Controls.Primitives.ToggleButton]::IsCheckedProperty,$false);Pump
 Assert (!$vm.Editor.Task.Environment.ApertureTracking) 'Tracking checkbox binding'
 SelectPage 4
 Assert ($vm.Editor.Task.Calculation.Modules.Count -eq 5) 'Five descriptive module regions'
 foreach($i in 0..4){SelectPage $i}
 SelectPage 0
 Assert ((FindInput 'Editor.Task.Settings.Metadata.Name').Text -eq '切页保留测试') 'Name survives all page switches'
 Assert ($vm.Editor.Task.IndividualTargets[1].EffectivePhysics.Radius -eq 0.7 -and $vm.Editor.Task.IndividualTargets[1].Motion.Position.Y -eq 45) 'Override and motion survive page switches'
 $invalid=Input 'Editor.Task.Settings.TargetCount' '0'
 Assert ([Windows.Controls.Validation]::GetHasError($invalid) -and $vm.Editor.Task.Settings.TargetCount -eq 3) 'Invalid count rejected visibly'
 $null=Input 'Editor.Task.Settings.TargetCount' '4'
 Assert ($vm.Editor.Task.IndividualTargets.Count -eq 4) 'Count growth'
 $vm.Editor.SelectedTarget=$vm.Editor.Task.IndividualTargets[3]
 $null=Input 'Editor.Task.Settings.TargetCount' '2'
 Assert ($vm.Editor.SelectedTarget.Id -eq 1 -and $vm.Editor.Task.IndividualTargets.Count -eq 2) 'Shrink repairs selected target'
 $old=$vm.Editor.Task
 $vm.Editor.OpenCommand.Execute($null)
 $vm.Editor.SaveCommand.Execute($null)
 Assert ([object]::ReferenceEquals($old,$vm.Editor.Task)) 'Open and save do not replace memory task'
 $vm.Editor.NewCommand.Execute($null);Pump
 Assert (![object]::ReferenceEquals($old,$vm.Editor.Task) -and $vm.Editor.Task.IndividualTargets.Count -eq 16) 'New resets shared task'
 Assert ((FindInput 'Editor.Task.Settings.Metadata.Name').Text -eq '未命名任务') 'New refreshes binding'
 foreach($i in 0..4){
  SelectPage $i;$window.Width=1000;$window.Height=650;Pump
  Assert ($content.ActualWidth -gt 0 -and $content.ActualHeight -gt 0) ('Minimum layout '+$i)
  $scroll=@(Descendants $content | Where-Object {$_ -is [Windows.Controls.ScrollViewer]})[0]
  Assert ($scroll.ViewportWidth -gt 0) ('Scrollable page '+$i)
 }
 $window.WindowState='Maximized';Pump
 Assert ($content.ActualWidth -gt 0) 'Maximized editor'
 $window.WindowState='Normal';$window.Width=1400;$window.Height=850;SelectPage 1;Pump
 SelectPage 1
 $shellInputs=@(Descendants $content | Where-Object {$_ -is [Windows.Controls.TextBox] -and $_.GetBindingExpression([Windows.Controls.TextBox]::TextProperty).ParentBinding.Path.Path -eq 'Geometry.ShellThickness'})
 Assert ($shellInputs.Count -eq 0) 'Fixed shell has no editable thickness'
 Assert (@(Descendants $content | Where-Object {$_ -is [Windows.Controls.TextBlock] -and $_.Text -eq '球壳（固定5mm）'}).Count -gt 0) 'Fixed shell label'
 SelectPage 4
 $view=@(Descendants $content | Where-Object {$_ -is [PreProcess.Wpf.Views.CalculationOutputView]})[0]
 $button=@(Descendants $view | Where-Object {$_ -is [Windows.Controls.Button] -and $_.Content -eq '生成 request.json…'})[0]
 Assert ($null -ne $button) 'Request generation button exists'
 $threshold=Input 'Editor.Task.Settings.SimilarityIndex' 'abc'
 $button.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
 Assert ($view.FindName('RequestStatus').Text -like '生成失败*') 'Invalid visible input blocks generation before dialog'
 $null=Input 'Editor.Task.Settings.SimilarityIndex' '91'
 Assert ($vm.Editor.Task.Settings.SimilarityIndex -eq 91) 'Scene acceptance threshold retained'
 SelectPage 1
 $bitmap=New-Object Windows.Media.Imaging.RenderTargetBitmap([int]$window.ActualWidth,[int]$window.ActualHeight,96,96,[Windows.Media.PixelFormats]::Pbgra32)
 $bitmap.Render($window)
 $encoder=New-Object Windows.Media.Imaging.PngBitmapEncoder
 $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
 $stream=[IO.File]::Create((Join-Path $PSScriptRoot 'target-page.png'))
 try {$encoder.Save($stream)} finally {$stream.Dispose()}
 $results | Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'ui-results.txt')
 $results | Select-Object -Last 10
} finally { $window.Close() }



