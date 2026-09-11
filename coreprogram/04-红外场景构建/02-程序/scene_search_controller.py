from __future__ import annotations

"""04-红外场景构建：温度相似度逆应用统一入口。

正式业务逻辑
------------
用户业务输入由三部分组成：
1) 与 01/02 共用的完整 forward-request-v1 输入文件；
2) 相似度要求（当前正式支持温度综合相似度）；
3) 需要保留的候选参数数量。

04 不再要求用户额外提供孤立的目标温度曲线。程序先调用 01 对 input_file
执行一次基准正向计算，得到 1 号目标温度曲线，然后进入逆搜索。

搜索链：
共享 input_file
 -> 复用同 input/同正向EXE的01基准缓存；无缓存时才运行01生成目标温度曲线
 -> 固定参数池原始顺序
 -> 02 温度代理模型快速预测
 -> 按 02 模型独立验证精度进行代理预筛（与用户输入相似度无关）
 -> 对预筛通过候选，在同一 input_file 场景基础上仅覆盖 q_int/eps_ir/alpha_s
 -> 01 正向复核
 -> 使用 03 的温度相似度定义进行最终复核
 -> 正向温度相似度 >= 用户要求时保留候选
 -> 达到规定候选数量后停止。

说明：
- input_file 是 01、02 已统一采用的完整六分区 JSON，不另设目标温度 CSV。
- reference_runs 使用 input SHA + 正向EXE SHA 缓存，同一目标工况重复搜索不再重复计算基准01。
- 用户相似度要求只用于最终正向复核，不参与代理预筛。
- 程序同时将用户相似度换算为温度特征裕度 D_allow，用于解释和结果记录。
- 候选复核继承 input_file 中的环境、运动、观测、非搜索物性等全部设置。
- 当前代理模型只允许统一搜索 q_int、eps_ir、alpha_s，rho_ir 同步取 1-eps_ir。
- 不做 Top-K 排名，不自动放宽条件。
"""

import argparse
import hashlib
import importlib.util
import json
import math
import sys
import time
import uuid
from datetime import datetime
from pathlib import Path
from typing import Any

import joblib
import numpy as np
import pandas as pd

from stage_utils import (
    FORWARD_PROGRAM_DIR,
    INPUT_DIR,
    MODEL_DIR,
    OUT,
    PACKAGE_ROOT,
    PROGRAM_DIR,
    config_path,
    ensure,
    jwrite,
    sha256,
)

EXPECTED_FEATURES = [
    "q_int",
    "emissivity_ir",
    "absorptivity_solar",
    "time_s",
    "q_int_times_absorptivity_solar",
    "q_int_div_emissivity_ir",
    "is_initial_condition",
]
TIMES = np.arange(0.0, 1000.0 + 10.0, 10.0, dtype=float)

DEFAULT_CONFIG_PATH = INPUT_DIR / "scene_search_config.json"
DEFAULT_REQUEST_PATH = INPUT_DIR / "scene_search_request.json"
SIMILARITY_CONFIG_PATH = PACKAGE_ROOT / "03-相似度评估" / "01-输入文件" / "similarity_config.json"
RUN_ROOT = OUT / "runs"
LATEST_RUN_PATH = OUT / "latest_run.json"
REFERENCE_RUN_ROOT = OUT / "reference_runs"
REFERENCE_CACHE_MANIFEST = REFERENCE_RUN_ROOT / "reference_cache_manifest.json"

ACTIVE_RUN_ID: str | None = None
ACTIVE_RUN_DIR: Path | None = None

TEMP_SIM_FIELDS = [
    ("temperature_rmse", "rmse_similarity_percent"),
    ("temperature_mae", "mae_similarity_percent"),
    ("temperature_max_abs", "max_similarity_percent"),
    ("temperature_final_abs", "final_similarity_percent"),
    ("temperature_plateau_abs", "plateau_similarity_percent"),
    ("temperature_early_abs", "early_similarity_percent"),
    ("temperature_rate_trend", "temperature_trend_similarity_percent"),
]


def _load_py_module(name: str, path: Path):
    if not path.exists():
        raise FileNotFoundError(path)
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise ImportError(f"无法加载模块：{path}")
    mod = importlib.util.module_from_spec(spec)
    sys.modules[name] = mod
    spec.loader.exec_module(mod)
    return mod


def load_similarity_module():
    p = PACKAGE_ROOT / "03-相似度评估" / "02-程序" / "similarity_evaluator.py"
    mod = _load_py_module("_scene_search_similarity_evaluator", p)
    if not hasattr(mod, "evaluate_temperature_curves"):
        raise RuntimeError("03 similarity_evaluator.py 缺少 evaluate_temperature_curves；请先安装03第一版相似度补丁。")
    return mod


def load_stage_g_module():
    return _load_py_module("_scene_search_stage_g", PROGRAM_DIR / "stage_g_run_source_case_forward_recheck_v1.py")


def load_surrogate_standard_input_module():
    p = PACKAGE_ROOT / "02-智能预测" / "02-程序" / "surrogate_standard_input.py"
    mod = _load_py_module("_scene_search_surrogate_standard_input", p)
    if not hasattr(mod, "load_and_standardize"):
        raise RuntimeError("02 surrogate_standard_input.py 缺少 load_and_standardize；请先安装02规范输入补丁。")
    return mod


def package_path(raw: str | Path) -> Path:
    p = Path(raw)
    return p.resolve() if p.is_absolute() else (PACKAGE_ROOT / p).resolve()


def _read_csv_compatible(path: Path) -> pd.DataFrame:
    last: Exception | None = None
    for encoding in ("utf-8-sig", "utf-8", "gb18030", "cp936"):
        try:
            return pd.read_csv(path, encoding=encoding)
        except UnicodeDecodeError as exc:
            last = exc
    raise UnicodeDecodeError("utf-8", b"", 0, 1, f"无法读取CSV编码：{path}; last={last}")


