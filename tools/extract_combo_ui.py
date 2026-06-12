"""Combo-counter glyphs + lock-on reticles from
sprites/hud/lockonreticlesandcombocounter.png (Silver blue top half,
Hilo purple bottom half).

Outputs:
  Assets/Art/UI/Combo/{silver,hilo}_{0..9}.png      digit glyphs
  Assets/Art/UI/Combo/{silver,hilo}_{hit,hits,x,max}.png
  Assets/Art/UI/LockOn/{silver,hilo}_reticle_{0..5}.png

Glyphs are glow art on black: alpha comes from luminance (soft edges kept).
"""
import os

import numpy as np
from PIL import Image

BASE = os.path.dirname(os.path.abspath(__file__))
SHEET = os.path.join(BASE, "..", "sprites", "hud", "lockonreticlesandcombocounter.png")
COMBO_OUT = os.path.join(BASE, "..", "Assets", "Art", "UI", "Combo")
LOCK_OUT = os.path.join(BASE, "..", "Assets", "Art", "UI", "LockOn")

# (label-list, region) per character half; Hilo = +512 on y
ROWS = [
    (["0", "1", "2", "3", "4", "5", "6", "7", "8", "9"], (16, 80, 880, 150), COMBO_OUT),
    (["hit", "hits", "x", "max"], (16, 172, 520, 218), COMBO_OUT),
    (["reticle_0", "reticle_1", "reticle_2", "reticle_3", "reticle_4", "reticle_5"],
     (888, 80, 1520, 152), LOCK_OUT),
]


def split_cells(mask, min_gap=12, min_w=8):
    cols = mask.sum(axis=0) > 0
    cells, start, gap = [], None, 0
    for x, v in enumerate(cols):
        if v:
            if start is None:
                start = x
            gap = 0
        elif start is not None:
            gap += 1
            if gap >= min_gap:
                end = x - gap + 1
                if end - start >= min_w:
                    cells.append((start, end))
                start, gap = None, 0
    if start is not None:
        cells.append((start, len(cols)))
    return cells


def main():
    os.makedirs(COMBO_OUT, exist_ok=True)
    os.makedirs(LOCK_OUT, exist_ok=True)
    img = np.asarray(Image.open(SHEET).convert("RGB"), dtype=np.int16)

    for who, dy in (("silver", 0), ("hilo", 512)):
        for labels, (x0, y0, x1, y1), out in ROWS:
            region = img[y0 + dy:y1 + dy, x0:x1]
            lum = region.max(axis=2)
            mask = lum > 40
            if labels[0] == "0":  # digits are monospaced: even grid beats gaps
                w = region.shape[1]
                cells = [(w * i // 10 + 2, w * (i + 1) // 10 - 2) for i in range(10)]
            else:
                cells = split_cells(mask)
            if len(cells) != len(labels):
                print(f"!! {who} {labels[0]}..: {len(cells)} cells for {len(labels)} labels")
            for (xa, xb), label in zip(cells, labels):
                cell = region[:, xa:xb]
                clum = cell.max(axis=2)
                if labels[0] == "0":
                    # italic lean bleeds neighbors into the grid cell:
                    # keep only the widest contiguous ink run
                    runs, s, g = [], None, 0
                    colink = (clum > 60).any(axis=0)
                    for x, v in enumerate(colink):
                        if v:
                            if s is None: s = x
                            g = 0
                        elif s is not None:
                            g += 1
                            if g >= 5:
                                runs.append((s, x - g + 1)); s, g = None, 0
                    if s is not None: runs.append((s, len(colink)))
                    if runs:
                        rs, re = max(runs, key=lambda r: r[1] - r[0])
                        cell = cell[:, max(0, rs - 2):re + 2]
                        clum = cell.max(axis=2)
                ys = np.where((clum > 40).any(axis=1))[0]
                if len(ys) == 0:
                    continue
                cell = cell[ys[0]:ys[-1] + 1]
                clum = clum[ys[0]:ys[-1] + 1]
                rgba = np.zeros((*clum.shape, 4), dtype=np.uint8)
                rgba[..., :3] = np.clip(cell, 0, 255)
                rgba[..., 3] = np.clip(clum * 1.8, 0, 255)  # soft glow alpha
                Image.fromarray(rgba).save(os.path.join(out, f"{who}_{label}.png"))
        print(who, "done")


if __name__ == "__main__":
    main()
