from __future__ import annotations
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
PROGRAM_DIR = Path(__file__).resolve().parent
MODULE_ROOT = PROGRAM_DIR.parent
PACKAGE_ROOT = MODULE_ROOT.parent
ROOT = PACKAGE_ROOT  # 兼容历史 stage 脚本中的 ROOT 名称

def _named_dir(suffix: str, fallback_name: str) -> Path:
    matches = sorted([p for p in MODULE_ROOT.iterdir() if p.is_dir() and p.name.endswith(suffix)])
    return matches[0] if matches else MODULE_ROOT / fallback_name

INPUT_DIR = _named_dir("-输入文件", "01-输入文件")
OUTPUT_DIR = _named_dir("-输出文件", "03-输出文件")
OUT = OUTPUT_DIR  # 兼容历史 stage 脚本中的 OUT 名称
MODEL_DIR = PACKAGE_ROOT / "02-智能预测" / "03-模型文件"
FORWARD_PROGRAM_DIR = PACKAGE_ROOT / "01-正向仿真" / "02-程序"
CONFIG_PATH = PACKAGE_ROOT / "config.json"

def _load_config() -> dict[str, Any]:
    if not CONFIG_PATH.exists():
        return {}
    return json.loads(CONFIG_PATH.read_text(encoding="utf-8"))

CONFIG = _load_config()

def package_path(relative_or_absolute: str | Path) -> Path:
    p = Path(relative_or_absolute)
    return p.resolve() if p.is_absolute() else (PACKAGE_ROOT / p).resolve()

def config_path(key: str, default: str | Path | None = None, *, must_exist: bool = False) -> Path:
    raw = CONFIG.get(key, default)
    if raw in (None, ""):
        raise FileNotFoundError(
            f"路径配置项 {key!r} 未设置。请编辑 {CONFIG_PATH} 后重试。"
        )
    p = package_path(raw)
    if must_exist and not p.exists():
        raise FileNotFoundError(
            f"路径配置项 {key!r} 指向的文件/目录不存在：{p}\n"
            f"请在 {CONFIG_PATH} 中填写实际路径。"
        )
    return p

def first_existing(*paths: Path) -> Path:
    for p in paths:
        if p.exists():
            return p
    return paths[0]

def sha256(path: Path) -> str:
    h=hashlib.sha256()
    with path.open("rb") as f:
        for b in iter(lambda:f.read(4*1024*1024),b""): h.update(b)
    return h.hexdigest()

def jwrite(path: Path, obj: Any):
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(obj,ensure_ascii=False,indent=2),encoding="utf-8")

def manifest(path: Path, files: list[Path]):
    path.parent.mkdir(parents=True,exist_ok=True)
    rows=[]
    for p in files:
        st=p.stat(); rows.append({"absolute_path":str(p.resolve()),"file_size":st.st_size,
          "modified_time":datetime.fromtimestamp(st.st_mtime).isoformat(timespec="seconds"),"sha256":sha256(p)})
    with path.open("w",encoding="utf-8-sig",newline="") as f:
        w=csv.DictWriter(f,fieldnames=list(rows[0]) if rows else ["absolute_path","file_size","modified_time","sha256"])
        w.writeheader(); w.writerows(rows)

def finish(stage: str, start: float, inputs: list[Path], outputs: list[Path], config: Path, status="Completed", checks=None):
    owner=Path(sys.argv[0]).resolve()
    manifest(OUT/"manifests"/f"stage_{stage.lower()}_input_manifest.csv",inputs)
    manifest(OUT/"manifests"/f"stage_{stage.lower()}_output_manifest.csv",outputs+[config])
    record={"stage":stage,"status":status,"owner":str(owner),"owner_sha256":sha256(owner),
      "actual_command":" ".join(sys.argv),"working_directory":str(Path.cwd()),"start_epoch":start,
      "end_epoch":time.time(),"elapsed_seconds":time.time()-start,"return_code":0,
      "config":str(config.resolve()),"config_sha256":sha256(config),"checks":checks or {}}
    jwrite(OUT/"logs"/f"stage_{stage.lower()}_execution_log.txt",record)
    return record

def ensure():
    """只保证模块输出根目录存在；具体子目录由实际任务按需创建。"""
    OUT.mkdir(parents=True, exist_ok=True)