def load_json(path: Path) -> dict[str, Any]:
    if not path.exists():
        raise FileNotFoundError(path)
    obj = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(obj, dict):
        raise ValueError(f"JSON 顶层必须为对象：{path}")
    return obj


def canonical_request_sha256(request: dict[str, Any]) -> str:
    """对已经规范化的请求计算与排版无关的内容哈希。"""
    raw = json.dumps(
        request, ensure_ascii=False, sort_keys=True, separators=(",", ":")
    ).encode("utf-8")
    return hashlib.sha256(raw).hexdigest()


def _load_cache_manifest() -> dict[str, Any]:
    if not REFERENCE_CACHE_MANIFEST.exists():
        return {"schema_version": "reference-cache-v1", "entries": []}
    try:
        obj = load_json(REFERENCE_CACHE_MANIFEST)
    except Exception:
        return {"schema_version": "reference-cache-v1", "entries": []}
    entries = obj.get("entries", [])
    if not isinstance(entries, list):
        entries = []
    return {"schema_version": "reference-cache-v1", "entries": entries}


def _save_cache_manifest(manifest: dict[str, Any]) -> None:
    REFERENCE_RUN_ROOT.mkdir(parents=True, exist_ok=True)
    manifest["schema_version"] = "reference-cache-v1"
    REFERENCE_CACHE_MANIFEST.write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8"
    )


def _reference_result_from_run_dir(
    run_dir: Path,
    source_input: Path,
    *,
    expected_request_sha256: str,
    expected_forward_exe_sha256: str,
) -> dict[str, Any] | None:
    """验证既有 reference run，成功时转换成与 stage_g.run_reference 一致的结果。"""
    run_dir = run_dir.resolve()
    request_path = run_dir / "request.json"
    result_path = run_dir / "result.json"
    if not request_path.exists() or not result_path.exists():
        return None
    try:
        cached_request = load_json(request_path)
        if canonical_request_sha256(cached_request) != expected_request_sha256:
            return None
        result = load_json(result_path)
        if int(result.get("return_code", 999)) != 0 or not bool(result.get("output_valid", False)):
            return None
        cached_exe_sha = str(result.get("forward_exe_sha256", "")).strip()
        if expected_forward_exe_sha256 and cached_exe_sha != expected_forward_exe_sha256:
            return None

        local_curve = run_dir / "sphere1_temperature.csv"
        local_history = run_dir / "output" / "temperature_history.csv"
        result_curve_raw = str(result.get("sphere1_temperature_csv") or "").strip()
        result_history_raw = str(result.get("temperature_history_csv") or "").strip()
        curve_path = local_curve if local_curve.exists() else (Path(result_curve_raw) if result_curve_raw else None)
        history_path = local_history if local_history.exists() else (Path(result_history_raw) if result_history_raw else None)
        usable = curve_path if (curve_path is not None and curve_path.exists()) else history_path
        if usable is None or not usable.exists():
            return None
        # 真正解析一次，避免把残缺/错误的历史目录当作命中。
        parse_forward_temperature(usable)

        output_dir = run_dir / "output"
        if not output_dir.exists():
            output_dir = Path(str(result.get("output_dir", "")))
        return {
            "reference_id": "SOURCE_INPUT_REFERENCE",
            "source_input_path": str(source_input),
            "forward_run_id": str(result.get("run_id", run_dir.name)),
            "candidate_input_path": str(run_dir / "input.dat"),
            "output_dir": str(output_dir),
            "return_code": 0,
            "elapsed_seconds": 0.0,
            "temperature_history_path": str(history_path) if (history_path is not None and history_path.exists()) else None,
            "temperature_curve_path": str(curve_path) if (curve_path is not None and curve_path.exists()) else str(usable),
            "infrared_response_history_path": str(run_dir / "output" / "infrared_response_history.csv"),
            "solver_status_path": str(run_dir / "output" / "solver_status.txt"),
            "stdout_path": str(run_dir / "stdout.txt"),
            "stderr_path": str(run_dir / "stderr.txt"),
            "forward_output_valid": True,
            "run_dir": str(run_dir),
        }
    except Exception:
        return None


def _register_reference_cache(
    *,
    source_input: Path,
    input_file_sha256: str,
    normalized_request_sha256: str,
    forward_exe_sha256: str,
    reference_run: dict[str, Any],
) -> None:
    manifest = _load_cache_manifest()
    entries = manifest["entries"]
    run_id = str(reference_run.get("forward_run_id", ""))
    run_dir = Path(str(reference_run.get("candidate_input_path", ""))).parent
    if not run_dir.exists():
        out_dir = Path(str(reference_run.get("output_dir", "")))
        run_dir = out_dir.parent if out_dir.name == "output" else out_dir
    entry = {
        "input_file": str(source_input),
        "input_file_sha256": input_file_sha256,
        "normalized_request_sha256": normalized_request_sha256,
        "forward_executable_sha256": forward_exe_sha256,
        "forward_run_id": run_id,
        "run_dir": str(run_dir),
        "output_dir": reference_run.get("output_dir"),
        "temperature_curve_path": reference_run.get("temperature_curve_path"),
        "registered_at_epoch": time.time(),
    }
    entries = [
        e for e in entries
        if not (
            str(e.get("input_file_sha256", "")) == input_file_sha256
            and str(e.get("forward_executable_sha256", "")) == forward_exe_sha256
        )
    ]
    entries.append(entry)
    # 只保留最近100条，防止长期调试无限增长。
    manifest["entries"] = entries[-100:]
    _save_cache_manifest(manifest)


