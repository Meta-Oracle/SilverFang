"""Extract animation frames from concept sprite sheets.

Usage:
  python extract_sprites.py preview <config.json>   # draw detected frames onto preview images
  python extract_sprites.py extract <config.json>   # write keyed PNG frames per animation

Config format:
{
  "sheets": [
    {
      "image": "../sprites/sheet.jpg",
      "preview_out": "preview_sheet.png",
      "out_dir": "../Assets/Art/Sprites/Silver",
      "bg_threshold": 38,
      "min_ink_per_column": 2,
      "gap_columns": 3,
      "min_frame_width": 12,
      "animations": [
        { "name": "idle", "region": [x0, y0, x1, y1] }
      ]
    }
  ]
}
"""
import json
import os
import sys
from PIL import Image, ImageDraw


def luminance_mask(img, region, threshold):
    """Return set of 'ink' columns/rows info: per-pixel test inside region."""
    x0, y0, x1, y1 = region
    crop = img.crop((x0, y0, x1, y1)).convert("RGB")
    w, h = crop.size
    px = crop.load()
    ink = [[False] * h for _ in range(w)]
    for x in range(w):
        for y in range(h):
            r, g, b = px[x, y]
            if max(r, g, b) > threshold:
                ink[x][y] = True
    return crop, ink, w, h


def split_frames(ink, w, h, min_ink, gap_cols, min_width):
    """Split region into frame x-ranges using column ink projection."""
    col_ink = [sum(1 for y in range(h) if ink[x][y]) for x in range(w)]
    frames = []
    in_frame = False
    start = 0
    gap = 0
    for x in range(w):
        has_ink = col_ink[x] >= min_ink
        if has_ink:
            if not in_frame:
                in_frame = True
                start = x
            gap = 0
        elif in_frame:
            gap += 1
            if gap >= gap_cols:
                end = x - gap + 1
                if end - start >= min_width:
                    frames.append((start, end))
                in_frame = False
                gap = 0
    if in_frame:
        end = w
        if end - start >= min_width:
            frames.append((start, end))
    return frames


def vertical_bounds(ink, h, x_range, min_ink=1):
    xa, xb = x_range
    ys = [y for y in range(h) if sum(1 for x in range(xa, xb) if ink[x][y]) >= min_ink]
    if not ys:
        return 0, h
    return min(ys), max(ys) + 1


def key_background(frame_img, threshold):
    """Flood-fill from the border: only dark pixels CONNECTED to the edge are
    background. Dark pixels inside the character (clothing, shadow) survive."""
    rgba = frame_img.convert("RGBA")
    px = rgba.load()
    w, h = rgba.size
    flood_threshold = threshold + 10

    def is_dark(x, y):
        r, g, b, _ = px[x, y]
        return max(r, g, b) <= flood_threshold

    bg = [[False] * h for _ in range(w)]
    stack = []
    for x in range(w):
        for y in (0, h - 1):
            if is_dark(x, y) and not bg[x][y]:
                bg[x][y] = True
                stack.append((x, y))
    for y in range(h):
        for x in (0, w - 1):
            if is_dark(x, y) and not bg[x][y]:
                bg[x][y] = True
                stack.append((x, y))

    while stack:
        x, y = stack.pop()
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if 0 <= nx < w and 0 <= ny < h and not bg[nx][ny] and is_dark(nx, ny):
                bg[nx][ny] = True
                stack.append((nx, ny))

    # Halo erosion: nibble dark-ish pixels that touch background, a few layers deep.
    # Bounded iterations so it cannot hollow out the character interior.
    halo_threshold = threshold + 38
    for _ in range(3):
        adds = []
        for x in range(w):
            for y in range(h):
                if bg[x][y]:
                    continue
                r, g, b, _ = px[x, y]
                if max(r, g, b) > halo_threshold:
                    continue
                for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                    if 0 <= nx < w and 0 <= ny < h and bg[nx][ny]:
                        adds.append((x, y))
                        break
        for x, y in adds:
            bg[x][y] = True

    for x in range(w):
        for y in range(h):
            if bg[x][y]:
                r, g, b, _ = px[x, y]
                px[x, y] = (r, g, b, 0)

    despeckle(px, w, h, min_island=20)
    return rgba


def despeckle(px, w, h, min_island):
    """Remove small disconnected opaque islands (leftover bg noise)."""
    seen = [[False] * h for _ in range(w)]
    for sx in range(w):
        for sy in range(h):
            if seen[sx][sy] or px[sx, sy][3] == 0:
                continue
            island = [(sx, sy)]
            seen[sx][sy] = True
            stack = [(sx, sy)]
            while stack:
                x, y = stack.pop()
                for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                    if 0 <= nx < w and 0 <= ny < h and not seen[nx][ny] and px[nx, ny][3] != 0:
                        seen[nx][ny] = True
                        island.append((nx, ny))
                        stack.append((nx, ny))
            if len(island) < min_island:
                for x, y in island:
                    r, g, b, _ = px[x, y]
                    px[x, y] = (r, g, b, 0)


def process_sheet(sheet, mode, base_dir):
    img_path = os.path.join(base_dir, sheet["image"])
    img = Image.open(img_path)
    threshold = sheet.get("bg_threshold", 38)
    min_ink = sheet.get("min_ink_per_column", 2)
    gap_cols = sheet.get("gap_columns", 3)
    min_width = sheet.get("min_frame_width", 12)

    preview = img.convert("RGB").copy() if mode == "preview" else None
    draw = ImageDraw.Draw(preview) if preview else None

    report = []
    for anim in sheet["animations"]:
        region = anim["region"]
        crop, ink, w, h = luminance_mask(img, region, threshold)
        frames = split_frames(ink, w, h, min_ink, gap_cols, min_width)
        report.append(f"{anim['name']}: {len(frames)} frames")

        if mode == "preview":
            x0, y0 = region[0], region[1]
            draw.rectangle(region, outline=(0, 255, 255), width=2)
            for (xa, xb) in frames:
                ya, yb = vertical_bounds(ink, h, (xa, xb))
                draw.rectangle([x0 + xa, y0 + ya, x0 + xb, y0 + yb], outline=(255, 0, 255), width=1)
        else:
            out_dir = os.path.join(base_dir, sheet["out_dir"], anim["name"])
            os.makedirs(out_dir, exist_ok=True)
            for i, (xa, xb) in enumerate(frames):
                ya, yb = vertical_bounds(ink, h, (xa, xb))
                pad = 2
                fx0 = max(0, xa - pad)
                fy0 = max(0, ya - pad)
                fx1 = min(w, xb + pad)
                fy1 = min(h, yb + pad)
                frame = crop.crop((fx0, fy0, fx1, fy1))
                keyed = key_background(frame, sheet.get("key_threshold", threshold))
                keyed.save(os.path.join(out_dir, f"{anim['name']}_{i:02d}.png"))

    if mode == "preview":
        out = os.path.join(base_dir, sheet.get("preview_out", "preview.png"))
        preview.save(out)
        report.append(f"preview -> {out}")
    return report


def main():
    if len(sys.argv) < 3 or sys.argv[1] not in ("preview", "extract"):
        print(__doc__)
        sys.exit(1)
    mode, config_path = sys.argv[1], sys.argv[2]
    base_dir = os.path.dirname(os.path.abspath(config_path))
    with open(config_path) as f:
        config = json.load(f)
    for sheet in config["sheets"]:
        print(f"== {sheet['image']} ==")
        for line in process_sheet(sheet, mode, base_dir):
            print("  " + line)


if __name__ == "__main__":
    main()
