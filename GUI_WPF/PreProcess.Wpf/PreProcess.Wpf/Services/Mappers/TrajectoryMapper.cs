using PreProcess.Requests;

namespace PreProcess.Wpf.Services.Mappers
{
    // 定义 TrajectoryMapper 类型，集中封装与该领域对象相关的状态和行为。
    public sealed class TrajectoryMapper
    {
        public TrajectoryRequest Map(string trajectoryCsv, bool includePrerelease)
        {
            // 返回当前步骤生成的结果，并结束本次调用。
            return new TrajectoryRequest
            // 说明当前文件承载的类型职责，便于维护者快速定位功能边界。
            {
                TrajectoryCsv = trajectoryCsv,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                IncludePrerelease = includePrerelease
            };
        }
    }
}
