from __future__ import annotations

"""03 formal entry point: feature extraction and similarity evaluation.

Modes
-----
features:
    One forward-response output directory (01 or 02-compatible) -> formal
    thermal/infrared feature products.
similarity:
    Reference/candidate forward-response output directories -> feature products
    + component and conservative aggregate similarity.  A common chain is
    reference=01 output and candidate=02 run_xxx/output.

Compatibility
-------------
``evaluate_temperature_curves`` remains importable with the historical signature
because module 04 currently imports it directly.
"""

import argparse
import json
import sys
import time
import uuid
from datetime import datetime
from pathlib import Path
from typing import Any

import pandas as pd

PROGRAM_DIR = Path(__file__).resolve().parent
MODULE_ROOT = PROGRAM_DIR.parent
DEFAULT_CONFIG = MODULE_ROOT / "01-输入文件" / "similarity_config.json"
OUTPUT_DIR = MODULE_ROOT / "03-输出文件"
DEFAULT_RUN_ROOT = OUTPUT_DIR / "runs"
LATEST_RUN_PATH = OUTPUT_DIR / "latest_run.json"

# Module 04 loads this file with importlib.spec_from_file_location; ensure sibling
# modules remain importable in that integration path as well as normal CLI use.
if str(PROGRAM_DIR) not in sys.path:
    sys.path.insert(0, str(PROGRAM_DIR))

from response_feature_extractor import FeatureExtractionError, extract_run_features, load_config, write_run_features
from periodic_feature_analyzer import analyze_feature_frame
from similarity_metrics import compare_feature_sets, evaluate_temperature_curves


def _new_run_dir(root: Path) -> tuple[str, Path]:
    root = root.resolve()
    root.mkdir(parents=True, exist_ok=True)
    run_id = datetime.now().strftime("run_%Y%m%d_%H%M%S_") + uuid.uuid4().hex[:8]
    run_dir = root / run_id
    run_dir.mkdir(parents=True, exist_ok=False)
    return run_id, run_dir


def _write_latest_run(run_id: str, run_dir: Path, status: str, mode: str) -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    payload = {
        "module": "03",
        "run_id": run_id,
        "run_dir": str(run_dir.resolve()),
        "status": status,
        "mode": mode,
        "timestamp": time.time(),
    }
    LATEST_RUN_PATH.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")


