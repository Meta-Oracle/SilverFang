"""Revolver ammo HUD states from sprites/hud/revolverammooverhaul.png.

Two state rows of 7 cylinders each (0/6 .. 6/6 rounds) + 3 firing-flash
frames, in BASE (blue) and AWAKENED (orange) palettes.

Outputs Assets/Art/UI/Revolver/:
  base_0..6.png / awk_0..6.png      cylinder fill states
  base_fire0..2.png / awk_fire0..2  muzzle-flash frames
Alpha keyed from luminance (soft glow edges kept).
"""
import os

import numpy as np
from PIL import Image

BASE = os.path.dirname(os.path.abspath(__file__))
SHEET = os.path.join(BASE, "..", "sprites", "hud", "revolverammooverhaul.png")
OUT = os.path.join(BASE, "..", "Assets", "Art", "UI", "Revolver")

COLS = [(37, 156), (189, 308), (341, 461), (494, 615), (647, 768), (799, 920), (950, 1072)]
FIRE_COLS = [(1116, 1234), (1253, 1369), (1393, 1509)]
ROWS = {"base": (205, 338), "awk": (445, 572)}


def cut(img, x0, y0, x1, y1):
    region = np.asarray(img.crop((x0, y0, x1, y1)).convert("RGB"), dtype=np.int16)
    lum = region.max(axis=2)
    ys, xs = np.where(lum > 38)
    if len(ys) == 0:
        return None
    region = region[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    lum = region.max(axis=2)
    rgba = np.zeros((*lum.shape, 4), dtype=np.uint8)
    rgba[..., :3] = np.clip(region, 0, 255)
    rgba[..., 3] = np.clip(lum.astype(np.int32) * 16 // 10, 0, 255)
    return Image.fromarray(rgba)


def main():
    os.makedirs(OUT, exist_ok=True)
    img = Image.open(SHEET)
    for who, (y0, y1) in ROWS.items():
        for i, (x0, x1) in enumerate(COLS):
            im = cut(img, x0, y0, x1, y1)
            if im: im.save(os.path.join(OUT, f"{who}_{i}.png"))
        for i, (x0, x1) in enumerate(FIRE_COLS):
            im = cut(img, x0, y0, x1, y1)
            if im: im.save(os.path.join(OUT, f"{who}_fire{i}.png"))
        print(who, "done")


if __name__ == "__main__":
    main()
