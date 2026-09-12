using PreProcess.Requests;

namespace PreProcess.Wpf.Services.Mappers
{
    public sealed class TrajectoryMapper
    {
        public TrajectoryRequest Map(string trajectoryCsv, bool includePrerelease)
        {
            return new TrajectoryRequest
            {
                TrajectoryCsv = trajectoryCsv,
                IncludePrerelease = includePrerelease
            };
        }
    }
}
