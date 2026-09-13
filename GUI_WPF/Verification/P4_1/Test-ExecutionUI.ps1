$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
$repo=Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
$assembly=Join-Path $repo 'GUI_WPF/PreProcess.Wpf/PreProcess.Wpf/bin/P4_1Release/PreProcess.Wpf.exe'
[void][Reflection.Assembly]::LoadFrom($assembly)
$app=New-Object PreProcess.Wpf.App
$app.InitializeComponent()
$window=New-Object PreProcess.Wpf.MainWindow
$window.ShowInTaskbar=$false;$window.ShowActivated=$false
$window.WindowStartupLocation='Manual';$window.Left=-16000;$window.Top=-16000
$window.Show()
$vm=$window.DataContext
$execution=$vm.Execution
$results=New-Object 'System.Collections.Generic.List[string]'
function Pump {$window.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle);$window.UpdateLayout()}
function Assert($value,$name){if(!$value){throw $name};$results.Add('PASS '+$name)}
function Descendants($node){if($node -isnot [Windows.Media.Visual]){return};for($i=0;$i -lt [Windows.Media.VisualTreeHelper]::GetChildrenCount($node);$i++){$child=[Windows.Media.VisualTreeHelper]::GetChild($node,$i);$child;Descendants $child}}
function WaitFor($predicate){$clock=[Diagnostics.Stopwatch]::StartNew();while(!(& $predicate)){if($clock.Elapsed.TotalSeconds -gt 15){throw 'UI test timed out'};Pump;Start-Sleep -Milliseconds 20};Pump}
function StartRun($paths){$script:paths=$paths;$window.Dispatcher.Invoke([Action]{$script:run=$execution.StartAsync($script:paths)})}
$paths=New-Object PreProcess.Wpf.Services.Execution.BackendPaths
$paths.PackageRoot=Join-Path $repo 'coreprogram'
$paths.Entry=Join-Path $PSScriptRoot 'ui-fixture.txt'
$paths.Python=Join-Path $repo 'GUI_WPF/PreProcess.Wpf/ProcessTest/bin/P4_1Release/ProcessTest.exe'
$paths.RuntimeRoot=Join-Path $PSScriptRoot 'ui-runtime'
[IO.File]::WriteAllText($paths.Entry,'tree')
$timer=New-Object Windows.Threading.DispatcherTimer
$timer.Interval=[TimeSpan]::FromMilliseconds(20)
$script:beats=0
$timer.add_Tick({$script:beats++})
$timer.Start()
try {
 Pump
 $runButton=@(Descendants $window | Where-Object {$_ -is [Windows.Controls.Button] -and $_.Content -eq '运行'})[0]
 $stopButton=@(Descendants $window | Where-Object {$_ -is [Windows.Controls.Button] -and $_.Content -eq '停止'})[0]
 Assert ($runButton.Command -eq $execution.RunCommand -and $stopButton.Command -eq $execution.StopCommand) 'Toolbar command bindings'
 $vm.NavigateCommand.Execute($vm.Navigation[1].Children[0]);Pump
 Assert (@(Descendants $window.FindName('EditorContent') | Where-Object {$_ -is [PreProcess.Wpf.Views.ForwardRunView]}).Count -eq 1) 'Left forward node opens actual run page'
 $execution.PrepareOnly=$true
 StartRun $paths
 Assert ($execution.IsBusy -and !$execution.RunCommand.CanExecute($null) -and $execution.StopCommand.CanExecute($null)) 'Run disabled and stop enabled during execution'
 $vm.NavigateCommand.Execute($vm.Navigation[0].Children[1]);Pump
 Assert (!$window.FindName('EditorContent').IsEnabled) 'Task editing locked while snapshot runs'
 WaitFor {$execution.LogText -match 'CHILD=(\d+)'}
 $child=[int]([regex]::Match($execution.LogText,'CHILD=(\d+)').Groups[1].Value)
 $before=$script:beats
 $window.Width=1100;$window.Height=720
 for($i=0;$i -lt 15;$i++){Pump;Start-Sleep -Milliseconds 20}
 Assert ($script:beats -gt $before+3 -and $window.ActualWidth -eq 1100) 'Dispatcher heartbeat and resize remain responsive'
 Assert ($execution.LogText -match 'GUI_PROGRESS' -and $vm.Status -notmatch '就绪') 'Realtime log and status binding'
 $stopButton.Command.Execute($null)
 WaitFor {$script:run.IsCompleted -and !$execution.IsBusy}
 $script:run.GetAwaiter().GetResult()
 Assert ($execution.LastRun.State.ToString() -eq 'Cancelled') 'Toolbar stop cancels service'
 Assert (!(Get-Process -Id $child -ErrorAction SilentlyContinue)) 'GUI stop removes child process'
 Assert ($window.FindName('EditorContent').IsEnabled -and $execution.RunCommand.CanExecute($null) -and !$execution.StopCommand.CanExecute($null)) 'Commands and editing restored after cancellation'
 $paths.Python=(Get-Command python.exe).Source
 $paths.Entry=Join-Path $paths.PackageRoot '01-正向仿真/02-程序/forward_simulation_runner.py'
 StartRun $paths
 WaitFor {$script:run.IsCompleted -and !$execution.IsBusy}
 $script:run.GetAwaiter().GetResult()
 Assert ($execution.LastRun.State.ToString() -eq 'Completed' -and $execution.LastRun.PrepareOnly) 'GUI orchestration real prepare-only completes'
 Assert ($execution.LogText -match 'prepared' -and (Test-Path $execution.ResultLocation)) 'GUI exposes real progress and result location'
 Assert ($vm.Status -match '未执行求解') 'Prepared status distinguishes computation completion'
 $execution.LastRun | ConvertTo-Json | Set-Content (Join-Path $PSScriptRoot 'gui-prepare-record.json') -Encoding UTF8
 $paths.Python=Join-Path $repo 'GUI_WPF/PreProcess.Wpf/ProcessTest/bin/P4_1Release/ProcessTest.exe'
 $paths.Entry=Join-Path $PSScriptRoot 'ui-fixture.txt'
 [IO.File]::WriteAllText($paths.Entry,'missing-output')
 StartRun $paths
 WaitFor {$script:run.IsCompleted -and !$execution.IsBusy}
 Assert ($execution.LastRun.State.ToString() -eq 'Failed' -and $execution.WarningText.Length -gt 0) 'Failure appears in GUI warnings'
 $vm.NavigateCommand.Execute($vm.Navigation[1].Children[0]);Pump
 $window.Width=1400;$window.Height=850;Pump
 $bitmap=New-Object Windows.Media.Imaging.RenderTargetBitmap([int]$window.ActualWidth,[int]$window.ActualHeight,96,96,[Windows.Media.PixelFormats]::Pbgra32)
 $bitmap.Render($window)
 $encoder=New-Object Windows.Media.Imaging.PngBitmapEncoder
 $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
 $stream=[IO.File]::Create((Join-Path $PSScriptRoot 'execution-page.png'))
 try{$encoder.Save($stream)}finally{$stream.Dispose()}
 # Closing an active window must also clean the tree.
 [IO.File]::WriteAllText($paths.Entry,'tree')
 StartRun $paths
 WaitFor {$execution.LogText -match 'CHILD=(\d+)'}
 $closeChild=[int]([regex]::Match($execution.LogText,'CHILD=(\d+)').Groups[1].Value)
 $window.Close()
 WaitFor {$script:run.IsCompleted}
 Assert (!(Get-Process -Id $closeChild -ErrorAction SilentlyContinue)) 'Window close cleans process tree'
 $results | Set-Content (Join-Path $PSScriptRoot 'execution-ui-tests.log') -Encoding UTF8
 $results
}finally{$timer.Stop();$execution.Stop();if($window.IsVisible){$window.Close()}}
