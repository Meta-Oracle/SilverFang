"""Combo level meter art from sprites/hud/combolevelmeters.png (v2 layout).

Column-major alphabetical ladder per character half:
  col1 F E D C | col2 B A S SS | col3 SSS X XX XXX
Silver blue top half, Hilo purple bottom half.

Outputs (Assets/Art/UI/ComboMeter/):
  {who}_frame.png      empty meter chassis (F panel, letter + bar blanked)
  {who}_fill.png       bar fill texture (XXX 100% panel interior)
  {who}_letter_{i}.png tier glyphs 0..11

Panel geometry consumed by SetupTools.BuildComboLevelMeter:
  panel crop 524x76 (24px left margin for wide glyph overhang)
  bar interior rel (111,29)-(464,52); letter block rel (0,0)-(120,76)
"""
import os

import numpy as np
from PIL import Image

BASE = os.path.dirname(os.path.abspath(__file__))
SHEET = os.path.join(BASE, "..", "sprites", "hud", "combolevelmeters.png")
OUT = os.path.join(BASE, "..", "Assets", "Art", "UI", "ComboMeter")

COLS = [20, 538, 1056]
ROWS = {"silver": [63, 151, 239, 327], "hilo": [572, 653, 739, 825]}
MARGIN = 24
PANEL_W, PANEL_H = 500 + MARGIN, 76
BAR = (111, 29, 464, 52)
LETTER = (0, 0, 120, 76)


def soft_rgba(region, alpha_gain=1.8):
    lum = region.max(axis=2)
    rgba = np.zeros((*lum.shape, 4), dtype=np.uint8)
    rgba[..., :3] = np.clip(region, 0, 255)
    rgba[..., 3] = np.clip((lum.astype(np.int32) * int(alpha_gain * 10)) // 10, 0, 255)
    return rgba


def main():
    os.makedirs(OUT, exist_ok=True)
    img = np.asarray(Image.open(SHEET).convert("RGB"), dtype=np.int16)
    img = np.pad(img, ((0, 0), (MARGIN, 0), (0, 0)))  # left margin: col1 glyphs sit at x~0

    for who, rows in ROWS.items():
        panels = []
        for cx in COLS:           # column-major: alphabetical ladder order
            for ry in rows:
                panels.append(img[ry:ry + PANEL_H, cx:cx + PANEL_W])  # +MARGIN pad cancels -MARGIN

        for i, panel in enumerate(panels):
            lx0, ly0, lx1, ly1 = LETTER
            glyph = panel[ly0:ly1, lx0:lx1]
            # tight-trim the glyph
            glum = glyph.max(axis=2)
            ys, xs = np.where(glum > 45)
            if len(ys) == 0:
                print(f"!! {who} letter {i}: empty glyph region")
                continue
            glyph = glyph[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
            Image.fromarray(soft_rgba(glyph)).save(os.path.join(OUT, f"{who}_letter_{i}.png"))

        # frame = F panel, letter + bar interior blanked for dynamic overlays
        frame = panels[0].copy()
        bx0, by0, bx1, by1 = BAR
        frame[by0 + 2:by1 - 2, bx0 + 2:bx1 - 2] = (6, 8, 12)
        frame[LETTER[1]:LETTER[3], LETTER[0]:LETTER[2]] = (6, 8, 12)
        frame[3:26, 130:410] = (6, 8, 12)    # baked tier name -> dynamic Text
        frame[26:56, 466:520] = (6, 8, 12)   # baked percent label
        rgba = np.zeros((PANEL_H, PANEL_W, 4), dtype=np.uint8)
        rgba[..., :3] = np.clip(frame, 0, 255)
        lum = frame.max(axis=2)
        rgba[..., 3] = np.where(lum > 14, 255, np.clip(lum.astype(np.int32) * 12, 0, 255)).astype(np.uint8)
        Image.fromarray(rgba).save(os.path.join(OUT, f"{who}_frame.png"))

        # fill texture from the XXX (100%) panel bar interior
        xxx = panels[11]
        fill = xxx[by0 + 3:by1 - 3, bx0 + 3:bx1 - 3]
        frgba = np.zeros((*fill.shape[:2], 4), dtype=np.uint8)
        frgba[..., :3] = np.clip(fill, 0, 255)
        frgba[..., 3] = 255
        Image.fromarray(frgba).save(os.path.join(OUT, f"{who}_fill.png"))
        print(who, "done:", len(panels), "panels")


if __name__ == "__main__":
    main()
