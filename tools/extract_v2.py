"""Character-sheet extractor v2.

Fixes the v1 quality issues:
- enclosed transparent holes inside bodies are restored (hole filling)
- halo erosion is one gentle pass, not three aggressive ones
- bleed-over label text and panel border lines are stripped per frame
- every frame of an animation shares one canvas (full region height,
  ink-centroid centered) so character size and feet stay consistent
  across idle/walk/attacks instead of jumping with each tight crop.

Usage:
  python extract_v2.py preview <config.json>   # report frame counts only
  python extract_v2.py extract <config.json>   # write frames

Config format matches extract_sprites.py (sheets -> animations -> regions).
"""
import json
import os
import sys
from collections import deque

import numpy as np
from PIL import Image


def scale2x(rgba):
    """Classic Scale2x: doubles size, preserving hard pixel-art edges while
    smoothing diagonals. Operates on an RGBA uint8 array."""
    h, w, _ = rgba.shape
    src = rgba.astype(np.int32)
    # neighbors with edge clamping
    b = np.vstack([src[:1], src[:-1]])      # up
    hh = np.vstack([src[1:], src[-1:]])     # down
    d = np.hstack([src[:, :1], src[:, :-1]])  # left
    f = np.hstack([src[:, 1:], src[:, -1:]])  # right

    def eq(p, q):
        return (p == q).all(axis=2)

    bd, bf, dh, hf = eq(b, d), eq(b, f), eq(d, hh), eq(hh, f)
    bh, df = eq(b, hh), eq(d, f)

    e0 = np.where((bd & ~bh & ~df)[..., None], d, src)
    e1 = np.where((bf & ~bh & ~df)[..., None], f, src)
    e2 = np.where((dh & ~bh & ~df)[..., None], d, src)
    e3 = np.where((hf & ~bh & ~df)[..., None], f, src)

    out = np.zeros((h * 2, w * 2, 4), dtype=np.int32)
    out[0::2, 0::2] = e0
    out[0::2, 1::2] = e1
    out[1::2, 0::2] = e2
    out[1::2, 1::2] = e3
    return out.astype(np.uint8)


def split_frames(ink_cols, min_ink, gap_cols, min_width):
    frames = []
    in_frame, start, gap = False, 0, 0
    for x, has in enumerate(ink_cols >= min_ink):
        if has:
            if not in_frame:
                in_frame, start = True, x
            gap = 0
        elif in_frame:
            gap += 1
            if gap >= gap_cols:
                end = x - gap + 1
                if end - start >= min_width:
                    frames.append((start, end))
                in_frame, gap = False, 0
    if in_frame and len(ink_cols) - start >= min_width:
        frames.append((start, len(ink_cols)))
    return frames


def flood_mask(dark, seeds):
    """BFS over True cells of `dark` starting from seed coords."""
    h, w = dark.shape
    mask = np.zeros_like(dark)
    queue = deque()
    for y, x in seeds:
        if dark[y, x] and not mask[y, x]:
            mask[y, x] = True
            queue.append((y, x))
    while queue:
        y, x = queue.popleft()
        for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
            if 0 <= ny < h and 0 <= nx < w and dark[ny, nx] and not mask[ny, nx]:
                mask[ny, nx] = True
                queue.append((ny, nx))
    return mask


def border_seeds(h, w):
    return ([(0, x) for x in range(w)] + [(h - 1, x) for x in range(w)]
            + [(y, 0) for y in range(h)] + [(y, w - 1) for y in range(h)])


