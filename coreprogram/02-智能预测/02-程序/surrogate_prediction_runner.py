from __future__ import annotations

# 继续执行当前业务步骤，保持处理上下文与数据状态一致。
"""02-智能预测统一标准入口。

正式输入与 01 正向仿真完全一致：forward-request-v1 JSON。
本入口先调用 surrogate_standard_input.py 复用 01 输入校验并检查当前冻结代理模型适用域，
再将完整业务输入转换为模型所需特征。

# 继续执行当前业务步骤，保持处理上下文与数据状态一致。
支持模式：temperature / point-image / both。

正式调用：
    python surrogate_prediction_runner.py --params-json forward_request.json --mode both
# 继续执行当前业务步骤，保持处理上下文与数据状态一致。
"""

import argparse
import hashlib
import importlib.util
import json
# 导入当前模块依赖的标准能力或领域组件。
import sys
import time
import uuid
# 导入当前模块依赖的标准能力或领域组件。
from datetime import datetime
from pathlib import Path
from typing import Any

# 导入当前模块依赖的标准能力或领域组件。
import numpy as np
import pandas as pd

import surrogate_standard_input as standard_input

# 读取或写入约定的数据文件，并维护统一的路径规则。
PROGRAM_DIR = Path(__file__).resolve().parent
MODULE_ROOT = PROGRAM_DIR.parent
PACKAGE_ROOT = MODULE_ROOT.parent
# 更新当前流程所需的中间数据，为下一计算步骤做好准备。
MODEL_DIR = MODULE_ROOT / "03-模型文件"
INPUT_DIR = MODULE_ROOT / "01-输入文件"
OUTPUT_DIR = MODULE_ROOT / "04-输出文件"
OUTPUT_ROOT = OUTPUT_DIR / "runs"
# 读取或写入约定的数据文件，并维护统一的路径规则。
LATEST_RUN_PATH = OUTPUT_DIR / "latest_run.json"
CONFIG_PATH = PACKAGE_ROOT / "config.json"

TIME_STEP_S = 10.0
MODEL_PARAMETER_DOMAIN = standard_input.MODEL_PARAMETER_DOMAIN


