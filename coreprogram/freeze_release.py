from __future__ import annotations

# 导入当前模块依赖的标准能力或领域组件。
import csv
import hashlib
import json
# 导入当前模块依赖的标准能力或领域组件。
import platform
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parent
# 读取或写入约定的数据文件，并维护统一的路径规则。
SCOPE_PATH = ROOT / "00-封版范围.json"
VERSION_PATH = ROOT / "00-版本信息.json"
MANIFEST_PATH = ROOT / "00-文件SHA256校验清单.csv"
# 读取或写入约定的数据文件，并维护统一的路径规则。
META_PATH = ROOT / "00-封版元数据.json"


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    # 限定资源的使用周期，离开作用域时自动完成释放。
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


# 定义 norm 处理过程，集中封装该步骤的输入、输出与异常边界。
def norm(p: Path) -> str:
    return p.relative_to(ROOT).as_posix()


def should_exclude(rel: str, scope: dict) -> bool:
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    parts = rel.split("/")
    for name in scope.get("exclude_patterns", []):
        if name == "__pycache__" and "__pycache__" in parts:
            # 向调用方返回当前步骤生成的数据或迭代结果。
            return True
        if name.startswith("*.") and rel.lower().endswith(name[1:].lower()):
            return True
        if name == "~$*" and Path(rel).name.startswith("~$"):
            # 向调用方返回当前步骤生成的数据或迭代结果。
            return True
    if rel in set(scope.get("exclude_files", [])):
        return True
    # 遍历当前数据或迭代计算，逐项更新处理结果。
    for base in scope.get("exclude_paths", []):
        b = base.rstrip("/")
        if rel == b or rel.startswith(b + "/"):
            # 向调用方返回当前步骤生成的数据或迭代结果。
            return True
    return False


def role_of(rel: str) -> str:
    if "/02-程序/" in f"/{rel}" or rel.endswith((".py", ".f90", ".exe")):
        # 向调用方返回当前步骤生成的数据或迭代结果。
        return "program"
    if "/03-模型文件/" in f"/{rel}" or rel.endswith((".keras", ".joblib")):
        return "model"
    # 检查当前条件，仅在满足约束时执行对应分支。
    if rel.endswith((".json", ".dat", ".csv")):
        return "contract_or_data"
    if rel.endswith((".docx", ".xlsx", ".txt", ".md")):
        # 向调用方返回当前步骤生成的数据或迭代结果。
        return "document"
    return "other"


def main() -> int:
    # 读取或写入约定的数据文件，并维护统一的路径规则。
    scope = json.loads(SCOPE_PATH.read_text(encoding="utf-8"))
    version = json.loads(VERSION_PATH.read_text(encoding="utf-8"))
    rows = []
    for p in sorted(ROOT.rglob("*")):
        # 检查当前条件，仅在满足约束时执行对应分支。
        if not p.is_file():
            continue
        rel = norm(p)
        # 检查当前条件，仅在满足约束时执行对应分支。
        if should_exclude(rel, scope):
            continue
        rows.append({
            # 读取或写入约定的数据文件，并维护统一的路径规则。
            "relative_path": rel,
            "size_bytes": p.stat().st_size,
            "sha256": sha256_file(p),
            "role": role_of(rel),
        # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
        })

    with MANIFEST_PATH.open("w", encoding="utf-8-sig", newline="") as f:
        w = csv.DictWriter(f, fieldnames=["relative_path", "size_bytes", "sha256", "role"])
        # 调用对应组件完成当前处理步骤，并保留返回结果。
        w.writeheader()
        w.writerows(rows)

    meta = {
        # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
        "schema_version": "software-freeze-metadata-v1",
        "release_version": version.get("release_version"),
        "release_state": version.get("release_state"),
        "validation_state": version.get("validation_state"),
        # 调用对应组件完成当前处理步骤，并保留返回结果。
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "platform": platform.platform(),
        "python": platform.python_version(),
        # 调用对应组件完成当前处理步骤，并保留返回结果。
        "file_count": len(rows),
        "total_size_bytes": sum(int(r["size_bytes"]) for r in rows),
        "manifest_file": MANIFEST_PATH.name,
        # 读取或写入约定的数据文件，并维护统一的路径规则。
        "manifest_sha256": sha256_file(MANIFEST_PATH),
        "scope_file": SCOPE_PATH.name,
        "scope_sha256": sha256_file(SCOPE_PATH),
    }
    # 读取或写入约定的数据文件，并维护统一的路径规则。
    META_PATH.write_text(json.dumps(meta, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"status": "FROZEN", **meta}, ensure_ascii=False, indent=2))
    return 0


# 检查当前条件，仅在满足约束时执行对应分支。
if __name__ == "__main__":
    raise SystemExit(main())