def find_cached_reference(
    *,
    source_input: Path,
    source_request: dict[str, Any],
    input_file_sha256: str,
    forward_exe_sha256: str,
) -> tuple[dict[str, Any] | None, str | None]:
    """优先按原始input SHA命中；兼容已有reference_runs时再按规范化请求内容回收。"""
    normalized_sha = canonical_request_sha256(source_request)
    manifest = _load_cache_manifest()
    for entry in reversed(manifest.get("entries", [])):
        if str(entry.get("input_file_sha256", "")) != input_file_sha256:
            continue
        if str(entry.get("forward_executable_sha256", "")) != forward_exe_sha256:
            continue
        run_dir = Path(str(entry.get("run_dir", "")))
        ref = _reference_result_from_run_dir(
            run_dir, source_input,
            expected_request_sha256=normalized_sha,
            expected_forward_exe_sha256=forward_exe_sha256,
        )
        if ref is not None:
            return ref, "input_file_sha256+forward_executable_sha256"

    # 兼容补丁安装前已经生成的 reference_runs。
    if REFERENCE_RUN_ROOT.exists():
        run_dirs = sorted(
            [p for p in REFERENCE_RUN_ROOT.glob("run_*") if p.is_dir()],
            key=lambda p: p.name,
            reverse=True,
        )
        for run_dir in run_dirs:
            ref = _reference_result_from_run_dir(
                run_dir, source_input,
                expected_request_sha256=normalized_sha,
                expected_forward_exe_sha256=forward_exe_sha256,
            )
            if ref is not None:
                _register_reference_cache(
                    source_input=source_input,
                    input_file_sha256=input_file_sha256,
                    normalized_request_sha256=normalized_sha,
                    forward_exe_sha256=forward_exe_sha256,
                    reference_run=ref,
                )
                return ref, "normalized_request_sha256+forward_executable_sha256(existing_run)"
    return None, None


def load_internal_config(path: Path) -> dict[str, Any]:
    cfg = load_json(path)
    if cfg.get("schema_version") != "scene-search-internal-config-v2":
        raise ValueError("scene_search_config.json schema_version 必须为 scene-search-internal-config-v2。")
    if "candidate_pool_csv" not in cfg:
        raise ValueError("scene_search_config.json 缺少 candidate_pool_csv。")
    pre = cfg.get("surrogate_prescreen")
    if not isinstance(pre, dict) or not isinstance(pre.get("thresholds_K"), dict):
        raise ValueError("scene_search_config.json 缺少 surrogate_prescreen.thresholds_K。")
    return cfg


def load_request(path: Path) -> dict[str, Any]:
    req = load_json(path)
    if req.get("schema_version") != "scene-search-request-v2":
        raise ValueError("scene_search_request.json schema_version 必须为 scene-search-request-v2。")
    required = ["input_file", "similarity_requirement", "required_candidate_count"]
    missing = [k for k in required if k not in req]
    if missing:
        raise ValueError(f"scene_search_request.json 缺少字段：{missing}")

    sim = req["similarity_requirement"]
    if not isinstance(sim, dict):
        raise ValueError("similarity_requirement 必须为对象。")
    sim_required = ["metric", "target_object_id", "required_percent"]
    missing_sim = [k for k in sim_required if k not in sim]
    if missing_sim:
        raise ValueError(f"similarity_requirement 缺少字段：{missing_sim}")
    if str(sim["metric"]) != "temperature_similarity":
        raise ValueError("当前04正式逆搜索仅支持 similarity_requirement.metric=temperature_similarity。")
    if int(sim["target_object_id"]) != 1:
        raise ValueError("当前02温度代理模型和04逆搜索固定以 1 号目标温度为对象，target_object_id 必须为 1。")
    sreq = float(sim["required_percent"])
    if not math.isfinite(sreq) or sreq <= 0.0 or sreq > 100.0:
        raise ValueError("similarity_requirement.required_percent 必须在 (0, 100]。")
    count = int(req["required_candidate_count"])
    if count <= 0:
        raise ValueError("required_candidate_count 必须 > 0。")
    return req


def load_similarity_temperature_windows() -> dict[str, float]:
    cfg = load_json(SIMILARITY_CONFIG_PATH)
    tc = cfg.get("temperature", {})
    early = tc.get("early_window_s", [0.0, 30.0])
    plateau = tc.get("plateau_window_s", [100.0, 1000.0])
    if not (isinstance(early, list) and len(early) == 2 and isinstance(plateau, list) and len(plateau) == 2):
        raise ValueError("03 similarity_config.json 温度窗口配置无效。")
    return {
        "early_start_s": float(early[0]),
        "early_end_s": float(early[1]),
        "plateau_start_s": float(plateau[0]),
        "plateau_end_s": float(plateau[1]),
    }


def emit_progress(status_path: Path, **payload: Any) -> None:
    payload = {"timestamp": time.time(), **payload}
    status_path.parent.mkdir(parents=True, exist_ok=True)
    status_path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    print("GUI_PROGRESS " + json.dumps(payload, ensure_ascii=False), flush=True)


def _new_task_run() -> tuple[str, Path]:
    RUN_ROOT.mkdir(parents=True, exist_ok=True)
    run_id = datetime.now().strftime("run_%Y%m%d_%H%M%S_") + uuid.uuid4().hex[:8]
    run_dir = RUN_ROOT / run_id
    run_dir.mkdir(parents=True, exist_ok=False)
    return run_id, run_dir


def _write_latest_run(run_id: str, run_dir: Path, status: str, *, mode: str | None = None) -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    payload = {
        "module": "04",
        "run_id": run_id,
        "run_dir": str(run_dir.resolve()),
        "status": status,
        "mode": mode,
        "timestamp": time.time(),
    }
    LATEST_RUN_PATH.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")


def validate_shared_input_for_surrogate(source_input: Path) -> dict[str, Any]:
    """确认 input_file 同时满足 01 输入合同和当前 02 冻结代理模型适用域。"""
    if not source_input.exists():
        raise FileNotFoundError(f"共享 input_file 不存在：{source_input}")
    surrogate_input = load_surrogate_standard_input_module()
    return surrogate_input.load_and_standardize(source_input)


