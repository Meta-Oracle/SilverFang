"""Key the Scematica logo and emit UI assets.

Usage: python make_scematica_icon.py <source_logo.png>
Writes Assets/Art/UI/scematica_logo.png (full, transparent) and
scematica_icon.png (emblem only, 256x256 square).
"""
import os
import sys
from PIL import Image
from extract_sprites import key_background

BASE = os.path.dirname(os.path.abspath(__file__))
UI_DIR = os.path.join(BASE, "..", "Assets", "Art", "UI")
WORDMARK_TOP = 340  # y where the SCEMATICA wordmark band starts in the 500x500 source


def erase_top_blob(keyed, gate=95):
    """Remove the background-smoke region connected to the top border.
    The flood only traverses dim pixels (max channel <= gate) so it stops
    at the bright metal of the emblem instead of eating the top chevron."""
    px = keyed.load()
    w, h = keyed.size

    def dim(x, y):
        r, g, b, a = px[x, y]
        return a != 0 and max(r, g, b) <= gate

    stack = [(x, 0) for x in range(w) if dim(x, 0)]
    seen = set(stack)
    while stack:
        x, y = stack.pop()
        px[x, y] = (0, 0, 0, 0)
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if 0 <= nx < w and 0 <= ny < h and (nx, ny) not in seen and dim(nx, ny):
                seen.add((nx, ny))
                stack.append((nx, ny))
    return len(seen)


def main():
    src = Image.open(sys.argv[1]).convert("RGB")
    keyed = key_background(src, 45)
    print("erased", erase_top_blob(keyed), "blob pixels")

    os.makedirs(UI_DIR, exist_ok=True)
    keyed.crop(keyed.getbbox()).save(os.path.join(UI_DIR, "scematica_logo.png"))

    emblem = keyed.crop((0, 0, keyed.width, WORDMARK_TOP))
    emblem = emblem.crop(emblem.getbbox())
    side = max(emblem.size)
    icon = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    icon.paste(emblem, ((side - emblem.width) // 2, (side - emblem.height) // 2))
    icon.resize((256, 256), Image.LANCZOS).save(os.path.join(UI_DIR, "scematica_icon.png"))

    chk = Image.new("RGBA", (560, 300), (30, 60, 30, 255))
    icon256 = Image.open(os.path.join(UI_DIR, "scematica_icon.png"))
    chk.paste(icon256, (10, 20), icon256)
    logo = Image.open(os.path.join(UI_DIR, "scematica_logo.png"))
    logo.thumbnail((270, 270))
    chk.paste(logo, (280, 20), logo)
    chk.convert("RGB").save(os.path.join(BASE, "zz_logo_final_check.png"))
    print("done")


if __name__ == "__main__":
    main()
