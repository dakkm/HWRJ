from __future__ import annotations

# 继续执行当前业务步骤，保持处理上下文与数据状态一致。
"""04-红外场景构建：温度相似度逆应用统一入口。

正式业务逻辑
------------
用户业务输入由三部分组成：
# 继续执行当前业务步骤，保持处理上下文与数据状态一致。
1) 与 01/02 共用的完整 forward-request-v1 输入文件；
2) 相似度要求（当前正式支持温度综合相似度）；
3) 需要保留的候选参数数量。

# 继续执行当前业务步骤，保持处理上下文与数据状态一致。
04 不再要求用户额外提供孤立的目标温度曲线。程序先调用 01 对 input_file
执行一次基准正向计算，得到 1 号目标温度曲线，然后进入逆搜索。

搜索链：
# 继续执行当前业务步骤，保持处理上下文与数据状态一致。
共享 input_file
 -> 复用同 input/同正向EXE的01基准缓存；无缓存时才运行01生成目标温度曲线
 -> 固定参数池原始顺序
 -> 02 温度代理模型快速预测
 # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
 -> 按 02 模型独立验证精度进行代理预筛（与用户输入相似度无关）
 -> 对预筛通过候选，在同一 input_file 场景基础上仅覆盖 q_int/eps_ir/alpha_s
 -> 01 正向复核
 # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
 -> 使用 03 的温度相似度定义进行最终复核
 -> 正向温度相似度 >= 用户要求时保留候选
 -> 达到规定候选数量后停止。

说明：
# 继续执行当前业务步骤，保持处理上下文与数据状态一致。
- input_file 是 01、02 已统一采用的完整六分区 JSON，不另设目标温度 CSV。
- reference_runs 使用 input SHA + 正向EXE SHA 缓存，同一目标工况重复搜索不再重复计算基准01。
- 用户相似度要求只用于最终正向复核，不参与代理预筛。
# 继续执行当前业务步骤，保持处理上下文与数据状态一致。
- 程序同时将用户相似度换算为温度特征裕度 D_allow，用于解释和结果记录。
- 候选复核继承 input_file 中的环境、运动、观测、非搜索物性等全部设置。
- 当前代理模型只允许统一搜索 q_int、eps_ir、alpha_s，rho_ir 同步取 1-eps_ir。
- 不做 Top-K 排名，不自动放宽条件。
# 继续执行当前业务步骤，保持处理上下文与数据状态一致。
"""

import argparse
import hashlib
# 导入当前模块依赖的标准能力或领域组件。
import importlib.util
import json
import math
# 导入当前模块依赖的标准能力或领域组件。
import sys
import time
import uuid
from datetime import datetime
# 导入当前模块依赖的标准能力或领域组件。
from pathlib import Path
from typing import Any

import joblib
# 导入当前模块依赖的标准能力或领域组件。
import numpy as np
import pandas as pd

from stage_utils import (
    FORWARD_PROGRAM_DIR,
    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
    INPUT_DIR,
    MODEL_DIR,
    OUT,
    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
    PACKAGE_ROOT,
    PROGRAM_DIR,
    config_path,
    ensure,
    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
    jwrite,
    sha256,
)

# 更新当前流程所需的中间数据，为下一计算步骤做好准备。
EXPECTED_FEATURES = [
    "q_int",
    "emissivity_ir",
    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
    "absorptivity_solar",
    "time_s",
    "q_int_times_absorptivity_solar",
    "q_int_div_emissivity_ir",
    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
    "is_initial_condition",
]
TIME_STEP_S = 10.0
# 更新当前流程所需的中间数据，为下一计算步骤做好准备。
MAX_SCENE_TIME_S = 1000.0

DEFAULT_CONFIG_PATH = INPUT_DIR / "scene_search_config.json"
DEFAULT_REQUEST_PATH = INPUT_DIR / "scene_search_request.json"
SIMILARITY_CONFIG_PATH = PACKAGE_ROOT / "03-相似度评估" / "01-输入文件" / "similarity_config.json"
# 更新当前流程所需的中间数据，为下一计算步骤做好准备。
RUN_ROOT = OUT / "runs"
LATEST_RUN_PATH = OUT / "latest_run.json"
REFERENCE_RUN_ROOT = OUT / "reference_runs"
# 更新当前流程所需的中间数据，为下一计算步骤做好准备。
REFERENCE_CACHE_MANIFEST = REFERENCE_RUN_ROOT / "reference_cache_manifest.json"

ACTIVE_RUN_ID: str | None = None
ACTIVE_RUN_DIR: Path | None = None

TEMP_SIM_FIELDS = [
    # 调用对应组件完成当前处理步骤，并保留返回结果。
    ("temperature_rmse", "rmse_similarity_percent"),
    ("temperature_mae", "mae_similarity_percent"),
    ("temperature_max_abs", "max_similarity_percent"),
    # 调用对应组件完成当前处理步骤，并保留返回结果。
    ("temperature_final_abs", "final_similarity_percent"),
    ("temperature_plateau_abs", "plateau_similarity_percent"),
    ("temperature_early_abs", "early_similarity_percent"),
    # 调用对应组件完成当前处理步骤，并保留返回结果。
    ("temperature_rate_trend", "temperature_trend_similarity_percent"),
]


def _load_py_module(name: str, path: Path):
    if not path.exists():
        # 检测到无效状态后立即报错，防止异常数据继续传播。
        raise FileNotFoundError(path)
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        # 检测到无效状态后立即报错，防止异常数据继续传播。
        raise ImportError(f"无法加载模块：{path}")
    mod = importlib.util.module_from_spec(spec)
    sys.modules[name] = mod
    spec.loader.exec_module(mod)
    # 向调用方返回当前步骤生成的数据或迭代结果。
    return mod


def load_similarity_module():
    p = PACKAGE_ROOT / "03-相似度评估" / "02-程序" / "similarity_evaluator.py"
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    mod = _load_py_module("_scene_search_similarity_evaluator", p)
    if not hasattr(mod, "evaluate_temperature_curves"):
        raise RuntimeError("03 similarity_evaluator.py 缺少 evaluate_temperature_curves；请先安装03第一版相似度补丁。")
    # 向调用方返回当前步骤生成的数据或迭代结果。
    return mod


def load_stage_g_module():
    return _load_py_module("_scene_search_stage_g", PROGRAM_DIR / "stage_g_run_source_case_forward_recheck_v1.py")


def load_surrogate_standard_input_module():
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    p = PACKAGE_ROOT / "02-智能预测" / "02-程序" / "surrogate_standard_input.py"
    mod = _load_py_module("_scene_search_surrogate_standard_input", p)
    if not hasattr(mod, "load_and_standardize"):
        # 检测到无效状态后立即报错，防止异常数据继续传播。
        raise RuntimeError("02 surrogate_standard_input.py 缺少 load_and_standardize；请先安装02规范输入补丁。")
    return mod


def package_path(raw: str | Path) -> Path:
    p = Path(raw)
    # 向调用方返回当前步骤生成的数据或迭代结果。
    return p.resolve() if p.is_absolute() else (PACKAGE_ROOT / p).resolve()


def _read_csv_compatible(path: Path) -> pd.DataFrame:
    last: Exception | None = None
    # 遍历当前数据或迭代计算，逐项更新处理结果。
    for encoding in ("utf-8-sig", "utf-8", "gb18030", "cp936"):
        try:
            return pd.read_csv(path, encoding=encoding)
        except UnicodeDecodeError as exc:
            # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
            last = exc
    raise UnicodeDecodeError("utf-8", b"", 0, 1, f"无法读取CSV编码：{path}; last={last}")