def build_reference_target(stage_g, source_input: Path) -> tuple[dict[str, Any], np.ndarray]:
    """由共享 input_file 调用 01 一次，生成04真正使用的目标温度曲线。"""
    ref = stage_g.run_reference(
        {
            "reference_id": "SOURCE_INPUT_REFERENCE",
            "source_input_path": str(source_input),
        }
    )
    if int(ref.get("return_code", 999)) != 0:
        raise RuntimeError(
            f"共享 input_file 的01基准正向计算失败：return_code={ref.get('return_code')}；"
            f"stderr={ref.get('stderr_path')}"
        )
    curve_path_raw = ref.get("temperature_curve_path") or ref.get("temperature_history_path")
    if not curve_path_raw:
        raise RuntimeError("01基准正向计算未返回温度结果路径。")
    target_temp = parse_forward_temperature(Path(curve_path_raw))
    return ref, target_temp


def load_pool(pool_csv: Path) -> pd.DataFrame:
    d = _read_csv_compatible(pool_csv)
    req = ["candidate_id", "q_int", "emissivity_ir", "absorptivity_solar"]
    missing = [c for c in req if c not in d.columns]
    if missing:
        raise ValueError(f"参数池缺少列：{missing}")
    d = d[req].copy()
    if d["candidate_id"].duplicated().any():
        raise ValueError("candidate_id 存在重复，固定顺序合同不成立。")
    for c in req[1:]:
        d[c] = pd.to_numeric(d[c], errors="raise")
    if d[req[1:]].isna().any().any() or not np.isfinite(d[req[1:]].to_numpy(float)).all():
        raise ValueError("参数池包含 NaN/Inf。")
    ids = d["candidate_id"].astype(str)
    if ids.str.fullmatch(r"CH4_C\d{6}").all():
        nums = ids.str.extract(r"(\d{6})", expand=False).astype(int).to_numpy()
        if np.any(np.diff(nums) <= 0):
            raise ValueError("参数池 candidate_id 不是严格升序；程序不会自行重排。")
    return d.reset_index(drop=True)


def load_m2(cfg: dict[str, Any]):
    model_path = config_path(
        "temperature_model",
        MODEL_DIR / "formal_configured_target_temperature_extratrees.joblib",
        must_exist=True,
    )
    actual = sha256(model_path)
    expected = str(cfg.get("temperature_model_expected_sha256", "")).strip()
    if expected and actual != expected:
        raise RuntimeError(f"M2 模型 SHA256 不匹配：actual={actual}, expected={expected}")
    payload = joblib.load(model_path)
    model = payload["model"]
    if hasattr(model, "named_steps") and "impute" in model.named_steps:
        imputer = model.named_steps["impute"]
        if not hasattr(imputer, "_fill_dtype") and hasattr(imputer, "statistics_"):
            imputer._fill_dtype = imputer.statistics_.dtype
    features = list(payload["features"])
    if features != EXPECTED_FEATURES:
        raise RuntimeError(f"M2 特征合同不匹配：{features}")
    return model, model_path


def predict_m2_candidate(model, row: pd.Series) -> np.ndarray:
    q = float(row.q_int)
    e = float(row.emissivity_ir)
    a = float(row.absorptivity_solar)
    if e == 0.0:
        raise ValueError("候选 emissivity_ir=0，无法构造 q_int_div_emissivity_ir 特征。")
    x = pd.DataFrame(
        {
            "q_int": np.full(len(TIMES), q),
            "emissivity_ir": np.full(len(TIMES), e),
            "absorptivity_solar": np.full(len(TIMES), a),
            "time_s": TIMES,
            "q_int_times_absorptivity_solar": np.full(len(TIMES), q * a),
            "q_int_div_emissivity_ir": np.full(len(TIMES), q / e),
            "is_initial_condition": (TIMES == 0.0).astype(int),
        }
    )
    y = np.asarray(model.predict(x[EXPECTED_FEATURES]), dtype=float)
    if y.shape != (101,) or not np.isfinite(y).all():
        raise RuntimeError("M2 返回的温度曲线不是101个有限值。")
    y[0] = 300.0
    return y


def parse_forward_temperature(candidate_csv: Path) -> np.ndarray:
    if not candidate_csv.exists():
        raise FileNotFoundError(f"正向结果不存在：{candidate_csv}")
    df = _read_csv_compatible(candidate_csv)
    if {"time_s", "temperature_K"}.issubset(df.columns) and "id" not in df.columns:
        d = df[["time_s", "temperature_K"]].copy()
        t = pd.to_numeric(d["time_s"], errors="raise").to_numpy(float)
        y = pd.to_numeric(d["temperature_K"], errors="raise").to_numpy(float)
    elif {"time_s", "T_1"}.issubset(df.columns):
        d = df[["time_s", "T_1"]].copy()
        d["time_s"] = pd.to_numeric(d["time_s"], errors="raise")
        d["T_1"] = pd.to_numeric(d["T_1"], errors="raise")
        d = d[d["time_s"].isin(TIMES)].sort_values("time_s", kind="mergesort")
        t = d["time_s"].to_numpy(float)
        y = d["T_1"].to_numpy(float)
    elif {"id", "type", "time_s", "temperature_K"}.issubset(df.columns):
        d = df[(pd.to_numeric(df["id"], errors="coerce") == 1) & (df["type"].astype(str).str.upper() == "SPHERE")].copy()
        d = d.sort_values("time_s", kind="mergesort")
        t = pd.to_numeric(d["time_s"], errors="raise").to_numpy(float)
        y = pd.to_numeric(d["temperature_K"], errors="raise").to_numpy(float)
    else:
        raise ValueError(f"无法识别正向温度文件字段：{list(df.columns)}")

    if len(y) != 101 or not np.array_equal(t, TIMES) or not np.isfinite(y).all():
        raise ValueError("正向1号目标温度必须覆盖0~1000 s、每10 s共101个有限值。")
    return y


