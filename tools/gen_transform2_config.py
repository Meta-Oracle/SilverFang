"""Config generator for the extended transformation sheet (7 phase strips
+ final awakened idle). Band tops are trimmed past the frame-number row."""
import json
import os

import numpy as np
from PIL import Image

BASE = os.path.dirname(os.path.abspath(__file__))
SHEET = os.path.join(BASE, "..", "sprites", "awakenedstatetransformationsprites2.png")

im = Image.open(SHEET).convert("RGB")
W, H = im.size
print("sheet", im.size)
arr = np.asarray(im).max(axis=2)
sx, sy = W / 600.0, H / 400.0

# strips: (name, display x0, x1, y0, y1) — phase7/idle are native overrides
STRIPS = [
    ("transform_phase1", 2, 510, 24, 68),
    ("transform_phase2", 2, 510, 70, 115),
    ("transform_phase3", 2, 510, 118, 168),
    ("transform_phase4", 2, 510, 170, 215),
    ("transform_phase5", 2, 510, 218, 263),
    ("transform_phase6", 2, 510, 265, 315),
    ("transform_phase7", None, None, None, None),
]
NATIVE = {
    "transform_phase7": (5, 862, 945, 950),
}


def trim_captions(x0, y0, x1, y1):
    """Small fixed trim; per-frame component stripping in extract_v2
    removes any remaining caption digits."""
    return y0 + 10


anims = []
for name, dx0, dx1, dy0, dy1 in STRIPS:
    if name in NATIVE:
        x0, y0, x1, y1 = NATIVE[name]
    else:
        x0, x1 = int(dx0 * sx), int(dx1 * sx)
        y0, y1 = int(dy0 * sy), int(dy1 * sy)
        region = arr[y0:y1, x0:x1]
        rows = (region > 26).sum(axis=1)
        nz = np.nonzero(rows >= 3)[0]
        if len(nz) > 0:
            y0, y1 = y0 + int(nz[0]), y0 + int(nz[-1]) + 1
    if name != "awakened_idle_final":
        y0 = trim_captions(x0, y0, x1, y1)
    anim = {"name": name, "region": [x0, y0, x1, y1]}
    # contiguous glow defeats gap splitting on these
    if name == "transform_phase3":
        anim["force_frames"] = 24
    elif name == "transform_phase2":
        anim["force_frames"] = 19
    elif name == "awakened_idle_final":
        anim["force_frames"] = 4
    anims.append(anim)

config = {"sheets": [{
    "image": "../sprites/awakenedstatetransformationsprites2.png",
    "out_dir": "../Assets/Art/Sprites/Awakened",
    "bg_threshold": 26, "key_threshold": 10,
    "min_ink_per_column": 2, "gap_columns": 3, "min_frame_width": 8,
    "animations": anims,
}]}

with open(os.path.join(BASE, "transform2_config.json"), "w") as f:
    json.dump(config, f, indent=2)
print(f"wrote transform2_config.json ({len(anims)} strips)")
