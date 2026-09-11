from __future__ import annotations

import csv
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent
MANIFEST_PATH = ROOT / "00-文件SHA256校验清单.csv"
META_PATH = ROOT / "00-封版元数据.json"


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def main() -> int:
    if not MANIFEST_PATH.exists():
        raise FileNotFoundError("未找到 00-文件SHA256校验清单.csv，请先运行 freeze_release.py")
    missing = []
    mismatch = []
    checked = 0
    with MANIFEST_PATH.open("r", encoding="utf-8-sig", newline="") as f:
        for row in csv.DictReader(f):
            rel = row["relative_path"]
            p = ROOT / Path(rel)
            if not p.exists():
                missing.append(rel)
                continue
            actual_size = p.stat().st_size
            actual_sha = sha256_file(p)
            if actual_size != int(row["size_bytes"]) or actual_sha.lower() != row["sha256"].lower():
                mismatch.append({
                    "relative_path": rel,
                    "expected_size": int(row["size_bytes"]),
                    "actual_size": actual_size,
                    "expected_sha256": row["sha256"],
                    "actual_sha256": actual_sha,
                })
            checked += 1

    manifest_sha_ok = None
    if META_PATH.exists():
        meta = json.loads(META_PATH.read_text(encoding="utf-8"))
        manifest_sha_ok = sha256_file(MANIFEST_PATH).lower() == str(meta.get("manifest_sha256", "")).lower()

    ok = not missing and not mismatch and (manifest_sha_ok is not False)
    result = {
        "status": "VERIFIED" if ok else "FAILED",
        "checked_count": checked,
        "missing_count": len(missing),
        "mismatch_count": len(mismatch),
        "manifest_sha_ok": manifest_sha_ok,
        "missing": missing,
        "mismatch": mismatch,
    }
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if ok else 2


if __name__ == "__main__":
    raise SystemExit(main())
