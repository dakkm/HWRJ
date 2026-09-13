# Models

P2 使用 TaskModel 聚合任务设置、统一物性、目标集合、群体运动、环境观测和计算模块说明。

GuiTaskMetadata 单独保存名称和说明。ObservableCollection 维护目标数量与编号，独立覆盖关闭时继承统一物性；质量是只读派生值。

模型只存在内存中，不代表后端合同，不含序列化、文件格式或请求映射。
