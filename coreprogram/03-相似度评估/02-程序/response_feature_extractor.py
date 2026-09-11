from __future__ import annotations

"""Extract formal temperature/infrared features from one 01-forward run directory.

Formal 01 inputs expected in ``run_dir``:
- temperature_history.csv
- infrared_response_history.csv
- frame_summary.csv (optional cross-check / auxiliary values)
- run_metadata.json (optional)

The extractor intentionally requires ``radiant_intensity_W_sr`` in the infrared
business output. It does not silently reconstruct that physical quantity from a
legacy column, so an outdated 01 executable is detected rather than hidden.
"""

import json
import math
from pathlib import Path
from typing import Any

import numpy as np
import pandas as pd


class FeatureExtractionError(RuntimeError):
    pass


def _read_csv_compatible(path: Path) -> pd.DataFrame:
    """Read 01 CSV outputs robustly across Windows/Fortran text encodings.

    Intel Fortran on a Chinese Windows environment may write non-ASCII text
    (for example a Chinese case_id) using the active ANSI code page instead of
    UTF-8.  The formal numeric columns are unchanged, so 03 first tries UTF-8
    variants and then GB18030/CP936.  We intentionally do not fall back to
    latin-1 because that would silently turn undecodable Chinese text into
    mojibake and could hide a malformed file.
    """
    attempts: list[str] = []
    for encoding in ("utf-8-sig", "utf-8", "gb18030", "cp936"):
        try:
            return pd.read_csv(path, encoding=encoding)
        except UnicodeDecodeError as exc:
            attempts.append(f"{encoding}: {exc}")

    raise FeatureExtractionError(
        f"Unable to decode CSV file: {path}. "
        "Tried utf-8-sig, utf-8, gb18030 and cp936. "
        + " | ".join(attempts)
    )


