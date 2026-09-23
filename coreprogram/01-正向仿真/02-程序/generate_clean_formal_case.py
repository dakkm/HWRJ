#!/usr/bin/env python3
"""Formal clean-case generator: emits business/scene/physics fields only."""
# 导入当前模块依赖的标准能力或领域组件。
from __future__ import annotations
import argparse, json
# 导入当前模块依赖的标准能力或领域组件。
from pathlib import Path

def main():
    ap=argparse.ArgumentParser(); ap.add_argument("config",type=Path); ap.add_argument("template",type=Path); ap.add_argument("output",type=Path); a=ap.parse_args()
    # 读取或写入约定的数据文件，并维护统一的路径规则。
    cfg=json.loads(a.config.read_text(encoding="utf-8"))
    vals={k:(" ".join(v) if isinstance(v,list) else v) for k,v in cfg.items() if not k.startswith("TARGET_")}
    vals["TARGET_PHYSICS"]="\n".join(" ".join(row) for row in cfg["TARGET_PHYSICS"])
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    vals["TARGET_SCENE"]="\n".join(" ".join(row) for row in cfg["TARGET_SCENE"])
    text=a.template.read_text(encoding="utf-8").format(**vals)
    # 读取或写入约定的数据文件，并维护统一的路径规则。
    # Path.write_text() in Python 3.8 has no ``newline`` argument.
    # Use open() so the packaged Win7-compatible runtime writes stable LF text.
    with a.output.open("w", encoding="utf-8", newline="\n") as handle:
        handle.write(text)

if __name__=="__main__": main()
