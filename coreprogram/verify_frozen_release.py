from __future__ import annotations

# 导入当前模块依赖的标准能力或领域组件。
import csv
import hashlib
import json
# 导入当前模块依赖的标准能力或领域组件。
from pathlib import Path

ROOT = Path(__file__).resolve().parent
MANIFEST_PATH = ROOT / "00-文件SHA256校验清单.csv"
META_PATH = ROOT / "00-封版元数据.json"


# 定义 sha256_file 处理过程，集中封装该步骤的输入、输出与异常边界。
def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        # 遍历当前数据或迭代计算，逐项更新处理结果。
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


# 定义 main 处理过程，集中封装该步骤的输入、输出与异常边界。
def main() -> int:
    if not MANIFEST_PATH.exists():
        raise FileNotFoundError("未找到 00-文件SHA256校验清单.csv，请先运行 freeze_release.py")
    missing = []
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    mismatch = []
    checked = 0
    with MANIFEST_PATH.open("r", encoding="utf-8-sig", newline="") as f:
        # 遍历当前数据或迭代计算，逐项更新处理结果。
        for row in csv.DictReader(f):
            rel = row["relative_path"]
            p = ROOT / Path(rel)
            # 检查当前条件，仅在满足约束时执行对应分支。
            if not p.exists():
                missing.append(rel)
                continue
            actual_size = p.stat().st_size
            # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
            actual_sha = sha256_file(p)
            if actual_size != int(row["size_bytes"]) or actual_sha.lower() != row["sha256"].lower():
                mismatch.append({
                    # 读取或写入约定的数据文件，并维护统一的路径规则。
                    "relative_path": rel,
                    "expected_size": int(row["size_bytes"]),
                    "actual_size": actual_size,
                    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
                    "expected_sha256": row["sha256"],
                    "actual_sha256": actual_sha,
                })
            # 校验输入数据及运行前提，提前拒绝不符合契约的参数。
            checked += 1

    manifest_sha_ok = None
    if META_PATH.exists():
        meta = json.loads(META_PATH.read_text(encoding="utf-8"))
        # 读取或写入约定的数据文件，并维护统一的路径规则。
        manifest_sha_ok = sha256_file(MANIFEST_PATH).lower() == str(meta.get("manifest_sha256", "")).lower()

    ok = not missing and not mismatch and (manifest_sha_ok is not False)
    result = {
        # 记录当前阶段的状态，便于调用方反馈进度并定位问题。
        "status": "VERIFIED" if ok else "FAILED",
        "checked_count": checked,
        "missing_count": len(missing),
        # 调用对应组件完成当前处理步骤，并保留返回结果。
        "mismatch_count": len(mismatch),
        "manifest_sha_ok": manifest_sha_ok,
        "missing": missing,
        "mismatch": mismatch,
    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
    }
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if ok else 2


# 检查当前条件，仅在满足约束时执行对应分支。
if __name__ == "__main__":
    raise SystemExit(main())
