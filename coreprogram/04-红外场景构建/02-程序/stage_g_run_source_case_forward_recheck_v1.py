from __future__ import annotations

"""04 内部依赖：基于同一份 01/02 规范 input_file 完成基准与候选正向计算。

职责：
1. run_reference()：直接使用用户给出的完整 forward-request-v1，调用 01 生成目标基准温度；
2. run_one()：复制同一份 input_file，仅覆盖当前代理模型允许搜索的 q_int/eps_ir/alpha_s，
   其余环境、运动、观测、结构和非搜索物性全部保持不变，然后调用 01 做真实正向复核。

本文件不是 GUI 公开入口。
"""

import copy
import importlib.util
import json
import sys
from pathlib import Path
from typing import Any

from stage_utils import PACKAGE_ROOT, OUT


def _load_forward_entry():
    path = PACKAGE_ROOT / "01-正向仿真" / "02-程序" / "forward_simulation_runner.py"
    if not path.exists():
        raise FileNotFoundError(path)
    spec = importlib.util.spec_from_file_location("_integrated_forward_simulation_runner", path)
    if spec is None or spec.loader is None:
        raise ImportError(f"无法加载01统一入口：{path}")
    mod = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = mod
    spec.loader.exec_module(mod)
    return mod


def _load_source_request(forward, source_input_path: str | Path) -> tuple[Path, dict[str, Any]]:
    path = Path(source_input_path).resolve()
    if not path.exists():
        raise FileNotFoundError(f"04共享 input_file 不存在：{path}")
    request = json.loads(path.read_text(encoding="utf-8-sig"))
    clean = forward.validate_request(request)
    return path, clean


def _forward_result(payload: dict[str, Any], source_path: Path, code: int, result: dict[str, Any]) -> dict[str, Any]:
    return {
        **payload,
        "forward_run_id": result.get("run_id"),
        "source_input_path": str(source_path),
        "candidate_input_path": result.get("input_dat"),
        "output_dir": result.get("output_dir"),
        "return_code": int(code if result.get("return_code") is None else result.get("return_code")),
        "elapsed_seconds": float(result.get("forward_elapsed_seconds", result.get("elapsed_seconds", 0.0))),
        "temperature_history_path": result.get("temperature_history_csv"),
        "temperature_curve_path": result.get("sphere1_temperature_csv"),
        "infrared_response_history_path": result.get("infrared_response_history_csv"),
        "solver_status_path": result.get("solver_status_txt"),
        "stdout_path": result.get("stdout_txt"),
        "stderr_path": result.get("stderr_txt"),
        "forward_output_valid": bool(result.get("output_valid", False)),
    }


def run_reference(payload: dict[str, Any]) -> dict[str, Any]:
    """用共享 input_file 原样运行一次 01，生成 04 的目标基准温度。"""
    forward = _load_forward_entry()
    source_path, params = _load_source_request(forward, payload["source_input_path"])
    code, result = forward.run_forward_simulation(
        params,
        run_root=OUT / "reference_runs",
        timeout_seconds=1800,
        prepare_only=False,
    )
    return _forward_result(payload, source_path, code, result)


def build_candidate_request(source_request: dict[str, Any], *, q_int: float, eps_ir: float, alpha_s: float) -> dict[str, Any]:
    """只覆盖当前 02 代理模型实际搜索的三项物理参数。"""
    params = copy.deepcopy(source_request)
    for row in params["TARGET_PHYSICS"]:
        row["q_int"] = float(q_int)
        row["eps_ir"] = float(eps_ir)
        row["alpha_s"] = float(alpha_s)
        # 当前代理模型训练合同采用不透明灰体互补关系。
        row["rho_ir"] = 1.0 - float(eps_ir)
    return params


def run_one(payload: dict[str, Any]) -> dict[str, Any]:
    """在同一共享 input_file 场景基础上，对一个候选参数执行 01 正向复核。"""
    forward = _load_forward_entry()
    source_path, source_request = _load_source_request(forward, payload["source_input_path"])

    q_int = float(payload["q_int"])
    eps_ir = float(payload["emissivity_ir"])
    alpha_s = float(payload["absorptivity_solar"])
    params = build_candidate_request(
        source_request,
        q_int=q_int,
        eps_ir=eps_ir,
        alpha_s=alpha_s,
    )
    params = forward.validate_request(params)

    run_root_raw = payload.get("run_root")
    run_root = Path(run_root_raw).resolve() if run_root_raw else (OUT / "forward_runs")
    code, result = forward.run_forward_simulation(
        params,
        run_root=run_root,
        timeout_seconds=1800,
        prepare_only=False,
    )
    return _forward_result(payload, source_path, code, result)
