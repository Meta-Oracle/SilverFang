"""Build enemies_v2_config.json (extract_v2 format) from the enemy sheets.

Strategy: detect horizontal content bands per sheet column-span, then split
each band into exactly the panels my survey says it contains by cutting at
the N-1 widest column gaps ("atoms merged at largest gaps"). Names come from
the per-band spec lists below; "_" skips a panel, a band spec of None skips
the whole band. The extended-units sheet (enemyextraframes.png) nests a
second band pass inside each unit column to slice its stacked mini-rows.

Run:  python gen_enemies_v2_config.py        # writes enemies_v2_config.json
"""
import json
import os

import numpy as np
from PIL import Image

BASE = os.path.dirname(os.path.abspath(__file__))

INK = 39
ROW_GAP = 5
MIN_BAND_H = 30

# sheet -> list of (column span, [band specs]) where each band spec is a list
# of panel names (left to right), "_" = skip panel, None = skip band.
CS_SPECS = [
    ["idle", "walk", "dash"],
    ["run"],
    ["jump_fall", "land", "crouch"],
    ["block_parry"],
    ["slash1", "slash2", "thrust", "rising_slash"],
    ["combo1", "combo2", "combo3", "spin_blade"],
    ["energy_wave", "teleport_slash", "pulse_slash", "dragon_cut"],
    ["dimension_cut", "blade_storm", "void_slash"],
    ["light_hit", "heavy_hit", "knocked_down", "get_up"],
    ["death1", "death2", "death3"],
    ["wall_cling", "wall_run", "wall_jump", "execution"],
]

SHEETS = [
    ("6f4d4156-f6a8-43c9-b36f-8cb64fad3e6b.png", "Werewolf", [
        ((8, 1224), [
            ["idle", "walk", "run", "dash"],
            ["jump", "land", "pounce", "crouch"],
            ["claw_slash", "double_claw", "bite"],
            ["cyber_rage", "shred_burst", "spine_blade", "howl"],
            ["light_hit", "heavy_hit", "knockdown", "getup"],
            ["death", "transform"],
        ]),
    ]),
    ("73345569-704b-47f7-bf32-e996870b23b4.png", "Chimera", [
        ((8, 1224), [
            ["_", "idle", "walk", "run", "dash"],
            ["_", "leap", "land", "roar", "taunt"],
            ["bite", "claw_slash", "tail_strike", "horn_thrust"],
            ["acid_breath", "plasma_beam", "cyber_pounce", "mortar_launch"],
            ["enrage", "light_hit", "heavy_hit", "knockdown"],
            ["death", "getup", "transform"],
        ]),
    ]),
    ("8404bf31-707e-48be-9ee3-5d104d91ba26.png", "Reaper", [
        ((8, 1224), [
            ["idle", "walk", "run", "dash"],
            ["jump", "fall", "land", "crouch", "aim", "shoot"],
            ["melee_slash", "dash_strike", "take_damage", "knockdown", "getup", "death"],
            None,  # variants + abilities showcase
            None,  # attack effects strip
        ]),
    ]),
    ("ec9cff60-7d87-4918-a5b1-c8a30d999382.png", "Sentinel", [
        ((8, 1224), [
            ["idle", "walk", "run", "dash"],
            ["jump", "fall", "land", "crouch", "roll", "getup"],
            ["punch_combo", "kick_combo", "slash_attack", "spin_slash", "uppercut"],
            ["aim", "rapid_fire", "charged_shot", "piercing_shot", "spread_shot", "missile_launch"],
            ["energy_blade", "phase_dash", "shield_generate", "overdrive", "drone_deploy"],
            ["wall_run", "wall_jump", "air_dash", "double_jump", "slide", "grapple"],
            ["light_hit", "heavy_hit", "critical_hit", "knockdown", "death"],
        ]),
    ]),
    ("chimera1.jpg", "Chimera", [
        ((8, 1068), [
            None,  # fused idle/sneak/sprint/roar block: explicit regions below
            ["claw_slash", "double_claw", "spinning_claw", "tail_whip", "predator_pounce"],
            ["blood_rend", "hyper_claw", "tail_spike_combo", "frenzied_rush"],
            ["plasma_beam", "acid_spray", "homing_orbs", "missile_barrage"],
            ["cyber_pounce", "blood_explosion", "plasma_core", "tail_drill"],
            ["overheat_mode", "armor_overdrive", "core_exposed", "berserk_mode"],
            ["light_hit", "heavy_hit", "critical_hit", "knockdown", "getup"],
        ]),
    ]),
    ("werewolf1.png", "Werewolf", (12, 0), [
        ((8, 540), [
            ["idle"], ["walk"], ["run"], ["dash"], ["jump"], ["transform"],
            None, None, None, None,
        ]),
    ]),
    ("cybersamurai.png", "Samurai", (12, 0), [((8, 755), CS_SPECS)]),
    ("cybersamurai.png", "SamuraiDual", (12, 0), [((770, 1530), CS_SPECS)]),
]

