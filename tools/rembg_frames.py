"""Re-crop frames from sheets and remove backgrounds with rembg (U2Net).

Usage: python rembg_frames.py <config.json> [anim_filter...]
Writes RGBA frames at 4x nearest-neighbor scale into out_dir/<anim>/.
"""
import json
import os
import sys
from PIL import Image
from rembg import remove, new_session
from extract_sprites import luminance_mask, split_frames, vertical_bounds

SCALE = 4


def main():
    config_path = sys.argv[1]
    only = set(sys.argv[2:])
    base_dir = os.path.dirname(os.path.abspath(config_path))
    with open(config_path) as f:
        config = json.load(f)

    session = new_session("u2net")

    for sheet in config["sheets"]:
        img = Image.open(os.path.join(base_dir, sheet["image"]))
        threshold = sheet.get("bg_threshold", 38)
        min_ink = sheet.get("min_ink_per_column", 2)
        gap_cols = sheet.get("gap_columns", 3)
        min_width = sheet.get("min_frame_width", 12)

        for anim in sheet["animations"]:
            if only and anim["name"] not in only:
                continue
            crop, ink, w, h = luminance_mask(img, anim["region"], threshold)
            frames = split_frames(ink, w, h, min_ink, gap_cols, min_width)
            out_dir = os.path.join(base_dir, sheet["out_dir"], anim["name"])
            os.makedirs(out_dir, exist_ok=True)

            for i, (xa, xb) in enumerate(frames):
                ya, yb = vertical_bounds(ink, h, (xa, xb))
                pad = 3
                frame = crop.crop((max(0, xa - pad), max(0, ya - pad),
                                   min(w, xb + pad), min(h, yb + pad)))
                big = frame.resize((frame.width * SCALE, frame.height * SCALE), Image.NEAREST)
                keyed = remove(big, session=session)
                keyed.save(os.path.join(out_dir, f"{anim['name']}_{i:02d}.png"))
            print(f"{anim['name']}: {len(frames)} frames -> {out_dir}")


if __name__ == "__main__":
    main()
