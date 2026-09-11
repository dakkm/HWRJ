# Workflow

文档版本：v0.2  
最后更新：2026-09-11  
当前适用阶段：MFC GUI 重构

---

## 1. 开始前检查

每次修改前先确认：

- [ ] 当前 Git 工作区是否干净；
- [ ] 当前分支和基线是否正确；
- [ ] 本次修改目标是否明确；
- [ ] 是否允许修改 `coreprogram`；
- [ ] 是否已经阅读 `00_project_brief.md`；
- [ ] 是否已经阅读 `01_architecture.md`；
- [ ] 本次新增 GUI 参数是否能在当前源码中找到正式支持；
- [ ] 是否误用了旧流程图中的过时参数。

---

## 2. 通用开发流程

```text
Step 1：阅读 00_project_brief.md
Step 2：确认本次允许修改范围
Step 3：检查当前源码真实输入输出
Step 4：确定用户语义层级
Step 5：确定 GUI 字段到后端合同的映射
Step 6：形成最小修改方案
Step 7：只完成一个阶段或一个闭环功能
Step 8：Visual Studio x64 Release 编译
Step 9：运行 Pre-process.exe
Step 10：执行最小回归测试
Step 11：检查 git diff
Step 12：更新维护文档
Step 13：Git 提交
```

---

## 3. GUI 参数设计规则

任何 GUI 参数进入界面前，必须确认：

```text
1. 当前后端是否真的支持？
2. 用户是否需要直接理解这个参数？
3. 应该放在哪个业务层级？
4. 是否需要默认值或高级设置隐藏？
5. 内部映射到 forward-request-v1 的哪个字段？
```

禁止：

```text
仅因为旧总体设计图中出现，就直接把参数加入 GUI。
```

当前已知不应加入：

```text
地球红外
对流换热边界
环境流体温度
```

---

## 4. Codex 使用规则

固定开场：

```text
先读取：
.docs/00_project_brief.md
.docs/01_architecture.md
.docs/02_workflow.md
.docs/04_decisions.md

先分析当前工程，不要立即修改代码。
coreprogram 只允许读取，不允许修改。

特别注意：
forward-request-v1 是后端合同，不是 GUI 页面结构。
GUI 必须使用业务语义层级。
```

---

## 5. 当前开发阶段划分

### P1：主窗口骨架

建立四区工作台。

### P2：业务任务编辑器

一级导航：

```text
① 任务设置
② 目标参数
③ 场景与运动
④ 环境与观测
⑤ 计算与输出
```

要求：

- 不直接使用后端六分区作为 GUI Tab；
- 不加入源码未支持参数；
- 先完成控件和数据模型。

### P3：任务映射与校验

```text
GUI 业务任务
→ forward-request-v1
→ 校验
→ JSON 导入/导出
```

### P4：统一进程执行器

```text
启动 Python 标准入口
→ 捕获 stdout/stderr
→ 解析 GUI_PROGRESS
→ 支持停止
```

### P5～P9

依次完成：

```text
01 结果显示
02 智能预测
03 相似度评估
04 场景构建
历史任务与辅助功能
```

---

## 6. 当前实例

当前任务：

```text
建立业务语义任务编辑器第一版。
```

允许修改：

```text
Pre-process/MainFrm.*
Pre-process/Pre-processView.*
Pre-process/Preprocess.rc
Pre-process/resource.h
新增必要任务编辑器、页面、数据模型类
```

禁止修改：

```text
coreprogram/**
```

本阶段暂不要求：

```text
完整后端运行
完整结果可视化
历史任务功能
```

---

## 7. 修改后检查

- [ ] `x64 Release` 编译成功；
- [ ] `Pre-process.exe` 正常启动；
- [ ] 五个业务一级页面可以切换；
- [ ] GUI 未直接暴露六个后端分区名作为主导航；
- [ ] 当前源码未支持参数没有出现在 GUI；
- [ ] 原菜单仍正常；
- [ ] `coreprogram` 未修改；
- [ ] 无绝对路径依赖；
- [ ] 新增资源 ID 无冲突。
