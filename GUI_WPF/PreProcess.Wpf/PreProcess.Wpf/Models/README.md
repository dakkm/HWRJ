# Models

P2 使用 TaskModel 聚合任务设置、统一物性、目标集合、群体运动、环境观测和计算模块说明。

GuiTaskMetadata 单独保存名称和说明。ObservableCollection 维护目标数量与编号，独立覆盖关闭时继承统一结构和物性；质量是只读派生值。

P2.1 增加 TargetGeometrySettings：默认球壳，外半径以m表示，ShellThickness以mm表示，默认5mm。TargetPhysics根据Geometry的球壳体积与密度派生Mass；独立覆盖深复制Geometry。TargetSettings.Geometry是统一几何对象的访问入口，不重复保存状态。TaskSettings.SimilarityIndex按百分数保存，范围50～100，GUI默认90。

模型只存在内存中，不代表后端合同，不含序列化、文件格式或请求映射。
