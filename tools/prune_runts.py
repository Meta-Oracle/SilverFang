"""Drop runt frames: cells whose INK mass or ink height is far below the
folder median (leading slivers, split debris). Run after extraction."""
import os, sys
import numpy as np
from PIL import Image

ROOTS = ["Silver", "Hilo", "Awakened"] + [f"Enemies/{e}" for e in
         ["Werewolf", "Chimera", "Reaper", "Sentinel", "Samurai", "SamuraiDual",
          "Guard", "Bruiser", "Ranger", "Drone", "Titan", "Shinobi"]]

def main():
    dropped = 0
    for root in ROOTS:
        base = os.path.join("Assets", "Art", "Sprites", root)
        if not os.path.isdir(base):
            continue
        for d in sorted(os.listdir(base)):
            p = os.path.join(base, d)
            if not os.path.isdir(p):
                continue
            fs = sorted(f for f in os.listdir(p) if f.endswith(".png"))
            if len(fs) < 3:
                continue
            stats = {}
            for f in fs:
                a = np.asarray(Image.open(os.path.join(p, f)).convert("RGBA"))[:, :, 3] > 60
                ys = np.where(a.any(axis=1))[0]
                stats[f] = (int(a.sum()), int(ys[-1] - ys[0] + 1) if len(ys) else 0)
            masses = sorted(v[0] for v in stats.values())
            heights = sorted(v[1] for v in stats.values())
            med_m, med_h = masses[len(masses) // 2], heights[len(heights) // 2]
            for f, (m, h) in stats.items():
                if m < med_m * 0.40 and h < med_h * 0.62:
                    os.remove(os.path.join(p, f))
                    meta = os.path.join(p, f + ".meta")
                    if os.path.exists(meta):
                        os.remove(meta)
                    print(f"runt: {root}/{d}/{f} mass={m}/{med_m} h={h}/{med_h}")
                    dropped += 1
    print(dropped, "runt frames dropped")

if __name__ == "__main__":
    main()
