#!/usr/bin/env python3
from __future__ import annotations

# 导入当前模块依赖的标准能力或领域组件。
import argparse
import csv
import json
# 导入当前模块依赖的标准能力或领域组件。
from decimal import Decimal
from pathlib import Path


def main() -> None:
    # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
    parser = argparse.ArgumentParser(description="Reconstruct a detector image without invoking the forward solver.")
    parser.add_argument("response_csv", type=Path)
    parser.add_argument("metadata_json", type=Path)
    parser.add_argument("time_s", type=Decimal)
    # 读取或写入约定的数据文件，并维护统一的路径规则。
    parser.add_argument("output_csv", type=Path)
    args = parser.parse_args()
    metadata = json.loads(args.metadata_json.read_text(encoding="utf-8"))
    # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
    nx, ny = int(metadata["grid_nx"]), int(metadata["grid_ny"])
    radius = int(metadata["spot_radius_cells"])
    if radius != 0:
        # 检测到无效状态后立即报错，防止异常数据继续传播。
        raise SystemExit("This frozen reconstruction contract currently supports SPOT_RADIUS_CELLS=0 only")
    size = Decimal(str(metadata["spot_plane_size_m"]))
    dx, dy = size / nx, size / ny
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    power = [[Decimal(0) for _ in range(nx)] for _ in range(ny)]
    selected = []
    with args.response_csv.open(newline="", encoding="utf-8") as handle:
        # 遍历当前数据或迭代计算，逐项更新处理结果。
        for row in csv.DictReader(handle):
            if Decimal(row["time_s"].strip()) == args.time_s:
                selected.append(row)
    if not selected:
        # 检测到无效状态后立即报错，防止异常数据继续传播。
        raise SystemExit(f"no response rows at time {args.time_s}")
    projected = []
    for row in selected:
        # 检查当前条件，仅在满足约束时执行对应分支。
        if int(row["released_flag"]) != 1 or int(row["in_screen_flag"]) != 1:
            continue
        x, y = Decimal(row["screen_x_m"].strip()), Decimal(row["screen_y_m"].strip())
        # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
        ix = int((x + size / 2) / dx) + 1
        iy = int((y + size / 2) / dy) + 1
        if not (1 <= ix <= nx and 1 <= iy <= ny):
            # 检测到无效状态后立即报错，防止异常数据继续传播。
            raise SystemExit(f"in_screen row maps out of bounds: object={row['object_id']} ix={ix} iy={iy}")
        value = Decimal(row["detector_received_power_W"].strip())
        power[iy - 1][ix - 1] += value
        # 把本次处理结果加入集合，供后续汇总或输出使用。
        projected.append((row["object_id"], ix, iy, value))
    args.output_csv.parent.mkdir(parents=True, exist_ok=True)
    with args.output_csv.open("w", newline="", encoding="utf-8") as handle:
        # 读取或写入约定的数据文件，并维护统一的路径规则。
        writer = csv.writer(handle)
        writer.writerow(["pixel_x", "pixel_y", "screen_center_x_m", "screen_center_y_m", "power_W", "irradiance_W_m2"])
        for iy in range(1, ny + 1):
            y = -size / 2 + (Decimal(iy) - Decimal("0.5")) * dy
            # 遍历当前数据或迭代计算，逐项更新处理结果。
            for ix in range(1, nx + 1):
                x = -size / 2 + (Decimal(ix) - Decimal("0.5")) * dx
                value = power[iy - 1][ix - 1]
                # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
                writer.writerow([ix, iy, str(x), str(y), str(value), str(value / (dx * dy))])
    summary = {"time_s": str(args.time_s), "response_row_count": len(selected), "projected_object_count": len(projected), "nonzero_pixel_count": sum(v != 0 for row in power for v in row), "total_power_W": str(sum((v for row in power for v in row), Decimal(0))), "max_irradiance_W_m2": str(max(v / (dx * dy) for row in power for v in row)), "object_pixels": [{"object_id": o, "pixel_x": x, "pixel_y": y, "power_W": str(v)} for o, x, y, v in projected]}
    args.output_csv.with_suffix(".summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")


# 检查当前条件，仅在满足约束时执行对应分支。
if __name__ == "__main__":
    main()
