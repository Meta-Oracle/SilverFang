"""Generate extraction config for sprites/extraframes.png
("SILVER - COMPLETE ANIMATION SET (AWAKENED STATE)", 1536x1024).

Three label columns plus a full-width extended transformation strip at the
bottom. All rows are awakened-state art -> Assets/Art/Sprites/Awakened.
Rows that sit side by side inside one ink band (idle|walk etc.) are split
by x afterwards.
"""
import json
import os

import numpy as np
from PIL import Image

BASE = os.path.dirname(os.path.abspath(__file__))
SHEET = os.path.join(BASE, "..", "sprites", "extraframes.png")

im = Image.open(SHEET).convert("RGB")
W, H = im.size
print("sheet", im.size)
arr = np.asarray(im).max(axis=2)
THRESH = 30


def row_bands(x0, x1, y0, y1, min_h=14, min_ink=3):
    region = arr[y0:y1, x0:x1]
    ink = (region > THRESH).sum(axis=1)
    bands, start = [], None
    for i, v in enumerate(ink >= min_ink):
        if v and start is None:
            start = i
        elif not v and start is not None:
            bands.append((y0 + start, y0 + i))
            start = None
    if start is not None:
        bands.append((y0 + start, y1))
    # merge thin caption bands into the following sprite band
    merged = []
    for b in bands:
        if merged and (merged[-1][1] - merged[-1][0]) < min_h and (b[0] - merged[-1][1]) < 14:
            merged[-1] = (merged[-1][0], b[1])
        else:
            merged.append(list(b))
    return [tuple(b) for b in merged if (b[1] - b[0]) >= min_h]


# (column x0, x1, scan y0, y1, expected row names top->bottom)
# None entries are rows we split by x afterwards (handled below).
LEFT = (8, 525, 36, 900, [
    "idle_walk",        # IDLE | WALK side by side
    "run", "jump", "crouch",
    "guard_hurts",      # GUARD/READY | HURT (LIGHT) | HURT (HEAVY)
    "down_getup",       # KNOCKED DOWN | GET UP
    "awk_light1", "awk_light2", "awk_light3",
    "awk_heavy1", "awk_heavy2", "awk_heavy3",
    "awk2_charge_light_l1", "awk2_charge_light_l2", "awk2_charge_light_l3",
    "awk2_charge_heavy_l1", "awk2_charge_heavy_l2", "awk2_charge_heavy_l3",
])
MID = (530, 1035, 20, 930, [
    "awk_dash", "awk_sprint", "awk_dash_atk", "awk_sprint_atk",
    "combo_a", "combo_b", "combo_c", "combo_d", "combo_e",
    "awk_shot_l1", "awk_shot_l2", "awk_shot_l3",
])
RIGHT = (1040, 1536, 20, 930, [
    "awk_tp_short", "awk_tp_long", "awk_tp_strike_short", "awk_tp_strike_long",
    "air_jump_slash", "air_slash", "air_spin_slash",
    "awk_downward_strike",
    "charge_awk_light_l1", "charge_awk_light_l2", "charge_awk_light_l3",
    "charge_awk_heavy_l1", "charge_awk_heavy_l2", "charge_awk_heavy_l3",
    "charge_awk_gun_l1", "charge_awk_gun_l2", "charge_awk_gun_l3",
])

anims = []
ok = True
for x0, x1, y0, y1, names in (LEFT, MID, RIGHT):
    bands = row_bands(x0, x1, y0, y1)
    print(f"column x{x0}: {len(bands)} bands for {len(names)} names")
    if len(bands) != len(names):
        for b in bands:
            print("   band", b)
        ok = False
        continue
    for name, (by0, by1) in zip(names, bands):
        if name == "idle_walk":
            anims.append({"name": "idle", "region": [x0, by0 - 2, 258, by1 + 2]})
            anims.append({"name": "walk", "region": [262, by0 - 2, x1, by1 + 2]})
        elif name == "guard_hurts":
            third = (x1 - x0) // 3
            anims.append({"name": "guard", "region": [x0, by0 - 2, x0 + third, by1 + 2]})
            anims.append({"name": "hurt", "region": [x0 + third + 4, by0 - 2, x0 + 2 * third, by1 + 2]})
            anims.append({"name": "hurt_heavy", "region": [x0 + 2 * third + 4, by0 - 2, x1, by1 + 2]})
        elif name == "down_getup":
            mid = x0 + (x1 - x0) * 3 // 5
            anims.append({"name": "knockdown", "region": [x0, by0 - 2, mid, by1 + 2]})
            anims.append({"name": "getup", "region": [mid + 4, by0 - 2, x1, by1 + 2]})
        else:
            anims.append({"name": name, "region": [x0, by0 - 2, x1, by1 + 2]})

# Extended transformation: full-width strip at the bottom; the wolf-head logo
# at the far right is excluded by the x cutoff.
tbands = row_bands(8, 1408, 925, H, min_h=24)
print("transform bands:", tbands)
if tbands:
    by0, by1 = tbands[-1]
    anims.append({"name": "transform_ext", "region": [8, by0 - 2, 1408, by1 + 2]})

config = {"sheets": [{
    "image": "../sprites/extraframes.png",
    "out_dir": "../Assets/Art/Sprites/Awakened",
    "bg_threshold": 30, "key_threshold": 12,
    "min_ink_per_column": 2, "gap_columns": 3, "min_frame_width": 10,
    "animations": anims,
}]}

out = os.path.join(BASE, "extraframes_config.json")
with open(out, "w") as f:
    json.dump(config, f, indent=2)
print(("wrote" if ok else "WROTE WITH MISMATCHES:"), out, f"({len(anims)} animations)")
