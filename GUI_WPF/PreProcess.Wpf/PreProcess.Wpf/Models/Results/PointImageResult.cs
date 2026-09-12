namespace PreProcess.Wpf.Models.Results
{
    public sealed class PointImageResult : ResultArtifact
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public double[] Values { get; set; }
        public long FrameId { get; set; }
        public double TimeSeconds { get; set; }
        public string ValueName { get; set; }
        public string ValueUnit { get; set; }
    }
}
