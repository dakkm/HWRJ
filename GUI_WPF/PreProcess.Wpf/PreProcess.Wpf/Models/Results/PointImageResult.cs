namespace PreProcess.Wpf.Models.Results
{
    // 定义 PointImageResult 类型，集中封装与该领域对象相关的状态和行为。
    public sealed class PointImageResult : ResultArtifact
    {
        public int Width { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public int Height { get; set; }
        public double[] Values { get; set; }
        public long FrameId { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public double TimeSeconds { get; set; }
        public string ValueName { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string ValueUnit { get; set; }
    }
}
