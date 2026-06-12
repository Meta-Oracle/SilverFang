"""Audit extracted animation frames for mis-sliced junk.

Flags frames whose size is wildly off from their animation's median:
slivers (auto-split caught a gap wrong), merged frames (two figures in
one), or near-empty frames. Run with --delete to remove flagged frames
(and their .meta files).
"""
import os
import sys
from PIL import Image

BASE = os.path.dirname(os.path.abspath(__file__))
ROOTS = [
    os.path.join(BASE, "..", "Assets", "Art", "Sprites", d)
    for d in ("Silver", "Enemies", "Awakened", "VFX")
]


def median(values):
    s = sorted(values)
    return s[len(s) // 2]


def opaque_ratio(path):
    im = Image.open(path).convert("RGBA")
    alpha = im.getchannel("A")
    hist = alpha.histogram()
    opaque = sum(hist[16:])
    return opaque / (im.width * im.height)


def components(alpha):
    w, h = alpha.size
    px = alpha.load()
    seen = [[False] * h for _ in range(w)]
    for sx in range(w):
        for sy in range(h):
            if seen[sx][sy] or px[sx, sy] <= 16:
                continue
            stack = [(sx, sy)]
            seen[sx][sy] = True
            cells = []
            while stack:
                x, y = stack.pop()
                cells.append((x, y))
                for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                    if 0 <= nx < w and 0 <= ny < h and not seen[nx][ny] and px[nx, ny] > 16:
                        seen[nx][ny] = True
                        stack.append((nx, ny))
            yield cells


def looks_like_debris(path):
    im = Image.open(path).convert("RGBA")
    alpha = im.getchannel("A")
    comps = list(components(alpha))
    if not comps:
        return True

    main = max(comps, key=len)
    xs = [c[0] for c in main]
    ys = [c[1] for c in main]
    main_w = max(xs) - min(xs) + 1
    main_h = max(ys) - min(ys) + 1
    area = len(main)
    w, h = im.size

    if area <= 24:
        return True
    if main_h < 5 and area <= 80:
        return True
    if main_h <= 3 and main_w <= 0.35 * w:
        return True
    return False


def main():
    delete = "--delete" in sys.argv
    flagged = []

    for root in ROOTS:
        if not os.path.isdir(root):
            continue
        for dirpath, _, filenames in os.walk(root):
            frames = sorted(f for f in filenames if f.endswith(".png"))
            if len(frames) < 2:
                continue
            sizes = {}
            for f in frames:
                with Image.open(os.path.join(dirpath, f)) as im:
                    sizes[f] = im.size
            med_w = median([s[0] for s in sizes.values()])
            med_h = median([s[1] for s in sizes.values()])

            for f in frames:
                w, h = sizes[f]
                path = os.path.join(dirpath, f)
                reasons = []
                if h < 0.55 * med_h:
                    reasons.append(f"short h={h} (median {med_h})")
                if w < 8:
                    reasons.append(f"sliver w={w}")
                elif w < 0.4 * med_w:
                    reasons.append(f"narrow w={w} (median {med_w})")
                elif w > 2.4 * med_w:
                    reasons.append(f"merged? w={w} (median {med_w})")
                if not reasons and opaque_ratio(path) < 0.02 and looks_like_debris(path):
                    reasons.append("near-empty")
                if reasons:
                    rel = os.path.relpath(path, os.path.join(BASE, ".."))
                    flagged.append((rel, ", ".join(reasons)))

    if not flagged:
        print("No faulty frames found.")
        return

    for rel, why in flagged:
        print(f"{rel}: {why}")
    print(f"\n{len(flagged)} flagged.")

    if delete:
        for rel, _ in flagged:
            path = os.path.join(BASE, "..", rel)
            os.remove(path)
            meta = path + ".meta"
            if os.path.exists(meta):
                os.remove(meta)
        print("Deleted flagged frames (+ .meta).")


if __name__ == "__main__":
    main()
