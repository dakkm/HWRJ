from __future__ import annotations

# 继续执行当前业务步骤，保持处理上下文与数据状态一致。
"""Periodic-feature extraction for 03 similarity evaluation.

The design report specifies dominant frequency/period and modulation amplitude,
but does not freeze a numerical extraction algorithm. This v1 implementation
# 继续执行当前业务步骤，保持处理上下文与数据状态一致。
therefore uses an explicit, reproducible convention:
- exact uniformly sampled series only;
- linear trend removed before FFT;
- dominant non-zero FFT bin gives dominant frequency;
# 更新当前流程所需的中间数据，为下一计算步骤做好准备。
- dominant sinusoidal amplitude = 2*|FFT(k)|/N;
- validity additionally requires a minimum number of samples, minimum observed
  cycles, non-negligible variance and configurable spectral concentration.

# 继续执行当前业务步骤，保持处理上下文与数据状态一致。
These implementation thresholds live in similarity_config.json and are not
physical similarity thresholds.
"""

# 导入当前模块依赖的标准能力或领域组件。
import math
from typing import Any

import numpy as np
import pandas as pd


# 定义 _invalid 处理过程，集中封装该步骤的输入、输出与异常边界。
def _invalid(signal: str, reason: str, n: int = 0) -> dict[str, Any]:
    return {
        "signal": signal,
        # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
        "periodic_valid": 0,
        "invalid_reason": reason,
        "sample_count": int(n),
        # 执行数组或表格数据运算，为后续数值处理准备结果。
        "sample_interval_s": np.nan,
        "nyquist_Hz": np.nan,
        "dominant_frequency_Hz": np.nan,
        "period_s": np.nan,
        # 执行数组或表格数据运算，为后续数值处理准备结果。
        "modulation_amplitude": np.nan,
        "modulation_depth": np.nan,
        "spectral_concentration": np.nan,
        # 执行数组或表格数据运算，为后续数值处理准备结果。
        "observed_cycles": np.nan,
    }


def analyze_periodic_series(
    # 执行数组或表格数据运算，为后续数值处理准备结果。
    time_s: np.ndarray,
    values: np.ndarray,
    signal: str,
    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
    cfg: dict[str, Any],
) -> dict[str, Any]:
    p = cfg["periodic"]
    t = np.asarray(time_s, dtype=float)
    # 执行数组或表格数据运算，为后续数值处理准备结果。
    y = np.asarray(values, dtype=float)
    if t.ndim != 1 or y.ndim != 1 or len(t) != len(y):
        return _invalid(signal, "time/value shape mismatch")
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    n = len(t)
    min_samples = int(p.get("min_samples", 16))
    if n < min_samples:
        # 向调用方返回当前步骤生成的数据或迭代结果。
        return _invalid(signal, f"sample_count<{min_samples}", n)
    finite = np.isfinite(t) & np.isfinite(y)
    if not finite.all():
        return _invalid(signal, "series contains NaN/Inf", n)
    # 执行数组或表格数据运算，为后续数值处理准备结果。
    dt = np.diff(t)
    if np.any(dt <= 0):
        return _invalid(signal, "time axis not strictly increasing", n)
    # 执行数组或表格数据运算，为后续数值处理准备结果。
    dt_mean = float(np.mean(dt))
    uniform_rtol = float(p.get("uniform_time_rtol", 1e-6))
    if not np.allclose(dt, dt_mean, rtol=uniform_rtol, atol=max(1e-12, abs(dt_mean) * uniform_rtol)):
        # 向调用方返回当前步骤生成的数据或迭代结果。
        return _invalid(signal, "nonuniform sampling; no interpolation is performed", n)

    # Linear detrend to avoid identifying a monotonic thermal ramp as a period.
    x = t - t[0]
    coeff = np.polyfit(x, y, deg=1)
    detrended = y - np.polyval(coeff, x)
    # 执行数组或表格数据运算，为后续数值处理准备结果。
    scale = max(float(np.max(np.abs(y))), float(np.mean(np.abs(y))), 1e-30)
    std = float(np.std(detrended))
    min_std_rel = float(p.get("min_std_relative", 1e-8))
    # 检查当前条件，仅在满足约束时执行对应分支。
    if std <= min_std_rel * scale:
        return _invalid(signal, "signal variation too small for periodic identification", n)

    spectrum = np.fft.rfft(detrended)
    # 执行数组或表格数据运算，为后续数值处理准备结果。
    freqs = np.fft.rfftfreq(n, d=dt_mean)
    if len(freqs) <= 1:
        return _invalid(signal, "insufficient positive-frequency bins", n)
    # 执行数组或表格数据运算，为后续数值处理准备结果。
    amp = 2.0 * np.abs(spectrum) / n
    amp[0] = 0.0

    max_frequency = p.get("max_frequency_Hz")
    eligible = freqs > 0
    # 检查当前条件，仅在满足约束时执行对应分支。
    if max_frequency is not None:
        eligible &= freqs <= float(max_frequency)
    indices = np.flatnonzero(eligible)
    # 检查当前条件，仅在满足约束时执行对应分支。
    if len(indices) == 0:
        return _invalid(signal, "no eligible FFT frequency bins", n)
    k = indices[np.argmax(amp[indices])]
    # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
    f = float(freqs[k])
    A = float(amp[k])
    if f <= 0:
        return _invalid(signal, "dominant frequency is zero", n)

    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    period = 1.0 / f
    duration = float(t[-1] - t[0])
    cycles = duration * f
    # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
    min_cycles = float(p.get("min_observed_cycles", 2.0))
    energy = np.abs(spectrum[1:]) ** 2
    denom = float(np.sum(energy))
    # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
    concentration = float((abs(spectrum[k]) ** 2) / denom) if denom > 0 else 0.0
    min_conc = float(p.get("min_spectral_concentration", 0.20))

    mean_abs = abs(float(np.mean(y)))
    depth = float(A / mean_abs) if mean_abs > 0 else np.nan

    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    reason = ""
    valid = 1
    if cycles < min_cycles:
        # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
        valid = 0
        reason = f"observed_cycles<{min_cycles:g}"
    elif concentration < min_conc:
        # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
        valid = 0
        reason = f"spectral_concentration<{min_conc:g}"

    return {
        # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
        "signal": signal,
        "periodic_valid": int(valid),
        "invalid_reason": reason,
        "sample_count": int(n),
        # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
        "sample_interval_s": dt_mean,
        "nyquist_Hz": 0.5 / dt_mean,
        "dominant_frequency_Hz": f,
        # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
        "period_s": period,
        "modulation_amplitude": A,
        "modulation_depth": depth,
        # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
        "spectral_concentration": concentration,
        "observed_cycles": cycles,
    }


def analyze_feature_frame(feature_df: pd.DataFrame, cfg: dict[str, Any]) -> pd.DataFrame:
    # 检查当前条件，仅在满足约束时执行对应分支。
    if "time_s" not in feature_df.columns:
        raise ValueError("feature_timeseries missing time_s")
    configured = cfg["periodic"].get(
        # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
        "signals",
        ["temperature_K", "total_radiant_intensity_W_sr", "total_received_power_W", "total_gray"],
    )
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    rows = []
    t = pd.to_numeric(feature_df["time_s"], errors="coerce").to_numpy(float)
    for signal in configured:
        if signal not in feature_df.columns:
            # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
            rows.append(_invalid(str(signal), "signal column not present", len(feature_df)))
            continue
        y = pd.to_numeric(feature_df[signal], errors="coerce").to_numpy(float)
        # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
        rows.append(analyze_periodic_series(t, y, str(signal), cfg))
    return pd.DataFrame(rows)