def load_json(path: Path) -> dict[str, Any]:
    # 检查当前条件，仅在满足约束时执行对应分支。
    if not path.exists():
        raise FileNotFoundError(path)
    obj = json.loads(path.read_text(encoding="utf-8-sig"))
    # 检查当前条件，仅在满足约束时执行对应分支。
    if not isinstance(obj, dict):
        raise ValueError(f"JSON 顶层必须为对象：{path}")
    return obj


def canonical_request_sha256(request: dict[str, Any]) -> str:
    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
    """对已经规范化的请求计算与排版无关的内容哈希。"""
    raw = json.dumps(
        request, ensure_ascii=False, sort_keys=True, separators=(",", ":")
    # 调用对应组件完成当前处理步骤，并保留返回结果。
    ).encode("utf-8")
    return hashlib.sha256(raw).hexdigest()


def _load_cache_manifest() -> dict[str, Any]:
    if not REFERENCE_CACHE_MANIFEST.exists():
        # 向调用方返回当前步骤生成的数据或迭代结果。
        return {"schema_version": "reference-cache-v1", "entries": []}
    try:
        obj = load_json(REFERENCE_CACHE_MANIFEST)
    # 捕获本阶段异常并转换为明确的错误信息或运行状态。
    except Exception:
        return {"schema_version": "reference-cache-v1", "entries": []}
    entries = obj.get("entries", [])
    if not isinstance(entries, list):
        # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
        entries = []
    return {"schema_version": "reference-cache-v1", "entries": entries}


def _save_cache_manifest(manifest: dict[str, Any]) -> None:
    # 读取或写入约定的数据文件，并维护统一的路径规则。
    REFERENCE_RUN_ROOT.mkdir(parents=True, exist_ok=True)
    manifest["schema_version"] = "reference-cache-v1"
    REFERENCE_CACHE_MANIFEST.write_text(
        # 读取或写入约定的数据文件，并维护统一的路径规则。
        json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8"
    )


def _reference_result_from_run_dir(
    run_dir: Path,
    # 读取或写入约定的数据文件，并维护统一的路径规则。
    source_input: Path,
    *,
    expected_request_sha256: str,
    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
    expected_forward_exe_sha256: str,
) -> dict[str, Any] | None:
    """验证既有 reference run，成功时转换成与 stage_g.run_reference 一致的结果。"""
    run_dir = run_dir.resolve()
    # 读取或写入约定的数据文件，并维护统一的路径规则。
    request_path = run_dir / "request.json"
    result_path = run_dir / "result.json"
    if not request_path.exists() or not result_path.exists():
        # 向调用方返回当前步骤生成的数据或迭代结果。
        return None
    try:
        cached_request = load_json(request_path)
        if canonical_request_sha256(cached_request) != expected_request_sha256:
            # 向调用方返回当前步骤生成的数据或迭代结果。
            return None
        result = load_json(result_path)
        if int(result.get("return_code", 999)) != 0 or not bool(result.get("output_valid", False)):
            # 向调用方返回当前步骤生成的数据或迭代结果。
            return None
        cached_exe_sha = str(result.get("forward_exe_sha256", "")).strip()
        if expected_forward_exe_sha256 and cached_exe_sha != expected_forward_exe_sha256:
            # 向调用方返回当前步骤生成的数据或迭代结果。
            return None

        local_curve = run_dir / "sphere1_temperature.csv"
        local_history = run_dir / "output" / "temperature_history.csv"
        result_curve_raw = str(result.get("sphere1_temperature_csv") or "").strip()
        # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
        result_history_raw = str(result.get("temperature_history_csv") or "").strip()
        curve_path = local_curve if local_curve.exists() else (Path(result_curve_raw) if result_curve_raw else None)
        history_path = local_history if local_history.exists() else (Path(result_history_raw) if result_history_raw else None)
        # 读取或写入约定的数据文件，并维护统一的路径规则。
        usable = curve_path if (curve_path is not None and curve_path.exists()) else history_path
        if usable is None or not usable.exists():
            return None
        # 真正解析一次，避免把残缺/错误的历史目录当作命中。
        parse_forward_temperature(usable)

        # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
        output_dir = run_dir / "output"
        if not output_dir.exists():
            output_dir = Path(str(result.get("output_dir", "")))
        # 向调用方返回当前步骤生成的数据或迭代结果。
        return {
            "reference_id": "SOURCE_INPUT_REFERENCE",
            "source_input_path": str(source_input),
            # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
            "forward_run_id": str(result.get("run_id", run_dir.name)),
            "candidate_input_path": str(run_dir / "input.dat"),
            "output_dir": str(output_dir),
            "return_code": 0,
            # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
            "elapsed_seconds": 0.0,
            "temperature_history_path": str(history_path) if (history_path is not None and history_path.exists()) else None,
            "temperature_curve_path": str(curve_path) if (curve_path is not None and curve_path.exists()) else str(usable),
            # 读取或写入约定的数据文件，并维护统一的路径规则。
            "infrared_response_history_path": str(run_dir / "output" / "infrared_response_history.csv"),
            "solver_status_path": str(run_dir / "output" / "solver_status.txt"),
            "stdout_path": str(run_dir / "stdout.txt"),
            "stderr_path": str(run_dir / "stderr.txt"),
            # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
            "forward_output_valid": True,
            "run_dir": str(run_dir),
        }
    # 捕获本阶段异常并转换为明确的错误信息或运行状态。
    except Exception:
        return None


def _register_reference_cache(
    *,
    # 读取或写入约定的数据文件，并维护统一的路径规则。
    source_input: Path,
    input_file_sha256: str,
    normalized_request_sha256: str,
    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
    forward_exe_sha256: str,
    reference_run: dict[str, Any],
) -> None:
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    manifest = _load_cache_manifest()
    entries = manifest["entries"]
    run_id = str(reference_run.get("forward_run_id", ""))
    run_dir = Path(str(reference_run.get("candidate_input_path", ""))).parent
    # 检查当前条件，仅在满足约束时执行对应分支。
    if not run_dir.exists():
        out_dir = Path(str(reference_run.get("output_dir", "")))
        run_dir = out_dir.parent if out_dir.name == "output" else out_dir
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    entry = {
        "input_file": str(source_input),
        "input_file_sha256": input_file_sha256,
        "normalized_request_sha256": normalized_request_sha256,
        # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
        "forward_executable_sha256": forward_exe_sha256,
        "forward_run_id": run_id,
        "run_dir": str(run_dir),
        # 调用对应组件完成当前处理步骤，并保留返回结果。
        "output_dir": reference_run.get("output_dir"),
        "temperature_curve_path": reference_run.get("temperature_curve_path"),
        "registered_at_epoch": time.time(),
    }
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    entries = [
        e for e in entries
        if not (
            # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
            str(e.get("input_file_sha256", "")) == input_file_sha256
            and str(e.get("forward_executable_sha256", "")) == forward_exe_sha256
        )
    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
    ]
    entries.append(entry)
    # 只保留最近100条，防止长期调试无限增长。
    manifest["entries"] = entries[-100:]
    _save_cache_manifest(manifest)


