# Services

P3 请求生成入口为 `RequestGenerator.Generate(TaskModel)` 与 `Save(TaskModel, path, overwrite=false)`。
完整任务生成后端六分区 JSON；`ValidateJson` 使用原样嵌入的后端自定义 schema 描述及实际入口规则验证。
文件保存使用 UTF-8 无 BOM，同目录临时文件及原子替换；默认拒绝覆盖。

`Mappers/` 和旧 DTO 保留以兼容已有调用与测试，不作为正式 JSON 序列化合同。
仅复用 `ForwardSimulationMapper.MapVector` 的三分量拷贝；原 Forward DTO 字段不全，不能序列化成正式请求。
旧 `CapabilityValidator` 也不是完整合同或预测适用性校验，生成入口不依赖它。
`PredictionMapper` 是旧内存结构；没有新建预测参数页面或独立三参数文件格式。
`SceneBuildMapper` 保留相似度阈值的业务含义；结果引用 Mapper 保留，不在本阶段生成或读取结果。
不继续扩展 Mapper 框架。本阶段不启动程序，不解析结果。
