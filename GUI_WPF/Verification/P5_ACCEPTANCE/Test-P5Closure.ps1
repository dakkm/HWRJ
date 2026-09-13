$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
$repo=Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
$exe=Join-Path $repo 'GUI_WPF/PreProcess.Wpf/PreProcess.Wpf/bin/P5_AcceptanceRelease/PreProcess.Wpf.exe'
[void][Reflection.Assembly]::LoadFrom($exe)
$app=New-Object PreProcess.Wpf.App
$app.InitializeComponent()
$window=New-Object PreProcess.Wpf.MainWindow
$window.ShowInTaskbar=$false
$window.ShowActivated=$false
$window.WindowStartupLocation='Manual'
$window.Left=-16000
$window.Top=-16000
$window.Show()
$vm=$window.DataContext
$execution=$vm.Execution
$results=New-Object 'System.Collections.Generic.List[string]'
function Pump {$window.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle);$window.UpdateLayout()}
function Assert($ok,$name){if(!$ok){throw $name};$results.Add('PASS '+$name)}
function Descendants($node){if($node -isnot [Windows.Media.Visual]){return};for($i=0;$i -lt [Windows.Media.VisualTreeHelper]::GetChildrenCount($node);$i++){$child=[Windows.Media.VisualTreeHelper]::GetChild($node,$i);$child;Descendants $child}}
try {
 $paths=New-Object PreProcess.Wpf.Services.Execution.BackendPaths
 $paths.PackageRoot=Join-Path $repo 'coreprogram'
 $paths.Python='C:\Users\1\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
 $paths.RuntimeRoot='C:\Users\1\Documents\Codex\p5a\closure'
 $vm.Editor.LoadTask([PreProcess.Wpf.Services.Execution.ReferenceTaskLoader]::Load($paths.PackageRoot))
 $execution.SelectedModule='02'
 Pump
 $clock=[Diagnostics.Stopwatch]::StartNew()
 $script:paths=$paths
 $window.Dispatcher.Invoke([Action]{$script:run=$execution.StartAsync($script:paths)})
 while(!$script:run.IsCompleted){if($clock.Elapsed.TotalSeconds -gt 60){$execution.Stop();throw 'Closure test timed out'};Pump;Start-Sleep -Milliseconds 20}
 $script:run.GetAwaiter().GetResult()
 Pump
 Assert ($execution.LastRun.State.ToString() -eq 'Completed' -and (Test-Path $execution.LastRun.ResultDirectory)) 'GUI input to backend output completed'
 Assert ($execution.ResultBrowser.RunStatus -eq '已完成' -and $execution.ResultBrowser.HasResult) 'Run status displayed'
 Assert ($execution.ResultBrowser.HasDataSets -and $execution.ResultBrowser.DataSets.Count -eq 1) '02 result reader connected to browser'
 Assert ($execution.ResultBrowser.SelectedDataSet.RowCount -eq 101 -and $execution.ResultBrowser.SelectedDataSet.Rows.Count -eq 101) 'Temperature prediction table displayed'
 Assert (@($execution.ResultBrowser.Summary|Where-Object {$_.Name -eq 'prediction.mode' -and $_.Value -eq 'temperature'}).Count -eq 1) 'Backend summary displayed'
 Assert (@(Descendants $window|Where-Object {$_ -is [PreProcess.Wpf.Views.ResultBrowserView]}).Count -eq 1) 'Right result browser rendered'
 $browserTabs=@(Descendants $window|Where-Object {$_ -is [Windows.Controls.TabControl] -and [Windows.Automation.AutomationProperties]::GetName($_) -eq '结果浏览器'})[0]
 $browserTabs.SelectedIndex=1
 Pump
 $table=@(Descendants $window|Where-Object {$_ -is [Windows.Controls.DataGrid] -and [Windows.Automation.AutomationProperties]::GetName($_) -eq '结果数据表'})[0]
 Assert ($table -and $table.Items.Count -eq 101) 'Visible data grid contains backend rows'
 $predictionBrowser=$execution.ResultBrowser
 $execution.SelectedModule='01'
 Assert (!$execution.ResultBrowser.HasDataSets -and $execution.ResultBrowser.RunStatus -eq '尚未运行') 'Module without current result prompts'
 $execution.SelectedModule='02'
 Assert ([Object]::ReferenceEquals($predictionBrowser,$execution.ResultBrowser)) 'Module switch restores result'
 Assert (!$execution.IsBusy -and $execution.RunCommand.CanExecute($null)) 'UI remains responsive after result load'
 $results.Add('TOTAL '+$results.Count+' passed')
 $results|Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'P5-closure-results.log')
 $results
} finally {$execution.Stop();if($window.IsVisible){$window.Close()}}


