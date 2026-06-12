"""Generate extraction config for sprites/hilo/firsthilospriteset.png
("HILO" complete character sheet, 1536x1024).

Sections (native coords, labels live inside row bands and are stripped by
the extractor's caption filter):
- MOVEMENT             x130..510  y15..356    11 rows (2 split rows)
- KINETIC ARM ATTACKS  x5..510    y358..695   9 rows (quick shot + charges + beams)
- BASIC ATTACKS        x515..1020 y15..488    12 rows
- ADVANCED COMBOS      x515..1020 y500..695   8 rows
- SPECIAL MOVES        x1025..1536 y15..335   9 rows
- THROW & GRAPPLE      x1025..1536 y338..560  rows (used: throw)
- AWAKENED (YIN-YANG)  x1025..1536 y565..695  3 rows
- FINISHERS            full width y700..775   6 panels (left 3 + right 3)
- DEEP SYNERGY         full width y788..1015  panels (sampled)
"""
import json
import os

import numpy as np
from PIL import Image

BASE = os.path.dirname(os.path.abspath(__file__))
SHEET = os.path.join(BASE, "..", "sprites", "hilo", "firsthilospriteset.png")

im = Image.open(SHEET).convert("RGB")
W, H = im.size
print("sheet", im.size)
arr = np.asarray(im).max(axis=2)
THRESH = 30


def row_bands(x0, x1, y0, y1, min_h=12):
    region = arr[y0:y1, x0:x1]
    ink = (region > THRESH).sum(axis=1)
    bands, start = [], None
    for i, v in enumerate(ink >= 3):
        if v and start is None:
            start = i
        elif not v and start is not None:
            bands.append((y0 + start, y0 + i))
            start = None
    if start is not None:
        bands.append((y0 + start, y1))
    merged = []
    for b in bands:
        if merged and (merged[-1][1] - merged[-1][0]) < min_h and (b[0] - merged[-1][1]) < 12:
            merged[-1] = [merged[-1][0], b[1]]
        else:
            merged.append(list(b))
    return [tuple(b) for b in merged if (b[1] - b[0]) >= min_h]


SECTIONS = [
    ("movement", 130, 510, 15, 356, [
        "idle", "walk", "run", "dash", "jump", "double_jump", "fall", "land",
        "crouch_pair", "hurt_pair", "down_pair"]),
    ("kinetic", 5, 510, 358, 695, [
        "quick_shot", "charge_shot_l1", "charge_shot_l2", "charge_shot_l3",
        "wide_beam", "phantom_beam", "burst_cannon", "homing_missile", "energy_wave"]),
    ("basic", 515, 1020, 15, 488, [
        "light1", "light2", "light3", "heavy1", "heavy2", "heavy3",
        "claw_slash", "claw_spin", "claw_thrust", "claw_uppercut",
        "snap_kick", "roundhouse_kick"]),
    ("combos", 515, 1020, 500, 695, [
        "punch_combo_a", "punch_combo_b", "claw_combo_a", "claw_combo_b",
        "claw_combo_c", "kinetic_combo", "aerial_combo", "kill_combo"]),
    ("specials", 1025, 1536, 15, 335, [
        "shadow_step", "yin_slip", "spinning_claw", "rising_palm",
        "phantom_strike", "shadow_clone", "energy_claw", "cyclone_kick", "yinyang_burst"]),
    ("awakened", 1025, 1536, 565, 695, [
        "awk_idle", "awk_movement", "awk_attacks"]),
]

anims = []
for name, x0, x1, y0, y1, names in SECTIONS:
    bands = row_bands(x0, x1, y0, y1)
    status = "OK" if len(bands) == len(names) else "MISMATCH"
    print(f"{name}: {len(bands)} bands for {len(names)} names [{status}]")
    if len(bands) != len(names):
        for b in bands:
            print("   band", b, b[1] - b[0])
        continue
    for n, (by0, by1) in zip(names, bands):
        if n == "crouch_pair":
            mid = x0 + (x1 - x0) // 2
            anims.append({"name": "crouch", "region": [x0, by0 - 2, mid, by1 + 2]})
            anims.append({"name": "crouch_walk", "region": [mid + 4, by0 - 2, x1, by1 + 2]})
        elif n == "hurt_pair":
            mid = x0 + (x1 - x0) // 2
            anims.append({"name": "hurt", "region": [x0, by0 - 2, mid, by1 + 2]})
            anims.append({"name": "hurt_heavy", "region": [mid + 4, by0 - 2, x1, by1 + 2]})
        elif n == "down_pair":
            mid = x0 + (x1 - x0) // 2
            anims.append({"name": "knockdown", "region": [x0, by0 - 2, mid, by1 + 2]})
            anims.append({"name": "getup", "region": [mid + 4, by0 - 2, x1, by1 + 2]})
        else:
            anims.append({"name": n, "region": [x0, by0 - 2, x1, by1 + 2]})

config = {"sheets": [{
    "image": "../sprites/hilo/firsthilospriteset.png",
    "out_dir": "../Assets/Art/Sprites/Hilo",
    "bg_threshold": 30, "key_threshold": 12,
    "min_ink_per_column": 2, "gap_columns": 3, "min_frame_width": 10,
    "animations": anims,
}]}

with open(os.path.join(BASE, "hilo_config.json"), "w") as f:
    json.dump(config, f, indent=2)
print("wrote hilo_config.json", len(anims), "animations")
