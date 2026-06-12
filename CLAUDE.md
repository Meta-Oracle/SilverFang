# Silver Fang — pipeline conventions

Unity 6000.4 2D belt-scroller. The animation and art style are the game's
primary draw: every change to sprites or animation MUST hold these rules.

## Art direction rules (mandated)
- **Blue energy frames belong ONLY to**: charge holds/releases, length-4
  combo finishers, and awakened-context moves (awakened form, teleport
  attacks). Base combos, locomotion, airs, and reactions use PLAIN art.
  Blueness test: `mean(B - max(R,G))` over bright pixels `> 6` = blue strip.
- **Body scale is uniform across every state**: idle, walk, run, attacks,
  reactions must show the same body height. normalize_sprite_canvases keys
  off the LOWER-QUARTILE frame ink height (arcs inflate medians).
  Canonical heights (px @ PPU 48): Silver/Awakened 100, Hilo 92; enemies in
  `tools/normalize_enemies.py` (humanoids 86/82 = hero parity; Werewolf 104,
  Titan 112, Chimera 132, Drone 62).
- **24fps action style**: SpriteBaker bakes attacks as anticipation (32%),
  strike window (32–58%), follow-through; idle loops 8fps, walk 12fps,
  run 14fps. Hitbox ON at strike start, OFF at strike end +8%. AttackEnd at
  93% (never the clip end — exit transitions can swallow it).
- Dash/landing feedback is **dust** (`dash_dust` VFX), not hit sparks.

## Sprite pipeline (run in this order)
1. `python tools/regen_all_sprites.py` — re-extracts ALL hero/VFX art from
   the sheets (configs in dependency order, fix-configs last, then
   `silver_locomotion_fix` restores PLAIN idle/walk/run/dash/jump over the
   blue 24fps rows), normalizes hero roots, re-cuts HUD/portraits.
2. `python tools/gen_enemies_v2_config.py && python tools/extract_v2.py
   extract tools/enemies_v2_config.json` — enemy sheets (specs live inline
   in the generator; per-sheet band shifts handle caption rows).
3. `python tools/normalize_enemies.py` — canonical enemy sizes.
4. `python tools/audit_frames.py --delete` — purge sliver/merged frames.
5. Unity batch chain (each `-quit` session separately; NEVER while the
   editor runs — a stale `Temp/UnityLockfile` after a force-kill makes every
   batch exit 1 with ~1KB logs; delete it first):
   `SetupTools.BuildAll` → plain import session → `MasterBake.All` →
   `SceneAudit.Run` (includes ValidateMoves: every move set entry must have
   its animator param + a sprite-keyed clip — 230/230 expected).

## Extraction (tools/extract_v2.py)
- Magic-wand keyer: background flood = NEUTRAL dark pixels only
  (chroma-aware, so dark fur/armor survives); one gentle halo pass;
  hole-restoration capped (big enclosed areas are arc interiors — leave
  transparent or they render as black slabs).
- clean_frame kills caption text (flat wide components in the upper frame)
  and border debris but keeps detached heads.
- `"upscale": 2|4` per sheet = Scale2x passes for tiny pixel art
  (extended-unit sheets). JPG sheets need higher ink thresholds.

## Verification protocol (do this BEFORE declaring art fixed)
- Render ANIMATION STRIPS (all frames side by side) for changed folders —
  single-frame checks miss caption bleed, strobing, and scale pops.
- Checkerboard-composite frames to expose transparency holes.
- Compare idle-vs-attack lower-quartile body heights per character.
- `SilverFang > Validate Moves` (or audit log) for route coverage.

## Compile check without Unity (no .NET SDK installed)
Unity's bundled Roslyn: `Editor\Data\NetCoreRuntime\dotnet.exe
Editor\Data\DotNetSdkRoslyn\csc.dll` + `Data\NetStandard\ref\2.1.0\
netstandard.dll` + UnityEngine modules + Library/ScriptAssemblies for
packages; editor scripts add `UnityEditor.CoreModule.dll` (never the legacy
UnityEditor.dll). Runtime dll first, then editor dll referencing it.

## Key runtime systems
- PlayerController: combo resolver (L/H/G strings), air combo chains,
  charge system (hold ≥0.3s, full 2s), lock-on (LT/LShift; backpedal state),
  awakened form (RT/F). ComboTracker: 12-tier ladder (F→XXX), payout
  multipliers ×1.0–7.5 on SCEMA/XP, window shrinks ×0.92 per tier.
- Progression: level cap 99, 3 paths × 8 tiers (54 nodes), LevelUpMenuUI on
  touchpad/T. Per-hero HUD groups under Canvas (SilverHud/HiloHud), combo
  meter hidden until a chain starts.
- Scene flow: IntroCrawlUI → CharacterSelectUI (heroes + HUDs start
  inactive) → gameplay. Rebuild order: Build Demo Scene → Bake Sprites →
  Bake VFX (scene rebuild wipes VfxManager).
