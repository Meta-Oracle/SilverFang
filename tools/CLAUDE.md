# Silver Fang — Sprite & Animation Refinement Playbook

Battle-tested techniques for getting the art/animation to a flawless, consistent
state. Apply these whenever touching sprites, extraction, or baked clips. (The
top-level CLAUDE.md owns the bake-order/pipeline; this file owns the *quality*
techniques that have actually worked on this project.)

## 1. Pixel-perfect edges (no smoothing — ever)
- **Resize with `Image.NEAREST` only.** LANCZOS was the original blur source.
- **Hard-binarize alpha:** `alpha = 255 if alpha >= 128 else 0`. Gives crisp,
  blocky, "true sprite" edges. The normalizer does this; for already-extracted
  folders run a standalone hard-alpha pass (skip frames already crisp:
  `((a>0)&(a<250)).sum() <= 20`).
- Unity import is already Point + Uncompressed; keep it.
- **Verify:** scan folders for soft edges — any frame with many partial-alpha
  pixels (`0 < a < 250`) is anti-aliased and needs the hard pass.

## 2. Degenerate / blink-frame repair (NEVER delete — repair in place)
Frames whose ink is a tiny outlier (extraction caught a near-empty/gap frame)
make the sprite "blink" or the combo "hang." Per folder:
- median = `np.median(ink_pixel_counts)`; flag frames with `ink < 0.30–0.32 * median`.
- Overwrite each bad frame with its nearest GOOD neighbour (prefer previous).
- This keeps the frame count (becomes a clean 2-frame hold) — no deletion, which
  preserves the move's timing and the animator's clip length.

## 3. Sizing: canonical hero heights, anchored on the body
- Canonical ink heights @ PPU 48: Silver/Awakened 100, Lucas ~104, Hilo 92,
  Big Man ~104. Enemies per `normalize_enemies.py`.
- `normalize_sprite_canvases.py <root> --anchor lq --target-height H` —
  **lower-quartile** ink height (arcs/effects inflate the median/max).
- **Oversize fix:** folders never scaled down (e.g. launchers at 400–520px) need
  a WIDE `--clamp` (e.g. `0.18 1.8`) so the big downscale isn't blocked.
- **Effect-heavy folders** (a giant arc/thrust frame): use `--anchor max` with a
  modest target so the lq (compact windup) doesn't inflate the big frames.
- **Effect-only frames** (muzzle/tracer-only) wreck lq sizing — repair them to
  body neighbours first (ink-HEIGHT < 0.55 * median), THEN normalize.

## 4. Extraction sanity (extract from the RIGHT rows)
- Always render an indexed montage of a row's cells before trusting it. The gun
  swap bug was extracting from TRANSITION/idle rows, not the firing rows.
- State sheets (Big Man / Psionic) mirror the base sheet's row layout — reuse the
  base config's regions.
- Region top needs **headroom** or hair/heads clip (the locomotion hair-clip bug).
- Plain locomotion must extract **LAST** in `regen_all_sprites.py` (after the
  blue overhaul configs) or dash/sprint get re-blued. Blue is reserved for
  charge/finisher/awakened per art direction.

## 5. Action timing (BakeAttackClip)
- High-speed read: `strikeStart = 0.12`, `strikeEnd = 0.88`; poses hold ~2 frames
  max; `length += 1/Fps` only.
- **Idle breathes slow:** `IdleFps = 9` (not 24). Walk/Run stay at `Fps = 24`.
- **Jotaro flurry** (multiHit ≥ 6): dedicated clip — quick windup → blow frames
  SEMI-LOOP fast (a hitbox window + a small paced slash VFX per blow) → a
  dramatic full PAUSE on a wind-up pose → an explosive cleave finisher
  (AnimEvent_FinisherHit: spike + ground-bounce). 1.5s for balance.

## 6. Combo flow (so every swing reads)
- **Gate combo-cancel on the LIVE clip progress**, not the spec `move.duration`
  (BakeAttackClip stretches clips, so duration-based cancels fire too early and
  drop swings): `AttackClipProgress() = Mathf.Repeat(animator state normalizedTime, 1)`,
  cancel only when `>= ~0.6`.
- Widen the chain window (`chainWindow ~0.85s`) for forgiving routing.
- Directional specials use 2-step motions `{2,6}`/`{2,4}` (keyboards can't hold a
  clean diagonal), window ~0.55s.

## 7. Slash hurtbox-extension VFX
- Spawn the slash trail on **Hitbox.Activate** (not on hit) so it renders ACROSS
  the active window, tinted to the attacker's aura (`CombatVfx.Slash`), and sized
  to the hurtbox (`reach = max(col.bounds.size)`), reading as the hurtbox's reach.
- VFX variety: every category (blood/spark/psi/electric/blue/slash) is an RNG
  POOL — pick a random variant per hit so sprites never repeat back-to-back.
- Keep impact VFX small/subtle (−60% base scale); reserve the radial spectacle
  for the top combo tiers.

## 8. Always verify before declaring done
- Render full animation STRIPS for changed folders (single-frame checks miss
  caption bleed, strobing, scale pops, blinks).
- Confirm `partialAlpha == 0`, lq body heights match the canon, idle≠attack pops.
- Bake the full chain and require **Player 111 / Awakened 32 / Hilo 81 /
  HiloAwakened 22 / Lucas 31, 0 missing**, zero CS errors, before sync.
