"""Extract Hilo's HUD plate from sprites/hilo/hilohud.png.

Outputs (Assets/Art/UI/HiloHUD/):
  hilo_player_plate.png  - idle plate (323,38)-(959,154), bar interiors blanked
  hilo_plate_emblem.png  - the center infinity medallion, re-drawn above fills
Plate-local geometry consumed by SetupTools.BuildHiloPlate:
  health fill  ( 92, 32) 490x13   (medallion overlays the middle - emblem image sits on top)
  energy fill  (133, 52) 100x11
  yin-yang row ( 88, 64) 150x13
"""
import os
from PIL import Image

BASE = os.path.dirname(os.path.abspath(__file__))
SHEET = os.path.join(BASE, "..", "sprites", "hilo", "hilohud.png")
OUT = os.path.join(BASE, "..", "Assets", "Art", "UI", "HiloHUD")

PLATE_REGION = (323, 38, 959, 154)
EMBLEM_LOCAL = (300, 2, 425, 68)   # infinity medallion, re-overlaid above the fills
PLATE_FILL = (8, 7, 10, 255)
ERASE = [
    (140, 38, 625, 60),   # health bar interior + numbers
    (108, 64, 318, 88),   # energy bar interior + numbers
    (84, 88, 318, 114),   # yin-yang orb row
    (520, 60, 636, 116),  # sheet annotation text (HEALTH BAR / ENERGY BAR / ...)
]


def main():
    os.makedirs(OUT, exist_ok=True)
    plate = Image.open(SHEET).convert("RGBA").crop(PLATE_REGION)

    emblem = plate.crop(EMBLEM_LOCAL)
    emblem.save(os.path.join(OUT, "hilo_plate_emblem.png"))

    px = plate.load()
    for x0, y0, x1, y1 in ERASE:
        for x in range(x0, min(x1, plate.width)):
            for y in range(y0, min(y1, plate.height)):
                px[x, y] = PLATE_FILL
    plate.save(os.path.join(OUT, "hilo_player_plate.png"))
    print("plate", plate.size, "emblem", emblem.size)


if __name__ == "__main__":
    main()