def key_frame(rgb, threshold):
    """Background keying with hole restoration. Returns alpha mask.

    Chroma-aware: the sheet background is NEUTRAL near-black while sprite
    shadows/dark armor carry tint (red glows, blue steel). Only achromatic
    dark pixels can join the background flood, so dark bodies that touch
    the backdrop no longer get eaten."""
    lum = rgb.max(axis=2).astype(np.int16)
    chroma = lum - rgb.min(axis=2).astype(np.int16)
    h, w = lum.shape
    neutral = chroma <= max(10, threshold // 2 + 5)

    # 1. background = neutral dark pixels connected to the frame border
    bg = flood_mask((lum <= threshold + 8) & neutral, border_seeds(h, w))

    # 2. one gentle halo pass: dim neutral pixels directly touching background
    halo = (lum <= threshold + 22) & neutral
    touching = np.zeros_like(bg)
    touching[1:, :] |= bg[:-1, :]
    touching[:-1, :] |= bg[1:, :]
    touching[:, 1:] |= bg[:, :-1]
    touching[:, :-1] |= bg[:, 1:]
    bg |= halo & touching

    # 3. fill holes: transparent regions NOT connected to the border are
    #    interior (dark armor, shadows) — restore them. Big enclosed areas
    #    are arc/effect interiors (background ringed by a sword trail), not
    #    body shadow: leave those transparent or they render as black slabs.
    outside = flood_mask(bg, [s for s in border_seeds(h, w) if bg[s[0], s[1]]])
    holes = bg & ~outside
    if holes.any():
        max_hole = max(120, int(0.06 * h * w))
        seen = np.zeros_like(holes)
        for sy, sx in zip(*np.where(holes)):
            if seen[sy, sx]:
                continue
            comp = flood_mask(holes & ~seen, [(sy, sx)])
            seen |= comp
            if comp.sum() > max_hole:
                holes &= ~comp  # arc interior, not body shadow
    alpha = ~bg | holes

    # 4. morphological closing: the source art draws near-black connective
    #    joints (grey mech undersuits) that no luminance key can keep —
    #    bridge small gaps and restore the original dark pixels there so
    #    limbs stay attached to bodies.
    def shift_or(m):
        out = m.copy()
        out[1:, :] |= m[:-1, :]
        out[:-1, :] |= m[1:, :]
        out[:, 1:] |= m[:, :-1]
        out[:, :-1] |= m[:, 1:]
        return out

    def shift_and(m):
        out = m.copy()
        out[1:, :] &= m[:-1, :]
        out[:-1, :] &= m[1:, :]
        out[:, 1:] &= m[:, :-1]
        out[:, :-1] &= m[:, 1:]
        return out

    grown = shift_or(shift_or(alpha))
    closed = shift_and(shift_and(grown))
    return alpha | closed


def components(alpha):
    h, w = alpha.shape
    seen = np.zeros_like(alpha)
    for sy in range(h):
        for sx in range(w):
            if not alpha[sy, sx] or seen[sy, sx]:
                continue
            queue = deque([(sy, sx)])
            seen[sy, sx] = True
            cells = []
            while queue:
                y, x = queue.popleft()
                cells.append((y, x))
                for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
                    if 0 <= ny < h and 0 <= nx < w and alpha[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        queue.append((ny, nx))
            yield cells


def clean_frame(alpha, min_island=14):
    """Remove debris: tiny islands, caption text floating above the figure,
    and full-height border slivers at the frame edges."""
    h, w = alpha.shape
    comps = list(components(alpha))
    if not comps:
        return
    main = max(comps, key=len)
    main_top = min(c[0] for c in main)

    for cells in comps:
        if cells is main:
            continue
        ys = [c[0] for c in cells]
        xs = [c[1] for c in cells]
        ch, cw = max(ys) - min(ys) + 1, max(xs) - min(xs) + 1
        kill = False
        if len(cells) < min_island:
            kill = True
        elif max(ys) < main_top:  # caption digits/text: entirely above the figure
            kill = True
        elif ch <= 14 and cw >= 2.2 * ch and len(cells) < 420 and min(ys) < h * 0.4:
            kill = True  # flat text-shaped strip in the upper region (row caption)
        elif (len(cells) < 80 and (min(ys) + max(ys)) / 2 < main_top
              and (cw >= 2.0 * ch or len(cells) < 30)):  # text-shaped or dust above; heads survive
            kill = True
        elif min(ys) < 12 and ch < 14 and cw >= 2.4 * ch:  # wide caption text
            kill = True
        elif cw <= 3 and ch > 0.6 * h and (min(xs) <= 1 or max(xs) >= w - 2):  # border line
            kill = True
        if kill:
            for y, x in cells:
                alpha[y, x] = False


def has_renderable_art(alpha):
    """Reject auto-split false positives: row labels, slivers, and dust."""
    comps = list(components(alpha))
    if not comps:
        return False

    main = max(comps, key=len)
    ys = [c[0] for c in main]
    xs = [c[1] for c in main]
    main_h = max(ys) - min(ys) + 1
    main_w = max(xs) - min(xs) + 1
    area = len(main)
    h, w = alpha.shape

    if area <= max(28, int(h * w * 0.0035)):
        return False

    # Caption fragments are short, flat glyph islands. Keep large slash beams
    # and teleport streaks even when they are thin.
    if main_h < max(9, int(h * 0.14)) and area <= max(96, int(h * w * 0.01)):
        return False
    if main_h <= 3 and main_w <= 0.35 * w:
        return False

    return True


def process_sheet(sheet, mode, base_dir):
    img = Image.open(os.path.join(base_dir, sheet["image"])).convert("RGB")
    threshold = sheet.get("bg_threshold", 38)
    min_ink = sheet.get("min_ink_per_column", 2)
    gap_cols = sheet.get("gap_columns", 3)
    min_width = sheet.get("min_frame_width", 12)
    key_thr = sheet.get("key_threshold", threshold)

    # "upscale": 2 or 4 -> Scale2x passes after keying. For tiny source art
    # (the extended-unit sheet) this reconstructs clean diagonals instead of
    # blocky nearest-neighbor stairs when frames get blown up to game size.
    upscale_passes = {1: 0, 2: 1, 4: 2}.get(int(sheet.get("upscale", 1)), 0)

    split_thr = sheet.get("split_threshold", threshold)
    for anim in sheet["animations"]:
        x0, y0, x1, y1 = anim["region"]
        region = np.asarray(img.crop((x0, y0, x1, y1)), dtype=np.uint8)
        lum = region.max(axis=2)
        ink_cols = (lum > split_thr).sum(axis=0)
        if "force_frames" in anim:
            # contiguous effects defeat gap splitting: cut an even grid
            n = anim["force_frames"]
            w = region.shape[1]
            frames = [(w * i // n, w * (i + 1) // n) for i in range(n)]
        else:
            frames = split_frames(ink_cols, min_ink, gap_cols, min_width)
        print(f"  {anim['name']}: {len(frames)} frames")
        if mode != "extract" or not frames:
            continue

        height = region.shape[0]
        # uniform canvas: widest frame decides; everyone gets full height
        widths = [xb - xa for xa, xb in frames]
        canvas_w = max(widths) + 4

        out_dir = os.path.join(base_dir, sheet["out_dir"], anim["name"])
        os.makedirs(out_dir, exist_ok=True)
        for old in os.listdir(out_dir):
            if old.endswith(".png") or old.endswith(".png.meta"):
                os.remove(os.path.join(out_dir, old))

        for i, (xa, xb) in enumerate(frames):
            cell = region[:, xa:xb]
            alpha = key_frame(cell, key_thr)
            clean_frame(alpha)
            if not has_renderable_art(alpha):
                continue
            # center the ink centroid on the shared canvas
            cx = int(round(np.argwhere(alpha)[:, 1].mean()))
            canvas = np.zeros((height, canvas_w, 4), dtype=np.uint8)
            ox = canvas_w // 2 - cx
            sx0, sx1 = max(0, -ox), min(cell.shape[1], canvas_w - ox)
            dx0 = sx0 + ox
            canvas[:, dx0:dx0 + (sx1 - sx0), :3] = cell[:, sx0:sx1]
            canvas[:, dx0:dx0 + (sx1 - sx0), 3] = alpha[:, sx0:sx1] * 255
            for _ in range(upscale_passes):
                canvas = scale2x(canvas)
            Image.fromarray(canvas).save(os.path.join(out_dir, f"{anim['name']}_{i:02d}.png"))


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
        process_sheet(sheet, mode, base_dir)


if __name__ == "__main__":
    main()
