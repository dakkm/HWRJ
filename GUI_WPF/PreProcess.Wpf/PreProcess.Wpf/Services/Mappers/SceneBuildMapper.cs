using PreProcess.Requests;
using PreProcess.Wpf.Models;

namespace PreProcess.Wpf.Services.Mappers
{
    // 说明当前文件承载的类型职责，便于维护者快速定位功能边界。
    public sealed class SceneBuildMapper
    {
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public SceneBuildRequest Map(
            TaskModel task,
            string sourceRequestFile,
            // 继续处理当前业务步骤，保持上下文状态一致。
            int requiredCandidateCount)
        {
            return new SceneBuildRequest
            {
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                SourceRequestFile = sourceRequestFile,
                RequiredSimilarityPercent = task.Settings.SimilarityIndex,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                RequiredCandidateCount = requiredCandidateCount
            // 本文件只处理所属模块的数据或界面逻辑，不在此处引入额外副作用。
            };
        }
    }
}
