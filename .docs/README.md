# GUI 维护文档使用说明

本目录用于维护 `Pre-process` MFC GUI 重构项目。

建议放置：

```text
Pre-process\
├─ .docs\
│  ├─ README.md
│  ├─ 00_project_brief.md
│  ├─ 01_architecture.md
│  ├─ 02_workflow.md
│  ├─ 03_experiments.md
│  ├─ 04_decisions.md
│  ├─ 05_known_issues.md
│  └─ 06_prompt_log.md
├─ Pre-process.sln
├─ Pre-process\
├─ coreprogram\
└─ x64\
```

当前最重要的规则：

```text
1. coreprogram 是冻结后端；
2. forward-request-v1 是内部合同，不是 GUI 页面结构；
3. GUI 使用业务语义层级；
4. 所有 GUI 参数必须以当前源码实际支持为准；
5. 旧总体设计图只作流程参考，不作为参数真值源。
```

当前 GUI 一级层级：

```text
① 任务设置
② 目标参数
③ 场景与运动
④ 环境与观测
⑤ 计算与输出
```