# 定义 find_cached_reference 处理过程，集中封装该步骤的输入、输出与异常边界。
def find_cached_reference(
    *,
    source_input: Path,
    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
    source_request: dict[str, Any],
    input_file_sha256: str,
    forward_exe_sha256: str,
) -> tuple[dict[str, Any] | None, str | None]:
    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
    """优先按原始input SHA命中；兼容已有reference_runs时再按规范化请求内容回收。"""
    normalized_sha = canonical_request_sha256(source_request)
    manifest = _load_cache_manifest()
    # 遍历当前数据或迭代计算，逐项更新处理结果。
    for entry in reversed(manifest.get("entries", [])):
        if str(entry.get("input_file_sha256", "")) != input_file_sha256:
            continue
        if str(entry.get("forward_executable_sha256", "")) != forward_exe_sha256:
            # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
            continue
        run_dir = Path(str(entry.get("run_dir", "")))
        ref = _reference_result_from_run_dir(
            # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
            run_dir, source_input,
            expected_request_sha256=normalized_sha,
            expected_forward_exe_sha256=forward_exe_sha256,
        # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
        )
        if ref is not None:
            return ref, "input_file_sha256+forward_executable_sha256"

    # 兼容补丁安装前已经生成的 reference_runs。
    if REFERENCE_RUN_ROOT.exists():
        # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
        run_dirs = sorted(
            [p for p in REFERENCE_RUN_ROOT.glob("run_*") if p.is_dir()],
            key=lambda p: p.name,
            # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
            reverse=True,
        )
        for run_dir in run_dirs:
            ref = _reference_result_from_run_dir(
                # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
                run_dir, source_input,
                expected_request_sha256=normalized_sha,
                expected_forward_exe_sha256=forward_exe_sha256,
            # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
            )
            if ref is not None:
                _register_reference_cache(
                    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
                    source_input=source_input,
                    input_file_sha256=input_file_sha256,
                    normalized_request_sha256=normalized_sha,
                    forward_exe_sha256=forward_exe_sha256,
                    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
                    reference_run=ref,
                )
                return ref, "normalized_request_sha256+forward_executable_sha256(existing_run)"
    # 向调用方返回当前步骤生成的数据或迭代结果。
    return None, None


def load_internal_config(path: Path) -> dict[str, Any]:
    cfg = load_json(path)
    if cfg.get("schema_version") != "scene-search-internal-config-v2":
        # 检测到无效状态后立即报错，防止异常数据继续传播。
        raise ValueError("scene_search_config.json schema_version 必须为 scene-search-internal-config-v2。")
    if "candidate_pool_csv" not in cfg:
        raise ValueError("scene_search_config.json 缺少 candidate_pool_csv。")
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    pre = cfg.get("surrogate_prescreen")
    if not isinstance(pre, dict) or not isinstance(pre.get("thresholds_K"), dict):
        raise ValueError("scene_search_config.json 缺少 surrogate_prescreen.thresholds_K。")
    return cfg


# 定义 load_request 处理过程，集中封装该步骤的输入、输出与异常边界。
def load_request(path: Path) -> dict[str, Any]:
    req = load_json(path)
    if req.get("schema_version") != "scene-search-request-v2":
        # 检测到无效状态后立即报错，防止异常数据继续传播。
        raise ValueError("scene_search_request.json schema_version 必须为 scene-search-request-v2。")
    required = ["input_file", "similarity_requirement", "required_candidate_count"]
    missing = [k for k in required if k not in req]
    # 检查当前条件，仅在满足约束时执行对应分支。
    if missing:
        raise ValueError(f"scene_search_request.json 缺少字段：{missing}")

    sim = req["similarity_requirement"]
    if not isinstance(sim, dict):
        # 检测到无效状态后立即报错，防止异常数据继续传播。
        raise ValueError("similarity_requirement 必须为对象。")
    sim_required = ["metric", "target_object_id", "required_percent"]
    missing_sim = [k for k in sim_required if k not in sim]
    # 检查当前条件，仅在满足约束时执行对应分支。
    if missing_sim:
        raise ValueError(f"similarity_requirement 缺少字段：{missing_sim}")
    if str(sim["metric"]) != "temperature_similarity":
        raise ValueError("当前04正式逆搜索仅支持 similarity_requirement.metric=temperature_similarity。")
    # 检查当前条件，仅在满足约束时执行对应分支。
    if int(sim["target_object_id"]) != 1:
        raise ValueError("当前02温度代理模型和04逆搜索固定以 1 号目标温度为对象，target_object_id 必须为 1。")
    sreq = float(sim["required_percent"])
    # 检查当前条件，仅在满足约束时执行对应分支。
    if not math.isfinite(sreq) or sreq <= 0.0 or sreq > 100.0:
        raise ValueError("similarity_requirement.required_percent 必须在 (0, 100]。")
    count = int(req["required_candidate_count"])
    if count <= 0:
        # 检测到无效状态后立即报错，防止异常数据继续传播。
        raise ValueError("required_candidate_count 必须 > 0。")
    return req


def prediction_times(duration_s: float) -> np.ndarray:
    # 检查当前条件，仅在满足约束时执行对应分支。
    if not math.isfinite(duration_s) or not (0.0 < duration_s <= MAX_SCENE_TIME_S):
        raise ValueError(f"场景构建时间必须大于 0 且不超过 {MAX_SCENE_TIME_S:g} s。")
    regular = np.arange(0.0, duration_s + 1.0e-9, TIME_STEP_S, dtype=float)
    # 检查当前条件，仅在满足约束时执行对应分支。
    if regular.size == 0 or not np.isclose(regular[-1], duration_s):
        regular = np.append(regular, duration_s)
    return regular


def load_similarity_temperature_windows(duration_s: float) -> dict[str, float]:
    # 读取或写入约定的数据文件，并维护统一的路径规则。
    cfg = load_json(SIMILARITY_CONFIG_PATH)
    tc = cfg.get("temperature", {})
    early = tc.get("early_window_s", [0.0, 30.0])
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    plateau = tc.get("plateau_window_s", [100.0, 1000.0])
    if not (isinstance(early, list) and len(early) == 2 and isinstance(plateau, list) and len(plateau) == 2):
        raise ValueError("03 similarity_config.json 温度窗口配置无效。")
    return {
        # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
        "early_start_s": float(early[0]),
        "early_end_s": min(float(early[1]), duration_s),
        "plateau_start_s": min(float(plateau[0]), duration_s),
        # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
        "plateau_end_s": min(float(plateau[1]), duration_s),
    }


def emit_progress(status_path: Path, **payload: Any) -> None:
    payload = {"timestamp": time.time(), **payload}
    # 读取或写入约定的数据文件，并维护统一的路径规则。
    status_path.parent.mkdir(parents=True, exist_ok=True)
    status_path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    print("GUI_PROGRESS " + json.dumps(payload, ensure_ascii=False), flush=True)


# 定义 _new_task_run 处理过程，集中封装该步骤的输入、输出与异常边界。
def _new_task_run() -> tuple[str, Path]:
    RUN_ROOT.mkdir(parents=True, exist_ok=True)
    run_id = datetime.now().strftime("run_%Y%m%d_%H%M%S_") + uuid.uuid4().hex[:8]
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    run_dir = RUN_ROOT / run_id
    run_dir.mkdir(parents=True, exist_ok=False)
    return run_id, run_dir