def evaluate_temperature(similarity, windows: dict[str, float], target: np.ndarray, candidate: np.ndarray) -> dict[str, Any]:
    metrics = similarity.evaluate_temperature_curves(TIMES, target, candidate, **windows)
    valid: list[tuple[str, float]] = []
    for name, field in TEMP_SIM_FIELDS:
        value = float(metrics.get(field, float("nan")))
        if np.isfinite(value):
            valid.append((name, value))
    if not valid:
        raise RuntimeError("03 未生成任何有效温度相似度分项。")
    limiting_name, total_similarity = min(valid, key=lambda x: x[1])
    metrics = dict(metrics)
    metrics["temperature_similarity_percent"] = float(total_similarity)
    metrics["temperature_limiting_feature"] = limiting_name
    metrics["temperature_valid_component_count"] = len(valid)
    return metrics


def threshold_pass(metrics: dict[str, Any], thresholds: dict[str, float]) -> tuple[bool, list[str]]:
    failed: list[str] = []
    for key, limit in thresholds.items():
        if key not in metrics:
            failed.append(f"missing:{key}")
            continue
        value = float(metrics[key])
        if not np.isfinite(value) or value > float(limit):
            failed.append(f"{key}={value:.6g}>{float(limit):.6g}")
    return len(failed) == 0, failed


def temperature_margin(reference_temperature: np.ndarray, required_similarity_percent: float) -> tuple[float, float]:
    rt = float(np.max(reference_temperature) - np.min(reference_temperature))
    if not math.isfinite(rt) or rt <= 0.0:
        raise ValueError("目标温度全过程变化幅度 R_T <= 0；当前04 v1不能为近似恒温目标自动定义相似度裕度。")
    margin = rt * (100.0 / required_similarity_percent - 1.0)
    return rt, float(margin)


def _metric_columns(prefix: str, metrics: dict[str, Any]) -> dict[str, Any]:
    out: dict[str, Any] = {
        f"{prefix}_rmse_K": float(metrics["rmse"]),
        f"{prefix}_mae_K": float(metrics["mae"]),
        f"{prefix}_max_abs_error_K": float(metrics["max_abs_error"]),
        f"{prefix}_final_abs_error_K": float(metrics["final_abs_error"]),
        f"{prefix}_plateau_abs_error_K": float(metrics["plateau_abs_error"]),
        f"{prefix}_early_abs_error_K": float(metrics["early_abs_error"]),
        f"{prefix}_temperature_similarity_percent": float(metrics["temperature_similarity_percent"]),
        f"{prefix}_temperature_limiting_feature": str(metrics["temperature_limiting_feature"]),
    }
    trend = float(metrics.get("temperature_trend_similarity_percent", float("nan")))
    out[f"{prefix}_temperature_trend_similarity_percent"] = trend if np.isfinite(trend) else np.nan
    return out


def write_outputs(
    output_dir: Path,
    log_rows: list[dict[str, Any]],
    valid_rows: list[dict[str, Any]],
    valid_curves: list[dict[str, Any]],
    summary: dict[str, Any],
    request: dict[str, Any],
) -> None:
    output_dir.mkdir(parents=True, exist_ok=True)
    log_df = pd.DataFrame(log_rows)
    candidate_columns = [
        "solution_index", "sequence_index", "target_id", "source_case_id", "candidate_id",
        "q_int", "emissivity_ir", "absorptivity_solar",
        "required_temperature_similarity_percent", "target_temperature_range_K", "temperature_margin_K",
        "proxy_rmse_K", "proxy_mae_K", "proxy_max_abs_error_K", "proxy_final_abs_error_K",
        "proxy_plateau_abs_error_K", "proxy_early_abs_error_K",
        "proxy_temperature_similarity_percent", "proxy_temperature_limiting_feature",
        "proxy_temperature_trend_similarity_percent",
        "forward_rmse_K", "forward_mae_K", "forward_max_abs_error_K", "forward_final_abs_error_K",
        "forward_plateau_abs_error_K", "forward_early_abs_error_K",
        "forward_temperature_similarity_percent", "forward_temperature_limiting_feature",
        "forward_temperature_trend_similarity_percent", "forward_level_margin_pass", "forward_run_dir",
    ]
    valid_df = pd.DataFrame(valid_rows, columns=candidate_columns)
    curve_df = pd.DataFrame(valid_curves)

    log_df.to_csv(output_dir / "scene_search_log.csv", index=False, encoding="utf-8-sig")
    valid_df.to_csv(output_dir / "candidate_parameters.csv", index=False, encoding="utf-8-sig")
    curve_columns = [
        "solution_index", "target_id", "source_case_id", "candidate_id", "time_s",
        "target_temperature_K", "m2_temperature_K", "forward_temperature_K",
    ]
    if curve_df.empty:
        curve_df = pd.DataFrame(columns=curve_columns)
    curve_df.to_csv(output_dir / "candidate_temperature_curves.csv", index=False, encoding="utf-8-sig")

    jwrite(output_dir / "scene_search_summary.json", summary)
    jwrite(output_dir / "scene_search_request_used.json", request)


