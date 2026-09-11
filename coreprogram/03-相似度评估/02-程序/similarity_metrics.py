from __future__ import annotations

"""Similarity metrics implementing the current 03 design-report logic."""

import math
from typing import Any

import numpy as np
import pandas as pd


def distance_similarity(distance: float, scale: float) -> float:
    if not (math.isfinite(distance) and math.isfinite(scale) and scale > 0 and distance >= 0):
        return float("nan")
    return 100.0 / (1.0 + distance / scale)


def amplitude_similarity(a: float, b: float, zero_tol: float = 1e-30) -> float:
    if not (math.isfinite(a) and math.isfinite(b)) or a < 0 or b < 0:
        return float("nan")
    if abs(a) <= zero_tol and abs(b) <= zero_tol:
        return 100.0
    hi = max(a, b)
    lo = min(a, b)
    if hi <= zero_tol:
        return 100.0
    return 100.0 * lo / hi


def pearson_similarity(a: np.ndarray, b: np.ndarray, variance_tol: float = 1e-14) -> tuple[float, float]:
    x = np.asarray(a, dtype=float)
    y = np.asarray(b, dtype=float)
    if len(x) != len(y) or len(x) < 2 or not (np.isfinite(x).all() and np.isfinite(y).all()):
        return float("nan"), float("nan")
    if np.var(x) <= variance_tol or np.var(y) <= variance_tol:
        return float("nan"), float("nan")
    rho = float(np.corrcoef(x, y)[0, 1])
    rho = min(1.0, max(-1.0, rho))
    return rho, 50.0 * (1.0 + rho)


def _component(
    name: str,
    category: str,
    raw_difference: float | None,
    raw_unit: str,
    similarity: float | None,
    valid: bool,
    reason: str = "",
    valid_count: int | None = None,
) -> dict[str, Any]:
    return {
        "feature_name": name,
        "category": category,
        "raw_difference": np.nan if raw_difference is None else raw_difference,
        "raw_unit": raw_unit,
        "similarity_percent": np.nan if similarity is None else similarity,
        "valid_flag": int(valid),
        "invalid_reason": reason,
        "valid_sample_count": np.nan if valid_count is None else int(valid_count),
    }


def evaluate_temperature_curves(
    time_s: np.ndarray | list[float],
    target_temperature: np.ndarray | list[float],
    candidate_temperature: np.ndarray | list[float],
    *,
    early_start_s: float = 0.0,
    early_end_s: float = 30.0,
    plateau_start_s: float = 100.0,
    plateau_end_s: float = 1000.0,
) -> dict[str, float | int]:
    """Legacy-compatible six temperature errors plus current similarity quantities.

    This function keeps the original public signature used by module 04.
    """
    t = np.asarray(time_s, dtype=float)
    ref = np.asarray(target_temperature, dtype=float)
    cand = np.asarray(candidate_temperature, dtype=float)
    if not (t.ndim == ref.ndim == cand.ndim == 1 and len(t) == len(ref) == len(cand)):
        raise ValueError("time_s, target_temperature and candidate_temperature must be equal-length 1D arrays")
    if len(t) < 2 or not (np.isfinite(t).all() and np.isfinite(ref).all() and np.isfinite(cand).all()):
        raise ValueError("temperature curves must have >=2 finite samples")
    if np.any(np.diff(t) <= 0):
        raise ValueError("time_s must be strictly increasing")
    early = (t >= early_start_s) & (t <= early_end_s)
    plateau = (t >= plateau_start_s) & (t <= plateau_end_s)
    if not early.any():
        raise ValueError(f"time axis does not cover early window [{early_start_s}, {early_end_s}] s")
    if not plateau.any():
        raise ValueError(f"time axis does not cover plateau window [{plateau_start_s}, {plateau_end_s}] s")

    e = cand - ref
    ae = np.abs(e)
    rt = float(np.max(ref) - np.min(ref))
    result: dict[str, float | int] = {
        "rmse": float(np.sqrt(np.mean(e * e))),
        "mae": float(np.mean(ae)),
        "max_abs_error": float(np.max(ae)),
        "final_abs_error": float(ae[-1]),
        "plateau_abs_error": float(abs(np.mean(e[plateau]))),
        "early_abs_error": float(np.max(ae[early])),
        "point_count": int(len(t)),
        "time_start_s": float(t[0]),
        "time_end_s": float(t[-1]),
        "final_time_s": float(t[-1]),
        "early_start_s": float(early_start_s),
        "early_end_s": float(early_end_s),
        "plateau_start_s": float(plateau_start_s),
        "plateau_end_s": float(plateau_end_s),
        "reference_temperature_range_K": rt,
    }
    if rt > 0:
        for key, out_key in [
            ("rmse", "rmse_similarity_percent"),
            ("mae", "mae_similarity_percent"),
            ("max_abs_error", "max_similarity_percent"),
            ("final_abs_error", "final_similarity_percent"),
            ("plateau_abs_error", "plateau_similarity_percent"),
            ("early_abs_error", "early_similarity_percent"),
        ]:
            result[out_key] = distance_similarity(float(result[key]), rt)
    else:
        for out_key in [
            "rmse_similarity_percent", "mae_similarity_percent", "max_similarity_percent",
            "final_similarity_percent", "plateau_similarity_percent", "early_similarity_percent",
        ]:
            result[out_key] = float("nan")

    gr = np.diff(ref) / np.diff(t)
    ge = np.diff(cand) / np.diff(t)
    rho, sim = pearson_similarity(gr, ge)
    result["temperature_rate_correlation"] = rho
    result["temperature_trend_similarity_percent"] = sim
    return result


