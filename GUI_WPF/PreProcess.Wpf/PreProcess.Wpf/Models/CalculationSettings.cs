using System.Collections.ObjectModel;

namespace PreProcess.Wpf.Models
{
    public sealed class CalculationSettings
    {
        public ReadOnlyCollection<CalculationModule> Modules { get; } =
            new ReadOnlyCollection<CalculationModule>(new[] {
                new CalculationModule("正向计算", "根据场景任务计算目标响应。", "场景任务参数", "温度、红外响应及轨迹等结果"),
                new CalculationModule("智能预测", "使用预测模型估计目标响应。", "场景任务参数", "温度或点图像预测结果"),
                new CalculationModule("轨迹生成", "调用01轨迹专用程序处理目标运动轨迹，不启动正向计算。", "外部轨迹文件，或最近一次01计算生成的默认轨迹", "轨迹统计、数据表与轨迹示意图"),
                new CalculationModule("相似度评估", "比较参考结果和候选结果。", "参考运行结果、候选运行结果", "特征及相似度指标"),
                new CalculationModule("红外场景构建", "根据要求组织候选场景。", "场景任务、相似度要求和候选数量", "候选场景及评估摘要")
            });
    }
    public sealed class CalculationModule
    {
        public string Name { get; }
        public string Description { get; }
        public string Input { get; }
        public string Output { get; }
        public CalculationModule(string name, string description, string input, string output)
        { Name = name; Description = description; Input = input; Output = output; }
    }
}
