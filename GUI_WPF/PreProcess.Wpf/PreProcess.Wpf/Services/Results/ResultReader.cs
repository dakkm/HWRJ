using System;
using PreProcess.Wpf.Models.Results;
using PreProcess.Wpf.Services.Execution;

namespace PreProcess.Wpf.Services.Results
{
    // 说明当前文件承载的类型职责，便于维护者快速定位功能边界。
    public sealed class ResultReader
    {
        public RunResult Read(RunRecord record, ResultModuleType? moduleOverride = null)
        {
            // 返回当前步骤生成的结果，并结束本次调用。
            return Read(new ResultDirectoryManager().Create(record, moduleOverride));
        }

        public RunResult Read(RunResult result)
        {
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (result == null) throw new ArgumentNullException("result");
            switch (result.ModuleType)
            {
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case ResultModuleType.Forward:
                case ResultModuleType.Trajectory: return new ForwardResultReader().Read(result);
                // 本文件只处理所属模块的数据或界面逻辑，不在此处引入额外副作用。
                case ResultModuleType.Prediction: return new PredictionResultReader().Read(result);
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case ResultModuleType.Similarity: return new SimilarityResultReader().Read(result);
                case ResultModuleType.Scene: return new SceneResultReader().Read(result);
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                default: throw new ArgumentOutOfRangeException("result");
            }
        }
    }
}
