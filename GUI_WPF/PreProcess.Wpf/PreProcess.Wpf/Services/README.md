# Services

当前正式链：`TaskModel → RequestGenerator → ForwardSimulationService → ProcessManager → 01统一入口`。

- `RequestGenerator` 保持P3完整六分区合同与文件保存职责；未修改TaskModel/Mapper/原测试。
- `Process/ProcessManager` 通用异步启动、stdout/stderr、GUI_PROGRESS、退出码、取消和超时；不包含01业务字段。
- `Process/WindowsJobProcess` 使用Windows Job管理子进程，挂起创建后先分配Job再恢复；启动失败不会留下挂起进程。
- `Execution/ForwardSimulationService` 负责环境预检、请求文件、正式01 CLI及运行定位信息；只读取result.json执行信封，不解析CSV或图像。
- `Execution/BackendPathResolver` 读取应用配置和后端config.json中的standard_entries；不硬编码开发机路径。

应用配置 `BackendRoot`、`PythonExecutable`、`RuntimeRoot` 可留空。默认向上查找coreprogram/config.json、从PATH发现真实python.exe并执行版本/pandas预检；运行文件默认写入LocalApplicationData/PreProcess/Runtime。相对配置路径以应用目录为基准。

运行结构为 `<RuntimeRoot>/requests/<submission-guid>/request.json + stdout.log + stderr.log + run-location.json`，后端通过正式 `--run-root <RuntimeRoot>/runs` 自己创建 `run_时间_uuid/output`。submission-guid是请求提交标识，后端run_id才是运行编号。coreprogram不得作为RuntimeRoot。

Python使用 `-B -u` 与UTF-8/禁写字节码环境变量；正式入口仍是后端config定义的forward_simulation_runner.py，不直接调用求解器。prepare-only只完成输入准备，不代表求解成功。

旧Mappers和DTO全部保留兼容，不作为正式JSON合同；预测/相似度/场景搜索尚未正式调用。结果曲线、图像、历史管理均留待后续。
