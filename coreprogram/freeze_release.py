from __future__ import annotations

import csv
import hashlib
import json
import platform
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parent
SCOPE_PATH = ROOT / "00-封版范围.json"
VERSION_PATH = ROOT / "00-版本信息.json"
MANIFEST_PATH = ROOT / "00-文件SHA256校验清单.csv"
META_PATH = ROOT / "00-封版元数据.json"


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def norm(p: Path) -> str:
    return p.relative_to(ROOT).as_posix()


def should_exclude(rel: str, scope: dict) -> bool:
    parts = rel.split("/")
    for name in scope.get("exclude_patterns", []):
        if name == "__pycache__" and "__pycache__" in parts:
            return True
        if name.startswith("*.") and rel.lower().endswith(name[1:].lower()):
            return True
        if name == "~$*" and Path(rel).name.startswith("~$"):
            return True
    if rel in set(scope.get("exclude_files", [])):
        return True
    for base in scope.get("exclude_paths", []):
        b = base.rstrip("/")
        if rel == b or rel.startswith(b + "/"):
            return True
    return False


def role_of(rel: str) -> str:
    if "/02-程序/" in f"/{rel}" or rel.endswith((".py", ".f90", ".exe")):
        return "program"
    if "/03-模型文件/" in f"/{rel}" or rel.endswith((".keras", ".joblib")):
        return "model"
    if rel.endswith((".json", ".dat", ".csv")):
        return "contract_or_data"
    if rel.endswith((".docx", ".xlsx", ".txt", ".md")):
        return "document"
    return "other"


def main() -> int:
    scope = json.loads(SCOPE_PATH.read_text(encoding="utf-8"))
    version = json.loads(VERSION_PATH.read_text(encoding="utf-8"))
    rows = []
    for p in sorted(ROOT.rglob("*")):
        if not p.is_file():
            continue
        rel = norm(p)
        if should_exclude(rel, scope):
            continue
        rows.append({
            "relative_path": rel,
            "size_bytes": p.stat().st_size,
            "sha256": sha256_file(p),
            "role": role_of(rel),
        })

    with MANIFEST_PATH.open("w", encoding="utf-8-sig", newline="") as f:
        w = csv.DictWriter(f, fieldnames=["relative_path", "size_bytes", "sha256", "role"])
        w.writeheader()
        w.writerows(rows)

    meta = {
        "schema_version": "software-freeze-metadata-v1",
        "release_version": version.get("release_version"),
        "release_state": version.get("release_state"),
        "validation_state": version.get("validation_state"),
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "platform": platform.platform(),
        "python": platform.python_version(),
        "file_count": len(rows),
        "total_size_bytes": sum(int(r["size_bytes"]) for r in rows),
        "manifest_file": MANIFEST_PATH.name,
        "manifest_sha256": sha256_file(MANIFEST_PATH),
        "scope_file": SCOPE_PATH.name,
        "scope_sha256": sha256_file(SCOPE_PATH),
    }
    META_PATH.write_text(json.dumps(meta, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"status": "FROZEN", **meta}, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
