using PreProcess.Requests;

namespace PreProcess.Wpf.Services.Mappers
{
    // 说明当前文件承载的类型职责，便于维护者快速定位功能边界。
    public sealed class SimilarityEvaluationMapper
    {
        public SimilarityEvaluationRequest Map(
            // 继续处理当前业务步骤，保持上下文状态一致。
            ResultReference reference,
            ResultReference candidate,
            // 继续处理当前业务步骤，保持上下文状态一致。
            EvaluationConfig config)
        {
            return new SimilarityEvaluationRequest
            // 本文件只处理所属模块的数据或界面逻辑，不在此处引入额外副作用。
            {
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                Reference = new ResultReference { RunDirectory = reference.RunDirectory },
                Candidate = new ResultReference { RunDirectory = candidate.RunDirectory },
                Config = new EvaluationConfig
                {
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    TargetObjectId = config.TargetObjectId,
                    Metric = config.Metric
                }
            // 调用方应通过公开成员访问功能，内部状态由当前类型统一维护。
            };
        }
    }
}