def load_config(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as f:
        cfg = json.load(f)
    if cfg.get("schema_version") != "similarity-config-v1":
        raise FeatureExtractionError(
            f"Unsupported config schema_version={cfg.get('schema_version')!r}; expected similarity-config-v1"
        )
    return cfg


def _require_columns(df: pd.DataFrame, required: list[str], file_label: str) -> None:
    missing = [c for c in required if c not in df.columns]
    if missing:
        raise FeatureExtractionError(f"{file_label} missing required columns: {missing}")


def _numeric(df: pd.DataFrame, columns: list[str], label: str) -> pd.DataFrame:
    out = df.copy()
    for col in columns:
        out[col] = pd.to_numeric(out[col], errors="coerce")
    bad = out[columns].isna().any(axis=1)
    if bad.any():
        examples = out.loc[bad, columns].head(5).to_dict("records")
        raise FeatureExtractionError(f"{label} contains nonnumeric/missing values; examples={examples}")
    return out


def _strict_time_axis(values: np.ndarray, label: str) -> None:
    if len(values) < 2:
        raise FeatureExtractionError(f"{label} requires at least 2 time points")
    if not np.isfinite(values).all():
        raise FeatureExtractionError(f"{label} contains NaN/Inf time values")
    if np.any(np.diff(values) <= 0):
        raise FeatureExtractionError(f"{label} time axis must be strictly increasing")


def finite_difference(time_s: np.ndarray, values: np.ndarray) -> np.ndarray:
    """Backward difference on an explicit nonuniform time axis; first sample is NaN."""
    t = np.asarray(time_s, dtype=float)
    y = np.asarray(values, dtype=float)
    if len(t) != len(y):
        raise ValueError("time/value length mismatch")
    out = np.full(len(t), np.nan, dtype=float)
    if len(t) > 1:
        dt = np.diff(t)
        if np.any(dt <= 0):
            raise ValueError("time axis must be strictly increasing")
        out[1:] = np.diff(y) / dt
    return out


def _temperature_table(run_dir: Path, cfg: dict[str, Any]) -> pd.DataFrame:
    path = run_dir / "temperature_history.csv"
    if not path.is_file():
        raise FeatureExtractionError(f"Missing formal 01 output: {path}")
    df = _read_csv_compatible(path)
    _require_columns(df, ["time_s"], "temperature_history.csv")
    object_id = int(cfg["temperature"]["object_id"])
    temp_col = f"T_{object_id}"
    _require_columns(df, [temp_col], "temperature_history.csv")
    df = _numeric(df, ["time_s", temp_col], "temperature_history.csv")
    df = df[["time_s", temp_col]].sort_values("time_s", kind="mergesort")
    if df["time_s"].duplicated().any():
        raise FeatureExtractionError("temperature_history.csv contains duplicate time_s values")
    t = df["time_s"].to_numpy(float)
    _strict_time_axis(t, "temperature_history.csv")
    T = df[temp_col].to_numpy(float)
    return pd.DataFrame(
        {
            "time_s": t,
            "temperature_object_id": object_id,
            "temperature_K": T,
            "temperature_rate_K_s": finite_difference(t, T),
        }
    )


def _infrared_table(run_dir: Path) -> pd.DataFrame:
    path = run_dir / "infrared_response_history.csv"
    if not path.is_file():
        raise FeatureExtractionError(f"Missing formal 01 output: {path}")
    df = _read_csv_compatible(path)
    required = [
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
    _require_columns(df, required, "infrared_response_history.csv")
    num_cols = [c for c in required if c != "case_id"]
    df = _numeric(df, num_cols, "infrared_response_history.csv")
    if df.duplicated(["time_s", "object_id"]).any():
        raise FeatureExtractionError("infrared_response_history.csv has duplicate (time_s, object_id) keys")
    return df.sort_values(["object_id", "time_s"], kind="mergesort").reset_index(drop=True)


def _add_object_rates(ir: pd.DataFrame) -> pd.DataFrame:
    out = ir.copy()
    out["radiant_intensity_rate_W_sr_s"] = np.nan
    out["radiation_power_rate_W_s"] = np.nan
    out["detector_received_power_rate_W_s"] = np.nan
    out["detector_irradiance_rate_W_m2_s"] = np.nan
    for _, idx in out.groupby("object_id", sort=False).groups.items():
        ix = np.asarray(list(idx), dtype=int)
        sub = out.loc[ix].sort_values("time_s", kind="mergesort")
        t = sub["time_s"].to_numpy(float)
        if len(t) < 2 or np.any(np.diff(t) <= 0):
            continue
        for source, target in [
            ("radiant_intensity_W_sr", "radiant_intensity_rate_W_sr_s"),
            ("radiation_power_W", "radiation_power_rate_W_s"),
            ("detector_received_power_W", "detector_received_power_rate_W_s"),
            ("detector_irradiance_W_m2", "detector_irradiance_rate_W_m2_s"),
        ]:
            out.loc[sub.index, target] = finite_difference(t, sub[source].to_numpy(float))
    return out


def _screen_geometry(cfg: dict[str, Any]) -> tuple[int, int, float, float, float, float]:
    s = cfg["screen"]
    nx = int(s["nx"])
    ny = int(s["ny"])
    width = float(s["width_m"])
    height = float(s["height_m"])
    if nx <= 0 or ny <= 0 or width <= 0 or height <= 0:
        raise FeatureExtractionError("screen nx/ny/width/height must be positive")
    return nx, ny, width, height, width / nx, height / ny


def _pixel_index(x: float, y: float, cfg: dict[str, Any]) -> tuple[int, int] | None:
    nx, ny, width, height, dx, dy = _screen_geometry(cfg)
    # Match the 01 reconstruction contract: 1-based cells over [-size/2, +size/2).
    ix = int(math.floor((x + width / 2.0) / dx))
    iy = int(math.floor((y + height / 2.0) / dy))
    if 0 <= ix < nx and 0 <= iy < ny:
        return ix, iy
    return None


def _gray_status(cfg: dict[str, Any]) -> tuple[bool, str, float | None, float | None, int]:
    g = cfg["gray_mapping"]
    enabled = bool(g.get("enabled", True))
    bit_depth = int(g.get("bit_depth", 8))
    lo = g.get("min_value")
    hi = g.get("max_value")
    if not enabled:
        return False, "gray mapping disabled by configuration", None, None, bit_depth
    if bit_depth <= 0 or bit_depth > 32:
        return False, "gray bit_depth must be in 1..32", None, None, bit_depth
    if lo is None or hi is None:
        return False, "fixed gray min_value/max_value not configured", None, None, bit_depth
    lo = float(lo)
    hi = float(hi)
    if not (math.isfinite(lo) and math.isfinite(hi) and hi > lo):
        return False, "invalid fixed gray mapping range", lo, hi, bit_depth
    return True, "", lo, hi, bit_depth


def _gray_value(value: float, lo: float, hi: float, bit_depth: int) -> int:
    max_gray = (1 << bit_depth) - 1
    u = (float(value) - lo) / (hi - lo)
    u = min(1.0, max(0.0, u))
    return int(round(u * max_gray))


def _scene_features(ir: pd.DataFrame, cfg: dict[str, Any]) -> pd.DataFrame:
    nx, ny, width, height, dx, dy = _screen_geometry(cfg)
    pixel_area = dx * dy
    gray_valid_global, gray_reason_global, gray_lo, gray_hi, bit_depth = _gray_status(cfg)

    rows: list[dict[str, Any]] = []
    for time_s, grp in ir.groupby("time_s", sort=True):
        g = grp.copy()
        physically_active = (g["active_flag"] == 1) & (g["released_flag"] == 1)
        active = g.loc[physically_active]
        on_screen = active.loc[active["in_screen_flag"] == 1].copy()

        total_radiation_power = float(active["radiation_power_W"].sum()) if len(active) else 0.0
        total_radiant_intensity = float(active["radiant_intensity_W_sr"].sum()) if len(active) else 0.0
        peak_radiant_intensity = float(active["radiant_intensity_W_sr"].max()) if len(active) else 0.0
        total_received = float(on_screen["detector_received_power_W"].sum()) if len(on_screen) else 0.0

        centroid_x = np.nan
        centroid_y = np.nan
        peak_x = np.nan
        peak_y = np.nan
        peak_object_id = np.nan
        peak_pixel_power = 0.0
        peak_pixel_irradiance = 0.0
        total_gray = np.nan
        peak_gray = np.nan
        gray_valid = int(gray_valid_global)
        gray_reason = gray_reason_global
        nonzero_pixel_count = 0

        pixel_power: dict[tuple[int, int], float] = {}
        if len(on_screen):
            weights = on_screen["detector_received_power_W"].to_numpy(float)
            wsum = float(weights.sum())
            if wsum > 0:
                centroid_x = float(np.sum(weights * on_screen["screen_x_m"].to_numpy(float)) / wsum)
                centroid_y = float(np.sum(weights * on_screen["screen_y_m"].to_numpy(float)) / wsum)
            peak_idx = on_screen["detector_irradiance_W_m2"].idxmax()
            peak_row = on_screen.loc[peak_idx]
            peak_x = float(peak_row["screen_x_m"])
            peak_y = float(peak_row["screen_y_m"])
            peak_object_id = int(peak_row["object_id"])

            for _, r in on_screen.iterrows():
                pix = _pixel_index(float(r["screen_x_m"]), float(r["screen_y_m"]), cfg)
                if pix is None:
                    # 01 says in_screen but coordinates do not map into the configured screen.
                    continue
                pixel_power[pix] = pixel_power.get(pix, 0.0) + float(r["detector_received_power_W"])

            nonzero_pixel_count = sum(1 for v in pixel_power.values() if v != 0.0)
            if pixel_power:
                peak_pixel_power = max(pixel_power.values())
                peak_pixel_irradiance = peak_pixel_power / pixel_area

            if gray_valid_global and gray_lo is not None and gray_hi is not None:
                # Zero-response pixels contribute gray=0 only when the fixed lower bound is <= 0.
                # If lo>0, the clipped mapping still maps zeros to 0.
                gray_values = [
                    _gray_value(power / pixel_area, gray_lo, gray_hi, bit_depth)
                    for power in pixel_power.values()
                ]
                total_gray = float(sum(gray_values))
                peak_gray = float(max(gray_values) if gray_values else 0)

        rows.append(
            {
                "time_s": float(time_s),
                "active_object_count": int(len(active)),
                "on_screen_object_count": int(len(on_screen)),
                "total_radiation_power_W": total_radiation_power,
                "total_radiant_intensity_W_sr": total_radiant_intensity,
                "peak_object_radiant_intensity_W_sr": peak_radiant_intensity,
                "total_received_power_W": total_received,
                "peak_pixel_power_W": float(peak_pixel_power),
                "peak_pixel_irradiance_W_m2": float(peak_pixel_irradiance),
                "centroid_x_m": centroid_x,
                "centroid_y_m": centroid_y,
                "peak_x_m": peak_x,
                "peak_y_m": peak_y,
                "peak_object_id": peak_object_id,
                "nonzero_pixel_count": int(nonzero_pixel_count),
                "total_gray": total_gray,
                "peak_gray": peak_gray,
                "gray_valid": gray_valid,
                "gray_invalid_reason": gray_reason,
            }
        )

    scene = pd.DataFrame(rows).sort_values("time_s", kind="mergesort").reset_index(drop=True)
    t = scene["time_s"].to_numpy(float)
    _strict_time_axis(t, "infrared scene features")
    for source, target in [
        ("total_radiant_intensity_W_sr", "total_radiant_intensity_rate_W_sr_s"),
        ("total_received_power_W", "total_received_power_rate_W_s"),
        ("peak_pixel_irradiance_W_m2", "peak_pixel_irradiance_rate_W_m2_s"),
        ("total_gray", "total_gray_rate_per_s"),
    ]:
        vals = scene[source].to_numpy(float)
        if np.isfinite(vals).all():
            scene[target] = finite_difference(t, vals)
        else:
            scene[target] = np.nan
    return scene


def extract_run_features(run_dir: Path, cfg: dict[str, Any]) -> dict[str, Any]:
    run_dir = run_dir.resolve()
    if not run_dir.is_dir():
        raise FeatureExtractionError(f"run_dir is not a directory: {run_dir}")

    temp = _temperature_table(run_dir, cfg)
    ir = _add_object_rates(_infrared_table(run_dir))
    scene = _scene_features(ir, cfg)

    # Attach the configured temperature to the infrared time grid by exact time key only.
    # The design contract forbids hidden interpolation inside similarity evaluation.
    feature = scene.merge(temp, on="time_s", how="left", validate="one_to_one")
    missing_temp = int(feature["temperature_K"].isna().sum())

    summary = {
        "schema_version": "response-features-v1",
        "run_dir": str(run_dir),
        "temperature_object_id": int(cfg["temperature"]["object_id"]),
        "scene_time_points": int(len(scene)),
        "object_rows": int(len(ir)),
        "object_count": int(ir["object_id"].nunique()),
        "time_start_s": float(scene["time_s"].iloc[0]),
        "time_end_s": float(scene["time_s"].iloc[-1]),
        "scene_times_missing_temperature": missing_temp,
        "gray_valid": bool(scene["gray_valid"].eq(1).all()),
        "gray_invalid_reason": "" if scene["gray_valid"].eq(1).all() else str(scene["gray_invalid_reason"].iloc[0]),
    }
    return {
        "feature_timeseries": feature,
        "object_feature_timeseries": ir,
        "temperature_timeseries": temp,
        "summary": summary,
    }


def write_run_features(features: dict[str, Any], output_dir: Path) -> dict[str, str]:
    output_dir.mkdir(parents=True, exist_ok=True)
    feature_path = output_dir / "feature_timeseries.csv"
    object_path = output_dir / "object_feature_timeseries.csv"
    summary_path = output_dir / "feature_summary.json"
    features["feature_timeseries"].to_csv(feature_path, index=False)
    features["object_feature_timeseries"].to_csv(object_path, index=False)
    summary_path.write_text(json.dumps(features["summary"], ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return {
        "feature_timeseries": str(feature_path),
        "object_feature_timeseries": str(object_path),
        "feature_summary": str(summary_path),
    }
