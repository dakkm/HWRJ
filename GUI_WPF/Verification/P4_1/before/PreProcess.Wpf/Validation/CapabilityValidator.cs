using System.Collections.Generic;
using PreProcess.Requests;

namespace PreProcess.Validation
{
    public class CapabilityValidator
    {
        public List<string> ValidateForward(ForwardSimulationRequest request)
        {
            var errors = new List<string>();

            foreach (var target in request.TargetPhysics)
            {
                if (target.Radius <= 0)
                    errors.Add("目标半径必须大于0");
            }

            return errors;
        }

        public List<string> ValidatePrediction(PredictionRequest request)
        {
            var errors = new List<string>();

            if (request.InternalPower < 0 || request.InternalPower > 300)
                errors.Add("代理模型内部热源超出0-300W");

            if (request.EmissivityIr < 0.2 || request.EmissivityIr > 0.95)
                errors.Add("发射率超出代理模型范围");

            if (request.SolarAbsorption < 0.2 || request.SolarAbsorption > 0.95)
                errors.Add("太阳吸收率超出代理模型范围");

            return errors;
        }

        public List<string> ValidateSceneBuild(SceneBuildRequest request)
        {
            var errors = new List<string>();

            if (request.RequiredSimilarityPercent <= 0 ||
                request.RequiredSimilarityPercent > 100)
                errors.Add("相似度要求范围错误");

            if (request.RequiredCandidateCount <= 0)
                errors.Add("候选数量必须为正");

            return errors;
        }
    }
}
