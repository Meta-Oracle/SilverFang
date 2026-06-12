"""Generate extraction config for the awakened combos & finishers sheet."""
import json
import os

from PIL import Image

BASE = os.path.dirname(os.path.abspath(__file__))
SHEET = os.path.join(BASE, "..", "sprites", "awakenedstatecombosandfinishers.png")

im = Image.open(SHEET)
W, H = im.size
print("sheet", im.size)
sx, sy = W / 600.0, H / 400.0

anims = []

# six combo sets, 7 evenly spaced rows each (display coords)
SETS = [
    ("comboA", 2, 196, 20, 145), ("comboB", 205, 396, 20, 145), ("comboC", 403, 596, 20, 145),
    ("comboD", 2, 196, 150, 263), ("comboE", 205, 396, 150, 263), ("comboF", 403, 596, 150, 263),
]
for name, dx0, dx1, dy0, dy1 in SETS:
    x0, x1 = int(dx0 * sx), int(dx1 * sx)
    y0, y1 = int(dy0 * sy), int(dy1 * sy)
    for i in range(7):
        ry0 = y0 + (y1 - y0) * i // 7
        ry1 = y0 + (y1 - y0) * (i + 1) // 7
        anims.append({"name": f"{name}_{i + 1}", "region": [x0, ry0 + 2, x1, ry1 - 1]})

# finishers: 7 panels across the sheet
FIN = ["fin_void_erasure", "fin_dimension_collapse", "fin_eclipse_slash",
       "fin_celestial_judgment", "fin_apocalypse_blade", "fin_silent_requiem", "fin_omega_cut"]
fy0, fy1 = int(272 * sy), int(335 * sy)
for i, name in enumerate(FIN):
    fx0 = int((2 + i * 85) * sx)
    fx1 = int((2 + (i + 1) * 85) * sx)
    anims.append({"name": name, "region": [fx0, fy0, fx1, fy1]})

# ultimate sequence: one long strip
anims.append({"name": "ultimate_void_ascension",
              "region": [int(2 * sx), int(345 * sy), int(596 * sx), int(398 * sy)]})

config = {"sheets": [{
    "image": "../sprites/awakenedstatecombosandfinishers.png",
    "out_dir": "../Assets/Art/Sprites/Awakened",
    "bg_threshold": 28, "key_threshold": 12,
    "min_ink_per_column": 2, "gap_columns": 3, "min_frame_width": 10,
    "animations": anims,
}]}

with open(os.path.join(BASE, "awakened_combos_config.json"), "w") as f:
    json.dump(config, f, indent=2)
print(f"wrote awakened_combos_config.json ({len(anims)} animations)")
