from __future__ import annotations

"""Build an 01-compatible response-output directory from 02 surrogate products.

The purpose of this adapter is interface compatibility, not to claim that the
current frozen surrogate predicts every physical quantity produced by module 01.
It writes the same *core* filenames and column names consumed by module 03:

- temperature_history.csv
- infrared_response_history.csv

Current model capability is represented explicitly:
- T_1 is predicted by the temperature surrogate; T_2..T_16 are unavailable.
- screen position and detector-side point power are predicted by the point-image
  surrogate.
- detector irradiance is deterministically reconstructed from predicted point
  power and configured pixel area under the current no-PSF/same-pixel contract.
- source radiation_power_W and radiant_intensity_W_sr are not outputs of the
  frozen 02 models and are therefore written as missing values, never fabricated.

Module 03 v1.0.1+ treats those explicitly unavailable source-radiation fields as
non-participating similarity components while still evaluating the temperature,
detector response and screen-position components that 02 actually predicts.
"""

import json
import math
import time
from pathlib import Path
from typing import Any

import numpy as np
import pandas as pd


CONTRACT_SCHEMA = "forward-response-output-v1"
TEMPERATURE_OBJECT_COUNT = 16
IR_COLUMNS = [
    "case_id",
    "frame_id",
    "time_s",
    "object_id",
    "active_flag",
    "released_flag",
    "radiation_power_W",
    "radiant_intensity_W_sr",
    "detector_received_power_W",
    "detector_irradiance_W_m2",
    "screen_x_m",
    "screen_y_m",
    "in_screen_flag",
    "range_to_detector_m",
]


class ForwardOutputContractError(RuntimeError):
    pass


def _finite_numeric(df: pd.DataFrame, cols: list[str], label: str) -> pd.DataFrame:
    out = df.copy()
    for col in cols:
        out[col] = pd.to_numeric(out[col], errors="coerce")
    bad = ~np.isfinite(out[cols].to_numpy(dtype=float)).all(axis=1)
    if bad.any():
        rows = out.loc[bad, cols].head(5).to_dict("records")
        raise ForwardOutputContractError(f"{label} contains missing/nonfinite values; examples={rows}")
    return out


def _write_temperature_history(run_dir: Path, output_dir: Path) -> dict[str, Any]:
    source = run_dir / "temperature_prediction.csv"
    if not source.is_file():
        raise ForwardOutputContractError(f"temperature mode completed but source file is missing: {source}")
    pred = pd.read_csv(source, encoding="utf-8-sig")
    need = ["time_s", "temperature_prediction_K"]
    missing = [c for c in need if c not in pred.columns]
    if missing:
        raise ForwardOutputContractError(f"temperature_prediction.csv missing columns: {missing}")
    pred = _finite_numeric(pred, need, "temperature_prediction.csv")
    pred = pred.sort_values("time_s", kind="mergesort").reset_index(drop=True)
    if pred["time_s"].duplicated().any() or len(pred) < 2 or np.any(np.diff(pred["time_s"].to_numpy(float)) <= 0):
        raise ForwardOutputContractError("temperature_prediction.csv time axis must be unique and strictly increasing")

    formal = pd.DataFrame({
        "frame": np.arange(len(pred), dtype=int),
        "time_s": pred["time_s"].to_numpy(float),
    })
    # The frozen temperature surrogate only predicts the configured target sphere (sphere 1).
    formal["T_1"] = pred["temperature_prediction_K"].to_numpy(float)
    for object_id in range(2, TEMPERATURE_OBJECT_COUNT + 1):
        formal[f"T_{object_id}"] = np.nan

    path = output_dir / "temperature_history.csv"
    formal.to_csv(path, index=False, encoding="utf-8-sig")
    return {
        "temperature_history_csv": str(path),
        "temperature_available_object_ids": [1],
        "temperature_unavailable_object_ids": list(range(2, TEMPERATURE_OBJECT_COUNT + 1)),
        "temperature_point_count": int(len(formal)),
    }