# 定义 sha256 处理过程，集中封装该步骤的输入、输出与异常边界。
def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        # 遍历当前数据或迭代计算，逐项更新处理结果。
        for block in iter(lambda: f.read(4 * 1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def load_config() -> dict[str, Any]:
    # 检查当前条件，仅在满足约束时执行对应分支。
    if not CONFIG_PATH.exists():
        return {}
    return json.loads(CONFIG_PATH.read_text(encoding="utf-8"))


# 定义 package_path 处理过程，集中封装该步骤的输入、输出与异常边界。
def package_path(raw: str | Path) -> Path:
    p = Path(raw)
    return p.resolve() if p.is_absolute() else (PACKAGE_ROOT / p).resolve()


# 定义 emit_progress 处理过程，集中封装该步骤的输入、输出与异常边界。
def emit_progress(**payload: Any) -> None:
    print("GUI_PROGRESS " + json.dumps({"module": "02", "timestamp": time.time(), **payload}, ensure_ascii=False), flush=True)


def _write_latest_run(run_id: str, run_dir: Path, status: str, *, mode: str | None = None) -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    payload = {
        "module": "02",
        "run_id": run_id,
        # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
        "run_dir": str(run_dir.resolve()),
        "status": status,
        "mode": mode,
        "timestamp": time.time(),
    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
    }
    LATEST_RUN_PATH.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")


def _to_float(name: str, value: Any) -> float:
    # 执行可能失败的操作，并由后续分支统一处理异常。
    try:
        out = float(value)
    except Exception as exc:
        raise ValueError(f"参数 {name} 必须为数值，当前值={value!r}") from exc
    # 检查当前条件，仅在满足约束时执行对应分支。
    if not np.isfinite(out):
        raise ValueError(f"参数 {name} 必须为有限数值。")
    return out


# 定义 validate_parameters 处理过程，集中封装该步骤的输入、输出与异常边界。
def validate_parameters(params: dict[str, Any]) -> dict[str, float]:
    clean: dict[str, float] = {}
    for name, rule in MODEL_PARAMETER_DOMAIN.items():
        if name not in params:
            # 检测到无效状态后立即报错，防止异常数据继续传播。
            raise ValueError(f"缺少必需参数：{name}")
        value = _to_float(name, params[name])
        if not (float(rule["min"]) <= value <= float(rule["max"])):
            # 检测到无效状态后立即报错，防止异常数据继续传播。
            raise ValueError(
                f"参数 {name} 超出当前代理模型训练/验证适用域：{value}，"
                f"允许 [{rule['min']}, {rule['max']}] {rule['unit']}。"
            )
        # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
        clean[name] = value
    return clean


def prediction_times(duration_s: float) -> np.ndarray:
    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
    """Return the frozen 10 s grid plus the exact requested end time."""
    regular = np.arange(0.0, duration_s + 1.0e-9, TIME_STEP_S, dtype=float)
    if regular.size == 0 or not np.isclose(regular[-1], duration_s):
        # 执行数组或表格数据运算，为后续数值处理准备结果。
        regular = np.append(regular, duration_s)
    return regular


def _load_forward_runner() -> Any:
    """从同一软件包动态加载正向计算入口，避免复制第二套热计算公式。"""
    runner_path = PACKAGE_ROOT / "01-正向仿真" / "02-程序" / "forward_simulation_runner.py"
    if not runner_path.exists():
        raise FileNotFoundError(f"正向计算入口不存在：{runner_path}")

    # 目录名包含连字符，不能使用普通 import；这里按文件路径加载正式入口。
    spec = importlib.util.spec_from_file_location("module01_forward_runner", runner_path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"无法加载正向计算入口：{runner_path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def predict_temperature(
    request: dict[str, Any],
    params: dict[str, float],
    run_dir: Path,
    duration_s: float,
) -> dict[str, Any]:
    """使用正式正向求解器生成可信温度，再转换为智能预测页的十秒采样格式。

    原 ExtraTrees 温度模型在训练域边界会把 300 K 初温错误预测为约 360 K，
    且零时刻是事后硬改，造成首个时间步不连续。缺少原训练样本时不能可靠重训，
    因此温度结果统一回退到模块 01 的同一求解内核，防止向用户展示错误物理量。
    """
    del params  # 参数已经包含在完整 request 中；保留形参用于明确调用合同。
    forward_runner = _load_forward_runner()
    forward_root = run_dir / "forward_temperature_reference"
    emit_progress(state="running_temperature_forward_reference")

    started = time.perf_counter()
    code, forward_result = forward_runner.run_forward_simulation(
        request,
        run_root=forward_root,
        timeout_seconds=1800,
    )
    elapsed_seconds = time.perf_counter() - started
    if code != 0 or not bool(forward_result.get("output_valid")):
        message = forward_result.get("output_message", forward_result.get("status", "未知错误"))
        raise RuntimeError(f"可信温度正向求解失败：{message}")

    # 正向输出为一秒间隔，智能预测页保持原有十秒横轴；非整十秒终点用线性插值补齐。
    source_path = Path(forward_result["temperature_history_csv"])
    source = pd.read_csv(source_path, encoding="utf-8-sig")
    if not {"time_s", "T_1"}.issubset(source.columns):
        raise RuntimeError("正向温度文件缺少 time_s 或 T_1 列。")
    source_time = pd.to_numeric(source["time_s"], errors="coerce").to_numpy(dtype=float)
    source_temperature = pd.to_numeric(source["T_1"], errors="coerce").to_numpy(dtype=float)
    if source_time.size < 2 or not np.isfinite(source_time).all() or not np.isfinite(source_temperature).all():
        raise RuntimeError("正向温度文件包含无效或不足的数据点。")
    if np.any(np.diff(source_time) <= 0.0):
        raise RuntimeError("正向温度时间轴不是严格递增序列。")

    times = prediction_times(duration_s)
    if times[0] < source_time[0] or times[-1] > source_time[-1] + 1.0e-9:
        raise RuntimeError("正向温度时间范围未覆盖请求时长。")
    temperatures = np.interp(times, source_time, source_temperature)
    out_path = run_dir / "temperature_prediction.csv"
    pd.DataFrame(
        {"time_s": times, "temperature_prediction_K": temperatures}
    ).to_csv(out_path, index=False, encoding="utf-8-sig")

    return {
        "temperature_prediction_csv": str(out_path),
        "temperature_point_count": int(len(times)),
        "temperature_initial_K": float(temperatures[0]),
        "temperature_final_K": float(temperatures[-1]),
        "temperature_min_K": float(temperatures.min()),
        "temperature_max_K": float(temperatures.max()),
        "temperature_inference_seconds": elapsed_seconds,
        "temperature_source": "module01_forward_solver",
        "temperature_accuracy_policy": "与正向计算共用求解内核，按十秒采样 T_1",
        "temperature_target_definition": "configured_target_sphere_temperature",
        "temperature_target_sphere_id": 1,
        "temperature_forward_run_dir": forward_result.get("output_dir"),
        "temperature_forward_exe": forward_result.get("forward_exe"),
        "temperature_forward_exe_sha256": forward_result.get("forward_exe_sha256"),
        "legacy_temperature_model_disabled": True,
        "legacy_temperature_model_reason": "边界输入误差过大且零时刻存在不连续",
    }


def _scene_until(state: pd.DataFrame, duration_s: float) -> pd.DataFrame:
    """Select the template timeline and interpolate an exact non-grid endpoint."""
    times = prediction_times(duration_s)
    # 执行数组或表格数据运算，为后续数值处理准备结果。
    existing_times = state["time_s"].drop_duplicates().to_numpy(float)
    selected = state[state["time_s"].isin(times)].copy()
    if np.any(np.isclose(existing_times, duration_s)):
        # 向调用方返回当前步骤生成的数据或迭代结果。
        return selected.sort_values(["frame_id", "sphere_id"], kind="mergesort").reset_index(drop=True)

    lower_time = existing_times[existing_times < duration_s].max()
    upper_time = existing_times[existing_times > duration_s].min()
    lower = state[np.isclose(state["time_s"], lower_time)].sort_values("sphere_id").reset_index(drop=True)
    # 执行数组或表格数据运算，为后续数值处理准备结果。
    upper = state[np.isclose(state["time_s"], upper_time)].sort_values("sphere_id").reset_index(drop=True)
    ratio = (duration_s - lower_time) / (upper_time - lower_time)
    endpoint = lower.copy()
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    static_columns = {
        "sphere_id", "sphere_id_norm", "active_flag", "sphere_init_x", "sphere_init_y", "sphere_init_z",
        "sphere_vx", "sphere_vy", "sphere_vz", "sphere_release_time", "input_GRID_NX", "input_GRID_NY",
        # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
        "input_SPOT_PLANE_SIZE",
    }
    for column in endpoint.columns:
        if column not in static_columns and column not in {"frame_id", "time_s", "sphere_released_flag"}:
            # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
            endpoint[column] = lower[column] + (upper[column] - lower[column]) * ratio
    endpoint["time_s"] = duration_s
    endpoint["frame_id"] = int(np.ceil(duration_s))
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    endpoint["sphere_released_flag"] = (duration_s >= endpoint["sphere_release_time"]).astype(float)
    endpoint["sphere_age_s"] = np.maximum(0.0, duration_s - endpoint["sphere_release_time"])
    return pd.concat([selected, endpoint], ignore_index=True).sort_values(
        ["time_s", "sphere_id"], kind="mergesort"
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    ).reset_index(drop=True)


def predict_point_image(params: dict[str, float], run_dir: Path, duration_s: float) -> dict[str, Any]:
    # 复用已经冻结并验证的 Stage F 模型/模板合同与自定义 Keras 兼容加载代码。
    import stage_f_run_point_image_candidate_evaluation_v1 as stage_f

    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    cfg = load_config()
    template_path = package_path(cfg.get("point_image_scene_template", "02-智能预测/03-模型文件/point_image_fixed_scene_runtime.csv"))
    model_dir = package_path(cfg.get("point_image_model_dir", "02-智能预测/03-模型文件"))
    contract = stage_f.validate_contract(template_path, model_dir)
    # 读取或写入约定的数据文件，并维护统一的路径规则。
    state = _scene_until(stage_f.load_scene_template(template_path), duration_s)
    scalers = json.loads((model_dir / "scalers.json").read_text(encoding="utf-8"))
    emit_progress(state="loading_point_image_model")
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    model = stage_f.load_forward_model(model_dir / "best_model.keras")

    rows = state.copy()
    for k, v in params.items():
        rows[k] = float(v)
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    sample = rows.groupby("frame_id", sort=False).head(1)
    nobj = 16
    gr = sample[stage_f.GLOBAL].to_numpy(np.float32)
    # 执行数组或表格数据运算，为后续数值处理准备结果。
    lr = rows[stage_f.LOCAL].to_numpy(np.float32).reshape(-1, nobj, len(stage_f.LOCAL))
    g = (gr - np.asarray(scalers["global_mean"], np.float32)) / np.asarray(scalers["global_scale"], np.float32)
    l = (lr - np.asarray(scalers["local_mean"], np.float32)) / np.asarray(scalers["local_scale"], np.float32)

    t0 = time.perf_counter()
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    ps = model.predict([g, l], batch_size=64, verbose=0)
    infer_seconds = time.perf_counter() - t0
    y = ps.reshape(-1, nobj, 4) * np.asarray(scalers["target_scale"])[None, None, :] + np.asarray(scalers["target_offset"])[None, None, :]
    # 检查当前条件，仅在满足约束时执行对应分支。
    if not np.isfinite(y).all():
        raise RuntimeError("点图像代理输出包含 NaN/Inf。")

    base = rows[["frame_id", "sphere_id", "time_s", "sphere_released_flag", "input_GRID_NX", "input_GRID_NY", "input_SPOT_PLANE_SIZE"]].copy().reset_index(drop=True)
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    flat = y.reshape(-1, 4)
    for i, c in enumerate(stage_f.TARGET):
        base[f"pred_{c}"] = flat[:, i]
    base["pred_spot_power"] = 10 ** base["pred_log_spot_power"]
    # 记录当前阶段的状态，便于调用方反馈进度并定位问题。
    base["pred_spot_intensity"] = 10 ** base["pred_log_spot_intensity"]
    px, py, valid = stage_f.pixel(
        base["pred_screen_x"].to_numpy(),
        # 执行数组或表格数据运算，为后续数值处理准备结果。
        base["pred_screen_y"].to_numpy(),
        base["input_GRID_NX"].to_numpy(),
        base["input_GRID_NY"].to_numpy(),
        base["input_SPOT_PLANE_SIZE"].to_numpy(),
    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
    )
    base["pixel_x"] = px
    base["pixel_y"] = py
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    base["in_bounds"] = valid

    token_path = run_dir / "point_token_predictions.csv.gz"
    base.to_csv(token_path, index=False, compression="gzip")

    frame_rows = []
    # 遍历当前数据或迭代计算，逐项更新处理结果。
    for frame_id, fg in base.groupby("frame_id", sort=True):
        mask = fg["in_bounds"].to_numpy(bool)
        power = fg["pred_spot_power"].to_numpy(float)
        # 执行数组或表格数据运算，为后续数值处理准备结果。
        intensity = fg["pred_spot_intensity"].to_numpy(float)
        w = power[mask]
        xx = fg["pixel_x"].to_numpy()[mask]
        yy = fg["pixel_y"].to_numpy()[mask]
        # 把本次处理结果加入集合，供后续汇总或输出使用。
        frame_rows.append(
            {
                "frame_id": int(frame_id),
                # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
                "time_s": float(fg["time_s"].iloc[0]),
                "released_count": int((fg["sphere_released_flag"] >= 0.5).sum()),
                "in_bounds_count": int(mask.sum()),
                "total_power": float(w.sum()),
                # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
                "peak_power": float(w.max()) if len(w) else 0.0,
                "total_intensity": float(intensity[mask].sum()),
                "centroid_x_pixel": float(np.average(xx, weights=w)) if w.sum() > 0 else None,
                # 执行数组或表格数据运算，为后续数值处理准备结果。
                "centroid_y_pixel": float(np.average(yy, weights=w)) if w.sum() > 0 else None,
            }
        )
    # 执行数组或表格数据运算，为后续数值处理准备结果。
    frame_df = pd.DataFrame(frame_rows)
    frame_path = run_dir / "point_image_frame_metrics.csv"
    frame_df.to_csv(frame_path, index=False, encoding="utf-8-sig")

    reconstruction = {
        # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
        "grid": "256x256",
        "pixel_index": "1-based",
        "same_pixel_policy": "sum",
        # 记录当前阶段的状态，便于调用方反馈进度并定位问题。
        "inverse_log_policy": "10**log_value",
        "psf": "none",
        "interpolation": "none",
        "smoothing": "none",
        # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
        "gui_reconstruction": "按 pixel_x/pixel_y 将 pred_spot_power 或 pred_spot_intensity 累加到对应像元。",
    }
    reconstruction_path = run_dir / "point_image_reconstruction_contract.json"
    # 读取或写入约定的数据文件，并维护统一的路径规则。
    reconstruction_path.write_text(json.dumps(reconstruction, ensure_ascii=False, indent=2), encoding="utf-8")
    return {
        "point_token_predictions_csv_gz": str(token_path),
        "point_image_frame_metrics_csv": str(frame_path),
        # 读取或写入约定的数据文件，并维护统一的路径规则。
        "point_image_reconstruction_contract_json": str(reconstruction_path),
        "point_image_token_rows": int(len(base)),
        "point_image_frame_count": int(frame_df.shape[0]),
        # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
        "point_image_inference_seconds": infer_seconds,
        "point_image_template": str(template_path),
        "point_image_model": str(model_dir / "best_model.keras"),
        "point_image_contract": contract,
    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
    }


def run_surrogate_prediction(
    request: dict[str, Any],
    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
    *,
    mode: str = "both",
    run_root: Path | None = None,
) -> tuple[int, dict[str, Any]]:
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    start = time.time()
    normalized = standard_input.standardize_surrogate_request(request)
    clean_request = normalized["full_request"]
    # 校验输入数据及运行前提，提前拒绝不符合契约的参数。
    model_params = validate_parameters(normalized["model_parameters"])
    duration_s = float(clean_request["CASE"]["TOTAL_TIME"])

    if mode not in {"temperature", "point-image", "both"}:
        # 检测到无效状态后立即报错，防止异常数据继续传播。
        raise ValueError("mode 仅允许 temperature / point-image / both。")
    root = Path(run_root).resolve() if run_root else OUTPUT_ROOT.resolve()
    run_id = datetime.now().strftime("run_%Y%m%d_%H%M%S_") + uuid.uuid4().hex[:8]
    run_dir = root / run_id
    # 读取或写入约定的数据文件，并维护统一的路径规则。
    run_dir.mkdir(parents=True, exist_ok=False)
    _write_latest_run(run_id, run_dir, "running", mode=mode)

    # 运行目录保存用户完整正式输入和模型规范化审计记录；02/01-输入文件不再作为正式业务输入源。
    request_path = run_dir / "request.json"
    # 读取或写入约定的数据文件，并维护统一的路径规则。
    request_path.write_text(json.dumps(clean_request, ensure_ascii=False, indent=2), encoding="utf-8")
    normalized_path = run_dir / "normalized_surrogate_input.json"
    normalized_for_file = {k: v for k, v in normalized.items() if k != "full_request"}
    normalized_path.write_text(json.dumps(normalized_for_file, ensure_ascii=False, indent=2), encoding="utf-8")

    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    result: dict[str, Any] = {
        "module": "02",
        "status": "running",
        # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
        "run_id": run_id,
        "mode": mode,
        "input_contract": "forward-request-v1",
        "surrogate_runtime_contract": "surrogate-standard-input-v1",
        # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
        "model_parameters": model_params,
        "model_parameter_domain": MODEL_PARAMETER_DOMAIN,
        "applicability": normalized["applicability"],
        # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
        "run_dir": str(run_dir),
        "request_json": str(request_path),
        "normalized_surrogate_input_json": str(normalized_path),
    }
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    emit_progress(state="running", run_id=run_id, mode=mode)

    result_path = run_dir / "prediction_summary.json"
    try:
        # 检查当前条件，仅在满足约束时执行对应分支。
        if mode in {"temperature", "both"}:
            result.update(predict_temperature(clean_request, model_params, run_dir, duration_s))
            emit_progress(state="temperature_completed", run_id=run_id)
        if mode in {"point-image", "both"}:
            # 把本次处理结果加入集合，供后续汇总或输出使用。
            result.update(predict_point_image(model_params, run_dir, duration_s))
            emit_progress(state="point_image_completed", run_id=run_id)

        result["status"] = "success"
        # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
        result["return_code"] = 0
        result["elapsed_seconds"] = time.time() - start
        result["prediction_summary_json"] = str(result_path)
        # 读取或写入约定的数据文件，并维护统一的路径规则。
        result_path.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
        _write_latest_run(run_id, run_dir, "success", mode=mode)
        emit_progress(state="success", run_id=run_id, elapsed_seconds=result["elapsed_seconds"])
        return 0, result
    # 捕获本阶段异常并转换为明确的错误信息或运行状态。
    except Exception as exc:
        result.update(
            {
                # 记录当前阶段的状态，便于调用方反馈进度并定位问题。
                "status": "error",
                "return_code": 2,
                "elapsed_seconds": time.time() - start,
                "error_type": type(exc).__name__,
                # 记录当前阶段的状态，便于调用方反馈进度并定位问题。
                "message": str(exc),
                "prediction_summary_json": str(result_path),
            }
        # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
        )
        result_path.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
        _write_latest_run(run_id, run_dir, "error", mode=mode)
        emit_progress(state="error", run_id=run_id, error_type=type(exc).__name__, message=str(exc))
        # 检测到无效状态后立即报错，防止异常数据继续传播。
        raise

def parse_args() -> argparse.Namespace:
    ap = argparse.ArgumentParser(
        # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
        description="02-智能预测统一入口：与01共用完整 forward-request-v1 JSON -> 适用域检查 -> 温度/点图像代理。"
    )
    ap.add_argument(
        "--params-json",
        # 读取或写入约定的数据文件，并维护统一的路径规则。
        type=Path,
        required=True,
        help="与01正向仿真完全相同的完整 forward-request-v1 JSON。",
    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
    )
    ap.add_argument("--mode", choices=["temperature", "point-image", "both"], default="both")
    ap.add_argument("--run-root", type=Path, default=None)
    return ap.parse_args()


# 定义 request_from_args 处理过程，集中封装该步骤的输入、输出与异常边界。
def request_from_args(args: argparse.Namespace) -> dict[str, Any]:
    return json.loads(args.params_json.read_text(encoding="utf-8"))


def main() -> int:
    # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
    args = parse_args()
    try:
        code, result = run_surrogate_prediction(request_from_args(args), mode=args.mode, run_root=args.run_root)
        # 读取或写入约定的数据文件，并维护统一的路径规则。
        print(json.dumps(result, ensure_ascii=False))
        return code
    except Exception as exc:
        err = {
            # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
            "module": "02",
            "status": "error",
            "error_type": type(exc).__name__,
            # 记录当前阶段的状态，便于调用方反馈进度并定位问题。
            "message": str(exc),
        }
        print(json.dumps(err, ensure_ascii=False), file=sys.stderr)
        return 2


# 检查当前条件，仅在满足约束时执行对应分支。
if __name__ == "__main__":
    raise SystemExit(main())
