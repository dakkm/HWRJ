namespace PreProcess.Wpf.Models.Results
{
    // 定义 ResultModuleType 类型，集中封装与该领域对象相关的状态和行为。
    public enum ResultModuleType
    {
        Forward,
        Prediction,
        // 继续处理当前业务步骤，保持上下文状态一致。
        Trajectory,
        Similarity,
        // 继续处理当前业务步骤，保持上下文状态一致。
        Scene
    }
}