def parse_args() -> argparse.Namespace:
    ap = argparse.ArgumentParser(description="04相似度逆应用：共享input -> 01生成目标 -> 02代理精度预筛 -> 01候选复核 -> 03温度相似度验收")
    ap.add_argument("--request", type=Path, default=DEFAULT_REQUEST_PATH, help="用户业务输入：共享input文件、相似度要求、候选数量")
    ap.add_argument("--config", type=Path, default=DEFAULT_CONFIG_PATH, help="内部运行配置：参数池和02模型预筛精度")
    ap.add_argument("--required-temperature-similarity", type=float, default=None, help="测试覆盖 similarity_requirement.required_percent（百分比）")
    ap.add_argument("--required-valid-count", type=int, default=None, help="兼容/测试覆盖 request 中的候选数量")
    ap.add_argument("--max-candidates", type=int, default=None, help="仅用于受控测试；正式运行默认遍历直到获得足量候选或参数池耗尽")
    ap.add_argument("--proxy-only", action="store_true", help="取得01基准目标后，仅验证02代理预筛；不对候选逐个做01正向复核")
    ap.add_argument("--no-reference-cache", action="store_true", help="强制重新运行01生成基准目标，不复用 reference_runs 缓存")
    # 保留旧命令行参数，避免历史脚本报 unknown argument；新正式流程不自动运行点图像阶段。
    ap.add_argument("--skip-point-image", action="store_true", help=argparse.SUPPRESS)
    ap.add_argument("--require-point-image", action="store_true", help=argparse.SUPPRESS)
    return ap.parse_args()


