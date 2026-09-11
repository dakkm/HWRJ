# Prompt Log

本文档只记录关键 AI / Codex 指令，不记录完整聊天。

---

## Current Prompt State

当前最近有效 prompt：

```text
PROMPT-20260911-002
```

当前阶段：

```text
业务语义任务编辑器
```

下一类 prompt：

```text
GUI 业务任务 → forward-request-v1 映射与校验
```

---

## PROMPT-20260911-001

### 目的

```text
建立新版 MFC 主窗口布局骨架。
```

### 状态

```text
Previous
```

---

## PROMPT-20260911-002

### 目的

```text
按用户业务语义实现场景任务编辑器第一版，
不直接暴露后端六分区。
```

### 使用对象

```text
Codex
```

### 指令边界

允许：

- 修改 MFC GUI 源码；
- 新增任务编辑器类；
- 新增业务任务数据模型；
- 新增必要资源；
- 读取 `coreprogram` 和正式 JSON 合同确认真实参数。

禁止：

- 修改 `coreprogram`；
- 修改 Python；
- 修改 Fortran；
- 修改冻结模型；
- 修改正式后端数据合同；
- 按旧总体设计图补充当前源码不存在的参数；
- 将 `CASE / ENVIRONMENT / GROUP_STATE / OBSERVATION / TARGET_PHYSICS / TARGET_SCENE`
  直接作为 GUI 一级导航。

### 输入

```text
Pre-process.sln
Pre-process/**
coreprogram/** 仅允许读取
.docs/00_project_brief.md
.docs/01_architecture.md
.docs/02_workflow.md
.docs/04_decisions.md
```

### 正式 Prompt

```text
这是一个 MFC GUI + 冻结后端项目。

先读取：
.docs/00_project_brief.md
.docs/01_architecture.md
.docs/02_workflow.md
.docs/04_decisions.md

先分析当前工程和 forward-request-v1 的真实字段，不要立即修改代码。

本次目标：
实现“场景任务编辑器”第一版，但 GUI 必须按用户业务语义组织，
不能把后端 JSON 六分区直接做成界面一级 Tab。

一级导航固定为：
1. 任务设置
2. 目标参数
3. 场景与运动
4. 环境与观测
5. 计算与输出

设计原则：
- GUI 使用用户容易理解的中文业务名称；
- forward-request-v1 只作为内部数据合同；
- 一个 GUI 页面可以映射到多个后端分区；
- 一个后端分区也可以由多个 GUI 控件共同生成；
- 所有 GUI 参数必须以当前 v1.0.0 源码实际支持为准；
- 不要因为旧总体设计图出现过某参数就把它加入 GUI；
- 当前不要加入地球红外、对流换热边界等后端未支持项；
- 普通参数直接显示，复杂或低频参数可预留“高级设置”结构；
- 多目标参数优先支持统一设置，再考虑单目标独立编辑。

本阶段要求：
A. 建立五个一级任务页面或对应容器；
B. 左侧任务树可以切换这五个页面；
C. 为每个页面建立第一版控件布局；
D. 建立 GUI 侧业务任务数据模型骨架；
E. 暂不要求完整运行后端；
F. 暂不要求完整结果绘图；
G. 暂不修改 coreprogram。

本阶段结束时必须给出：
1. 修改文件清单；
2. 新增文件清单；
3. 五个页面分别包含哪些参数；
4. 每个 GUI 参数对应的当前源码依据；
5. 哪些参数被放入“高级设置”；
6. 哪些旧设计参数因为当前源码不支持而明确排除；
7. 编译方法；
8. 当前尚未实现的映射和校验部分；
9. 下一步建议。

修改完成后不要继续自动推进到 ProcessManager 或结果显示。
```

### 预期结果

```text
Pre-process.exe 中形成第一版场景任务编辑器：
任务设置 / 目标参数 / 场景与运动 / 环境与观测 / 计算与输出。
界面层级不再暴露后端六分区。
```

### 验收重点

- [ ] 五个页面可正常切换；
- [ ] 参数名称采用用户语言；
- [ ] 无地球红外、对流等当前未支持输入；
- [ ] coreprogram 没有修改；
- [ ] 编译通过；
- [ ] 原菜单功能未破坏。

### 影响文档

- `03_experiments.md`
- `05_known_issues.md`

### 后续动作

```text
下一条 Prompt：
实现 GUI 业务任务对象到 forward-request-v1 的正式映射、JSON 导入导出与任务校验。
```


### PROMPT-20260911-002 本次执行记录

已完成源码实现：业务模型、五个中文业务页面、树切换、统一物性、单目标运动、高级设置、GUI 任务文件、四模块设置路由。未推进 ProcessManager、后端运行、JSON 合同映射或绘图。

状态：等待编译/运行验收；环境缺少 v143 与 MFC，未产生可验证的新 EXE。下一条应先补本阶段验收，再考虑原定 P3 映射与校验。完整报告：[07_task_editor_v1_report.md](07_task_editor_v1_report.md)。
