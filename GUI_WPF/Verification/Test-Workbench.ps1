param([string]$BuildFolder="Release")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
$root=Split-Path $PSScriptRoot -Parent
$exe=Join-Path $root ("PreProcess.Wpf/PreProcess.Wpf/bin/"+$BuildFolder+"/PreProcess.Wpf.exe")
[void][Reflection.Assembly]::LoadFrom($exe)
$app=New-Object PreProcess.Wpf.App
$app.InitializeComponent()
$window=New-Object PreProcess.Wpf.MainWindow
$window.ShowInTaskbar=$false
$window.ShowActivated=$false
$window.Left=-16000
$window.Top=-16000
$window.WindowStartupLocation='Manual'
$window.Show()
function Pump { $window.Dispatcher.Invoke([Action]{},[Windows.Threading.DispatcherPriority]::ApplicationIdle); $window.UpdateLayout() }
function Assert($condition,$message) { if(!$condition){throw $message}; $script:results.Add($message+' PASS') }
$results=New-Object 'System.Collections.Generic.List[string]'
Pump
Assert ($window.Width -eq 1400 -and $window.Height -eq 850) 'Default window 1400x850'
$vm=$window.DataContext
Assert ($vm.Navigation.Count -eq 3) 'Three navigation groups'
$expected=@('任务设置','目标参数','场景与运动','环境与观测','计算与输出','正向计算','智能预测','轨迹生成','相似度评估','红外场景构建','当前任务','历史任务','输出结果','日志与告警')
$actual=@($vm.Navigation | ForEach-Object { $_.Children } | ForEach-Object { $_.Title })
Assert (($actual -join '|') -eq ($expected -join '|')) 'Fourteen business navigation leaves'
$tree=$window.FindName('NavigationTree')
$content=$window.FindName('EditorContent')
foreach($group in $vm.Navigation) {
 $container=$tree.ItemContainerGenerator.ContainerFromItem($group)
 $container.IsExpanded=$true
 Pump
 foreach($leaf in $group.Children) {
  $item=$container.ItemContainerGenerator.ContainerFromItem($leaf)
  $item.IsSelected=$true
  Pump
  Assert ($content.Content.Title -eq $leaf.Title) ('Tree selection: '+$leaf.Title)
 }
}
foreach($pair in @(@('ResultTabs',4),@('LogTabs',3))) {
 $tabs=$window.FindName($pair[0])
 Assert ($tabs.Items.Count -eq $pair[1]) ($pair[0]+' count')
 for($i=0;$i -lt $tabs.Items.Count;$i++) {$tabs.SelectedIndex=$i;Pump;Assert ($tabs.SelectedItem.IsSelected) ($pair[0]+' switch '+$i)}
}
foreach($action in @('新建任务','打开任务','保存任务','运行','停止')) {
 $vm.PlaceholderCommand.Execute($action)
 Assert ($vm.Status.Contains('尚未接入')) ('Placeholder '+$action)
}
$body=$content.Parent.Parent.Parent.Parent
# Body grid: navigation / splitter / center / splitter / results.
Assert ($body -is [Windows.Controls.Grid] -and $body.ColumnDefinitions.Count -eq 5) 'Body Grid structure'
foreach($size in @(@(1000,650),@(1400,850),@(1800,1000))) {
 $window.Width=$size[0];$window.Height=$size[1];Pump
 Assert ($content.ActualWidth -ge 300 -and $content.ActualHeight -gt 100) ('Resize '+$size[0]+'x'+$size[1])
 foreach($child in $body.Children) {Assert ($child.ActualWidth -gt 0 -and $child.ActualHeight -gt 0) 'Main region visible'}
}
$window.WindowState='Maximized';Pump
Assert ($window.WindowState -eq 'Maximized' -and $content.ActualWidth -gt 0) 'Maximized layout'
$window.WindowState='Normal';Pump
$body.ColumnDefinitions[0].Width=New-Object Windows.GridLength(190)
$body.ColumnDefinitions[4].Width=New-Object Windows.GridLength(280)
$center=$body.Children[2]
$center.RowDefinitions[2].Height=New-Object Windows.GridLength(150)
Pump
Assert ($content.ActualWidth -gt 0 -and $content.ActualHeight -gt 0) 'Adjusted panel dimensions'
$splitters=@($body.Children | Where-Object {$_ -is [Windows.Controls.GridSplitter]})+@($center.Children | Where-Object {$_ -is [Windows.Controls.GridSplitter]})
Assert ($splitters.Count -eq 3) 'Three GridSplitters'
$window.Close()
$results | Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'ui-smoke-results.txt')
$results | Select-Object -Last 8
