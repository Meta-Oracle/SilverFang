"""Generate extraction config for the dash/sprint/teleport sheet by
profiling ink rows inside each labeled section, then emit JSON consumed
by extract_v2.py."""
import json
import os

import numpy as np
from PIL import Image

BASE = os.path.dirname(os.path.abspath(__file__))
SHEET = os.path.join(BASE, "..", "sprites", "dashandsprintanimationssprites.png")

im = Image.open(SHEET).convert("RGB")
W, H = im.size
print("sheet", im.size)
arr = np.asarray(im).max(axis=2)
sx = W / 600.0  # display->native scale
sy = H / 400.0

# (out_prefix, x0, x1, y0, y1, names) — display coords of each section's row area
SECTIONS = [
    ("",     58, 295, 30, 158, ["da_slash", "da_thrust", "da_uppercut", "da_spin", "da_shoot", "da_heavy"]),
    ("",     58, 295, 168, 272, ["sp_slash", "sp_thrust", "sp_spin", "sp_shoot", "sp_heavy"]),
    ("",     58, 295, 290, 398, ["tp_short", "tp_long", "tp_strike", "tp_shoot"]),
    ("awk",  358, 595, 30, 158, ["da_slash", "da_thrust", "da_uppercut", "da_spin", "da_shoot", "da_heavy"]),
    ("awk",  358, 595, 168, 272, ["sp_slash", "sp_thrust", "sp_spin", "sp_shoot", "sp_heavy"]),
    ("awk",  358, 595, 290, 398, ["tp_short", "tp_long", "tp_strike", "tp_shoot"]),
]

THRESH = 30


def row_bands(x0, x1, y0, y1):
    region = arr[y0:y1, x0:x1]
    ink = (region > THRESH).sum(axis=1)
    bands, start = [], None
    for i, v in enumerate(ink >= 3):
        if v and start is None:
            start = i
        elif not v and start is not None:
            if i - start >= 8:
                bands.append((y0 + start, y0 + i))
            start = None
    if start is not None and (y1 - y0) - start >= 8:
        bands.append((y0 + start, y1))
    return bands


anims_normal, anims_awk = [], []
for prefix, dx0, dx1, dy0, dy1, names in SECTIONS:
    x0, x1 = int(dx0 * sx), int(dx1 * sx)
    y0, y1 = int(dy0 * sy), int(dy1 * sy)
    bands = row_bands(x0, x1, y0, y1)
    if len(bands) != len(names):
        n = len(names)
        bands = [(y0 + (y1 - y0) * i // n, y0 + (y1 - y0) * (i + 1) // n) for i in range(n)]
        print(f"section {prefix or 'normal'} {names[0]}..: mismatch -> even split into {n}")
    else:
        print(f"section {prefix or 'normal'} {names[0]}..: {len(bands)} bands for {len(names)} names")
    target = anims_awk if prefix else anims_normal
    for name, (by0, by1) in zip(names, bands):
        target.append({"name": (prefix + name) if prefix else name,
                       "region": [x0, max(0, by0 - 2), x1, min(H, by1 + 2)]})

config = {"sheets": [
    {
        "image": "../sprites/dashandsprintanimationssprites.png",
        "out_dir": "../Assets/Art/Sprites/Silver",
        "bg_threshold": 30, "key_threshold": 12,
        "min_ink_per_column": 2, "gap_columns": 3, "min_frame_width": 10,
        "animations": anims_normal,
    },
    {
        "image": "../sprites/dashandsprintanimationssprites.png",
        "out_dir": "../Assets/Art/Sprites/Awakened",
        "bg_threshold": 30, "key_threshold": 12,
        "min_ink_per_column": 2, "gap_columns": 3, "min_frame_width": 10,
        "animations": anims_awk,
    },
]}

with open(os.path.join(BASE, "dash_config.json"), "w") as f:
    json.dump(config, f, indent=2)
print("wrote dash_config.json")
