from __future__ import annotations

"""02-智能预测规范输入适配程序。

统一原则：
    01 正向仿真与 02 智能预测读取完全相同的 forward-request-v1 JSON。

本程序不定义第二套业务输入格式，而是：
    1. 复用 01 的 validate_request() 对六分区正式输入做严格校验；
    2. 检查当前冻结代理模型的适用域；
    3. 从完整业务输入中提取当前模型实际使用的三项学习参数。

当前冻结代理模型仍建立在固定场景/固定非学习物理量条件下，只允许统一改变：
    q_int, eps_ir, alpha_s
因此“输入文件统一”不等于“当前模型已经支持六分区所有参数任意变化”。
超出当前模型适用域时必须拒绝推理，不能静默忽略输入字段。
"""

import argparse
import importlib.util
import json
import math
import sys
from pathlib import Path
from typing import Any

PROGRAM_DIR = Path(__file__).resolve().parent
MODULE_ROOT = PROGRAM_DIR.parent
PACKAGE_ROOT = MODULE_ROOT.parent
FORWARD_PROGRAM = PACKAGE_ROOT / "01-正向仿真" / "02-程序" / "forward_simulation_runner.py"
FORWARD_REFERENCE = MODULE_ROOT / "03-模型文件" / "surrogate_reference_request.json"

# 这是“代理模型训练/验证适用域”，不是 01 正向程序的业务输入硬约束。
MODEL_PARAMETER_DOMAIN = {
    "q_int": {"min": 0.0, "max": 300.0, "unit": "W"},
    "emissivity_ir": {"min": 0.2, "max": 0.95, "unit": "-"},
    "absorptivity_solar": {"min": 0.2, "max": 0.95, "unit": "-"},
}

ALLOWED_VARYING_PHYSICS_FIELDS = {"q_int", "eps_ir", "alpha_s", "rho_ir"}
FLOAT_RTOL = 1.0e-9
FLOAT_ATOL = 1.0e-9


class SurrogateApplicabilityError(ValueError):
    """完整业务输入合法，但超出当前冻结代理模型的适用域。"""


def _load_forward_module():
    if not FORWARD_PROGRAM.exists():
        raise FileNotFoundError(f"找不到 01 正向仿真标准入口：{FORWARD_PROGRAM}")
    spec = importlib.util.spec_from_file_location("forward_simulation_runner_contract", FORWARD_PROGRAM)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"无法加载 01 正向仿真标准入口：{FORWARD_PROGRAM}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if not hasattr(module, "validate_request"):
        raise RuntimeError("01 标准入口缺少 validate_request()，无法共享输入合同。")
    return module


def _load_reference() -> dict[str, Any]:
    if not FORWARD_REFERENCE.exists():
        raise FileNotFoundError(f"找不到代理模型冻结参考请求：{FORWARD_REFERENCE}")
    obj = json.loads(FORWARD_REFERENCE.read_text(encoding="utf-8"))
    forward = _load_forward_module()
    return forward.validate_request(obj)


def _number_equal(a: Any, b: Any) -> bool:
    if isinstance(a, bool) or isinstance(b, bool):
        return a is b
    if isinstance(a, (int, float)) and isinstance(b, (int, float)):
        return math.isclose(float(a), float(b), rel_tol=FLOAT_RTOL, abs_tol=FLOAT_ATOL)
    return a == b


def _assert_same(path: str, actual: Any, reference: Any) -> None:
    if isinstance(reference, dict):
        if not isinstance(actual, dict) or set(actual) != set(reference):
            raise SurrogateApplicabilityError(f"{path} 与当前代理模型固定场景合同不一致。")
        for key in reference:
            _assert_same(f"{path}.{key}", actual[key], reference[key])
        return
    if isinstance(reference, list):
        if not isinstance(actual, list) or len(actual) != len(reference):
            raise SurrogateApplicabilityError(f"{path} 与当前代理模型固定场景合同不一致。")
        for i, (a, r) in enumerate(zip(actual, reference)):
            _assert_same(f"{path}[{i}]", a, r)
        return
    if not _number_equal(actual, reference):
        raise SurrogateApplicabilityError(
            f"{path}={actual!r} 超出当前冻结代理模型的固定场景合同；参考值={reference!r}。"
        )


def _check_model_parameter_domain(name: str, value: float) -> None:
    rule = MODEL_PARAMETER_DOMAIN[name]
    if not math.isfinite(float(value)):
        raise SurrogateApplicabilityError(f"{name} 必须为有限数值。")
    if not (float(rule["min"]) <= float(value) <= float(rule["max"])):
        raise SurrogateApplicabilityError(
            f"{name}={value} 超出当前代理模型训练/验证适用域 "
            f"[{rule['min']}, {rule['max']}] {rule['unit']}。"
        )