def _write_infrared_history(run_dir: Path, output_dir: Path, run_id: str) -> dict[str, Any]:
    source = run_dir / "point_token_predictions.csv.gz"
    if not source.is_file():
        raise ForwardOutputContractError(f"point-image mode completed but source file is missing: {source}")
    tok = pd.read_csv(source, compression="gzip")
    need = [
        "frame_id",
        "sphere_id",
        "time_s",
        "active_flag",
        "sphere_released_flag",
        "distance_to_detector",
        "input_GRID_NX",
        "input_GRID_NY",
        "input_SPOT_PLANE_SIZE",
        "pred_screen_x",
        "pred_screen_y",
        "pred_spot_power",
        "in_bounds",
    ]
    missing = [c for c in need if c not in tok.columns]
    if missing:
        raise ForwardOutputContractError(
            "point_token_predictions.csv.gz cannot build the 01-compatible infrared contract; "
            f"missing columns: {missing}. Replace surrogate_prediction_runner.py with the v1.0.1 patch."
        )

    numeric = [c for c in need if c != "in_bounds"]
    tok = _finite_numeric(tok, numeric, "point_token_predictions.csv.gz")
    if pd.api.types.is_bool_dtype(tok["in_bounds"]):
        in_bounds_values = tok["in_bounds"].to_numpy(bool)
    else:
        raw_bool = tok["in_bounds"].astype(str).str.strip().str.lower()
        valid_bool = raw_bool.isin({"true", "false", "1", "0"})
        if not valid_bool.all():
            raise ForwardOutputContractError("point_token_predictions.csv.gz.in_bounds contains invalid boolean values")
        in_bounds_values = raw_bool.isin({"true", "1"}).to_numpy(bool)
    tok["in_bounds"] = in_bounds_values

    nx = tok["input_GRID_NX"].to_numpy(float)
    ny = tok["input_GRID_NY"].to_numpy(float)
    size = tok["input_SPOT_PLANE_SIZE"].to_numpy(float)
    pixel_area = (size / nx) * (size / ny)
    if not np.isfinite(pixel_area).all() or np.any(pixel_area <= 0):
        raise ForwardOutputContractError("invalid pixel area reconstructed from point-image template")

    active = tok["active_flag"].to_numpy(float) >= 0.5
    released = tok["sphere_released_flag"].to_numpy(float) >= 0.5
    in_screen = tok["in_bounds"].to_numpy(bool) & active & released
    pred_power = tok["pred_spot_power"].to_numpy(float)
    if not np.isfinite(pred_power).all() or np.any(pred_power < 0):
        raise ForwardOutputContractError("pred_spot_power contains invalid/negative values")

    received_power = np.where(in_screen, pred_power, 0.0)
    detector_irradiance = np.where(in_screen, received_power / pixel_area, 0.0)

    formal = pd.DataFrame(
        {
            "case_id": [f"02_surrogate_{run_id}"] * len(tok),
            "frame_id": tok["frame_id"].astype(int),
            "time_s": tok["time_s"].astype(float),
            "object_id": tok["sphere_id"].astype(int),
            "active_flag": active.astype(int),
            "released_flag": released.astype(int),
            # Not predicted by current frozen 02 models. Missing is intentional.
            "radiation_power_W": np.nan,
            "radiant_intensity_W_sr": np.nan,
            "detector_received_power_W": received_power,
            "detector_irradiance_W_m2": detector_irradiance,
            "screen_x_m": tok["pred_screen_x"].astype(float),
            "screen_y_m": tok["pred_screen_y"].astype(float),
            "in_screen_flag": in_screen.astype(int),
            "range_to_detector_m": tok["distance_to_detector"].astype(float),
        },
        columns=IR_COLUMNS,
    )
    if formal.duplicated(["time_s", "object_id"]).any():
        raise ForwardOutputContractError("constructed infrared history has duplicate (time_s, object_id) keys")
    formal = formal.sort_values(["object_id", "time_s"], kind="mergesort").reset_index(drop=True)
    path = output_dir / "infrared_response_history.csv"
    formal.to_csv(path, index=False, encoding="utf-8-sig")
    return {
        "infrared_response_history_csv": str(path),
        "infrared_row_count": int(len(formal)),
        "infrared_object_count": int(formal["object_id"].nunique()),
        "infrared_time_point_count": int(formal["time_s"].nunique()),
        "predicted_or_derived_ir_fields": [
            "detector_received_power_W",
            "detector_irradiance_W_m2",
            "screen_x_m",
            "screen_y_m",
            "in_screen_flag",
            "range_to_detector_m",
        ],
        "unavailable_ir_fields": ["radiation_power_W", "radiant_intensity_W_sr"],
    }


