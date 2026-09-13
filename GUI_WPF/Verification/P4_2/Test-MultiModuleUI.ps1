$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
$repo=Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
[void][Reflection.Assembly]::LoadFrom((Join-Path $repo 'GUI_WPF/PreProcess.Wpf/PreProcess.Wpf/bin/P4_2Release/PreProcess.Wpf.exe'))
$app=New-Object PreProcess.Wpf.App; $app.InitializeComponent()
$window=New-Object PreProcess.Wpf.MainWindow
$window.ShowInTaskbar=$false; $window.ShowActivated=$false; $window.WindowStartupLocation='Manual'; $window.Left=-16000; $window.Top=-16000
$window.Show(); $vm=$window.DataContext; $execution=$vm.Execution
$results=New-Object 'System.Collections.Generic.List[string]'
function Pump {$window.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle);$window.UpdateLayout()}
function Assert($ok,$name){if(!$ok){throw $name};$results.Add('PASS '+$name)}
function Descendants($node){if($node -isnot [Windows.Media.Visual]){return};for($i=0;$i -lt [Windows.Media.VisualTreeHelper]::GetChildrenCount($node);$i++){$child=[Windows.Media.VisualTreeHelper]::GetChild($node,$i);$child;Descendants $child}}
function WaitDone {$watch=[Diagnostics.Stopwatch]::StartNew();while(!$script:run.IsCompleted){if($watch.Elapsed.TotalSeconds -gt 30){$execution.Stop();throw 'Test timed out'};Pump;Start-Sleep -Milliseconds 20};Pump;$script:run.GetAwaiter().GetResult()}
function StartRun {$window.Dispatcher.Invoke([Action]{$script:run=$execution.StartAsync($script:paths)})}
$paths=New-Object PreProcess.Wpf.Services.Execution.BackendPaths
$paths.PackageRoot=Join-Path $repo 'coreprogram'; $paths.Python=[PreProcess.Wpf.Services.Execution.BackendPathResolver]::FindExecutable('python.exe',$repo)
$paths.RuntimeRoot=Join-Path $PSScriptRoot 'integration/runtime'
try {
 $ids=@('01','02','轨迹','03','04')
 for($i=0;$i -lt 5;$i++){$vm.NavigateCommand.Execute($vm.Navigation[1].Children[$i]);Pump;Assert ($execution.SelectedModule -eq $ids[$i] -and $vm.CurrentContent -eq $execution) ('Module navigation '+$ids[$i])}
 $vm.NavigateCommand.Execute($vm.Navigation[0].Children[4]);Pump
 Assert (@(Descendants $window | Where-Object {$_ -is [PreProcess.Wpf.Views.ModuleExecutionPanel] -and $_.DataContext -eq $execution}).Count -eq 1) 'Calculation page shares execution instance'
 $vm.Editor.LoadTask([PreProcess.Wpf.Services.Execution.ReferenceTaskLoader]::Load($paths.PackageRoot));Pump
 $vm.NavigateCommand.Execute($vm.Navigation[1].Children[1]);Pump;StartRun
 Assert ($execution.IsBusy -and !$execution.RunCommand.CanExecute($null) -and $execution.StopCommand.CanExecute($null)) '02 busy command state'
 $execution.SelectedModule='03';Assert ($execution.SelectedModule -eq '02') 'Module cannot change during active run'
 $vm.NavigateCommand.Execute($vm.Navigation[0].Children[4]);Pump
 Assert ($window.FindName('EditorContent').IsEnabled -and @((Descendants $window) | Where-Object {$_ -is [Windows.Controls.Button] -and $_.Content -eq '停止' -and $_.IsEnabled}).Count -ge 2) 'Calculation page stop stays available while running'
 WaitDone
 Assert ($execution.LastRun.Module -eq '02' -and $execution.LastRun.State -eq 'Completed') 'GUI02 real execution completed'
 Assert ($execution.LogText -match 'temperature_completed' -and $execution.ResultLocation -eq $execution.LastRun.ResultDirectory) 'GUI02 log progress and directory binding'
 Assert (!$execution.IsBusy -and $execution.RunCommand.CanExecute($null)) '02 controls restored'
 $prediction=$execution.ResultLocation
 $vm.NavigateCommand.Execute($vm.Navigation[1].Children[3]);Pump
 $execution.ReferenceDirectory=Join-Path $PSScriptRoot 'integration/SYNTHETIC_CONTRACT_FIXTURE/reference'
 $execution.CandidateDirectory=$execution.ReferenceDirectory
 StartRun;WaitDone
 Assert ($execution.LastRun.Module -eq '03' -and $execution.LastRun.State -eq 'Completed') 'GUI03 real execution completed with contract fixture'
 Assert ($execution.ResultLocation -match '03-相似度评估' -and $execution.Status -match '完成') 'GUI03 completion status and directory'
 $execution.CandidateDirectory=$prediction;StartRun;WaitDone
 Assert ($execution.LastRun.State -eq 'Failed' -and $execution.WarningText -match '02') 'GUI03 incompatible result warning'
 $vm.NavigateCommand.Execute($vm.Navigation[1].Children[4]);Pump
 $vm.Editor.LoadTask((New-Object PreProcess.Wpf.Models.TaskModel));StartRun;WaitDone
 Assert ($execution.LastRun.Module -eq '04' -and $execution.LastRun.ExitCode -eq 2 -and $execution.WarningText.Length -gt 0) 'GUI04 real entry failure and stderr visible'
 $box=@(Descendants $window | Where-Object {$_ -is [Windows.Controls.TextBox] -and $_.GetBindingExpression([Windows.Controls.TextBox]::TextProperty).ParentBinding.Path.Path -eq 'RequiredCandidates'})[0]
 $box.Text='0';$box.GetBindingExpression([Windows.Controls.TextBox]::TextProperty).UpdateSource();StartRun;Pump
 Assert (!$execution.IsBusy -and $execution.Status -match '标红') 'Invalid candidate count blocks launch'
 $box.Text='1';$box.GetBindingExpression([Windows.Controls.TextBox]::TextProperty).UpdateSource();Pump
 $vm.Editor.LoadTask([PreProcess.Wpf.Services.Execution.ReferenceTaskLoader]::Load($paths.PackageRoot));StartRun
 $window.Close()
 WaitDone
 Assert ($execution.LastRun.State -eq 'Cancelled' -and !$window.IsVisible) 'Closing04 while preparing waits for cancellation'
 $results | Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'multi-module-ui-results.txt');$results
}finally{$execution.Stop();if($window.IsVisible){$window.Close()}}

