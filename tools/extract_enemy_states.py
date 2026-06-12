"""Extract per-enemy status-state sprites from the 'STATUS EFFECT STATES -
ALL ENEMY TYPES' sheet (13-column grid per enemy section: NORMAL, FROZEN,
BURNING, RADIATED, ...).

Writes single-frame VFX dirs the VfxBaker auto-discovers:
  Assets/Art/Sprites/VFX/status_<status>_<enemy>/status_<status>_<enemy>_00.png
Also writes zz_enemy_states_check.png contact sheet for visual review.
"""
import os
from PIL import Image
from extract_sprites import key_background

BASE = os.path.dirname(os.path.abspath(__file__))
SHEET = os.path.join(BASE, "..", "sprites", "vfx", "bb1c7e9f-d904-42a1-82f4-6b905901db6e.png")
OUT_ROOT = os.path.join(BASE, "..", "Assets", "Art", "Sprites", "VFX")

COLUMNS = 13
STATUSES = {1: "frozen", 2: "burning", 3: "radiated"}  # grid column -> status id
CELL_INSET = 5  # stay inside any cell border lines

# enemy -> (half x0, y_top, y_bottom) of the figure band, excluding labels/titles
SECTIONS = {
    "reaper":   (0,   122, 246),
    "samurai":  (768, 122, 246),
    "werewolf": (0,   292, 404),
    "chimera":  (768, 294, 371),
    "sentinel": (0,   462, 565),
}

KEY_THRESHOLD = 14  # gentle: keeps the dim radiated glow intact
LABEL_BAND = 18     # column labels live in the top rows of each cell
LABEL_MAX_HEIGHT = 16


def strip_labels(keyed):
    """Erase small components that start in the cell's top label band —
    leftover column text. The figure survives because it is one tall
    component even when its head reaches the top rows."""
    px = keyed.load()
    w, h = keyed.size
    seen = [[False] * h for _ in range(w)]
    for sx in range(w):
        for sy in range(min(LABEL_BAND, h)):
            if seen[sx][sy] or px[sx, sy][3] == 0:
                continue
            island = [(sx, sy)]
            seen[sx][sy] = True
            stack = [(sx, sy)]
            min_y, max_y = sy, sy
            min_x, max_x = sx, sx
            while stack:
                x, y = stack.pop()
                min_y = min(min_y, y)
                max_y = max(max_y, y)
                min_x = min(min_x, x)
                max_x = max(max_x, x)
                for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                    if 0 <= nx < w and 0 <= ny < h and not seen[nx][ny] and px[nx, ny][3] != 0:
                        seen[nx][ny] = True
                        island.append((nx, ny))
                        stack.append((nx, ny))
            height = max_y - min_y + 1
            width = max_x - min_x + 1
            if height >= LABEL_MAX_HEIGHT:
                continue
            # label text is wide and short; figure fragments (ears, heads) aren't
            if width >= 2.2 * height:
                for x, y in island:
                    px[x, y] = (0, 0, 0, 0)


def main():
    sheet = Image.open(SHEET).convert("RGB")
    pitch = 768.0 / COLUMNS

    cells = []
    for enemy, (x0, y0, y1) in SECTIONS.items():
        for col, status in STATUSES.items():
            cx0 = int(x0 + col * pitch) + CELL_INSET
            cx1 = int(x0 + (col + 1) * pitch) - CELL_INSET
            cell = sheet.crop((cx0, y0, cx1, y1))
            keyed = key_background(cell, KEY_THRESHOLD)
            bbox = keyed.getbbox()
            if bbox is None:
                print(f"WARN: empty cell {enemy}/{status}")
                continue
            keyed = keyed.crop(bbox)

            name = f"status_{status}_{enemy}"
            out_dir = os.path.join(OUT_ROOT, name)
            os.makedirs(out_dir, exist_ok=True)
            keyed.save(os.path.join(out_dir, f"{name}_00.png"))
            cells.append((enemy, status, keyed))
            print(f"{name}: {keyed.size}")

    # contact sheet: rows = enemies, cols = statuses, on contrast background
    cw, ch = 130, 170
    sheet_img = Image.new("RGBA", (3 * cw, len(SECTIONS) * ch), (35, 60, 35, 255))
    enemies = list(SECTIONS.keys())
    for enemy, status, img in cells:
        row = enemies.index(enemy)
        col = list(STATUSES.values()).index(status)
        x = col * cw + (cw - img.width) // 2
        y = row * ch + (ch - img.height) // 2
        sheet_img.paste(img, (max(0, x), max(0, y)), img)
    sheet_img.convert("RGB").save(os.path.join(BASE, "zz_enemy_states_check.png"))
    print("contact sheet -> zz_enemy_states_check.png")


if __name__ == "__main__":
    main()
