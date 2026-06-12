"""Extract HUD bar sprites and datashard chip icons from the provided sheets.

Outputs:
  Assets/Art/UI/HUD/hud_health_fill.png / hud_awakened_fill.png / hud_xp_fill.png
  Assets/Art/UI/Collectibles/shard_01.png .. shard_10.png
Writes zz_hud_check.png contact sheet for review.
"""
import os
from PIL import Image
from extract_sprites import key_background

BASE = os.path.dirname(os.path.abspath(__file__))
HUD_SHEET = os.path.join(BASE, "..", "sprites", "hud", "ccde245d-ba94-49e6-9db3-6f864d9fd7c6.png")
COLL_SHEET = os.path.join(BASE, "..", "sprites", "collectibles", "8036a999-04e0-4407-8924-1d7b3e9aaff4.png")
HUD_OUT = os.path.join(BASE, "..", "Assets", "Art", "UI", "HUD")
COLL_OUT = os.path.join(BASE, "..", "Assets", "Art", "UI", "Collectibles")

# BARS & METERS rows on the ccde245d sheet: name -> region
BARS = {
    "hud_health_fill":   (582, 38, 754, 58),
    "hud_awakened_fill": (582, 140, 754, 160),  # actual AWAKENING METER row
    "hud_xp_fill":       (582, 208, 754, 228),  # FOCUS row (orange, full) as XP
}

# DATA LOGS & HOLOGRAM CHIPS: 11-cell grid, we use the first 10
CHIP_X0, CHIP_X1 = 8, 790
CHIP_Y0, CHIP_Y1 = 100, 178
CHIP_COUNT = 11


# Player plate with the real Silver portrait (second HUD sheet)
PLATE_SHEET = os.path.join(BASE, "..", "sprites", "hud", "ccde245d-ba94-49e6-9db3-6f864d9fd7c6.png")
PLATE_REGION = (4, 22, 464, 170)
PLATE_FILL = (7, 8, 12, 255)
# plate-local areas to blank: baked bar fills, static numbers, gauge
PLATE_ERASE = [
    (350, 22, 458, 42),    # health numbers
    (150, 42, 455, 61),    # health bar interior
    (350, 61, 458, 80),    # energy numbers
    (150, 80, 455, 99),    # energy bar interior (used for XP)
    (240, 99, 458, 117),   # XP text row + gray strip
    (226, 122, 402, 147),  # awakening gauge + percent
]


def extract_plate(hud_unused, results):
    # HUD panel stays opaque — keying would eat the dim frame linework.
    plate = Image.open(PLATE_SHEET).convert("RGBA").crop(PLATE_REGION)
    px = plate.load()
    for x0, y0, x1, y1 in PLATE_ERASE:
        for x in range(x0, min(x1, plate.width)):
            for y in range(y0, min(y1, plate.height)):
                px[x, y] = PLATE_FILL
    plate.save(os.path.join(HUD_OUT, "hud_player_plate.png"))
    results.append(("hud_player_plate", plate))
    print("hud_player_plate", plate.size)


def main():
    os.makedirs(HUD_OUT, exist_ok=True)
    os.makedirs(COLL_OUT, exist_ok=True)
    results = []

    hud = Image.open(HUD_SHEET).convert("RGB")
    extract_plate(hud, results)
    for name, region in BARS.items():
        keyed = key_background(hud.crop(region), 18)
        bbox = keyed.getbbox()
        if bbox is None:
            print("WARN empty:", name)
            continue
        keyed = keyed.crop(bbox)
        keyed.save(os.path.join(HUD_OUT, name + ".png"))
        results.append((name, keyed))
        print(name, keyed.size)

    coll = Image.open(COLL_SHEET).convert("RGB")
    pitch = (CHIP_X1 - CHIP_X0) / CHIP_COUNT
    for i in range(10):
        x0 = int(CHIP_X0 + i * pitch) + 3
        x1 = int(CHIP_X0 + (i + 1) * pitch) - 3
        keyed = key_background(coll.crop((x0, CHIP_Y0, x1, CHIP_Y1)), 18)
        bbox = keyed.getbbox()
        if bbox is None:
            print(f"WARN empty: chip {i}")
            continue
        keyed = keyed.crop(bbox)
        name = f"shard_{i + 1:02d}"
        keyed.save(os.path.join(COLL_OUT, name + ".png"))
        results.append((name, keyed))
        print(name, keyed.size)

    return  # contact sheet below is scratch-only; skip in automated runs
    sheet = Image.new("RGBA", (1000, 340), (35, 60, 35, 255))
    x = 10
    bar_y = 140
    for name, img in results:
        if name == "hud_player_plate":
            sheet.paste(img, (10, 10), img)
        elif name.startswith("hud_"):
            sheet.paste(img, (500, bar_y), img)
            bar_y += 24
        else:
            sheet.paste(img, (x, 240), img)
            x += img.width + 12
    sheet.convert("RGB").save(os.path.join(BASE, "zz_hud_check.png"))
    print("contact sheet -> zz_hud_check.png")


if __name__ == "__main__":
    main()