def _write_latest_run(run_id: str, run_dir: Path, status: str, *, mode: str | None = None) -> None:
    # 读取或写入约定的数据文件，并维护统一的路径规则。
    OUT.mkdir(parents=True, exist_ok=True)
    payload = {
        "module": "04",
        # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
        "run_id": run_id,
        "run_dir": str(run_dir.resolve()),
        "status": status,
        "mode": mode,
        # 调用对应组件完成当前处理步骤，并保留返回结果。
        "timestamp": time.time(),
    }
    LATEST_RUN_PATH.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")


# 定义 validate_shared_input_for_surrogate 处理过程，集中封装该步骤的输入、输出与异常边界。
def validate_shared_input_for_surrogate(source_input: Path) -> dict[str, Any]:
    """确认 input_file 同时满足 01 输入合同和当前 02 冻结代理模型适用域。"""
    if not source_input.exists():
        # 检测到无效状态后立即报错，防止异常数据继续传播。
        raise FileNotFoundError(f"共享 input_file 不存在：{source_input}")
    surrogate_input = load_surrogate_standard_input_module()
    return surrogate_input.load_and_standardize(source_input)


def build_reference_target(stage_g, source_input: Path) -> tuple[dict[str, Any], np.ndarray]:
    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
    """由共享 input_file 调用 01 一次，生成04真正使用的目标温度曲线。"""
    ref = stage_g.run_reference(
        {
            # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
            "reference_id": "SOURCE_INPUT_REFERENCE",
            "source_input_path": str(source_input),
        }
    )
    # 检查当前条件，仅在满足约束时执行对应分支。
    if int(ref.get("return_code", 999)) != 0:
        raise RuntimeError(
            f"共享 input_file 的01基准正向计算失败：return_code={ref.get('return_code')}；"
            # 读取或写入约定的数据文件，并维护统一的路径规则。
            f"stderr={ref.get('stderr_path')}"
        )
    curve_path_raw = ref.get("temperature_curve_path") or ref.get("temperature_history_path")
    if not curve_path_raw:
        # 检测到无效状态后立即报错，防止异常数据继续传播。
        raise RuntimeError("01基准正向计算未返回温度结果路径。")
    target_temp = parse_forward_temperature(Path(curve_path_raw))
    return ref, target_temp


# 定义 load_pool 处理过程，集中封装该步骤的输入、输出与异常边界。
def load_pool(pool_csv: Path) -> pd.DataFrame:
    d = _read_csv_compatible(pool_csv)
    req = ["candidate_id", "q_int", "emissivity_ir", "absorptivity_solar"]
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    missing = [c for c in req if c not in d.columns]
    if missing:
        raise ValueError(f"参数池缺少列：{missing}")
    d = d[req].copy()
    # 检查当前条件，仅在满足约束时执行对应分支。
    if d["candidate_id"].duplicated().any():
        raise ValueError("candidate_id 存在重复，固定顺序合同不成立。")
    for c in req[1:]:
        # 执行数组或表格数据运算，为后续数值处理准备结果。
        d[c] = pd.to_numeric(d[c], errors="raise")
    if d[req[1:]].isna().any().any() or not np.isfinite(d[req[1:]].to_numpy(float)).all():
        raise ValueError("参数池包含 NaN/Inf。")
    ids = d["candidate_id"].astype(str)
    # 检查当前条件，仅在满足约束时执行对应分支。
    if ids.str.fullmatch(r"CH4_C\d{6}").all():
        nums = ids.str.extract(r"(\d{6})", expand=False).astype(int).to_numpy()
        if np.any(np.diff(nums) <= 0):
            # 检测到无效状态后立即报错，防止异常数据继续传播。
            raise ValueError("参数池 candidate_id 不是严格升序；程序不会自行重排。")
    return d.reset_index(drop=True)


def load_m2(cfg: dict[str, Any]):
    model_path = config_path(
        # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
        "temperature_model",
        MODEL_DIR / "formal_configured_target_temperature_extratrees.joblib",
        must_exist=True,
    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
    )
    actual = sha256(model_path)
    expected = str(cfg.get("temperature_model_expected_sha256", "")).strip()
    # 检查当前条件，仅在满足约束时执行对应分支。
    if expected and actual != expected:
        raise RuntimeError(f"M2 模型 SHA256 不匹配：actual={actual}, expected={expected}")
    payload = joblib.load(model_path)
    model = payload["model"]
    # 检查当前条件，仅在满足约束时执行对应分支。
    if hasattr(model, "named_steps") and "impute" in model.named_steps:
        imputer = model.named_steps["impute"]
        if not hasattr(imputer, "_fill_dtype") and hasattr(imputer, "statistics_"):
            # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
            imputer._fill_dtype = imputer.statistics_.dtype
    features = list(payload["features"])
    if features != EXPECTED_FEATURES:
        raise RuntimeError(f"M2 特征合同不匹配：{features}")
    # 向调用方返回当前步骤生成的数据或迭代结果。
    return model, model_path


def predict_m2_candidate(model, row: pd.Series, times: np.ndarray) -> np.ndarray:
    q = float(row.q_int)
    # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
    e = float(row.emissivity_ir)
    a = float(row.absorptivity_solar)
    if e == 0.0:
        raise ValueError("候选 emissivity_ir=0，无法构造 q_int_div_emissivity_ir 特征。")
    # 执行数组或表格数据运算，为后续数值处理准备结果。
    x = pd.DataFrame(
        {
            "q_int": np.full(len(times), q),
            # 执行数组或表格数据运算，为后续数值处理准备结果。
            "emissivity_ir": np.full(len(times), e),
            "absorptivity_solar": np.full(len(times), a),
            "time_s": times,
            # 执行数组或表格数据运算，为后续数值处理准备结果。
            "q_int_times_absorptivity_solar": np.full(len(times), q * a),
            "q_int_div_emissivity_ir": np.full(len(times), q / e),
            "is_initial_condition": (times == 0.0).astype(int),
        }
    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
    )
    y = np.asarray(model.predict(x[EXPECTED_FEATURES]), dtype=float)
    if y.shape != (len(times),) or not np.isfinite(y).all():
        # 检测到无效状态后立即报错，防止异常数据继续传播。
        raise RuntimeError(f"M2 返回的温度曲线不是{len(times)}个有限值。")
    y[0] = 300.0
    return y


