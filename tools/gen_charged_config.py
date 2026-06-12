"""Generate extraction config for the charged-attacks sheet
(sprites/chargedattacks.png) by profiling ink rows inside each labeled
section, then emit JSON consumed by extract_v2.py.

Sheet layout (display coords 600x400):
- top-left:    CHARGED REVOLVER SHOTS (GUN)        levels 1-3 -> charge_shot_l1..3
- top-right:   CHARGED LIGHT ATTACKS (MELEE SLASH) levels 1-3 -> charge_light_l1..3
- mid-left:    CHARGED HEAVY ATTACKS               levels 1-3 -> charge_heavy_l1..3
- mid-right:   CHARGED AWAKENED ATTACKS            light/heavy/gun -> charge_awk_* (Awakened)
- lower-left:  CHARGE UP (ALL ATTACK TYPES)        light/heavy/gun -> charge_hold_*
- bottom rows: 2.00s showcase panels (skipped: single huge cells, not animation strips)
"""
import json
import os

import numpy as np
from PIL import Image

BASE = os.path.dirname(os.path.abspath(__file__))
SHEET = os.path.join(BASE, "..", "sprites", "chargedattacks.png")

im = Image.open(SHEET).convert("RGB")
W, H = im.size
print("sheet", im.size)
arr = np.asarray(im).max(axis=2)
sx = W / 600.0
sy = H / 400.0

# (out, x0, x1, y0, y1, names) in display coords
SECTIONS = [
    ("silver",   32, 300, 30, 124, ["charge_shot_l1", "charge_shot_l2", "charge_shot_l3"]),
    ("silver",  310, 600, 30, 124, ["charge_light_l1", "charge_light_l2", "charge_light_l3"]),
    ("silver",   32, 300, 130, 222, ["charge_heavy_l1", "charge_heavy_l2", "charge_heavy_l3"]),
    ("awakened", 310, 600, 130, 222, ["charge_awk_light", "charge_awk_heavy", "charge_awk_gun"]),
    ("silver",   32, 475, 226, 292, ["charge_hold_light", "charge_hold_heavy", "charge_hold_gun"]),
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


anims_silver, anims_awk = [], []
for out, dx0, dx1, dy0, dy1, names in SECTIONS:
    x0, x1 = int(dx0 * sx), int(dx1 * sx)
    y0, y1 = int(dy0 * sy), int(dy1 * sy)
    bands = row_bands(x0, x1, y0, y1)
    # caption lines above each strip can split off as thin bands: merge any
    # band thinner than 14 native px into its successor
    merged = []
    for b in bands:
        if merged and (merged[-1][1] - merged[-1][0]) < 14:
            merged[-1] = (merged[-1][0], b[1])
        else:
            merged.append(list(b))
    bands = [tuple(b) for b in merged]
    if len(bands) != len(names):
        n = len(names)
        bands = [(y0 + (y1 - y0) * i // n, y0 + (y1 - y0) * (i + 1) // n) for i in range(n)]
        print(f"section {names[0]}..: mismatch -> even split into {n}")
    else:
        print(f"section {names[0]}..: {len(bands)} bands")
    target = anims_silver if out == "silver" else anims_awk
    for name, (by0, by1) in zip(names, bands):
        target.append({"name": name, "region": [x0, max(0, by0 - 2), x1, min(H, by1 + 2)]})

config = {"sheets": [
    {
        "image": "../sprites/chargedattacks.png",
        "out_dir": "../Assets/Art/Sprites/Silver",
        "bg_threshold": 30, "key_threshold": 12,
        "min_ink_per_column": 2, "gap_columns": 3, "min_frame_width": 10,
        "animations": anims_silver,
    },
    {
        "image": "../sprites/chargedattacks.png",
        "out_dir": "../Assets/Art/Sprites/Awakened",
        "bg_threshold": 30, "key_threshold": 12,
        "min_ink_per_column": 2, "gap_columns": 3, "min_frame_width": 10,
        "animations": anims_awk,
    },
]}

with open(os.path.join(BASE, "charged_config.json"), "w") as f:
    json.dump(config, f, indent=2)
print("wrote charged_config.json")
