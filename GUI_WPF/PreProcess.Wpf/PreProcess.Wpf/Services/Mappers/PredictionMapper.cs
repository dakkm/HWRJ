using PreProcess.Requests;
using PreProcess.Wpf.Models;

namespace PreProcess.Wpf.Services.Mappers
{
    public sealed class PredictionMapper
    // 说明当前文件承载的类型职责，便于维护者快速定位功能边界。
    {
        private readonly ForwardSimulationMapper forwardMapper;

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public PredictionMapper()
            : this(new ForwardSimulationMapper())
        {
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public PredictionMapper(ForwardSimulationMapper forwardMapper)
        {
            this.forwardMapper = forwardMapper;
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public PredictionRequest Map(TaskModel task, string mode)
        {
            var supportedPhysics = task.Targets.Uniform;
            // 返回当前步骤生成的结果，并结束本次调用。
            return new PredictionRequest
            {
                SourceRequest = forwardMapper.Map(task),
                // 本文件只处理所属模块的数据或界面逻辑，不在此处引入额外副作用。
                InternalPower = supportedPhysics.InternalPower,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                EmissivityIr = supportedPhysics.Emissivity,
                SolarAbsorption = supportedPhysics.SolarAbsorption,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                Mode = mode
            };
        }
    }
}
