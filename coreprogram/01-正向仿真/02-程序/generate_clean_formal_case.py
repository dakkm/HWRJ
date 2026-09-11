#!/usr/bin/env python3
"""Formal clean-case generator: emits business/scene/physics fields only."""
from __future__ import annotations
import argparse, json
from pathlib import Path

def main():
    ap=argparse.ArgumentParser(); ap.add_argument("config",type=Path); ap.add_argument("template",type=Path); ap.add_argument("output",type=Path); a=ap.parse_args()
    cfg=json.loads(a.config.read_text(encoding="utf-8"))
    vals={k:(" ".join(v) if isinstance(v,list) else v) for k,v in cfg.items() if not k.startswith("TARGET_")}
    vals["TARGET_PHYSICS"]="\n".join(" ".join(row) for row in cfg["TARGET_PHYSICS"])
    vals["TARGET_SCENE"]="\n".join(" ".join(row) for row in cfg["TARGET_SCENE"])
    text=a.template.read_text(encoding="utf-8").format(**vals)
    a.output.write_text(text,encoding="utf-8",newline="\n")

if __name__=="__main__": main()
