#!/usr/bin/env python3
from __future__ import annotations

import argparse
import csv
import json
from decimal import Decimal
from pathlib import Path


def main() -> None:
    parser = argparse.ArgumentParser(description="Reconstruct a detector image without invoking the forward solver.")
    parser.add_argument("response_csv", type=Path)
    parser.add_argument("metadata_json", type=Path)
    parser.add_argument("time_s", type=Decimal)
    parser.add_argument("output_csv", type=Path)
    args = parser.parse_args()
    metadata = json.loads(args.metadata_json.read_text(encoding="utf-8"))
    nx, ny = int(metadata["grid_nx"]), int(metadata["grid_ny"])
    radius = int(metadata["spot_radius_cells"])
    if radius != 0:
        raise SystemExit("This frozen reconstruction contract currently supports SPOT_RADIUS_CELLS=0 only")
    size = Decimal(str(metadata["spot_plane_size_m"]))
    dx, dy = size / nx, size / ny
    power = [[Decimal(0) for _ in range(nx)] for _ in range(ny)]
    selected = []
    with args.response_csv.open(newline="", encoding="utf-8") as handle:
        for row in csv.DictReader(handle):
            if Decimal(row["time_s"].strip()) == args.time_s:
                selected.append(row)
    if not selected:
        raise SystemExit(f"no response rows at time {args.time_s}")
    projected = []
    for row in selected:
        if int(row["released_flag"]) != 1 or int(row["in_screen_flag"]) != 1:
            continue
        x, y = Decimal(row["screen_x_m"].strip()), Decimal(row["screen_y_m"].strip())
        ix = int((x + size / 2) / dx) + 1
        iy = int((y + size / 2) / dy) + 1
        if not (1 <= ix <= nx and 1 <= iy <= ny):
            raise SystemExit(f"in_screen row maps out of bounds: object={row['object_id']} ix={ix} iy={iy}")
        value = Decimal(row["detector_received_power_W"].strip())
        power[iy - 1][ix - 1] += value
        projected.append((row["object_id"], ix, iy, value))
    args.output_csv.parent.mkdir(parents=True, exist_ok=True)
    with args.output_csv.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerow(["pixel_x", "pixel_y", "screen_center_x_m", "screen_center_y_m", "power_W", "irradiance_W_m2"])
        for iy in range(1, ny + 1):
            y = -size / 2 + (Decimal(iy) - Decimal("0.5")) * dy
            for ix in range(1, nx + 1):
                x = -size / 2 + (Decimal(ix) - Decimal("0.5")) * dx
                value = power[iy - 1][ix - 1]
                writer.writerow([ix, iy, str(x), str(y), str(value), str(value / (dx * dy))])
    summary = {"time_s": str(args.time_s), "response_row_count": len(selected), "projected_object_count": len(projected), "nonzero_pixel_count": sum(v != 0 for row in power for v in row), "total_power_W": str(sum((v for row in power for v in row), Decimal(0))), "max_irradiance_W_m2": str(max(v / (dx * dy) for row in power for v in row)), "object_pixels": [{"object_id": o, "pixel_x": x, "pixel_y": y, "power_W": str(v)} for o, x, y, v in projected]}
    args.output_csv.with_suffix(".summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