def _same_time_frame(ref: pd.DataFrame, cand: pd.DataFrame, cols: list[str]) -> pd.DataFrame:
    a = ref[["time_s"] + cols].copy()
    b = cand[["time_s"] + cols].copy()
    merged = a.merge(b, on="time_s", how="outer", suffixes=("_ref", "_cand"), indicator=True)
    if not merged["_merge"].eq("both").all():
        missing_ref = merged.loc[merged["_merge"] == "right_only", "time_s"].head(10).tolist()
        missing_cand = merged.loc[merged["_merge"] == "left_only", "time_s"].head(10).tolist()
        raise ValueError(
            "reference/candidate time axes must match exactly; "
            f"missing_in_reference={missing_ref}, missing_in_candidate={missing_cand}"
        )
    return merged.drop(columns="_merge").sort_values("time_s", kind="mergesort")


def _mean_ratio_component(name: str, category: str, a: np.ndarray, b: np.ndarray, unit: str, zero_tol: float) -> dict[str, Any]:
    finite = np.isfinite(a) & np.isfinite(b) & (a >= 0) & (b >= 0)
    if not finite.any():
        return _component(name, category, None, unit, None, False, "no valid paired nonnegative amplitudes", 0)
    sims = np.array([amplitude_similarity(float(x), float(y), zero_tol) for x, y in zip(a[finite], b[finite])])
    raw = float(np.mean(np.abs(a[finite] - b[finite])))
    return _component(name, category, raw, unit, float(np.nanmean(sims)), True, valid_count=int(finite.sum()))


