"""Normalize extracted sprite frames so the character keeps one size and one
baseline across EVERY animation folder of a character root.

Two passes per root:
1. Size: each folder's character height is estimated as the median alpha-bbox
   height of its frames (median resists effect-outlier frames). Folders whose
   estimate differs from the reference folder (idle by default) are uniformly
   rescaled - art and canvas together - so the painted character matches size.
2. Canvas: all frames are padded onto one shared canvas (center-x, bottom-y of
   the ORIGINAL canvas, not the per-frame bbox) so in-folder placement is
   preserved exactly and cross-folder swaps neither resize nor shift the body.

Usage:
  python normalize_sprite_canvases.py Assets/Art/Sprites/Silver [--ref idle] [--clamp 0.7 1.6]
"""
import argparse
import statistics
from pathlib import Path

from PIL import Image


def alpha_bbox(im):
    return im.getchannel("A").getbbox()


def folder_frames(folder):
    return sorted(p for p in folder.glob("*.png"))


def char_height(folder):
    heights = []
    for path in folder_frames(folder):
        with Image.open(path).convert("RGBA") as im:
            bbox = alpha_bbox(im)
            if bbox:
                heights.append(bbox[3] - bbox[1])
    if not heights:
        return 0
    # arcs/effect trails inflate most attack frames; the lower quartile is
    # the bare-body windup/recovery height, keeping the BODY scale uniform
    # between idle and attack folders
    heights.sort()
    return heights[max(0, (len(heights) - 1) // 4)]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("root")
    parser.add_argument("--ref", default="idle", help="reference folder for character height")
    parser.add_argument("--clamp", nargs=2, type=float, default=(0.7, 1.6),
                        help="min/max rescale factor")
    parser.add_argument("--target-height", type=float, default=0,
                        help="absolute character pixel height (e.g. Silver's idle height) "
                             "so different roots stay the same size in game")
    args = parser.parse_args()

    root = Path(args.root)
    folders = [d for d in sorted(root.iterdir()) if d.is_dir() and folder_frames(d)]
    if not folders:
        print(f"{root}: no frame folders")
        return

    if args.target_height > 0:
        ref_h = args.target_height
    else:
        ref_dir = root / args.ref
        ref_h = char_height(ref_dir) if ref_dir.is_dir() else 0
        if not ref_h:
            ref_h = statistics.median(char_height(d) for d in folders)
            print(f"{root}: no '{args.ref}' reference, using median height {ref_h:.0f}")

    # Pass 1: per-folder uniform rescale toward the reference character height.
    rescaled = 0
    for folder in folders:
        h = char_height(folder)
        if not h:
            continue
        factor = ref_h / h
        factor = max(args.clamp[0], min(args.clamp[1], factor))
        if abs(factor - 1.0) < 0.08:
            continue  # close enough; avoid needless resampling
        for path in folder_frames(folder):
            with Image.open(path).convert("RGBA") as im:
                new_size = (max(1, round(im.width * factor)), max(1, round(im.height * factor)))
                # big upscales (tiny source art) stay crisp with nearest
                resample = Image.NEAREST if (factor >= 1.4 and im.height < 60) else Image.LANCZOS
                im.resize(new_size, resample).save(path)
        rescaled += 1
        print(f"  {folder.name}: char {h:.0f}px -> x{factor:.2f}")

    # Pass 2: per-folder shared canvas = union of the folder's art bboxes
    # (small pad). Every frame keeps its canvas-relative placement, all frames
    # of a folder share one canvas, and dead transparent borders are trimmed
    # so textures stay small. BottomCenter pivots line up because each frame
    # is first centered/bottom-aligned onto the folder's common canvas.
    PAD = 4
    padded = 0
    for folder in folders:
        paths = folder_frames(folder)
        ims = [Image.open(p).convert("RGBA") for p in paths]
        max_w = max(im.width for im in ims)
        max_h = max(im.height for im in ims)

        union = None
        placed = []
        for im in ims:
            dx, dy = (max_w - im.width) // 2, max_h - im.height
            placed.append((im, dx, dy))
            bbox = alpha_bbox(im)
            if bbox:
                box = (bbox[0] + dx, bbox[1] + dy, bbox[2] + dx, bbox[3] + dy)
                union = box if union is None else (
                    min(union[0], box[0]), min(union[1], box[1]),
                    max(union[2], box[2]), max(union[3], box[3]))
        if union is None:
            continue
        x0 = max(0, union[0] - PAD)
        y0 = max(0, union[1] - PAD)
        x1 = min(max_w, union[2] + PAD)
        y1 = min(max_h, union[3] + PAD)

        for path, (im, dx, dy) in zip(paths, placed):
            out = Image.new("RGBA", (max_w, max_h), (0, 0, 0, 0))
            out.alpha_composite(im, (dx, dy))
            out = out.crop((x0, y0, x1, y1))
            out.save(path)
            padded += 1

    print(f"{root}: rescaled {rescaled} folders, re-canvased {padded} frames")


if __name__ == "__main__":
    main()
