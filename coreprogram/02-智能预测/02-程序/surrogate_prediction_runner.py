from __future__ import annotations

"""02-智能预测统一标准入口。

正式输入与 01 正向仿真完全一致：forward-request-v1 JSON。
本入口先调用 surrogate_standard_input.py 复用 01 输入校验并检查当前冻结代理模型适用域，
再将完整业务输入转换为模型所需特征。

成功运行后还会在当前 run 目录下生成 output\，其中 temperature_history.csv
和 infrared_response_history.csv 使用与 01/03 对接一致的核心文件名与列合同。
当前冻结模型不能提供的源辐射量保持为空，不伪造成物理结果。

支持模式：temperature / point-image / both。

正式调用：
    python surrogate_prediction_runner.py --params-json forward_request.json --mode both
"""

import argparse
import hashlib
import json
import sys
import time
import uuid
from datetime import datetime
from pathlib import Path
from typing import Any

import joblib
import numpy as np
import pandas as pd

import surrogate_standard_input as standard_input
import surrogate_forward_output_contract as forward_output_contract

PROGRAM_DIR = Path(__file__).resolve().parent
MODULE_ROOT = PROGRAM_DIR.parent
PACKAGE_ROOT = MODULE_ROOT.parent
MODEL_DIR = MODULE_ROOT / "03-模型文件"
INPUT_DIR = MODULE_ROOT / "01-输入文件"
OUTPUT_DIR = MODULE_ROOT / "04-输出文件"
OUTPUT_ROOT = OUTPUT_DIR / "runs"
LATEST_RUN_PATH = OUTPUT_DIR / "latest_run.json"
CONFIG_PATH = PACKAGE_ROOT / "config.json"