# extended-units sheet: six stacked-row unit columns
EEF_SHEET = "../hilo/enemyextraframes.png"
EEF_UNITS = ["Guard", "Bruiser", "Ranger", "Drone", "Titan", "Shinobi"]
EEF_ROWS = ["idle", "walk", "run", "aim", "shoot", "reload", "melee",
            "block", "hurt_light", "hurt_heavy", "knockdown", "getup",
            "death1", "death2"]
EEF_BAND = (29, 441)  # the unit-columns strip
EEF_COLS = [(12, 263), (287, 539), (552, 840), (852, 1046), (1060, 1289), (1310, 1519)]


def bands(ink, y0=0, row_gap=ROW_GAP, min_h=MIN_BAND_H):
    rows = ink.sum(axis=1)
    w = ink.shape[1]
    content = (rows > 2) & (rows < 0.55 * w)
    out, start, gap = [], None, 0
    for y, v in enumerate(content):
        if v:
            if start is None:
                start = y
            gap = 0
        elif start is not None:
            gap += 1
            if gap >= row_gap:
                end = y - gap + 1
                if end - start >= min_h:
                    out.append((start + y0, end + y0))
                start, gap = None, 0
    if start is not None and len(rows) - start >= min_h:
        out.append((start + y0, len(rows) + y0))
    return out


def atoms(band_ink, min_gap=8, min_w=10):
    """Contiguous ink column runs separated by gaps >= min_gap."""
    cols = band_ink.sum(axis=0) > 0
    out, start, gap = [], None, 0
    for x, v in enumerate(cols):
        if v:
            if start is None:
                start = x
            gap = 0
        elif start is not None:
            gap += 1
            if gap >= min_gap:
                end = x - gap + 1
                if end - start >= min_w:
                    out.append((start, end))
                start, gap = None, 0
    if start is not None:
        out.append((start, len(cols)))
    return out


def caption_clusters(band_text):
    """Bright caption-text clusters in the band's top strip."""
    strip = band_text[: min(16, band_text.shape[0])].sum(axis=0) > 0
    out, gap = [], 999
    for x, v in enumerate(strip):
        if v:
            if gap >= 20:
                out.append(x)
            gap = 0
        else:
            gap += 1
    return out


def split_to_n(band_ink, n, band_text=None):
    """Split a band into exactly n panels: cut at caption starts when the
    caption count matches, else merge ink atoms at the n-1 widest gaps."""
    caps = caption_clusters(band_text if band_text is not None else band_ink)
    w = band_ink.shape[1]
    if len(caps) == n:
        cuts = [max(0, c - 4) for c in caps[1:]]
        edges = [0] + cuts + [w]
        panels = []
        for a, b in zip(edges, edges[1:]):
            seg = band_ink[:, a:b]
            xs = np.where(seg.sum(axis=0) > 0)[0]
            if len(xs) == 0:
                panels.append((a, b))
            else:
                panels.append((a + int(xs[0]), a + int(xs[-1]) + 1))
        return panels

    parts = atoms(band_ink)
    if len(parts) <= n:
        return parts
    gaps = sorted(range(len(parts) - 1),
                  key=lambda i: parts[i + 1][0] - parts[i][1], reverse=True)
    cuts = sorted(gaps[: n - 1])
    panels, start = [], 0
    for c in cuts:
        panels.append((parts[start][0], parts[c][1]))
        start = c + 1
    panels.append((parts[start][0], parts[-1][1]))
    return panels


