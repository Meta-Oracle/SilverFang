"""Canonical enemy body heights (px @ PPU 48). Humanoids match the hero
body scale (~84px); beasts and mechs run deliberately larger.

Run after any enemy re-extraction:  python tools/normalize_enemies.py
"""
import os
import subprocess
import sys

BASE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(BASE)

HEIGHTS = {
    "Werewolf": 104, "Chimera": 132, "Titan": 112, "Drone": 62,   # big/odd
    "Reaper": 86, "Sentinel": 86, "Samurai": 86, "SamuraiDual": 86,
    "Bruiser": 86,                                                 # hero-sized
    "Guard": 82, "Ranger": 82, "Shinobi": 82,                      # light units
}


def main():
    fails = 0
    for name, height in HEIGHTS.items():
        result = subprocess.run(
            [sys.executable, os.path.join(BASE, "normalize_sprite_canvases.py"),
             f"Assets/Art/Sprites/Enemies/{name}",
             "--target-height", str(height), "--clamp", "0.4", "4.0"],
            cwd=ROOT, capture_output=True, text=True)
        tail = (result.stdout + result.stderr).strip().splitlines()
        print(tail[-1] if tail else name)
        fails += result.returncode != 0
    sys.exit(1 if fails else 0)


if __name__ == "__main__":
    main()