def build_forward_response_output(
    run_dir: Path,
    *,
    run_id: str,
    mode: str,
    prediction_result: dict[str, Any],
) -> dict[str, Any]:
    """Create ``run_dir/output`` with the 01-compatible response core."""
    run_dir = Path(run_dir).resolve()
    output_dir = run_dir / "output"
    output_dir.mkdir(parents=True, exist_ok=True)

    details: dict[str, Any] = {}
    if mode in {"temperature", "both"}:
        details.update(_write_temperature_history(run_dir, output_dir))
    if mode in {"point-image", "both"}:
        details.update(_write_infrared_history(run_dir, output_dir, run_id))

    ready = (output_dir / "temperature_history.csv").is_file() and (output_dir / "infrared_response_history.csv").is_file()
    contract = {
        "schema_version": CONTRACT_SCHEMA,
        "source_module": "02",
        "run_id": run_id,
        "mode": mode,
        "status": "complete_for_03_similarity" if ready else "partial",
        "03_similarity_ready": bool(ready),
        "consumer_contract": "03 similarity evaluator v1.0.1+",
        "core_files": {
            "temperature_history.csv": (output_dir / "temperature_history.csv").is_file(),
            "infrared_response_history.csv": (output_dir / "infrared_response_history.csv").is_file(),
        },
        "temperature_contract": {
            "columns": ["frame", "time_s"] + [f"T_{i}" for i in range(1, TEMPERATURE_OBJECT_COUNT + 1)],
            "available_object_ids": details.get("temperature_available_object_ids", []),
            "unavailable_object_ids": details.get("temperature_unavailable_object_ids", list(range(1, TEMPERATURE_OBJECT_COUNT + 1))),
            "note": "Current ExtraTrees model predicts only configured target sphere 1; unavailable temperature columns are intentionally blank.",
        },
        "infrared_contract": {
            "columns": IR_COLUMNS,
            "predicted_or_derived_fields": details.get("predicted_or_derived_ir_fields", []),
            "unavailable_fields": details.get("unavailable_ir_fields", []),
            "mapping": {
                "detector_received_power_W": "point-image model pred_spot_power for active/released/in-screen token",
                "detector_irradiance_W_m2": "detector_received_power_W / pixel_area under current no-PSF/same-pixel reconstruction",
                "screen_x_m": "point-image model pred_screen_x",
                "screen_y_m": "point-image model pred_screen_y",
                "range_to_detector_m": "frozen scene runtime template distance_to_detector",
                "radiation_power_W": "UNAVAILABLE in frozen 02 model; intentionally blank",
                "radiant_intensity_W_sr": "UNAVAILABLE in frozen 02 model; intentionally blank",
            },
            "native_pred_spot_intensity_policy": "Kept only in point_token_predictions.csv.gz; not relabeled as radiant_intensity_W_sr because the legacy training field is not the W/sr source-radiant-intensity contract.",
        },
        "generated_at_epoch_s": time.time(),
    }
    contract_path = output_dir / "output_contract.json"
    contract_path.write_text(json.dumps(contract, ensure_ascii=False, indent=2), encoding="utf-8")

    metadata = {
        "schema_version": "surrogate-forward-response-metadata-v1",
        "source_module": "02",
        "run_id": run_id,
        "mode": mode,
        "input_contract": prediction_result.get("input_contract"),
        "surrogate_runtime_contract": prediction_result.get("surrogate_runtime_contract"),
        "output_contract": CONTRACT_SCHEMA,
        "03_similarity_ready": bool(ready),
        "model_parameters": prediction_result.get("model_parameters"),
        "temperature_model": prediction_result.get("temperature_model"),
        "temperature_model_sha256": prediction_result.get("temperature_model_sha256"),
        "point_image_model": prediction_result.get("point_image_model"),
        "point_image_contract": prediction_result.get("point_image_contract"),
        "physical_availability_note": "Contract shape is aligned for 03 consumption; current model capability is not expanded. Missing source-radiation fields remain invalid/non-participating.",
    }
    metadata_path = output_dir / "run_metadata.json"
    metadata_path.write_text(json.dumps(metadata, ensure_ascii=False, indent=2), encoding="utf-8")

    status_path = output_dir / "solver_status.txt"
    status_path.write_text(
        "MODULE=02\n"
        "STATUS=SUCCESS\n"
        f"RUN_ID={run_id}\n"
        f"OUTPUT_CONTRACT={CONTRACT_SCHEMA}\n"
        f"SIMILARITY_READY={str(bool(ready)).lower()}\n",
        encoding="utf-8",
    )

    return {
        "formal_output_dir": str(output_dir),
        "forward_response_contract": CONTRACT_SCHEMA,
        "03_similarity_ready": bool(ready),
        "output_contract_json": str(contract_path),
        "formal_run_metadata_json": str(metadata_path),
        "formal_solver_status_txt": str(status_path),
        **details,
    }