def main():
    sheets_out = []

    for entry in SHEETS:
        fname, out_dir = entry[0], entry[1]
        shift, spans, extra = (0, 0), None, None
        for item in entry[2:]:
            if isinstance(item, dict):
                extra = item
            elif isinstance(item, tuple):
                shift = item
            else:
                spans = item
        path = os.path.join(BASE, "..", "sprites", "enemies", fname)
        lum = np.asarray(Image.open(path).convert("L")).astype(np.int16)
        # JPEG sheets carry speckle noise on the black bg; raise the floor
        ink = lum > (52 if fname.endswith(".jpg") else INK)
        text = lum > 150 if fname.endswith(".jpg") else lum > 140
        anims = []
        for (cx0, cx1), specs in spans:
            col = ink[:, cx0:cx1]
            tcol = text[:, cx0:cx1]
            found = bands(col)
            if len(found) != len(specs):
                print(f"!! {out_dir} span x{cx0}-{cx1}: {len(found)} bands, "
                      f"expected {len(specs)} -> {[b for b in found]}")
            for (y0, y1), spec in zip(found, specs):
                if spec is None:
                    continue
                y0 = max(0, min(y0 + shift[0], y1 - 4))
                y1 = min(y1 + shift[1], col.shape[0])
                panels = split_to_n(col[y0:y1], len(spec), tcol[y0:y1])
                if len(panels) != len(spec):
                    print(f"!! {out_dir} band y{y0}: {len(panels)} panels for {spec}")
                for (px0, px1), name in zip(panels, spec):
                    if name == "_":
                        continue
                    anims.append({"name": name,
                                  "region": [cx0 + px0, int(y0), cx0 + px1, int(y1)]})
        for name, region in (extra or {}).get("extra", []):
            anims.append({"name": name, "region": list(region)})
        jpg = fname.endswith(".jpg")
        sheets_out.append({
            "image": f"../sprites/enemies/{fname}",
            "preview_out": f"preview_v2_{out_dir.lower()}.png",
            "out_dir": f"../Assets/Art/Sprites/Enemies/{out_dir}",
            "bg_threshold": 40 if jpg else 38, "key_threshold": 16 if jpg else 10,
            "min_ink_per_column": 8 if jpg else 5, "gap_columns": 1,
            "animations": anims,
        })
        print(out_dir, len(anims), "animations")

    # extended units: nested row pass inside each unit column
    path = os.path.join(BASE, "..", "sprites", "hilo", "enemyextraframes.png")
    lum = np.asarray(Image.open(path).convert("L")).astype(np.int16)
    ink = lum > INK
    for (ux0, ux1), unit in zip(EEF_COLS, EEF_UNITS):
        col = ink[EEF_BAND[0]:EEF_BAND[1], ux0:ux1]
        rows = bands(col, y0=EEF_BAND[0], row_gap=3, min_h=12)
        names = EEF_ROWS if len(rows) == len(EEF_ROWS) else None
        if names is None:
            print(f"!! {unit}: {len(rows)} rows, expected {len(EEF_ROWS)} - using row_NN names")
        anims = []
        for i, (y0, y1) in enumerate(rows):
            name = EEF_ROWS[i] if names else f"row_{i:02d}"
            anims.append({"name": name, "region": [ux0, int(y0), ux1, int(y1)]})
        sheets_out.append({
            "image": "../sprites/hilo/enemyextraframes.png",
            "preview_out": f"preview_v2_{unit.lower()}.png",
            "out_dir": f"../Assets/Art/Sprites/Enemies/{unit}",
            "bg_threshold": 26, "key_threshold": 10,
            "min_ink_per_column": 4, "gap_columns": 1,
            "upscale": 4,  # Scale2x x2: tiny unit art -> clean 4x frames
            "animations": anims,
        })
        print(unit, len(anims), "rows")

    # EEF2: sprites/enemies/enemyextraframes.png - six unit ROWS at ~2x size
    eef2 = os.path.join(BASE, "..", "sprites", "enemies", "enemyextraframes.png")
    lum2 = np.asarray(Image.open(eef2).convert("L")).astype(np.int16)
    ink2 = lum2 > INK
    eef2_rows = [(54, 120), (132, 203), (211, 272), (287, 343), (355, 431), (444, 507)]
    eef2_units = ["Guard", "Bruiser", "Ranger", "Drone", "Titan", "Shinobi"]
    eef2_anims = ["idle", "walk", "run", "shoot", "special", "hit_big", "death_big"]
    # global column spans from the header caption row (y32-52)
    eef2_cols = [(131, 267), (267, 522), (522, 731), (731, 937),
                 (937, 1117), (1117, 1293), (1293, 1530)]
    for (ry0, ry1), unit in zip(eef2_rows, eef2_units):
        anims = []
        for (cx0, cx1), name in zip(eef2_cols, eef2_anims):
            anims.append({"name": name, "region": [cx0, ry0, cx1, ry1]})
        sheets_out.append({
            "image": "../sprites/enemies/enemyextraframes.png",
            "preview_out": f"preview_eef2_{unit.lower()}.png",
            "out_dir": f"../Assets/Art/Sprites/Enemies/{unit}",
            "bg_threshold": 38, "key_threshold": 10,
            "min_ink_per_column": 5, "gap_columns": 1,
            "upscale": 2,
            "animations": anims,
        })
        print(unit, "(big rows)", len(anims))

    with open(os.path.join(BASE, "enemies_v2_config.json"), "w") as f:
        json.dump({"sheets": sheets_out}, f, indent=1)
    total = sum(len(s["animations"]) for s in sheets_out)
    print("wrote enemies_v2_config.json:", total, "animations")


if __name__ == "__main__":
    main()
