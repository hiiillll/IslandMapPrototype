import argparse
import json
import math
import sys

import bpy
import numpy as np


argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
parser = argparse.ArgumentParser()
parser.add_argument("--source", required=True)
args = parser.parse_args(argv)

bpy.ops.wm.open_mainfile(filepath=args.source)
report = {}
for name in ("Wheel_FL_Spin", "Wheel_RL_Spin"):
    obj = bpy.data.objects[name]
    points = np.array([tuple(vertex.co) for vertex in obj.data.vertices], dtype=float)
    centered = points - np.median(points, axis=0)
    covariance = np.cov(centered, rowvar=False)
    values, vectors = np.linalg.eigh(covariance)
    axis = vectors[:, int(np.argmin(values))]
    if axis[0] < 0:
        axis = -axis
    radial = np.sqrt(centered[:, 1] ** 2 + centered[:, 2] ** 2)
    core_axes = {}
    for cutoff in (0.08, 0.10, 0.12, 0.14):
        core = centered[radial < cutoff]
        core_values, core_vectors = np.linalg.eigh(np.cov(core, rowvar=False))
        core_axis = core_vectors[:, int(np.argmin(core_values))]
        if core_axis[0] < 0:
            core_axis = -core_axis
        core_axes[str(cutoff)] = {
            "count": len(core),
            "axis": core_axis.tolist(),
            "angle_from_x_degrees": float(np.degrees(np.arccos(np.clip(core_axis[0], -1.0, 1.0)))),
        }
    outer_threshold = float(np.percentile(radial, 95))
    angle_bins = {}
    for point, radius in zip(centered, radial):
        if radius < outer_threshold:
            continue
        angle = math.degrees(math.atan2(point[2], point[1]))
        bucket = int(math.floor((angle + 180.0) / 15.0)) * 15 - 180
        values_for_bucket = angle_bins.setdefault(str(bucket), {"count": 0, "max_radius": 0.0, "min_x": 999.0, "max_x": -999.0})
        values_for_bucket["count"] += 1
        values_for_bucket["max_radius"] = max(values_for_bucket["max_radius"], float(radius))
        values_for_bucket["min_x"] = min(values_for_bucket["min_x"], float(point[0]))
        values_for_bucket["max_x"] = max(values_for_bucket["max_x"], float(point[0]))
    report[name] = {
        "vertex_count": len(points),
        "median": np.median(points, axis=0).tolist(),
        "mean": np.mean(points, axis=0).tolist(),
        "pca_values": values.tolist(),
        "pca_axle": axis.tolist(),
        "axis_angle_from_x_degrees": float(np.degrees(np.arccos(np.clip(axis[0], -1.0, 1.0)))),
        "core_axes": core_axes,
        "outer_angle_bins": angle_bins,
        "radial_percentiles": {
            str(p): float(np.percentile(radial, p))
            for p in (50, 75, 90, 95, 97, 98, 99, 99.5, 100)
        },
    }

print("WHEEL_ANALYSIS=" + json.dumps(report, ensure_ascii=False))