def parse_forward_temperature(candidate_csv: Path, expected_times: np.ndarray | None = None) -> np.ndarray:
    # 检查当前条件，仅在满足约束时执行对应分支。
    if not candidate_csv.exists():
        raise FileNotFoundError(f"正向结果不存在：{candidate_csv}")
    df = _read_csv_compatible(candidate_csv)
    # 检查当前条件，仅在满足约束时执行对应分支。
    if {"time_s", "temperature_K"}.issubset(df.columns) and "id" not in df.columns:
        d = df[["time_s", "temperature_K"]].copy()
        t = pd.to_numeric(d["time_s"], errors="raise").to_numpy(float)
        y = pd.to_numeric(d["temperature_K"], errors="raise").to_numpy(float)
    # 检查当前条件，仅在满足约束时执行对应分支。
    elif {"time_s", "T_1"}.issubset(df.columns):
        d = df[["time_s", "T_1"]].copy()
        d["time_s"] = pd.to_numeric(d["time_s"], errors="raise")
        # 执行数组或表格数据运算，为后续数值处理准备结果。
        d["T_1"] = pd.to_numeric(d["T_1"], errors="raise")
        d = d.sort_values("time_s", kind="mergesort")
        t = d["time_s"].to_numpy(float)
        # 执行数组或表格数据运算，为后续数值处理准备结果。
        y = d["T_1"].to_numpy(float)
    elif {"id", "type", "time_s", "temperature_K"}.issubset(df.columns):
        d = df[(pd.to_numeric(df["id"], errors="coerce") == 1) & (df["type"].astype(str).str.upper() == "SPHERE")].copy()
        d = d.sort_values("time_s", kind="mergesort")
        # 执行数组或表格数据运算，为后续数值处理准备结果。
        t = pd.to_numeric(d["time_s"], errors="raise").to_numpy(float)
        y = pd.to_numeric(d["temperature_K"], errors="raise").to_numpy(float)
    else:
        # 检测到无效状态后立即报错，防止异常数据继续传播。
        raise ValueError(f"无法识别正向温度文件字段：{list(df.columns)}")

    if len(y) < 2 or not (np.isfinite(t).all() and np.isfinite(y).all()) or np.any(np.diff(t) <= 0):
        raise ValueError("正向1号目标温度必须包含至少两个按时间递增的有限值。")
    times = prediction_times(float(t[-1])) if expected_times is None else np.asarray(expected_times, dtype=float)
    # 检查当前条件，仅在满足约束时执行对应分支。
    if times[0] < t[0] - 1.0e-9 or times[-1] > t[-1] + 1.0e-9:
        raise ValueError(f"正向1号目标温度未覆盖请求时间 0~{times[-1]:g} s。")
    return np.interp(times, t, y)


# 定义 evaluate_temperature 处理过程，集中封装该步骤的输入、输出与异常边界。
def evaluate_temperature(similarity, windows: dict[str, float], times: np.ndarray, target: np.ndarray, candidate: np.ndarray) -> dict[str, Any]:
    metrics = similarity.evaluate_temperature_curves(times, target, candidate, **windows)
    valid: list[tuple[str, float]] = []
    # 遍历当前数据或迭代计算，逐项更新处理结果。
    for name, field in TEMP_SIM_FIELDS:
        value = float(metrics.get(field, float("nan")))
        if np.isfinite(value):
            valid.append((name, value))
    # 检查当前条件，仅在满足约束时执行对应分支。
    if not valid:
        raise RuntimeError("03 未生成任何有效温度相似度分项。")
    limiting_name, total_similarity = min(valid, key=lambda x: x[1])
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    metrics = dict(metrics)
    metrics["temperature_similarity_percent"] = float(total_similarity)
    metrics["temperature_limiting_feature"] = limiting_name
    metrics["temperature_valid_component_count"] = len(valid)
    # 向调用方返回当前步骤生成的数据或迭代结果。
    return metrics


def threshold_pass(metrics: dict[str, Any], thresholds: dict[str, float]) -> tuple[bool, list[str]]:
    failed: list[str] = []
    # 遍历当前数据或迭代计算，逐项更新处理结果。
    for key, limit in thresholds.items():
        if key not in metrics:
            failed.append(f"missing:{key}")
            continue
        # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
        value = float(metrics[key])
        if not np.isfinite(value) or value > float(limit):
            failed.append(f"{key}={value:.6g}>{float(limit):.6g}")
    # 向调用方返回当前步骤生成的数据或迭代结果。
    return len(failed) == 0, failed


def temperature_margin(reference_temperature: np.ndarray, required_similarity_percent: float) -> tuple[float, float]:
    rt = float(np.max(reference_temperature) - np.min(reference_temperature))
    # 检查当前条件，仅在满足约束时执行对应分支。
    if not math.isfinite(rt) or rt <= 0.0:
        raise ValueError("目标温度全过程变化幅度 R_T <= 0；当前04 v1不能为近似恒温目标自动定义相似度裕度。")
    margin = rt * (100.0 / required_similarity_percent - 1.0)
    return rt, float(margin)


# 定义 _metric_columns 处理过程，集中封装该步骤的输入、输出与异常边界。
def _metric_columns(prefix: str, metrics: dict[str, Any]) -> dict[str, Any]:
    out: dict[str, Any] = {
        f"{prefix}_rmse_K": float(metrics["rmse"]),
        # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
        f"{prefix}_mae_K": float(metrics["mae"]),
        f"{prefix}_max_abs_error_K": float(metrics["max_abs_error"]),
        f"{prefix}_final_abs_error_K": float(metrics["final_abs_error"]),
        f"{prefix}_plateau_abs_error_K": float(metrics["plateau_abs_error"]),
        # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
        f"{prefix}_early_abs_error_K": float(metrics["early_abs_error"]),
        f"{prefix}_temperature_similarity_percent": float(metrics["temperature_similarity_percent"]),
        f"{prefix}_temperature_limiting_feature": str(metrics["temperature_limiting_feature"]),
    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
    }
    trend = float(metrics.get("temperature_trend_similarity_percent", float("nan")))
    out[f"{prefix}_temperature_trend_similarity_percent"] = trend if np.isfinite(trend) else np.nan
    return out


# 定义 write_outputs 处理过程，集中封装该步骤的输入、输出与异常边界。
def write_outputs(
    output_dir: Path,
    log_rows: list[dict[str, Any]],
    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
    valid_rows: list[dict[str, Any]],
    valid_curves: list[dict[str, Any]],
    summary: dict[str, Any],
    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
    request: dict[str, Any],
) -> None:
    output_dir.mkdir(parents=True, exist_ok=True)
    log_df = pd.DataFrame(log_rows)
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    candidate_columns = [
        "solution_index", "sequence_index", "target_id", "source_case_id", "candidate_id",
        "q_int", "emissivity_ir", "absorptivity_solar",
        # 校验输入数据及运行前提，提前拒绝不符合契约的参数。
        "required_temperature_similarity_percent", "target_temperature_range_K", "temperature_margin_K",
        "proxy_rmse_K", "proxy_mae_K", "proxy_max_abs_error_K", "proxy_final_abs_error_K",
        "proxy_plateau_abs_error_K", "proxy_early_abs_error_K",
        "proxy_temperature_similarity_percent", "proxy_temperature_limiting_feature",
        # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
        "proxy_temperature_trend_similarity_percent",
        "forward_rmse_K", "forward_mae_K", "forward_max_abs_error_K", "forward_final_abs_error_K",
        "forward_plateau_abs_error_K", "forward_early_abs_error_K",
        # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
        "forward_temperature_similarity_percent", "forward_temperature_limiting_feature",
        "forward_temperature_trend_similarity_percent", "forward_level_margin_pass", "forward_run_dir",
    ]
    valid_df = pd.DataFrame(valid_rows, columns=candidate_columns)
    # 执行数组或表格数据运算，为后续数值处理准备结果。
    curve_df = pd.DataFrame(valid_curves)

    log_df.to_csv(output_dir / "scene_search_log.csv", index=False, encoding="utf-8-sig")
    valid_df.to_csv(output_dir / "candidate_parameters.csv", index=False, encoding="utf-8-sig")
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    curve_columns = [
        "solution_index", "target_id", "source_case_id", "candidate_id", "time_s",
        "target_temperature_K", "m2_temperature_K", "forward_temperature_K",
    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
    ]
    if curve_df.empty:
        curve_df = pd.DataFrame(columns=curve_columns)
    curve_df.to_csv(output_dir / "candidate_temperature_curves.csv", index=False, encoding="utf-8-sig")

    # 调用对应组件完成当前处理步骤，并保留返回结果。
    jwrite(output_dir / "scene_search_summary.json", summary)
    jwrite(output_dir / "scene_search_request_used.json", request)