def compare_feature_sets(
    reference: dict[str, Any],
    candidate: dict[str, Any],
    reference_periodic: pd.DataFrame,
    candidate_periodic: pd.DataFrame,
    cfg: dict[str, Any],
) -> tuple[pd.DataFrame, dict[str, Any]]:
    ref = reference["feature_timeseries"]
    cand = candidate["feature_timeseries"]
    obj_r = reference["object_feature_timeseries"]
    obj_c = candidate["object_feature_timeseries"]
    components: list[dict[str, Any]] = []

    # Temperature block: strict exact time matching for points where both temperature values exist.
    tm = _same_time_frame(ref.dropna(subset=["temperature_K"]), cand.dropna(subset=["temperature_K"]), ["temperature_K"])
    tc = cfg["temperature"]
    tres = evaluate_temperature_curves(
        tm["time_s"].to_numpy(float),
        tm["temperature_K_ref"].to_numpy(float),
        tm["temperature_K_cand"].to_numpy(float),
        early_start_s=float(tc["early_window_s"][0]),
        early_end_s=float(tc["early_window_s"][1]),
        plateau_start_s=float(tc["plateau_window_s"][0]),
        plateau_end_s=float(tc["plateau_window_s"][1]),
    )
    rt = float(tres["reference_temperature_range_K"])
    temp_items = [
        ("temperature_rmse", "rmse", "rmse_similarity_percent"),
        ("temperature_mae", "mae", "mae_similarity_percent"),
        ("temperature_max_abs", "max_abs_error", "max_similarity_percent"),
        ("temperature_final_abs", "final_abs_error", "final_similarity_percent"),
        ("temperature_plateau_abs", "plateau_abs_error", "plateau_similarity_percent"),
        ("temperature_early_abs", "early_abs_error", "early_similarity_percent"),
    ]
    for name, rawkey, simkey in temp_items:
        valid = math.isfinite(float(tres[simkey]))
        components.append(_component(name, "temperature_level", float(tres[rawkey]), "K", float(tres[simkey]), valid, "" if valid else "reference temperature range is zero", int(tres["point_count"])))
    trend_valid = math.isfinite(float(tres["temperature_trend_similarity_percent"]))
    components.append(_component(
        "temperature_rate_trend", "temperature_trend",
        None if not math.isfinite(float(tres["temperature_rate_correlation"])) else float(tres["temperature_rate_correlation"]),
        "correlation", None if not trend_valid else float(tres["temperature_trend_similarity_percent"]), trend_valid,
        "" if trend_valid else "temperature-rate variance too small or invalid", max(int(tres["point_count"]) - 1, 0),
    ))

    # Scene scalar amplitude and trend features.
    sm = _same_time_frame(
        ref,
        cand,
        ["total_radiant_intensity_W_sr", "total_received_power_W", "peak_pixel_irradiance_W_m2", "total_gray", "centroid_x_m", "centroid_y_m", "peak_x_m", "peak_y_m"],
    )
    zero_tol = float(cfg["similarity"].get("amplitude_zero_tolerance", 1e-30))
    for name, col, unit in [
        ("scene_total_radiant_intensity", "total_radiant_intensity_W_sr", "W/sr"),
        ("scene_total_received_power", "total_received_power_W", "W"),
        ("scene_peak_irradiance", "peak_pixel_irradiance_W_m2", "W/m^2"),
        ("scene_total_gray", "total_gray", "gray-level sum"),
    ]:
        a = sm[f"{col}_ref"].to_numpy(float)
        b = sm[f"{col}_cand"].to_numpy(float)
        components.append(_mean_ratio_component(name, "infrared_amplitude", a, b, unit, zero_tol))
        finite = np.isfinite(a) & np.isfinite(b)
        rho, sim = pearson_similarity(a[finite], b[finite], float(cfg["similarity"].get("variance_tolerance", 1e-14))) if finite.sum() >= 2 else (np.nan, np.nan)
        components.append(_component(
            name + "_trend", "infrared_trend", None if not math.isfinite(rho) else rho, "correlation",
            None if not math.isfinite(sim) else sim, math.isfinite(sim),
            "" if math.isfinite(sim) else "series variance too small or invalid", int(finite.sum()),
        ))

    # Scene spatial features: distance with detector-screen diagonal as the scale.
    screen = cfg["screen"]
    rpos = math.hypot(float(screen["width_m"]), float(screen["height_m"]))
    for name, xcol, ycol in [
        ("screen_centroid_position", "centroid_x_m", "centroid_y_m"),
        ("screen_peak_position", "peak_x_m", "peak_y_m"),
    ]:
        xr, yr = sm[f"{xcol}_ref"].to_numpy(float), sm[f"{ycol}_ref"].to_numpy(float)
        xc, yc = sm[f"{xcol}_cand"].to_numpy(float), sm[f"{ycol}_cand"].to_numpy(float)
        finite = np.isfinite(xr) & np.isfinite(yr) & np.isfinite(xc) & np.isfinite(yc)
        if finite.any():
            d = np.hypot(xc[finite] - xr[finite], yc[finite] - yr[finite])
            raw = float(np.mean(d))
            components.append(_component(name, "infrared_position", raw, "m", distance_similarity(raw, rpos), True, valid_count=int(finite.sum())))
        else:
            components.append(_component(name, "infrared_position", None, "m", None, False, "no valid paired screen positions", 0))

    # Object-level paired data, keyed by exact (time, object_id).
    cols = ["time_s", "object_id", "active_flag", "released_flag", "in_screen_flag", "screen_x_m", "screen_y_m", "radiation_power_W", "radiant_intensity_W_sr"]
    om = obj_r[cols].merge(obj_c[cols], on=["time_s", "object_id"], how="outer", suffixes=("_ref", "_cand"), indicator=True)
    paired = om["_merge"].eq("both")
    state_ok = paired & (om["active_flag_ref"] == 1) & (om["released_flag_ref"] == 1) & (om["active_flag_cand"] == 1) & (om["released_flag_cand"] == 1)
    for name, col, unit in [
        ("object_radiation_power", "radiation_power_W", "W"),
        ("object_radiant_intensity", "radiant_intensity_W_sr", "W/sr"),
    ]:
        a = pd.to_numeric(om.loc[state_ok, f"{col}_ref"], errors="coerce").to_numpy(float)
        b = pd.to_numeric(om.loc[state_ok, f"{col}_cand"], errors="coerce").to_numpy(float)
        components.append(_mean_ratio_component(name, "infrared_amplitude", a, b, unit, zero_tol))

    pos_ok = state_ok & (om["in_screen_flag_ref"] == 1) & (om["in_screen_flag_cand"] == 1)
    xr = pd.to_numeric(om.loc[pos_ok, "screen_x_m_ref"], errors="coerce").to_numpy(float)
    yr = pd.to_numeric(om.loc[pos_ok, "screen_y_m_ref"], errors="coerce").to_numpy(float)
    xc = pd.to_numeric(om.loc[pos_ok, "screen_x_m_cand"], errors="coerce").to_numpy(float)
    yc = pd.to_numeric(om.loc[pos_ok, "screen_y_m_cand"], errors="coerce").to_numpy(float)
    finite = np.isfinite(xr) & np.isfinite(yr) & np.isfinite(xc) & np.isfinite(yc)
    if finite.any():
        d = np.hypot(xc[finite] - xr[finite], yc[finite] - yr[finite])
        raw = float(np.mean(d))
        components.append(_component("object_screen_position", "infrared_position", raw, "m", distance_similarity(raw, rpos), True, valid_count=int(finite.sum())))
    else:
        components.append(_component("object_screen_position", "infrared_position", None, "m", None, False, "no valid paired on-screen objects", 0))

    # Periodic components: compare only if both sides independently passed the periodic validity test.
    pmerge = reference_periodic.merge(candidate_periodic, on="signal", how="outer", suffixes=("_ref", "_cand"))
    for _, row in pmerge.iterrows():
        signal = str(row["signal"])
        both_valid = int(row.get("periodic_valid_ref", 0) or 0) == 1 and int(row.get("periodic_valid_cand", 0) or 0) == 1
        if not both_valid:
            rr = str(row.get("invalid_reason_ref", ""))
            cr = str(row.get("invalid_reason_cand", ""))
            reason = f"periodic feature not jointly valid; reference={rr}; candidate={cr}"
            components.append(_component(f"periodic_frequency:{signal}", "periodic", None, "Hz", None, False, reason, 0))
            components.append(_component(f"periodic_amplitude:{signal}", "periodic", None, "signal-unit", None, False, reason, 0))
            continue
        fr, fc = float(row["dominant_frequency_Hz_ref"]), float(row["dominant_frequency_Hz_cand"])
        ar, ac = float(row["modulation_amplitude_ref"]), float(row["modulation_amplitude_cand"])
        components.append(_component(
            f"periodic_frequency:{signal}", "periodic", abs(fc - fr), "Hz", amplitude_similarity(fr, fc, zero_tol), True, valid_count=1
        ))
        components.append(_component(
            f"periodic_amplitude:{signal}", "periodic", abs(ac - ar), "signal-unit", amplitude_similarity(ar, ac, zero_tol), True, valid_count=1
        ))

    comp = pd.DataFrame(components)
    valid = comp[(comp["valid_flag"] == 1) & pd.to_numeric(comp["similarity_percent"], errors="coerce").notna()].copy()
    temp_valid = valid[valid["category"].str.startswith("temperature")]
    ir_valid = valid[~valid["category"].str.startswith("temperature")]

    def block_summary(df: pd.DataFrame) -> tuple[float | None, str | None]:
        if df.empty:
            return None, None
        ix = pd.to_numeric(df["similarity_percent"], errors="coerce").idxmin()
        return float(df.loc[ix, "similarity_percent"]), str(df.loc[ix, "feature_name"])

    st, st_lim = block_summary(temp_valid)
    sir, sir_lim = block_summary(ir_valid)
    enabled = [x for x in [st, sir] if x is not None]
    scene = min(enabled) if enabled else None
    if scene is None:
        scene_lim = None
    elif st is not None and st == scene:
        scene_lim = st_lim
    else:
        scene_lim = sir_lim

    summary = {
        "schema_version": "similarity-summary-v1",
        "aggregation": "minimum_valid_component",
        "temperature_similarity_percent": st,
        "temperature_limiting_feature": st_lim,
        "infrared_similarity_percent": sir,
        "infrared_limiting_feature": sir_lim,
        "scene_similarity_percent": scene,
        "scene_limiting_feature": scene_lim,
        "valid_component_count": int(len(valid)),
        "invalid_component_count": int((comp["valid_flag"] != 1).sum()),
    }
    return comp, summary
