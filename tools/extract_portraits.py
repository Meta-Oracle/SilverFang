"""High-res character-select portraits.

Sources:
  Silver: sprites/fa0569d9-...png  (0,0)-(253,416)   full-body awakened pose
  Hilo:   sprites/hilo/hilohud.png (6,88)-(314,522)  full-body clan-warrior pose

Outputs Assets/Art/UI/Portraits/{silver,hilo}_select.png, consumed by
SetupTools.BuildCharacterSelect (falls back to idle frame 0 if missing).
"""
import os

from PIL import Image

BASE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(BASE, "..", "Assets", "Art", "UI", "Portraits")

JOBS = [
    ("silver_select.png",
     os.path.join(BASE, "..", "sprites", "fa0569d9-5bbc-4a06-a660-b0b87f85e68f.png"),
     (0, 0, 253, 416)),
    ("hilo_select.png",
     os.path.join(BASE, "..", "sprites", "hilo", "hilohud.png"),
     (6, 88, 314, 522)),
]


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, src, box in JOBS:
        img = Image.open(src).convert("RGBA").crop(box)
        img.save(os.path.join(OUT, name))
        print(name, img.size)


if __name__ == "__main__":
    main()
