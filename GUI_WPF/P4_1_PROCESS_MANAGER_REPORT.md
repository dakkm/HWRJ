# P4.1 统一程序调用器与正向计算接入报告

日期：2026-09-12。完成范围：通用进程基础设施 + 01正向计算调用。所有本轮写入均位于 GUI_WPF/**。coreprogram、GUI_MFC_Legacy、.docs 均保持本轮开始时的内容。

## 1. 完成结果与使用入口

链路已接通：TaskModel → RequestGenerator → 本次request.json → ForwardSimulationService → ProcessManager → 01正式统一入口 → 日志/进度/运行定位信息。

工具栏及计算菜单的“运行”“停止”已连接实际命令。左侧“功能模块 → 正向计算”打开运行页面，使用现有共享任务；可勾选“仅准备输入（不执行求解）”。右侧曲线、点图像、数据表、摘要保持占位。

本机原 bin/Release/PreProcess.Wpf.exe 正被既有GUI实例占用。未关闭用户窗口。使用相同Release配置、独立OutputPath编译成功：

`GUI_WPF/PreProcess.Wpf/PreProcess.Wpf/bin/P4_1Release/PreProcess.Wpf.exe`

这是本轮可运行的新程序；仍在运行的旧Release实例不会自动获得这些功能。

## 2. 修改/新增文件清单

生产路径相对 GUI_WPF/PreProcess.Wpf/PreProcess.Wpf/。共调整6个既有文件、新增12个生产文件。

|操作|文件|用途|
|---|---|---|
|新增|Services/Process/ProcessRunRequest.cs|通用启动参数及运行状态枚举|
|新增|Services/Process/ProcessRunResult.cs|退出码、状态、时间、进程ID与运行定位|
|新增|Services/Process/ProcessProgressEvent.cs|结构化进度事件及原始字段|
|新增|Services/Process/ProcessLogEvent.cs|stdout/stderr日志事件|
|新增|Services/Process/GuiProgressParser.cs|独立GUI_PROGRESS解析|
|新增|Services/Process/WindowsJobProcess.cs|原生Windows挂起启动、管道和Job生命周期|
|新增|Services/Process/ProcessManager.cs|异步进程、读取、停止、超时、并发防护|
|新增|Services/Execution/BackendPathResolver.cs|轻量路径发现与配置解析|
|新增|Services/Execution/ForwardSimulationService.cs|01业务编排和ForwardRunRecord|
|新增|ViewModels/ForwardRunViewModel.cs|UI线程回写、运行/停止命令、限量日志显示|
|新增|Views/ForwardRunView.xaml、.xaml.cs|正向运行页面|
|调整|MainWindow.xaml|工具栏/菜单、日志、告警、进度绑定|
|调整|MainWindow.xaml.cs|运行时编辑锁定、输入错误检查、关窗停止|
|调整|ViewModels/MainWindowViewModel.cs|共享执行VM、左侧正向入口、状态栏|
|调整|App.config|BackendRoot/PythonExecutable/RuntimeRoot可选配置|
|调整|PreProcess.Wpf.csproj|注册新文件、WPF页面及System.Configuration引用|
|调整|Services/README.md|运行职责与路径约定|

测试新增：`GUI_WPF/PreProcess.Wpf/ProcessTest/Program.cs`、`ProcessTest.csproj`。它是独立测试工程，不修改既有MapperTest或解决方案。

验证脚本及证据位于 `GUI_WPF/Verification/P4_1/`：

- Test-P3Regression.ps1、Test-P2Regression.ps1、Test-ExecutionUI.ps1。
- build-release.log（原输出目录锁定记录）、build-isolated-release.log、build-tests.log。
- process-tests.log、mapper-tests.log、request-tests.log、ui-results.txt、execution-ui-tests.log。
- runtime/prepare-record.json、gui-prepare-record.json；对应runtime/ui-runtime下的请求、后端运行目录及日志。
- target-page.png、execution-page.png（后者为缺失输出目录的受控失败显示）。
- before/源文件快照、before_hashes.csv、after_hashes.csv、protected-check.json。
- source-changes.csv、p4-production-diff.patch（本轮生产差异）；git-diff-stat.txt、git-diff.patch（含原有工作区更改）。
- 本报告：GUI_WPF/P4_1_PROCESS_MANAGER_REPORT.md。

没有改动P2 TaskModel、目标物性模型、P3 RequestGenerator/Validator/schema、既有Mapper或其测试源码。

## 3. 实际读取的01入口与依据

已重新读取用户指定的七份.docs文件、P3_MODULE_IO_AUDIT_REPORT.md、P3_2_MAPPER_REPORT.md、P3_REQUEST_GENERATOR_REPORT.md及当前WPF窗口/VM/服务/测试。

后端只读依据：

1. `coreprogram/config.json`：standard_entries.01_forward、资源路径及runtime_root描述。
2. `coreprogram/00-运行环境与部署说明.txt`：Windows求解器、Python依赖、正式入口边界。
3. `coreprogram/01-正向仿真/02-程序/forward_simulation_runner.py`：parse_args、main、run_forward_simulation、generate_clean_input、emit_progress、validate_request及退出路径。
4. 同目录 `generate_clean_formal_case.py`：输入生成器CLI与文件输出方式，确认prepare-only仍会运行此Python子进程。
5. P3已核对的forward_request_schema.json、参数接口表、固定球壳和入口校验；本轮没有改变输入合同。

实际源码优先于旧文档。后端说明没有绑定特定Python安装路径或附带专用解释器；本机发现系统PATH中的Python 3.13.5、pandas 2.2.3。GUI预检要求Python≥3.10及pandas导入成功，适配当前生成器使用的Path.write_text(newline=...)接口。未安装任何Python环境或依赖。

## 4. 真实启动命令与工作目录

直接启动解释器，不经过cmd、不调用Fortran EXE作为业务入口。以下是实际启动参数结构，路径由解析器提供：

```text
<PythonExecutable> -B -u <BackendRoot>/01-正向仿真/02-程序/forward_simulation_runner.py
  --params-json <RuntimeRoot>/requests/<submission-guid>/request.json
  --run-root <RuntimeRoot>/runs
  --timeout-seconds 1800
  [--prepare-only]
```

解释器预检：`-B -u -c "import sys,pandas; assert sys.version_info >= (3,10), 'Python 3.10+ required'; print('Python environment ready: '+sys.version.split()[0])"`，最多20秒。

环境：PYTHONUTF8=1、PYTHONIOENCODING=utf-8、PYTHONDONTWRITEBYTECODE=1。`-B`与禁写字节码环境变量避免在冻结包生成__pycache__；环境由子进程继承至输入生成器。`-u`减少入口输出缓冲。

WorkingDirectory选择BackendRoot。实际入口的程序/资源路径主要依据__file__和package_path解析，绝对params-json/run-root不依赖调用者当前目录。入口创建运行目录后，输入生成器和求解器子进程的cwd为该后端run目录，由入口设置。

正式参数均来自parse_args：--params-json必填；--run-root可选；--timeout-seconds整数，默认1800；--prepare-only布尔开关。未添加后端不支持的参数。

成功prepared/success退出码0；forward_failed退出码3；main捕获的异常（包括请求拒绝、超时异常）退出码2；argparse参数错误也可退出2。导入错误等在main外发生的异常通常退出1，GUI按非零码统一处理。

最近一次服务联调的完整Executable/Arguments/WorkingDirectory，保存在runtime/prepare-record.json所指RequestPath同目录的stdout.log中。这些日志含本机绝对路径作为证据；生产代码没有硬编码这些机器路径。

## 5. ProcessManager职责与实现

ProcessManager只处理可执行程序、参数列表、工作目录、环境、超时、退出码、日志、结构化进度及通用run_id/run_dir。不包含CASE、TARGET_PHYSICS或结果CSV文件名。

RunAsync以原子标志阻止重复运行。启动、阻塞式OS等待和两条管道读取均在后台任务中完成；stdout与stderr独立排空，避免一条管道堵塞另一条。每行带时间和错误流标记触发事件。进程返回后还等待管道EOF，不丢弃退出前尾部日志。

原始输出与GUI_PROGRESS均进入日志；成功解析的结构化行另触发Progress事件。普通文本不能改变运行生命周期。事件订阅者异常通过Trace记录，不使通用进程执行因UI监听器异常直接崩溃。

ProcessRunResult记录开始/结束时间、进程ID、退出码、终态、超时标记和进度携带的运行定位。stderr存在本身不会强制失败；以退出码、取消/超时及上层合同确认判定结果。

## 6. ForwardSimulationService职责

服务按顺序：检查任务并调用现有RequestGenerator → 解析路径 → 保存本次提交request.json及日志 → Python依赖预检 → 构造正式01参数 → 调用ProcessManager → 核对运行完成信封 → 保存run-location.json。

01字段映射仍只属于RequestGenerator。进程管理仍只属于ProcessManager。服务只读取后端result.json中的status、return_code、run_id、output_dir、request_json等执行定位字段；没有解析温度/红外/轨迹CSV或图像。

退出码0后仍检查：后端run目录存在；信封模式与本次prepare/full一致；run_id一致；输出目录和后端request记录属于该run且存在。缺失结果目录不会被标记为Completed。

记录ForwardRunRecord：run_id、GUI提交request路径、后端使用request路径、开始/结束时间、状态、退出码、prepare模式、run/output目录、业务消息及技术诊断。失败或取消已得到run定位时仍保留可用目录信息。

## 7. GUI_PROGRESS实际字段与显示

入口输出：`GUI_PROGRESS {JSON}`，基础字段module="01"、timestamp（Unix秒）、state。

|入口状态|实际附属字段|GUI解释|
|---|---|---|
|input_ready|run_id、run_dir|输入已准备，记住后端运行目录|
|running_forward|run_id|正向计算运行中|
|prepared|run_id|输入准备完成，未求解|
|success|run_id|待服务核对result.json后确认完成|
|forward_failed|run_id|后端计算失败|

没有正式百分比、当前迭代数或总迭代数。GUI使用阶段文字和IsIndeterminate进度条，不构造假百分比。未知state保留日志，解析器保留额外字段；畸形JSON、非对象或普通日志不崩溃。

重要实际边界：入口使用subprocess.run(capture_output=True)运行求解器，因此求解器内部stdout/stderr不会被入口逐行实时转发。GUI实时展示的是Python统一入口实际输出，不能声称已经实时显示求解器每一步日志。求解器自身stdout.txt/stderr.txt由入口在结束后保存，本阶段不额外读取这些输出。

## 8. 运行状态机

```text
Idle → Preparing → Running → Completed
                      ├──→ Failed
Preparing / Running → Stopping → Cancelled
Preparing → Failed（参数/路径/环境/启动失败）
Preparing / Running → Failed（超时）
```

运行期间IsBusy=true；准备阶段也禁止第二次运行。停止后必须等进程树退出和日志排空，才恢复运行按钮。TaskModel编辑、新建/打开任务在运行中锁定；导航、窗口移动/缩放、底部日志和停止仍可用。运行使用P2已经接受的有效内存值，当前可见红框阻止启动。

prepare-only在通用生命周期中为Completed，但PrepareOnly=true，业务消息明确“输入准备完成（未执行求解）”，不混同完整计算完成。

## 9. 停止进程策略及限制

WindowsJobProcess使用CreateProcessW的CREATE_SUSPENDED与CREATE_NO_WINDOW：先创建挂起的根进程，再AssignProcessToJobObject，成功后ResumeThread。Job开启KILL_ON_JOB_CLOSE，不开启breakaway。若分配失败，直接终止仍挂起的根进程并报告启动失败，不退化成无法清理子进程的普通启动。

用户停止与GUI整体超时调用TerminateJobObject。主进程自然退出后也清理Job中的残留子进程，再收取stdout/stderr尾部。关窗会先StopAndWaitAsync；进程句柄关闭及Job关闭作为资源兜底。

方案参考：[Microsoft Job Objects](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects)、[Microsoft redirected process pipes](https://learn.microsoft.com/en-us/windows/win32/procthread/creating-a-child-process-with-redirected-input-and-output)。匿名管道父读取端不继承，子进程标准句柄使用继承端；参数采用Windows引号/反斜杠转义，不经过shell展开。

实际验证覆盖：根进程+存活子进程停止、根进程先退出时残留子进程回收、超时清理、GUI停止、关窗清理。测试结束未留下ProcessTest或模拟Python子进程。

限制：此实现面向Windows；本机Windows 11测试通过。受限父Job/不支持嵌套Job环境可能拒绝分配，此时失败关闭。不能据此承诺清理通过外部服务/WMI脱离Job创建的进程；当前01源码使用正常subprocess子进程链。强制终止不保证后端写出完整结果，Cancelled目录只作诊断。后端仅有求解器timeout机制，无正式协作式stop协议；GUI不伪造该协议。

## 10. 路径与运行目录策略

App.config三个可选键：

- BackendRoot：相对应用目录或绝对路径；留空则从应用目录向上查找coreprogram/config.json。
- PythonExecutable：显式解释器路径；留空从PATH查找真实python.exe，跳过WindowsApps商店别名，并执行实际预检。
- RuntimeRoot：相对应用目录或绝对目录；留空为LocalApplicationData/PreProcess/Runtime。

入口从后端config.json的standard_entries.01_forward读取。没有独立复杂配置框架、Python安装器或硬编码用户目录。

入口源码默认OUTPUT_ROOT在模块03-输出文件/runs；虽然config也描述forward_runtime_root，当前runner的默认OUTPUT_ROOT并非直接消费该键。为满足coreprogram只读，通过真实支持的--run-root显式指定外部可写根，而不是修改后端合同。

```text
RuntimeRoot/
  requests/<submission-guid>/
    request.json
    stdout.log
    stderr.log
    run-location.json
  runs/<后端run_id>/
    request.json
    case_config.json
    input.dat
    result.json
    output/
```

submission-guid只用于启动前的请求和日志，不冒充run_id。run_id仍由后端datetime.now + uuid生成，后端仍拥有run_xxx/output结构。默认运行文件不写GUI源码目录。自动联调显式使用GUI_WPF/Verification/P4_1下测试根，含中文与空格路径；没有在coreprogram产生runs或缓存。

## 11. 异常处理方案

|场景|处理|
|---|---|
|Python不存在/商店别名/不可启动|预检失败，提示未找到可用Python环境或缺依赖；技术细节进告警|
|pandas缺失/版本不足|预检非零退出，阻止正式调用|
|入口/工作目录不存在|进入Failed，记录路径/异常|
|request生成失败|阻止后端启动，显示输入问题|
|后端拒绝request|真实联调确认退出2；stderr进入告警，标记Failed|
|启动失败/非PE文件/Job失败|捕获Win32错误，清理已建句柄/挂起进程|
|后端异常退出|保留退出码和stderr；Failed|
|仅stderr警告但退出0|不单独决定失败，继续检查完成信封|
|结果目录/请求记录/状态信封异常|即使退出0仍标记Failed|
|用户停止|终止Job，终态Cancelled，保留已有诊断记录|
|超时|后端求解器1800秒；GUI另有整体1800+120秒上限，清理Job并标记Failed|
|日志/记录写入失败|提示存储问题，终止正在运行的任务或标记失败；不报告成功|

日志通过DispatcherTimer批量回写WPF；界面保留最近约64k字符，排队最多2000条，防止日志洪峰拖慢窗口。完整stdout/stderr保存在requests提交目录；GUI可能丢弃超量待显示行，但磁盘日志不因显示限额丢弃。没有阻塞UI线程调用WaitForExit。

## 12. prepare-only真实联调结果

本次多轮联调均只执行prepare-only，没有完整求解smoke。原默认任务为16目标/1000秒，未为验收强制运行其耗时计算。

最近一次服务测试：

- run_id：run_20260912_040745_925fc494。
- 退出码：0；后端status=prepared；GUI Completed且PrepareOnly=true。
- 目录：`Verification/P4_1/runtime/真实联调 with spaces/runs/run_20260912_040745_925fc494/output`。
- 收到input_ready、prepared；生成input.dat、case_config.json、后端request.json、result.json及output空目录。
- 没有求解器运行后才会写出的stdout.txt；与源码prepare分支提前返回一致。
- 证据：runtime/prepare-record.json及其RequestPath同目录stdout.log/stderr.log/run-location.json。

GUI真实联调：run_20260912_041309_b7b792ab，退出0，状态栏显示“输入准备完成（未执行求解）”；证据gui-prepare-record.json与execution-ui-tests.log。

另通过真实入口输入空对象{}，确认后端拒绝并退出2。异常/缺失目录/长运行等使用C#模拟进程测试，不改或新增Python程序。

## 13. Release编译结果

VS2019 Professional自带MSBuild，目标.NET Framework 4.8，Configuration=Release / Platform=Any CPU，独立OutputPath=bin/P4_1Release/：WPF、MapperTest、ProcessTest编译通过，0警告、0错误。

标准bin/Release复制因原EXE被占用而失败，属于输出文件锁定，不是编译错误；build-release.log保留该事实。没有强制关闭用户实例或覆盖占用程序。新版本可从第1节独立输出目录启动。

重复构建示例（MSBuild.exe应从VS2019开发者命令环境调用）：

```text
MSBuild.exe GUI_WPF/PreProcess.Wpf/PreProcess.Wpf.sln /p:Configuration=Release /p:Platform="Any CPU" /p:OutputPath=bin/P4_1Release/
MSBuild.exe GUI_WPF/PreProcess.Wpf/ProcessTest/ProcessTest.csproj /p:Configuration=Release /p:OutputPath=bin/P4_1Release/
```

## 14. 自动测试与P2/P3回归

|测试|结果|证据|
|---|---|---|
|ProcessManager/编排/路径/真实入口|37项通过|process-tests.log|
|既有MapperTest|5通过，0失败|mapper-tests.log|
|P3 RequestGenerator|114项通过|request-tests.log|
|P2与P3页面回归|50项通过|ui-results.txt|
|P4执行UI|14项通过|execution-ui-tests.log|

进程测试覆盖正常/异常退出、Unicode stdout/stderr、4000行双流同时排空、进度解析、未知state、畸形进度、空格/中文/引号/反斜杠/空字符串参数、无shell展开、重复启动、预取消、树停止、主进程退出后的残留清理、超时、启动失败、路径发现、真实prepare-only和真实请求拒绝、缺失输出、依赖预检失败。

P4 UI测试覆盖工具栏命令绑定、左侧正向页、运行/停止按钮状态、任务编辑锁定、运行时Dispatcher心跳与缩放、实时日志/状态、工具栏停止和子进程消失、恢复编辑、真实prepare-only、结果定位、失败告警及关窗树清理。运行的是独立隐藏测试窗口，未操纵用户已打开实例。

P2/P3测试脚本从原脚本复制，仅调整测试构建目录和输出证据位置；原测试源码与历史输出保留。P3固定5mm策略保持，没有恢复P2.1旧可变壳厚输入。最后补充的完成信封路径验证与日志关闭异常处理后，37项服务测试与14项执行UI测试均已重新通过。

## 15. Git diff摘要

本轮生产差异：6个既有文件调整、12个新增文件。详见source-changes.csv与p4-production-diff.patch；后者相对于本轮开始时快照，包含原本未跟踪文件的实际差异，适合本轮评审。

最终整个工作区tracked diff：13个文件、450行新增、1307行删除，包含用户先前.docs、P1/P2/P3的修改，不能归算为本轮。普通git diff不会完整展示新服务文件，已另提供本轮补丁和文件清单。

git diff --check通过。没有git commit、暂存、重置或清理用户既有修改。

## 16. coreprogram与其他冻结文件哈希

|目录|开始文件数|结束文件数|SHA-256变化|
|---|---:|---:|---:|
|coreprogram|119|119|0|
|GUI_MFC_Legacy|1743|1743|0|
|.docs|9|9|0|

采用只读共享文件流计算SHA-256，包含被其他程序占用的文档。逐文件证据在before_hashes.csv/after_hashes.csv，汇总在protected-check.json。两个冻结目录的git tracked diff与untracked清单均为空。没有新增后端输出、Python字节码、改入口、改Fortran或改forward-request-v1。

## 17. 尚未实现与验证边界

未正式接通02/03/04；未实现结果CSV解析、曲线/点图像/数据表展示、完整历史任务管理、队列、并发运行、远程运行或Python安装。

完整01求解调用参数和运行分支已接通，但本次只做真实prepare-only和请求拒绝联调；未声称真实完整求解成功、物理结果正确或真实Fortran长运行测试通过。停止能力通过同类型Windows父/子进程树与GUI模拟长任务验证。

Job策略未在所有Windows/受限Job环境测试。运行根禁止位于后端包内；用户部署时仍需为外部运行根提供写权限。GUI日志能实时显示入口输出，不能突破现有入口对求解器stdout/stderr的缓存行为。

## 18. 下一阶段建议与停止

建议下一轮评审先确认本阶段运行/停止和目录行为，再决定是否安排小规模完整01求解验证及02固定场景适用性设计。该建议未执行。

P4.1完成后停止，不推进P4.2或02/03/04接入，等待评审。

