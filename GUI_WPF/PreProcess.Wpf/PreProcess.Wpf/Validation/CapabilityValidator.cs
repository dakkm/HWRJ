using System.Collections.Generic;
using PreProcess.Requests;

namespace PreProcess.Validation
{
    // 定义 CapabilityValidator 类型，集中封装与该领域对象相关的状态和行为。
    public class CapabilityValidator
    {
        public List<string> ValidateForward(ForwardSimulationRequest request)
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var errors = new List<string>();

            foreach (var target in request.TargetPhysics)
            {
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (target.Radius <= 0)
                    errors.Add("目标半径必须大于0");
            }

            return errors;
        }

        // 检查输入及依赖是否满足要求，提前阻止无效操作。
        public List<string> ValidatePrediction(PredictionRequest request)
        {
            var errors = new List<string>();

            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (request.InternalPower < 0 || request.InternalPower > 300)
                errors.Add("代理模型内部热源超出0-300W");

            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (request.EmissivityIr < 0.2 || request.EmissivityIr > 0.95)
                errors.Add("发射率超出代理模型范围");

            if (request.SolarAbsorption < 0.2 || request.SolarAbsorption > 0.95)
                // 将当前结果加入集合，供后续汇总或界面展示。
                errors.Add("太阳吸收率超出代理模型范围");

            return errors;
        }

        // 检查输入及依赖是否满足要求，提前阻止无效操作。
        public List<string> ValidateSceneBuild(SceneBuildRequest request)
        {
            var errors = new List<string>();

            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (request.RequiredSimilarityPercent <= 0 ||
                request.RequiredSimilarityPercent > 100)
                // 将当前结果加入集合，供后续汇总或界面展示。
                errors.Add("相似度要求范围错误");

            if (request.RequiredCandidateCount <= 0)
                errors.Add("候选数量必须为正");

            // 返回当前步骤生成的结果，并结束本次调用。
            return errors;
        }
    }
}