def parse_args() -> argparse.Namespace:
    # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
    ap = argparse.ArgumentParser(description="04相似度逆应用：共享input -> 01生成目标 -> 02代理精度预筛 -> 01候选复核 -> 03温度相似度验收")
    ap.add_argument("--request", type=Path, default=DEFAULT_REQUEST_PATH, help="用户业务输入：共享input文件、相似度要求、候选数量")
    ap.add_argument("--config", type=Path, default=DEFAULT_CONFIG_PATH, help="内部运行配置：参数池和02模型预筛精度")
    ap.add_argument("--required-temperature-similarity", type=float, default=None, help="测试覆盖 similarity_requirement.required_percent（百分比）")
    # 校验输入数据及运行前提，提前拒绝不符合契约的参数。
    ap.add_argument("--required-valid-count", type=int, default=None, help="兼容/测试覆盖 request 中的候选数量")
    ap.add_argument("--max-candidates", type=int, default=None, help="仅用于受控测试；正式运行默认遍历直到获得足量候选或参数池耗尽")
    ap.add_argument("--proxy-only", action="store_true", help="取得01基准目标后，仅验证02代理预筛；不对候选逐个做01正向复核")
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    ap.add_argument("--no-reference-cache", action="store_true", help="强制重新运行01生成基准目标，不复用 reference_runs 缓存")
    # 保留旧命令行参数，避免历史脚本报 unknown argument；新正式流程不自动运行点图像阶段。
    ap.add_argument("--skip-point-image", action="store_true", help=argparse.SUPPRESS)
    ap.add_argument("--require-point-image", action="store_true", help=argparse.SUPPRESS)
    # 向调用方返回当前步骤生成的数据或迭代结果。
    return ap.parse_args()