def standardize_surrogate_request(request: dict[str, Any]) -> dict[str, Any]:
    """校验完整 forward-request-v1，并转换为当前冻结代理模型的规范输入。"""
    forward = _load_forward_module()
    clean = forward.validate_request(request)
    reference = _load_reference()

    # 当前温度代理与点图像代理均冻结为 1000 s / 16 目标任务。
    _assert_same("CASE.TOTAL_TIME", clean["CASE"]["TOTAL_TIME"], reference["CASE"]["TOTAL_TIME"])
    _assert_same("CASE.NUM_SPHERES", clean["CASE"]["NUM_SPHERES"], reference["CASE"]["NUM_SPHERES"])

    # 当前模型没有把这些量作为可学习输入，因此必须与训练/验证参考工况一致。
    for section in ("ENVIRONMENT", "GROUP_STATE", "OBSERVATION"):
        _assert_same(section, clean[section], reference[section])
    _assert_same("TARGET_SCENE", clean["TARGET_SCENE"], reference["TARGET_SCENE"])

    phys = sorted(clean["TARGET_PHYSICS"], key=lambda row: row["id"])
    ref_phys = sorted(reference["TARGET_PHYSICS"], key=lambda row: row["id"])

    # 非学习物理量固定；q_int / eps_ir / alpha_s 为当前模型的统一全局学习参数。
    for row, ref in zip(phys, ref_phys):
        for key in ref:
            if key in ALLOWED_VARYING_PHYSICS_FIELDS:
                continue
            _assert_same(f"TARGET_PHYSICS[id={row['id']}].{key}", row[key], ref[key])

    first = phys[0]
    q_int = float(first["q_int"])
    eps_ir = float(first["eps_ir"])
    alpha_s = float(first["alpha_s"])

    # 当前模型只包含一组全局 q/eps/alpha，不能表示“不同目标各用不同物性参数”。
    for row in phys:
        for key, expected in (("q_int", q_int), ("eps_ir", eps_ir), ("alpha_s", alpha_s)):
            if not _number_equal(row[key], expected):
                raise SurrogateApplicabilityError(
                    f"当前代理模型要求所有目标的 {key} 使用同一全局值；"
                    f"目标1={expected}，目标{row['id']}={row[key]}。"
                )
        # rho_ir 未进入代理模型输入，只允许作为 eps_ir 的灰体互补量存在，禁止静默忽略独立变化。
        expected_rho_ir = 1.0 - float(row["eps_ir"])
        if not math.isclose(float(row["rho_ir"]), expected_rho_ir, rel_tol=1.0e-9, abs_tol=1.0e-9):
            raise SurrogateApplicabilityError(
                f"目标{row['id']}：当前代理模型要求 rho_ir = 1 - eps_ir；"
                f"收到 rho_ir={row['rho_ir']}，应为 {expected_rho_ir}."
            )

    model_parameters = {
        "q_int": q_int,
        "emissivity_ir": eps_ir,
        "absorptivity_solar": alpha_s,
    }
    for name, value in model_parameters.items():
        _check_model_parameter_domain(name, value)

    return {
        "input_contract": "forward-request-v1",
        "surrogate_runtime_contract": "surrogate-standard-input-v1",
        "full_request": clean,
        "model_parameters": model_parameters,
        "model_parameter_domain": MODEL_PARAMETER_DOMAIN,
        "applicability": {
            "status": "accepted",
            "task_duration_s": 1000.0,
            "sphere_count": 16,
            "scene_policy": "ENVIRONMENT/GROUP_STATE/OBSERVATION/TARGET_SCENE must match the frozen training reference",
            "physics_policy": "r/rho/cp/t_init fixed; q_int/eps_ir/alpha_s vary globally and identically for all targets; rho_ir=1-eps_ir",
            "reference_request": str(FORWARD_REFERENCE),
        },
    }


def load_and_standardize(path: Path) -> dict[str, Any]:
    request = json.loads(Path(path).read_text(encoding="utf-8"))
    return standardize_surrogate_request(request)


def _cli() -> int:
    ap = argparse.ArgumentParser(
        description="02 智能预测规范输入：读取与 01 完全相同的 forward-request-v1 JSON，并检查当前代理模型适用域。"
    )
    ap.add_argument("--params-json", type=Path, required=True, help="与 01 正向仿真完全相同的完整请求 JSON。")
    ap.add_argument("--output", type=Path, default=None, help="可选：输出规范化后的代理输入审计 JSON。")
    args = ap.parse_args()
    try:
        result = load_and_standardize(args.params_json)
        text = json.dumps(result, ensure_ascii=False, indent=2)
        if args.output is not None:
            args.output.parent.mkdir(parents=True, exist_ok=True)
            args.output.write_text(text, encoding="utf-8")
        print(text)
        return 0
    except Exception as exc:
        print(
            json.dumps(
                {
                    "module": "02",
                    "status": "invalid_input",
                    "error_type": type(exc).__name__,
                    "message": str(exc),
                },
                ensure_ascii=False,
            ),
            file=sys.stderr,
        )
        return 2


if __name__ == "__main__":
    raise SystemExit(_cli())
