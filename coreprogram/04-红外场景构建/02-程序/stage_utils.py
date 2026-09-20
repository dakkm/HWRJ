from __future__ import annotations
# 导入当前模块依赖的标准能力或领域组件。
import csv, hashlib, json, os, platform, sys, time
from datetime import datetime
from pathlib import Path
from typing import Any

# ============================================================
# 交接版路径合同
# - 不依赖 D:\vscode\hwfz、D:\ir_formal_runs 等开发机绝对路径。
# - 所有软件内资源均从当前脚本位置反推“软件说明”根目录。
# - 正式运行资源随包交付；根目录 config.json 统一管理相对路径。
# ============================================================
# 读取或写入约定的数据文件，并维护统一的路径规则。
PROGRAM_DIR = Path(__file__).resolve().parent
MODULE_ROOT = PROGRAM_DIR.parent
PACKAGE_ROOT = MODULE_ROOT.parent
ROOT = PACKAGE_ROOT  # 兼容历史 stage 脚本中的 ROOT 名称

# 定义 _named_dir 处理过程，集中封装该步骤的输入、输出与异常边界。
def _named_dir(suffix: str, fallback_name: str) -> Path:
    matches = sorted([p for p in MODULE_ROOT.iterdir() if p.is_dir() and p.name.endswith(suffix)])
    return matches[0] if matches else MODULE_ROOT / fallback_name

INPUT_DIR = _named_dir("-输入文件", "01-输入文件")
# 更新当前流程所需的中间数据，为下一计算步骤做好准备。
OUTPUT_DIR = _named_dir("-输出文件", "03-输出文件")
OUT = OUTPUT_DIR  # 兼容历史 stage 脚本中的 OUT 名称
MODEL_DIR = PACKAGE_ROOT / "02-智能预测" / "03-模型文件"
FORWARD_PROGRAM_DIR = PACKAGE_ROOT / "01-正向仿真" / "02-程序"
# 读取或写入约定的数据文件，并维护统一的路径规则。
CONFIG_PATH = PACKAGE_ROOT / "config.json"

def _load_config() -> dict[str, Any]:
    if not CONFIG_PATH.exists():
        return {}
    # 向调用方返回当前步骤生成的数据或迭代结果。
    return json.loads(CONFIG_PATH.read_text(encoding="utf-8"))

CONFIG = _load_config()

def package_path(relative_or_absolute: str | Path) -> Path:
    p = Path(relative_or_absolute)
    # 向调用方返回当前步骤生成的数据或迭代结果。
    return p.resolve() if p.is_absolute() else (PACKAGE_ROOT / p).resolve()

def config_path(key: str, default: str | Path | None = None, *, must_exist: bool = False) -> Path:
    raw = CONFIG.get(key, default)
    if raw in (None, ""):
        # 检测到无效状态后立即报错，防止异常数据继续传播。
        raise FileNotFoundError(
            f"路径配置项 {key!r} 未设置。请编辑 {CONFIG_PATH} 后重试。"
        )
    p = package_path(raw)
    # 检查当前条件，仅在满足约束时执行对应分支。
    if must_exist and not p.exists():
        raise FileNotFoundError(
            f"路径配置项 {key!r} 指向的文件/目录不存在：{p}\n"
            f"请在 {CONFIG_PATH} 中填写实际路径。"
        # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
        )
    return p

def first_existing(*paths: Path) -> Path:
    # 遍历当前数据或迭代计算，逐项更新处理结果。
    for p in paths:
        if p.exists():
            return p
    return paths[0]

# 定义 sha256 处理过程，集中封装该步骤的输入、输出与异常边界。
def sha256(path: Path) -> str:
    h=hashlib.sha256()
    with path.open("rb") as f:
        for b in iter(lambda:f.read(4*1024*1024),b""): h.update(b)
    # 向调用方返回当前步骤生成的数据或迭代结果。
    return h.hexdigest()

def jwrite(path: Path, obj: Any):
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(obj,ensure_ascii=False,indent=2),encoding="utf-8")

# 定义 manifest 处理过程，集中封装该步骤的输入、输出与异常边界。
def manifest(path: Path, files: list[Path]):
    path.parent.mkdir(parents=True,exist_ok=True)
    rows=[]
    for p in files:
        # 读取或写入约定的数据文件，并维护统一的路径规则。
        st=p.stat(); rows.append({"absolute_path":str(p.resolve()),"file_size":st.st_size,
          "modified_time":datetime.fromtimestamp(st.st_mtime).isoformat(timespec="seconds"),"sha256":sha256(p)})
    with path.open("w",encoding="utf-8-sig",newline="") as f:
        w=csv.DictWriter(f,fieldnames=list(rows[0]) if rows else ["absolute_path","file_size","modified_time","sha256"])
        # 调用对应组件完成当前处理步骤，并保留返回结果。
        w.writeheader(); w.writerows(rows)

def finish(stage: str, start: float, inputs: list[Path], outputs: list[Path], config: Path, status="Completed", checks=None):
    owner=Path(sys.argv[0]).resolve()
    manifest(OUT/"manifests"/f"stage_{stage.lower()}_input_manifest.csv",inputs)
    # 调用对应组件完成当前处理步骤，并保留返回结果。
    manifest(OUT/"manifests"/f"stage_{stage.lower()}_output_manifest.csv",outputs+[config])
    record={"stage":stage,"status":status,"owner":str(owner),"owner_sha256":sha256(owner),
      "actual_command":" ".join(sys.argv),"working_directory":str(Path.cwd()),"start_epoch":start,
      "end_epoch":time.time(),"elapsed_seconds":time.time()-start,"return_code":0,
      # 校验输入数据及运行前提，提前拒绝不符合契约的参数。
      "config":str(config.resolve()),"config_sha256":sha256(config),"checks":checks or {}}
    jwrite(OUT/"logs"/f"stage_{stage.lower()}_execution_log.txt",record)
    return record

def ensure():
    # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
    """只保证模块输出根目录存在；具体子目录由实际任务按需创建。"""
    OUT.mkdir(parents=True, exist_ok=True)