def main() -> int:
    global ACTIVE_RUN_ID, ACTIVE_RUN_DIR

    ensure()
    # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
    args = parse_args()
    start = time.time()
    run_id, task_dir = _new_task_run()
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    ACTIVE_RUN_ID, ACTIVE_RUN_DIR = run_id, task_dir
    mode_name = "proxy_only" if args.proxy_only else "full"
    status_path = task_dir / "scene_search_status.json"
    _write_latest_run(run_id, task_dir, "running", mode=mode_name)

    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    cfg = load_internal_config(args.config.resolve())
    request = load_request(args.request.resolve())

    sim_req = request["similarity_requirement"]
    # 校验输入数据及运行前提，提前拒绝不符合契约的参数。
    required_similarity = float(
        args.required_temperature_similarity
        if args.required_temperature_similarity is not None
        else sim_req["required_percent"]
    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
    )
    if required_similarity <= 0.0 or required_similarity > 100.0:
        raise ValueError("要求温度相似度必须在 (0,100]%。")
    # 校验输入数据及运行前提，提前拒绝不符合契约的参数。
    required_count = int(
        args.required_valid_count
        if args.required_valid_count is not None
        # 校验输入数据及运行前提，提前拒绝不符合契约的参数。
        else request["required_candidate_count"]
    )
    if required_count <= 0:
        raise ValueError("required_candidate_count 必须 > 0。")

    # 04 的目标不再来自独立CSV，而来自与01/02共用的完整 input_file。
    # 读取或写入约定的数据文件，并维护统一的路径规则。
    source_input = package_path(request["input_file"])
    standardized_source = validate_shared_input_for_surrogate(source_input)
    source_request = standardized_source["full_request"]
    # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
    duration_s = float(source_request["CASE"]["TOTAL_TIME"])
    times = prediction_times(duration_s)
    target_id = "object_1"
    source_case_id = source_input.stem

    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    stage_g = load_stage_g_module()
    forward_exe_path = config_path("forward_exe", FORWARD_PROGRAM_DIR / "production_main_output_interface.exe", must_exist=True)
    forward_exe_sha = sha256(forward_exe_path)
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    input_file_sha = sha256(source_input)
    normalized_request_sha = canonical_request_sha256(source_request)

    reference_cache_hit = False
    reference_cache_match_basis: str | None = None
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    reference_phase_start = time.time()
    emit_progress(
        status_path,
        # 校验输入数据及运行前提，提前拒绝不符合契约的参数。
        state="checking_reference_cache",
        input_file=str(source_input),
        input_file_sha256=input_file_sha,
        # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
        forward_executable_sha256=forward_exe_sha,
        similarity_metric=str(sim_req["metric"]),
        required_temperature_similarity_percent=required_similarity,
        required_valid_count=required_count,
    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
    )

    reference_run = None
    if not args.no_reference_cache:
        # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
        reference_run, reference_cache_match_basis = find_cached_reference(
            source_input=source_input,
            source_request=source_request,
            input_file_sha256=input_file_sha,
            # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
            forward_exe_sha256=forward_exe_sha,
        )

    if reference_run is not None:
        # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
        reference_cache_hit = True
        curve_path_raw = reference_run.get("temperature_curve_path") or reference_run.get("temperature_history_path")
        if not curve_path_raw:
            raise RuntimeError("reference cache命中但未返回温度结果路径。")
        # 读取或写入约定的数据文件，并维护统一的路径规则。
        target_temp = parse_forward_temperature(Path(curve_path_raw), times)
        emit_progress(
            status_path,
            # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
            state="reference_cache_hit",
            input_file=str(source_input),
            input_file_sha256=input_file_sha,
            # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
            reference_forward_run_id=reference_run.get("forward_run_id"),
            reference_cache_match_basis=reference_cache_match_basis,
            reference_temperature_curve_path=str(curve_path_raw),
        )
    # 前置条件不成立时进入备用处理路径。
    else:
        emit_progress(
            status_path,
            # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
            state="building_reference_target",
            input_file=str(source_input),
            input_file_sha256=input_file_sha,
            similarity_metric=str(sim_req["metric"]),
            # 校验输入数据及运行前提，提前拒绝不符合契约的参数。
            required_temperature_similarity_percent=required_similarity,
            required_valid_count=required_count,
        )
        # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
        reference_run, target_temp = build_reference_target(stage_g, source_input)
        reference_curve = reference_run.get("temperature_curve_path") or reference_run.get("temperature_history_path")
        if not reference_curve:
            # 检测到无效状态后立即报错，防止异常数据继续传播。
            raise RuntimeError("新建参考正向计算未返回温度结果路径。")
        target_temp = parse_forward_temperature(Path(reference_curve), times)
        _register_reference_cache(
            source_input=source_input,
            # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
            input_file_sha256=input_file_sha,
            normalized_request_sha256=normalized_request_sha,
            forward_exe_sha256=forward_exe_sha,
            # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
            reference_run=reference_run,
        )
        reference_cache_match_basis = "new_reference_run_registered"

    reference_phase_elapsed = time.time() - reference_phase_start
    # 校验输入数据及运行前提，提前拒绝不符合契约的参数。
    target_range_K, margin_K = temperature_margin(target_temp, required_similarity)

    proxy_thresholds = {k: float(v) for k, v in cfg["surrogate_prescreen"]["thresholds_K"].items()}
    pool_csv = package_path(cfg["candidate_pool_csv"])
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    pool_df = load_pool(pool_csv)
    if args.max_candidates is not None:
        if args.max_candidates <= 0:
            raise ValueError("--max-candidates 必须 > 0。")
        # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
        pool_df = pool_df.iloc[: args.max_candidates].copy()

    similarity = load_similarity_module()
    similarity_windows = load_similarity_temperature_windows(duration_s)
    # 读取或写入约定的数据文件，并维护统一的路径规则。
    model, model_path = load_m2(cfg)

    log_rows: list[dict[str, Any]] = []
    valid_rows: list[dict[str, Any]] = []
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    valid_curves: list[dict[str, Any]] = []
    proxy_pass_count = 0
    forward_attempt_count = 0

    emit_progress(
        # 读取或写入约定的数据文件，并维护统一的路径规则。
        status_path,
        state="running",
        mode="proxy_only" if args.proxy_only else "full",
        # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
        target_id=target_id,
        source_case_id=source_case_id,
        input_file=str(source_input),
        required_temperature_similarity_percent=required_similarity,
        # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
        target_temperature_range_K=target_range_K,
        temperature_margin_K=margin_K,
        current_candidate=0,
        # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
        candidates_total=len(pool_df),
        proxy_pass_count=0,
        forward_attempt_count=0,
        valid_count=0,
        # 校验输入数据及运行前提，提前拒绝不符合契约的参数。
        required_valid_count=required_count,
    )

    for seq, row in enumerate(pool_df.itertuples(index=False), start=1):
        # 执行数组或表格数据运算，为后续数值处理准备结果。
        row_s = pd.Series(row._asdict())
        m2_temp = predict_m2_candidate(model, row_s, times)
        proxy_metrics = evaluate_temperature(similarity, similarity_windows, times, target_temp, m2_temp)

        # 代理初筛只使用02独立验证精度，不读取 required_similarity。
        # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
        proxy_pass, proxy_failed = threshold_pass(proxy_metrics, proxy_thresholds)
        if proxy_pass:
            proxy_pass_count += 1

        record: dict[str, Any] = {
            # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
            "sequence_index": seq,
            "target_id": target_id,
            "source_case_id": source_case_id,
            # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
            "candidate_id": str(row.candidate_id),
            "q_int": float(row.q_int),
            "emissivity_ir": float(row.emissivity_ir),
            "absorptivity_solar": float(row.absorptivity_solar),
            # 校验输入数据及运行前提，提前拒绝不符合契约的参数。
            "required_temperature_similarity_percent": required_similarity,
            "target_temperature_range_K": target_range_K,
            "temperature_margin_K": margin_K,
            # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
            "proxy_rule_source": str(cfg["surrogate_prescreen"].get("source", "")),
            "proxy_pass": bool(proxy_pass),
            "proxy_failed_conditions": ";".join(proxy_failed),
            **_metric_columns("proxy", proxy_metrics),
            # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
            "forward_attempted": False,
            "forward_output_valid": False,
            "forward_pass": False,
            # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
            "forward_failed_conditions": "",
            "forward_return_code": "",
            "forward_elapsed_seconds": "",
            # 调用对应组件完成当前处理步骤，并保留返回结果。
            "accepted_count_after_candidate": len(valid_rows),
            "decision": "proxy_reject" if not proxy_pass else "proxy_pass",
        }

        if proxy_pass and not args.proxy_only:
            # 校验输入数据及运行前提，提前拒绝不符合契约的参数。
            assert stage_g is not None
            forward_attempt_count += 1
            run_payload = {
                # 校验输入数据及运行前提，提前拒绝不符合契约的参数。
                "recheck_id": f"SEARCH_{seq:06d}_{row.candidate_id}",
                "target_id": target_id,
                "source_case_id": source_case_id,
                "candidate_id": str(row.candidate_id),
                # 读取或写入约定的数据文件，并维护统一的路径规则。
                "source_input_path": str(source_input),
                "run_root": str(task_dir / "forward_runs"),
                "q_int": float(row.q_int),
                # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
                "emissivity_ir": float(row.emissivity_ir),
                "absorptivity_solar": float(row.absorptivity_solar),
            }
            # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
            frun = stage_g.run_one(run_payload)
            record["forward_attempted"] = True
            record["forward_return_code"] = int(frun["return_code"])
            record["forward_elapsed_seconds"] = float(frun["elapsed_seconds"])
            # 执行可能失败的操作，并由后续分支统一处理异常。
            try:
                if int(frun["return_code"]) != 0:
                    raise RuntimeError(f"forward return_code={frun['return_code']}")
                # 读取或写入约定的数据文件，并维护统一的路径规则。
                curve_path_raw = frun.get("temperature_curve_path") or frun.get("temperature_history_path")
                if not curve_path_raw:
                    raise RuntimeError("01统一入口未返回温度结果路径。")
                ftemp = parse_forward_temperature(Path(curve_path_raw), times)
                # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
                record["forward_output_valid"] = True

                forward_metrics = evaluate_temperature(similarity, similarity_windows, times, target_temp, ftemp)
                fsim = float(forward_metrics["temperature_similarity_percent"])
                # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
                flim = str(forward_metrics["temperature_limiting_feature"])
                # 最终验收只看用户输入的温度相似度要求；03定义是唯一判据。
                fpass = bool(np.isfinite(fsim) and fsim >= required_similarity)
                ffailed = [] if fpass else [f"temperature_similarity={fsim:.6g}%<{required_similarity:.6g}%"]

                record.update(_metric_columns("forward", forward_metrics))
                # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
                record["forward_level_margin_pass"] = bool(float(forward_metrics["max_abs_error"]) <= margin_K + 1e-12)
                record["forward_pass"] = fpass
                record["forward_failed_conditions"] = ";".join(ffailed)
                # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
                record["decision"] = "accepted" if fpass else "forward_reject"

                if fpass:
                    valid_index = len(valid_rows) + 1
                    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
                    accepted = {
                        "solution_index": valid_index,
                        "sequence_index": seq,
                        "target_id": target_id,
                        # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
                        "source_case_id": source_case_id,
                        "candidate_id": str(row.candidate_id),
                        "q_int": float(row.q_int),
                        # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
                        "emissivity_ir": float(row.emissivity_ir),
                        "absorptivity_solar": float(row.absorptivity_solar),
                        "required_temperature_similarity_percent": required_similarity,
                        "target_temperature_range_K": target_range_K,
                        # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
                        "temperature_margin_K": margin_K,
                        **_metric_columns("proxy", proxy_metrics),
                        **_metric_columns("forward", forward_metrics),
                        # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
                        "forward_level_margin_pass": bool(float(forward_metrics["max_abs_error"]) <= margin_K + 1e-12),
                        "forward_run_dir": str(Path(frun["candidate_input_path"]).parent) if frun.get("candidate_input_path") else str(frun.get("output_dir", "")),
                    }
                    valid_rows.append(accepted)
                    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
                    record["accepted_count_after_candidate"] = len(valid_rows)
                    valid_curves.extend(
                        {
                            # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
                            "solution_index": valid_index,
                            "target_id": target_id,
                            "source_case_id": source_case_id,
                            # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
                            "candidate_id": str(row.candidate_id),
                            "time_s": float(tt),
                            "target_temperature_K": float(yt),
                            "m2_temperature_K": float(mt),
                            # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
                            "forward_temperature_K": float(ft),
                        }
                        for tt, yt, mt, ft in zip(times, target_temp, m2_temp, ftemp)
                    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
                    )
            except Exception as exc:
                record["decision"] = "forward_blocked"
                record["forward_failed_conditions"] = f"{type(exc).__name__}: {exc}"

        # 记录当前阶段的状态，便于调用方反馈进度并定位问题。
        log_rows.append(record)
        pd.DataFrame(log_rows).to_csv(task_dir / "scene_search_log.csv", index=False, encoding="utf-8-sig")
        if valid_rows:
            # 执行数组或表格数据运算，为后续数值处理准备结果。
            pd.DataFrame(valid_rows).to_csv(task_dir / "candidate_parameters.csv", index=False, encoding="utf-8-sig")

        emit_progress(
            status_path,
            state="running",
            # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
            mode="proxy_only" if args.proxy_only else "full",
            target_id=target_id,
            required_temperature_similarity_percent=required_similarity,
            # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
            temperature_margin_K=margin_K,
            current_candidate=seq,
            current_candidate_id=str(row.candidate_id),
            # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
            candidates_total=len(pool_df),
            proxy_pass_count=proxy_pass_count,
            forward_attempt_count=forward_attempt_count,
            valid_count=len(valid_rows),
            # 校验输入数据及运行前提，提前拒绝不符合契约的参数。
            required_valid_count=required_count,
            last_decision=record["decision"],
        )

        # 检查当前条件，仅在满足约束时执行对应分支。
        if (not args.proxy_only) and len(valid_rows) >= required_count:
            break

    evaluated_count = len(log_rows)
    if args.proxy_only:
        # 记录当前阶段的状态，便于调用方反馈进度并定位问题。
        search_status = "ProxyOnlyCompleted"
    elif len(valid_rows) >= required_count:
        search_status = "Completed"
    # 前置条件不成立时进入备用处理路径。
    else:
        search_status = "Controlled incomplete"

    effective_request = json.loads(json.dumps(request, ensure_ascii=False))
    # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
    effective_request["input_file_resolved"] = str(source_input)
    effective_request["input_file_sha256"] = input_file_sha
    effective_request["similarity_requirement"]["required_percent"] = required_similarity
    effective_request["required_candidate_count"] = required_count

    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    summary = {
        "schema_version": "scene-search-summary-v2",
        "run_id": run_id,
        # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
        "run_dir": str(task_dir.resolve()),
        "status": search_status,
        "target_id": target_id,
        "source_case_id": source_case_id,
        # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
        "input_file": str(source_input),
        "input_file_sha256": input_file_sha,
        "input_contract": "forward-request-v1",
        # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
        "normalized_request_sha256": normalized_request_sha,
        "reference_cache_hit": reference_cache_hit,
        "reference_cache_match_basis": reference_cache_match_basis,
        "reference_cache_manifest": str(REFERENCE_CACHE_MANIFEST),
        # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
        "reference_phase_elapsed_seconds": reference_phase_elapsed,
        "similarity_metric": str(sim_req["metric"]),
        "target_object_id": int(sim_req["target_object_id"]),
        # 调用对应组件完成当前处理步骤，并保留返回结果。
        "reference_forward_run_id": reference_run.get("forward_run_id"),
        "reference_forward_output_dir": reference_run.get("output_dir"),
        "reference_temperature_curve_path": reference_run.get("temperature_curve_path") or reference_run.get("temperature_history_path"),
        # 校验输入数据及运行前提，提前拒绝不符合契约的参数。
        "required_temperature_similarity_percent": required_similarity,
        "target_temperature_range_K": target_range_K,
        "scene_duration_s": duration_s,
        "temperature_margin_K": margin_K,
        # 校验输入数据及运行前提，提前拒绝不符合契约的参数。
        "margin_formula": "D_allow = R_T * (100 / S_required - 1)",
        "temperature_similarity_definition": "03 module; minimum valid temperature component similarity",
        "required_candidate_count": required_count,
        # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
        "evaluated_candidate_count": evaluated_count,
        "proxy_pass_count": proxy_pass_count,
        "forward_attempt_count": forward_attempt_count,
        "forward_confirmed_valid_count": len(valid_rows),
        # 校验输入数据及运行前提，提前拒绝不符合契约的参数。
        "stopped_early": bool((not args.proxy_only) and len(valid_rows) >= required_count and evaluated_count < len(pool_df)),
        "candidate_order": str(cfg.get("candidate_order", "input file order; no sorting or ranking")),
        "surrogate_prescreen_source": str(cfg["surrogate_prescreen"].get("source", "")),
        # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
        "surrogate_prescreen_rule": str(cfg["surrogate_prescreen"].get("rule", "")),
        "surrogate_prescreen_thresholds_K": proxy_thresholds,
        "surrogate_prescreen_independent_of_user_similarity": True,
        "m2_model": str(model_path),
        # 读取或写入约定的数据文件，并维护统一的路径规则。
        "m2_model_sha256": sha256(model_path),
        "forward_executable": None if forward_exe_path is None else str(forward_exe_path),
        "forward_executable_sha256": forward_exe_sha,
        # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
        "forward_executable_sha_policy": str(cfg.get("forward_executable_sha_policy", "record_only")),
        "point_image_in_default_acceptance_chain": False,
        "elapsed_seconds": time.time() - start,
        # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
        "mode": "proxy_only" if args.proxy_only else "full",
        "search_rule": "shared input_file -> cached/executed 01 reference target -> 02 accuracy-based pre-screen -> candidate 01 forward recheck on same scene -> 03 temperature similarity >= user requirement -> keep candidate",
    }

    write_outputs(task_dir, log_rows, valid_rows, valid_curves, summary, effective_request)
    # 调用对应组件完成当前处理步骤，并保留返回结果。
    jwrite(task_dir / "source_forward_request_used.json", source_request)
    emit_progress(status_path, state="finished", **summary)
    _write_latest_run(run_id, task_dir, "success" if search_status in {"Completed", "ProxyOnlyCompleted"} else "incomplete", mode=mode_name)
    # 读取或写入约定的数据文件，并维护统一的路径规则。
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    return 0 if search_status in {"Completed", "ProxyOnlyCompleted"} else 3


if __name__ == "__main__":
    try:
        # 检测到无效状态后立即报错，防止异常数据继续传播。
        raise SystemExit(main())
    except SystemExit:
        raise
    # 捕获本阶段异常并转换为明确的错误信息或运行状态。
    except Exception as exc:
        err = {
            "state": "error",
            "run_id": ACTIVE_RUN_ID,
            # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
            "run_dir": None if ACTIVE_RUN_DIR is None else str(ACTIVE_RUN_DIR.resolve()),
            "error_type": type(exc).__name__,
            "message": str(exc),
        # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
        }
        try:
            if ACTIVE_RUN_DIR is not None and ACTIVE_RUN_ID is not None:
                # 记录当前阶段的状态，便于调用方反馈进度并定位问题。
                emit_progress(ACTIVE_RUN_DIR / "scene_search_status.json", **err)
                _write_latest_run(ACTIVE_RUN_ID, ACTIVE_RUN_DIR, "error")
        except Exception:
            pass
        # 读取或写入约定的数据文件，并维护统一的路径规则。
        print(json.dumps(err, ensure_ascii=False), file=sys.stderr)
        raise SystemExit(2)