M2_EXPECTED_SHA = "7aae9e013ed816e35c73a48fbbe53f6d4331298090e77c1c1a2d04cc2f13ad7a"
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
MODEL_PARAMETER_DOMAIN = standard_input.MODEL_PARAMETER_DOMAIN


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for block in iter(lambda: f.read(4 * 1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def load_config() -> dict[str, Any]:
    if not CONFIG_PATH.exists():
        return {}
    return json.loads(CONFIG_PATH.read_text(encoding="utf-8"))


def package_path(raw: str | Path) -> Path:
    p = Path(raw)
    return p.resolve() if p.is_absolute() else (PACKAGE_ROOT / p).resolve()


def emit_progress(**payload: Any) -> None:
    print("GUI_PROGRESS " + json.dumps({"module": "02", "timestamp": time.time(), **payload}, ensure_ascii=False), flush=True)


def _write_latest_run(run_id: str, run_dir: Path, status: str, *, mode: str | None = None) -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    payload = {
        "module": "02",
        "run_id": run_id,
        "run_dir": str(run_dir.resolve()),
        "status": status,
        "mode": mode,
        "timestamp": time.time(),
    }
    LATEST_RUN_PATH.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")


def _to_float(name: str, value: Any) -> float:
    try:
        out = float(value)
    except Exception as exc:
        raise ValueError(f"参数 {name} 必须为数值，当前值={value!r}") from exc
    if not np.isfinite(out):
        raise ValueError(f"参数 {name} 必须为有限数值。")
    return out


def validate_parameters(params: dict[str, Any]) -> dict[str, float]:
    clean: dict[str, float] = {}
    for name, rule in MODEL_PARAMETER_DOMAIN.items():
        if name not in params:
            raise ValueError(f"缺少必需参数：{name}")
        value = _to_float(name, params[name])
        if not (float(rule["min"]) <= value <= float(rule["max"])):
            raise ValueError(
                f"参数 {name} 超出当前代理模型训练/验证适用域：{value}，"
                f"允许 [{rule['min']}, {rule['max']}] {rule['unit']}。"
            )
        clean[name] = value
    return clean


def predict_temperature(params: dict[str, float], run_dir: Path) -> dict[str, Any]:
    cfg = load_config()
    model_path = package_path(cfg.get("temperature_model", "02-智能预测/03-模型文件/formal_configured_target_temperature_extratrees.joblib"))
    if not model_path.exists():
        raise FileNotFoundError(f"M2温度模型不存在：{model_path}")
    actual_sha = sha256(model_path)
    if actual_sha != M2_EXPECTED_SHA:
        raise RuntimeError(f"M2温度模型 SHA256 不匹配：{actual_sha}")
    payload = joblib.load(model_path)
    # scikit-learn 1.6.1 保存的 SimpleImputer 在较新版本中可能缺少 _fill_dtype。
    # 仅在该属性缺失时按其已冻结 statistics_ dtype 恢复；本兼容补丁不改变模型参数。
    model = payload["model"]
    compatibility_patch = False
    if hasattr(model, "named_steps") and "impute" in model.named_steps:
        imputer = model.named_steps["impute"]
        if not hasattr(imputer, "_fill_dtype") and hasattr(imputer, "statistics_"):
            imputer._fill_dtype = imputer.statistics_.dtype
            compatibility_patch = True
    features = list(payload.get("features", []))
    if features != EXPECTED_FEATURES:
        raise RuntimeError(f"M2特征合同不匹配：{features}")

    q = params["q_int"]
    e = params["emissivity_ir"]
    a = params["absorptivity_solar"]
    x = pd.DataFrame(
        {
            "q_int": np.full(101, q),
            "emissivity_ir": np.full(101, e),
            "absorptivity_solar": np.full(101, a),
            "time_s": TIMES,
            "q_int_times_absorptivity_solar": np.full(101, q * a),
            "q_int_div_emissivity_ir": np.full(101, q / e),
            "is_initial_condition": (TIMES == 0.0).astype(int),
        }
    )
    t0 = time.perf_counter()
    y = np.asarray(model.predict(x[features]), dtype=float)
    infer_seconds = time.perf_counter() - t0
    if y.shape != (101,) or not np.isfinite(y).all():
        raise RuntimeError("M2输出不是101个有限温度值。")
    y[0] = 300.0
    out = pd.DataFrame({"time_s": TIMES, "temperature_prediction_K": y})
    out_path = run_dir / "temperature_prediction.csv"
    out.to_csv(out_path, index=False, encoding="utf-8-sig")
    return {
        "temperature_prediction_csv": str(out_path),
        "temperature_point_count": 101,
        "temperature_initial_K": float(y[0]),
        "temperature_final_K": float(y[-1]),
        "temperature_min_K": float(y.min()),
        "temperature_max_K": float(y.max()),
        "temperature_inference_seconds": infer_seconds,
        "temperature_model": str(model_path),
        "temperature_model_sha256": actual_sha,
        "temperature_target_definition": payload.get("physical_definition", "configured_target_sphere_temperature"),
        "temperature_target_sphere_id": payload.get("target_sphere_id", 1),
        "sklearn_simpleimputer_compatibility_patch_applied": compatibility_patch,
    }


def predict_point_image(params: dict[str, float], run_dir: Path) -> dict[str, Any]:
    # 复用已经冻结并验证的 Stage F 模型/模板合同与自定义 Keras 兼容加载代码。
    import stage_f_run_point_image_candidate_evaluation_v1 as stage_f

    cfg = load_config()
    template_path = package_path(cfg.get("point_image_scene_template", "02-智能预测/03-模型文件/point_image_fixed_scene_runtime.csv"))
    model_dir = package_path(cfg.get("point_image_model_dir", "02-智能预测/03-模型文件"))
    contract = stage_f.validate_contract(template_path, model_dir)
    state = stage_f.load_scene_template(template_path)
    scalers = json.loads((model_dir / "scalers.json").read_text(encoding="utf-8"))
    emit_progress(state="loading_point_image_model")
    model = stage_f.load_forward_model(model_dir / "best_model.keras")

    rows = state.copy()
    for k, v in params.items():
        rows[k] = float(v)
    sample = rows.groupby("frame_id", sort=False).head(1)
    nobj = 16
    gr = sample[stage_f.GLOBAL].to_numpy(np.float32)
    lr = rows[stage_f.LOCAL].to_numpy(np.float32).reshape(-1, nobj, len(stage_f.LOCAL))
    g = (gr - np.asarray(scalers["global_mean"], np.float32)) / np.asarray(scalers["global_scale"], np.float32)
    l = (lr - np.asarray(scalers["local_mean"], np.float32)) / np.asarray(scalers["local_scale"], np.float32)

    t0 = time.perf_counter()
    ps = model.predict([g, l], batch_size=64, verbose=0)
    infer_seconds = time.perf_counter() - t0
    y = ps.reshape(-1, nobj, 4) * np.asarray(scalers["target_scale"])[None, None, :] + np.asarray(scalers["target_offset"])[None, None, :]
    if not np.isfinite(y).all():
        raise RuntimeError("点图像代理输出包含 NaN/Inf。")

    # Keep the state fields required to build the 01-compatible infrared-response contract.
    base = rows[[
        "frame_id",
        "sphere_id",
        "time_s",
        "active_flag",
        "sphere_released_flag",
        "distance_to_detector",
        "input_GRID_NX",
        "input_GRID_NY",
        "input_SPOT_PLANE_SIZE",
    ]].copy().reset_index(drop=True)
    flat = y.reshape(-1, 4)
    for i, c in enumerate(stage_f.TARGET):
        base[f"pred_{c}"] = flat[:, i]
    base["pred_spot_power"] = 10 ** base["pred_log_spot_power"]
    base["pred_spot_intensity"] = 10 ** base["pred_log_spot_intensity"]
    px, py, valid = stage_f.pixel(
        base["pred_screen_x"].to_numpy(),
        base["pred_screen_y"].to_numpy(),
        base["input_GRID_NX"].to_numpy(),
        base["input_GRID_NY"].to_numpy(),
        base["input_SPOT_PLANE_SIZE"].to_numpy(),
    )
    base["pixel_x"] = px
    base["pixel_y"] = py
    base["in_bounds"] = valid

    token_path = run_dir / "point_token_predictions.csv.gz"
    base.to_csv(token_path, index=False, compression="gzip")

    frame_rows = []
    for frame_id, fg in base.groupby("frame_id", sort=True):
        mask = fg["in_bounds"].to_numpy(bool)
        power = fg["pred_spot_power"].to_numpy(float)
        intensity = fg["pred_spot_intensity"].to_numpy(float)
        w = power[mask]
        xx = fg["pixel_x"].to_numpy()[mask]
        yy = fg["pixel_y"].to_numpy()[mask]
        frame_rows.append(
            {
                "frame_id": int(frame_id),
                "time_s": float(fg["time_s"].iloc[0]),
                "released_count": int((fg["sphere_released_flag"] >= 0.5).sum()),
                "in_bounds_count": int(mask.sum()),
                "total_power": float(w.sum()),
                "peak_power": float(w.max()) if len(w) else 0.0,
                "total_intensity": float(intensity[mask].sum()),
                "centroid_x_pixel": float(np.average(xx, weights=w)) if w.sum() > 0 else None,
                "centroid_y_pixel": float(np.average(yy, weights=w)) if w.sum() > 0 else None,
            }
        )
    frame_df = pd.DataFrame(frame_rows)
    frame_path = run_dir / "point_image_frame_metrics.csv"
    frame_df.to_csv(frame_path, index=False, encoding="utf-8-sig")

    reconstruction = {
        "grid": "256x256",
        "pixel_index": "1-based",
        "same_pixel_policy": "sum",
        "inverse_log_policy": "10**log_value",
        "psf": "none",
        "interpolation": "none",
        "smoothing": "none",
        "gui_reconstruction": "按 pixel_x/pixel_y 将 pred_spot_power 或 pred_spot_intensity 累加到对应像元。",
    }
    reconstruction_path = run_dir / "point_image_reconstruction_contract.json"
    reconstruction_path.write_text(json.dumps(reconstruction, ensure_ascii=False, indent=2), encoding="utf-8")
    return {
        "point_token_predictions_csv_gz": str(token_path),
        "point_image_frame_metrics_csv": str(frame_path),
        "point_image_reconstruction_contract_json": str(reconstruction_path),
        "point_image_token_rows": int(len(base)),
        "point_image_frame_count": int(frame_df.shape[0]),
        "point_image_inference_seconds": infer_seconds,
        "point_image_template": str(template_path),
        "point_image_model": str(model_dir / "best_model.keras"),
        "point_image_contract": contract,
    }


def run_surrogate_prediction(
    request: dict[str, Any],
    *,
    mode: str = "both",
    run_root: Path | None = None,
) -> tuple[int, dict[str, Any]]:
    start = time.time()
    normalized = standard_input.standardize_surrogate_request(request)
    clean_request = normalized["full_request"]
    model_params = validate_parameters(normalized["model_parameters"])

    if mode not in {"temperature", "point-image", "both"}:
        raise ValueError("mode 仅允许 temperature / point-image / both。")
    root = Path(run_root).resolve() if run_root else OUTPUT_ROOT.resolve()
    run_id = datetime.now().strftime("run_%Y%m%d_%H%M%S_") + uuid.uuid4().hex[:8]
    run_dir = root / run_id
    run_dir.mkdir(parents=True, exist_ok=False)
    _write_latest_run(run_id, run_dir, "running", mode=mode)

    # 运行目录保存用户完整正式输入和模型规范化审计记录；02/01-输入文件不再作为正式业务输入源。
    request_path = run_dir / "request.json"
    request_path.write_text(json.dumps(clean_request, ensure_ascii=False, indent=2), encoding="utf-8")
    normalized_path = run_dir / "normalized_surrogate_input.json"
    normalized_for_file = {k: v for k, v in normalized.items() if k != "full_request"}
    normalized_path.write_text(json.dumps(normalized_for_file, ensure_ascii=False, indent=2), encoding="utf-8")

    result: dict[str, Any] = {
        "module": "02",
        "status": "running",
        "run_id": run_id,
        "mode": mode,
        "input_contract": "forward-request-v1",
        "surrogate_runtime_contract": "surrogate-standard-input-v1",
        "model_parameters": model_params,
        "model_parameter_domain": MODEL_PARAMETER_DOMAIN,
        "applicability": normalized["applicability"],
        "run_dir": str(run_dir),
        "request_json": str(request_path),
        "normalized_surrogate_input_json": str(normalized_path),
    }
    emit_progress(state="running", run_id=run_id, mode=mode)

    result_path = run_dir / "prediction_summary.json"
    try:
        if mode in {"temperature", "both"}:
            result.update(predict_temperature(model_params, run_dir))
            emit_progress(state="temperature_completed", run_id=run_id)
        if mode in {"point-image", "both"}:
            result.update(predict_point_image(model_params, run_dir))
            emit_progress(state="point_image_completed", run_id=run_id)

        # Build an 01-compatible response core under run_dir/output.  In mode=both
        # this directory can be passed directly to 03 as the candidate run.
        result.update(
            forward_output_contract.build_forward_response_output(
                run_dir, run_id=run_id, mode=mode, prediction_result=result
            )
        )
        emit_progress(
            state="formal_output_completed",
            run_id=run_id,
            formal_output_dir=result.get("formal_output_dir"),
            similarity_ready=result.get("03_similarity_ready", False),
        )

        result["status"] = "success"
        result["return_code"] = 0
        result["elapsed_seconds"] = time.time() - start
        result["prediction_summary_json"] = str(result_path)
        result_path.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
        _write_latest_run(run_id, run_dir, "success", mode=mode)
        emit_progress(state="success", run_id=run_id, elapsed_seconds=result["elapsed_seconds"])
        return 0, result
    except Exception as exc:
        result.update(
            {
                "status": "error",
                "return_code": 2,
                "elapsed_seconds": time.time() - start,
                "error_type": type(exc).__name__,
                "message": str(exc),
                "prediction_summary_json": str(result_path),
            }
        )
        result_path.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
        _write_latest_run(run_id, run_dir, "error", mode=mode)
        emit_progress(state="error", run_id=run_id, error_type=type(exc).__name__, message=str(exc))
        raise

def parse_args() -> argparse.Namespace:
    ap = argparse.ArgumentParser(
        description="02-智能预测统一入口：与01共用完整 forward-request-v1 JSON -> 适用域检查 -> 温度/点图像代理。"
    )
    ap.add_argument(
        "--params-json",
        type=Path,
        required=True,
        help="与01正向仿真完全相同的完整 forward-request-v1 JSON。",
    )
    ap.add_argument("--mode", choices=["temperature", "point-image", "both"], default="both")
    ap.add_argument("--run-root", type=Path, default=None)
    return ap.parse_args()


def request_from_args(args: argparse.Namespace) -> dict[str, Any]:
    return json.loads(args.params_json.read_text(encoding="utf-8"))


def main() -> int:
    args = parse_args()
    try:
        code, result = run_surrogate_prediction(request_from_args(args), mode=args.mode, run_root=args.run_root)
        print(json.dumps(result, ensure_ascii=False))
        return code
    except Exception as exc:
        err = {
            "module": "02",
            "status": "error",
            "error_type": type(exc).__name__,
            "message": str(exc),
        }
        print(json.dumps(err, ensure_ascii=False), file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
