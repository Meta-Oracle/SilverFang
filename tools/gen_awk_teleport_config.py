"""Config generator for the awakened dash & teleport phases sheet."""
import json
import os

import numpy as np
from PIL import Image

BASE = os.path.dirname(os.path.abspath(__file__))
SHEET = os.path.join(BASE, "..", "sprites", "dashandsprintteleportattacksandanimations.png")

im = Image.open(SHEET).convert("RGB")
W, H = im.size
print("sheet", im.size)
arr = np.asarray(im).max(axis=2)
sx, sy = W / 384.0, H / 256.0

SECTIONS = [
    ("awkdash", 2, 185, 16, 90, 3),
    ("awktp2", 2, 185, 100, 178, 3),
    ("awktpatk", 2, 185, 188, 252, 3),
    ("awkrun", 195, 380, 16, 90, 3),
    ("awkltp", 195, 380, 100, 178, 3),
    ("awkltpatk", 195, 380, 188, 252, 3),
]


def row_bands(x0, x1, y0, y1, expected):
    region = arr[y0:y1, x0:x1]
    ink = (region > 28).sum(axis=1)
    bands, start = [], None
    for i, v in enumerate(ink >= 3):
        if v and start is None:
            start = i
        elif not v and start is not None:
            if i - start >= 6:
                bands.append((y0 + start, y0 + i))
            start = None
    if start is not None:
        bands.append((y0 + start, y1))
    if len(bands) != expected:
        bands = [(y0 + (y1 - y0) * i // expected, y0 + (y1 - y0) * (i + 1) // expected)
                 for i in range(expected)]
        print(f"  (even split for {expected})")
    return bands


anims = []
for name, dx0, dx1, dy0, dy1, rows in SECTIONS:
    x0, x1 = int(dx0 * sx), int(dx1 * sx)
    y0, y1 = int(dy0 * sy), int(dy1 * sy)
    bands = row_bands(x0, x1, y0, y1, rows)
    print(name, len(bands), "rows")
    for i, (by0, by1) in enumerate(bands):
        anims.append({"name": f"{name}_{i + 1}", "region": [x0, max(0, by0 - 1), x1, min(H, by1 + 1)]})

config = {"sheets": [{
    "image": "../sprites/dashandsprintteleportattacksandanimations.png",
    "out_dir": "../Assets/Art/Sprites/Awakened",
    "bg_threshold": 26, "key_threshold": 10,
    "min_ink_per_column": 2, "gap_columns": 3, "min_frame_width": 8,
    "animations": anims,
}]}

with open(os.path.join(BASE, "awk_teleport_config.json"), "w") as f:
    json.dump(config, f, indent=2)
print(f"wrote awk_teleport_config.json ({len(anims)} animations)")