def _write_run_request(run_dir: Path, args: argparse.Namespace, cfg: dict[str, Any]) -> None:
    request = {
        "module": "03",
        "mode": args.mode,
        "run_dir": None if args.run_dir is None else str(args.run_dir.resolve()),
        "reference_run": None if args.reference_run is None else str(args.reference_run.resolve()),
        "candidate_run": None if args.candidate_run is None else str(args.candidate_run.resolve()),
        "config": str(args.config.resolve()),
        "output_root": str((args.output_dir or DEFAULT_RUN_ROOT).resolve()),
    }
    (run_dir / "evaluation_request.json").write_text(
        json.dumps(request, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    (run_dir / "similarity_config_used.json").write_text(
        json.dumps(cfg, ensure_ascii=False, indent=2), encoding="utf-8"
    )


def _write_periodic(df: pd.DataFrame, outdir: Path) -> Path:
    outdir.mkdir(parents=True, exist_ok=True)
    p = outdir / "periodic_features.csv"
    df.to_csv(p, index=False)
    return p


def run_features(run_dir: Path, output_dir: Path, cfg: dict[str, Any]) -> dict[str, Any]:
    features = extract_run_features(run_dir, cfg)
    paths = write_run_features(features, output_dir)
    periodic = analyze_feature_frame(features["feature_timeseries"], cfg)
    periodic_path = _write_periodic(periodic, output_dir)
    return {
        "status": "success",
        "mode": "features",
        "run_dir": str(run_dir.resolve()),
        "output_dir": str(output_dir.resolve()),
        "outputs": {**paths, "periodic_features": str(periodic_path)},
        "summary": features["summary"],
    }


def run_similarity(reference_run: Path, candidate_run: Path, output_dir: Path, cfg: dict[str, Any]) -> dict[str, Any]:
    ref = extract_run_features(reference_run, cfg)
    cand = extract_run_features(candidate_run, cfg)
    ref_periodic = analyze_feature_frame(ref["feature_timeseries"], cfg)
    cand_periodic = analyze_feature_frame(cand["feature_timeseries"], cfg)

    ref_dir = output_dir / "reference_features"
    cand_dir = output_dir / "candidate_features"
    write_run_features(ref, ref_dir)
    write_run_features(cand, cand_dir)
    _write_periodic(ref_periodic, ref_dir)
    _write_periodic(cand_periodic, cand_dir)

    components, summary = compare_feature_sets(ref, cand, ref_periodic, cand_periodic, cfg)
    output_dir.mkdir(parents=True, exist_ok=True)
    comp_path = output_dir / "similarity_components.csv"
    summary_path = output_dir / "similarity_summary.json"
    components.to_csv(comp_path, index=False)
    payload = {
        **summary,
        "reference_run": str(reference_run.resolve()),
        "candidate_run": str(candidate_run.resolve()),
        "config_schema": cfg.get("schema_version"),
    }
    summary_path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return {
        "status": "success",
        "mode": "similarity",
        "reference_run": str(reference_run.resolve()),
        "candidate_run": str(candidate_run.resolve()),
        "output_dir": str(output_dir.resolve()),
        "outputs": {
            "similarity_components": str(comp_path),
            "similarity_summary": str(summary_path),
            "reference_features": str(ref_dir),
            "candidate_features": str(cand_dir),
        },
        "summary": payload,
    }


def parse_args() -> argparse.Namespace:
    ap = argparse.ArgumentParser(description="03-相似度评估：01/02统一响应输出特征提取与成对相似度评价")
    ap.add_argument("--mode", choices=["features", "similarity"], required=True)
    ap.add_argument("--run-dir", type=Path, help="features模式：单个响应output目录（01正式output或02 run_xxx/output）")
    ap.add_argument("--reference-run", type=Path, help="similarity模式：参考响应output目录（通常为01正式output）")
    ap.add_argument("--candidate-run", type=Path, help="similarity模式：待评价响应output目录（可为01 output或02 run_xxx/output）")
    ap.add_argument("--config", type=Path, default=DEFAULT_CONFIG)
    ap.add_argument(
        "--output-dir",
        type=Path,
        default=None,
        help="任务输出根目录；每次调用都会在其下自动创建唯一 run_xxx 子目录。默认：03-输出文件/runs",
    )
    return ap.parse_args()


def main() -> int:
    args = parse_args()
    run_id: str | None = None
    task_dir: Path | None = None
    try:
        cfg = load_config(args.config.resolve())
        run_root = (args.output_dir or DEFAULT_RUN_ROOT).resolve()
        run_id, task_dir = _new_run_dir(run_root)
        _write_latest_run(run_id, task_dir, "running", args.mode)
        _write_run_request(task_dir, args, cfg)

        if args.mode == "features":
            if args.run_dir is None:
                raise ValueError("features mode requires --run-dir")
            result = run_features(args.run_dir, task_dir, cfg)
        else:
            if args.reference_run is None or args.candidate_run is None:
                raise ValueError("similarity mode requires --reference-run and --candidate-run")
            result = run_similarity(args.reference_run, args.candidate_run, task_dir, cfg)

        result["run_id"] = run_id
        result["task_dir"] = str(task_dir.resolve())
        status = {
            "module": "03",
            "status": "success",
            "run_id": run_id,
            "mode": args.mode,
            "task_dir": str(task_dir.resolve()),
            "timestamp": time.time(),
        }
        (task_dir / "evaluation_status.json").write_text(
            json.dumps(status, ensure_ascii=False, indent=2), encoding="utf-8"
        )
        _write_latest_run(run_id, task_dir, "success", args.mode)
        print(json.dumps(result, ensure_ascii=False))
        return 0
    except (FeatureExtractionError, ValueError, KeyError, OSError, json.JSONDecodeError) as exc:
        err = {
            "status": "error",
            "mode": args.mode,
            "run_id": run_id,
            "task_dir": None if task_dir is None else str(task_dir.resolve()),
            "error_type": type(exc).__name__,
            "message": str(exc),
            "timestamp": time.time(),
        }
        if task_dir is not None:
            (task_dir / "evaluation_status.json").write_text(
                json.dumps(err, ensure_ascii=False, indent=2), encoding="utf-8"
            )
            _write_latest_run(run_id or task_dir.name, task_dir, "error", args.mode)
        print(json.dumps(err, ensure_ascii=False), file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
