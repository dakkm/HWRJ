using PreProcess.Requests;
using PreProcess.Wpf.Models;

namespace PreProcess.Wpf.Services.Mappers
{
    public sealed class PredictionMapper
    {
        private readonly ForwardSimulationMapper forwardMapper;

        public PredictionMapper()
            : this(new ForwardSimulationMapper())
        {
        }

        public PredictionMapper(ForwardSimulationMapper forwardMapper)
        {
            this.forwardMapper = forwardMapper;
        }

        public PredictionRequest Map(TaskModel task, string mode)
        {
            var supportedPhysics = task.Targets.Uniform;
            return new PredictionRequest
            {
                SourceRequest = forwardMapper.Map(task),
                InternalPower = supportedPhysics.InternalPower,
                EmissivityIr = supportedPhysics.Emissivity,
                SolarAbsorption = supportedPhysics.SolarAbsorption,
                Mode = mode
            };
        }
    }
}