def main() -> int:
    global ACTIVE_RUN_ID, ACTIVE_RUN_DIR

    ensure()
    args = parse_args()
    start = time.time()
    run_id, task_dir = _new_task_run()
    ACTIVE_RUN_ID, ACTIVE_RUN_DIR = run_id, task_dir
    mode_name = "proxy_only" if args.proxy_only else "full"
    status_path = task_dir / "scene_search_status.json"
    _write_latest_run(run_id, task_dir, "running", mode=mode_name)

    cfg = load_internal_config(args.config.resolve())
    request = load_request(args.request.resolve())

    sim_req = request["similarity_requirement"]
    required_similarity = float(
        args.required_temperature_similarity
        if args.required_temperature_similarity is not None
        else sim_req["required_percent"]
    )
    if required_similarity <= 0.0 or required_similarity > 100.0:
        raise ValueError("要求温度相似度必须在 (0,100]%。")
    required_count = int(
        args.required_valid_count
        if args.required_valid_count is not None
        else request["required_candidate_count"]
    )
    if required_count <= 0:
        raise ValueError("required_candidate_count 必须 > 0。")

    # 04 的目标不再来自独立CSV，而来自与01/02共用的完整 input_file。
    source_input = package_path(request["input_file"])
    standardized_source = validate_shared_input_for_surrogate(source_input)
    source_request = standardized_source["full_request"]
    target_id = "object_1"
    source_case_id = source_input.stem

    stage_g = load_stage_g_module()
    forward_exe_path = config_path("forward_exe", FORWARD_PROGRAM_DIR / "production_main_output_interface.exe", must_exist=True)
    forward_exe_sha = sha256(forward_exe_path)
    input_file_sha = sha256(source_input)
    normalized_request_sha = canonical_request_sha256(source_request)

    reference_cache_hit = False
    reference_cache_match_basis: str | None = None
    reference_phase_start = time.time()
    emit_progress(
        status_path,
        state="checking_reference_cache",
        input_file=str(source_input),
        input_file_sha256=input_file_sha,
        forward_executable_sha256=forward_exe_sha,
        similarity_metric=str(sim_req["metric"]),
        required_temperature_similarity_percent=required_similarity,
        required_valid_count=required_count,
    )

    reference_run = None
    if not args.no_reference_cache:
        reference_run, reference_cache_match_basis = find_cached_reference(
            source_input=source_input,
            source_request=source_request,
            input_file_sha256=input_file_sha,
            forward_exe_sha256=forward_exe_sha,
        )

    if reference_run is not None:
        reference_cache_hit = True
        curve_path_raw = reference_run.get("temperature_curve_path") or reference_run.get("temperature_history_path")
        if not curve_path_raw:
            raise RuntimeError("reference cache命中但未返回温度结果路径。")
        target_temp = parse_forward_temperature(Path(curve_path_raw))
        emit_progress(
            status_path,
            state="reference_cache_hit",
            input_file=str(source_input),
            input_file_sha256=input_file_sha,
            reference_forward_run_id=reference_run.get("forward_run_id"),
            reference_cache_match_basis=reference_cache_match_basis,
            reference_temperature_curve_path=str(curve_path_raw),
        )
    else:
        emit_progress(
            status_path,
            state="building_reference_target",
            input_file=str(source_input),
            input_file_sha256=input_file_sha,
            similarity_metric=str(sim_req["metric"]),
            required_temperature_similarity_percent=required_similarity,
            required_valid_count=required_count,
        )
        reference_run, target_temp = build_reference_target(stage_g, source_input)
        _register_reference_cache(
            source_input=source_input,
            input_file_sha256=input_file_sha,
            normalized_request_sha256=normalized_request_sha,
            forward_exe_sha256=forward_exe_sha,
            reference_run=reference_run,
        )
        reference_cache_match_basis = "new_reference_run_registered"

    reference_phase_elapsed = time.time() - reference_phase_start
    target_range_K, margin_K = temperature_margin(target_temp, required_similarity)

    proxy_thresholds = {k: float(v) for k, v in cfg["surrogate_prescreen"]["thresholds_K"].items()}
    pool_csv = package_path(cfg["candidate_pool_csv"])
    pool_df = load_pool(pool_csv)
    if args.max_candidates is not None:
        if args.max_candidates <= 0:
            raise ValueError("--max-candidates 必须 > 0。")
        pool_df = pool_df.iloc[: args.max_candidates].copy()

    similarity = load_similarity_module()
    similarity_windows = load_similarity_temperature_windows()
    model, model_path = load_m2(cfg)

    log_rows: list[dict[str, Any]] = []
    valid_rows: list[dict[str, Any]] = []
    valid_curves: list[dict[str, Any]] = []
    proxy_pass_count = 0
    forward_attempt_count = 0

    emit_progress(
        status_path,
        state="running",
        mode="proxy_only" if args.proxy_only else "full",
        target_id=target_id,
        source_case_id=source_case_id,
        input_file=str(source_input),
        required_temperature_similarity_percent=required_similarity,
        target_temperature_range_K=target_range_K,
        temperature_margin_K=margin_K,
        current_candidate=0,
        candidates_total=len(pool_df),
        proxy_pass_count=0,
        forward_attempt_count=0,
        valid_count=0,
        required_valid_count=required_count,
    )

    for seq, row in enumerate(pool_df.itertuples(index=False), start=1):
        row_s = pd.Series(row._asdict())
        m2_temp = predict_m2_candidate(model, row_s)
        proxy_metrics = evaluate_temperature(similarity, similarity_windows, target_temp, m2_temp)

        # 代理初筛只使用02独立验证精度，不读取 required_similarity。
        proxy_pass, proxy_failed = threshold_pass(proxy_metrics, proxy_thresholds)
        if proxy_pass:
            proxy_pass_count += 1

        record: dict[str, Any] = {
            "sequence_index": seq,
            "target_id": target_id,
            "source_case_id": source_case_id,
            "candidate_id": str(row.candidate_id),
            "q_int": float(row.q_int),
            "emissivity_ir": float(row.emissivity_ir),
            "absorptivity_solar": float(row.absorptivity_solar),
            "required_temperature_similarity_percent": required_similarity,
            "target_temperature_range_K": target_range_K,
            "temperature_margin_K": margin_K,
            "proxy_rule_source": str(cfg["surrogate_prescreen"].get("source", "")),
            "proxy_pass": bool(proxy_pass),
            "proxy_failed_conditions": ";".join(proxy_failed),
            **_metric_columns("proxy", proxy_metrics),
            "forward_attempted": False,
            "forward_output_valid": False,
            "forward_pass": False,
            "forward_failed_conditions": "",
            "forward_return_code": "",
            "forward_elapsed_seconds": "",
            "accepted_count_after_candidate": len(valid_rows),
            "decision": "proxy_reject" if not proxy_pass else "proxy_pass",
        }

        if proxy_pass and not args.proxy_only:
            assert stage_g is not None
            forward_attempt_count += 1
            run_payload = {
                "recheck_id": f"SEARCH_{seq:06d}_{row.candidate_id}",
                "target_id": target_id,
                "source_case_id": source_case_id,
                "candidate_id": str(row.candidate_id),
                "source_input_path": str(source_input),
                "run_root": str(task_dir / "forward_runs"),
                "q_int": float(row.q_int),
                "emissivity_ir": float(row.emissivity_ir),
                "absorptivity_solar": float(row.absorptivity_solar),
            }
            frun = stage_g.run_one(run_payload)
            record["forward_attempted"] = True
            record["forward_return_code"] = int(frun["return_code"])
            record["forward_elapsed_seconds"] = float(frun["elapsed_seconds"])
            try:
                if int(frun["return_code"]) != 0:
                    raise RuntimeError(f"forward return_code={frun['return_code']}")
                curve_path_raw = frun.get("temperature_curve_path") or frun.get("temperature_history_path")
                if not curve_path_raw:
                    raise RuntimeError("01统一入口未返回温度结果路径。")
                ftemp = parse_forward_temperature(Path(curve_path_raw))
                record["forward_output_valid"] = True

                forward_metrics = evaluate_temperature(similarity, similarity_windows, target_temp, ftemp)
                fsim = float(forward_metrics["temperature_similarity_percent"])
                flim = str(forward_metrics["temperature_limiting_feature"])
                # 最终验收只看用户输入的温度相似度要求；03定义是唯一判据。
                fpass = bool(np.isfinite(fsim) and fsim >= required_similarity)
                ffailed = [] if fpass else [f"temperature_similarity={fsim:.6g}%<{required_similarity:.6g}%"]

                record.update(_metric_columns("forward", forward_metrics))
                record["forward_level_margin_pass"] = bool(float(forward_metrics["max_abs_error"]) <= margin_K + 1e-12)
                record["forward_pass"] = fpass
                record["forward_failed_conditions"] = ";".join(ffailed)
                record["decision"] = "accepted" if fpass else "forward_reject"

                if fpass:
                    valid_index = len(valid_rows) + 1
                    accepted = {
                        "solution_index": valid_index,
                        "sequence_index": seq,
                        "target_id": target_id,
                        "source_case_id": source_case_id,
                        "candidate_id": str(row.candidate_id),
                        "q_int": float(row.q_int),
                        "emissivity_ir": float(row.emissivity_ir),
                        "absorptivity_solar": float(row.absorptivity_solar),
                        "required_temperature_similarity_percent": required_similarity,
                        "target_temperature_range_K": target_range_K,
                        "temperature_margin_K": margin_K,
                        **_metric_columns("proxy", proxy_metrics),
                        **_metric_columns("forward", forward_metrics),
                        "forward_level_margin_pass": bool(float(forward_metrics["max_abs_error"]) <= margin_K + 1e-12),
                        "forward_run_dir": str(Path(frun["candidate_input_path"]).parent) if frun.get("candidate_input_path") else str(frun.get("output_dir", "")),
                    }
                    valid_rows.append(accepted)
                    record["accepted_count_after_candidate"] = len(valid_rows)
                    valid_curves.extend(
                        {
                            "solution_index": valid_index,
                            "target_id": target_id,
                            "source_case_id": source_case_id,
                            "candidate_id": str(row.candidate_id),
                            "time_s": float(tt),
                            "target_temperature_K": float(yt),
                            "m2_temperature_K": float(mt),
                            "forward_temperature_K": float(ft),
                        }
                        for tt, yt, mt, ft in zip(TIMES, target_temp, m2_temp, ftemp)
                    )
            except Exception as exc:
                record["decision"] = "forward_blocked"
                record["forward_failed_conditions"] = f"{type(exc).__name__}: {exc}"

        log_rows.append(record)
        pd.DataFrame(log_rows).to_csv(task_dir / "scene_search_log.csv", index=False, encoding="utf-8-sig")
        if valid_rows:
            pd.DataFrame(valid_rows).to_csv(task_dir / "candidate_parameters.csv", index=False, encoding="utf-8-sig")

        emit_progress(
            status_path,
            state="running",
            mode="proxy_only" if args.proxy_only else "full",
            target_id=target_id,
            required_temperature_similarity_percent=required_similarity,
            temperature_margin_K=margin_K,
            current_candidate=seq,
            current_candidate_id=str(row.candidate_id),
            candidates_total=len(pool_df),
            proxy_pass_count=proxy_pass_count,
            forward_attempt_count=forward_attempt_count,
            valid_count=len(valid_rows),
            required_valid_count=required_count,
            last_decision=record["decision"],
        )

        if (not args.proxy_only) and len(valid_rows) >= required_count:
            break

    evaluated_count = len(log_rows)
    if args.proxy_only:
        search_status = "ProxyOnlyCompleted"
    elif len(valid_rows) >= required_count:
        search_status = "Completed"
    else:
        search_status = "Controlled incomplete"

    effective_request = json.loads(json.dumps(request, ensure_ascii=False))
    effective_request["input_file_resolved"] = str(source_input)
    effective_request["input_file_sha256"] = input_file_sha
    effective_request["similarity_requirement"]["required_percent"] = required_similarity
    effective_request["required_candidate_count"] = required_count

    summary = {
        "schema_version": "scene-search-summary-v2",
        "run_id": run_id,
        "run_dir": str(task_dir.resolve()),
        "status": search_status,
        "target_id": target_id,
        "source_case_id": source_case_id,
        "input_file": str(source_input),
        "input_file_sha256": input_file_sha,
        "input_contract": "forward-request-v1",
        "normalized_request_sha256": normalized_request_sha,
        "reference_cache_hit": reference_cache_hit,
        "reference_cache_match_basis": reference_cache_match_basis,
        "reference_cache_manifest": str(REFERENCE_CACHE_MANIFEST),
        "reference_phase_elapsed_seconds": reference_phase_elapsed,
        "similarity_metric": str(sim_req["metric"]),
        "target_object_id": int(sim_req["target_object_id"]),
        "reference_forward_run_id": reference_run.get("forward_run_id"),
        "reference_forward_output_dir": reference_run.get("output_dir"),
        "reference_temperature_curve_path": reference_run.get("temperature_curve_path") or reference_run.get("temperature_history_path"),
        "required_temperature_similarity_percent": required_similarity,
        "target_temperature_range_K": target_range_K,
        "temperature_margin_K": margin_K,
        "margin_formula": "D_allow = R_T * (100 / S_required - 1)",
        "temperature_similarity_definition": "03 module; minimum valid temperature component similarity",
        "required_candidate_count": required_count,
        "evaluated_candidate_count": evaluated_count,
        "proxy_pass_count": proxy_pass_count,
        "forward_attempt_count": forward_attempt_count,
        "forward_confirmed_valid_count": len(valid_rows),
        "stopped_early": bool((not args.proxy_only) and len(valid_rows) >= required_count and evaluated_count < len(pool_df)),
        "candidate_order": str(cfg.get("candidate_order", "input file order; no sorting or ranking")),
        "surrogate_prescreen_source": str(cfg["surrogate_prescreen"].get("source", "")),
        "surrogate_prescreen_rule": str(cfg["surrogate_prescreen"].get("rule", "")),
        "surrogate_prescreen_thresholds_K": proxy_thresholds,
        "surrogate_prescreen_independent_of_user_similarity": True,
        "m2_model": str(model_path),
        "m2_model_sha256": sha256(model_path),
        "forward_executable": None if forward_exe_path is None else str(forward_exe_path),
        "forward_executable_sha256": forward_exe_sha,
        "forward_executable_sha_policy": str(cfg.get("forward_executable_sha_policy", "record_only")),
        "point_image_in_default_acceptance_chain": False,
        "elapsed_seconds": time.time() - start,
        "mode": "proxy_only" if args.proxy_only else "full",
        "search_rule": "shared input_file -> cached/executed 01 reference target -> 02 accuracy-based pre-screen -> candidate 01 forward recheck on same scene -> 03 temperature similarity >= user requirement -> keep candidate",
    }

    write_outputs(task_dir, log_rows, valid_rows, valid_curves, summary, effective_request)
    jwrite(task_dir / "source_forward_request_used.json", source_request)
    emit_progress(status_path, state="finished", **summary)
    _write_latest_run(run_id, task_dir, "success" if search_status in {"Completed", "ProxyOnlyCompleted"} else "incomplete", mode=mode_name)
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    return 0 if search_status in {"Completed", "ProxyOnlyCompleted"} else 3


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except SystemExit:
        raise
    except Exception as exc:
        err = {
            "state": "error",
            "run_id": ACTIVE_RUN_ID,
            "run_dir": None if ACTIVE_RUN_DIR is None else str(ACTIVE_RUN_DIR.resolve()),
            "error_type": type(exc).__name__,
            "message": str(exc),
        }
        try:
            if ACTIVE_RUN_DIR is not None and ACTIVE_RUN_ID is not None:
                emit_progress(ACTIVE_RUN_DIR / "scene_search_status.json", **err)
                _write_latest_run(ACTIVE_RUN_ID, ACTIVE_RUN_DIR, "error")
        except Exception:
            pass
        print(json.dumps(err, ensure_ascii=False), file=sys.stderr)
        raise SystemExit(2)
