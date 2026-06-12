using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SilverFang.EditorTools
{
    /// Turns extracted sprite frames (tools/extract_sprites.py output) into
    /// animation clips, per-enemy animator controllers, and scene background.
    public static class SpriteBaker
    {
        private const string SilverDir = "Assets/Art/Sprites/Silver";
        private const string EnemiesDir = "Assets/Art/Sprites/Enemies";
        private const string BackgroundDir = "Assets/Art/Sprites/Background";
        private const string AnimDir = "Assets/Art/Animations";
        private const float CharacterPPU = 48f;
        // 24fps-style action timing: strikes land on single 24fps frames while
        // anticipation and follow-through hold longer, reading cleanly at 60fps.
        private const float Fps = 24f;
        private const float IdleFps = 8f;   // calm loops hold on threes
        private const float WalkFps = 12f;  // locomotion on twos

        private const string AwakenedDir = "Assets/Art/Sprites/Awakened";
        private const string HiloDir = "Assets/Art/Sprites/Hilo";

        [MenuItem("SilverFang/Bake Sprites Into Game")]
        public static void BakeAll()
        {
            SetupTools.RebuildCombatAssets();
            ImportCharacterSprites(SilverDir);
            ImportCharacterSprites(EnemiesDir);
            ImportCharacterSprites(AwakenedDir);
            ImportCharacterSprites(HiloDir);
            BakePlayer();
            BakeEnemies();
            BakeAwakened();
            BakeHilo();
            AddBackgroundToScene();
            AssetDatabase.SaveAssets();
            Debug.Log("SpriteBaker: done");
        }

        /// Batch-mode diagnostic: log file vs loaded-sprite counts per key dir.
        [MenuItem("SilverFang/Debug Probe Frames")]
        public static void ProbeFrames()
        {
            foreach (var dir in new[]
                     {
                         $"{SilverDir}/idle", $"{HiloDir}/idle",
                         $"{EnemiesDir}/Werewolf/idle", $"{AwakenedDir}/idle"
                     })
            {
                int files = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.png").Length : -1;
                Debug.Log($"PROBE {dir}: files={files} sprites={LoadFrames(dir).Length}");
            }
        }

        private static void ImportCharacterSprites(string root)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.alphaIsTransparency = true;
                importer.spritePixelsPerUnit = CharacterPPU;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        private static float LoopLength(Sprite[] frames, float fps) =>
            frames.Length > 0 ? frames.Length / fps : 0.5f;

        private static Sprite[] LoadFrames(string dir)
        {
            if (!Directory.Exists(dir)) return new Sprite[0];
            return Directory.GetFiles(dir, "*.png")
                .OrderBy(p => p)
                .Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p.Replace('\\', '/')))
                .Where(s => s != null)
                .ToArray();
        }

        /// Replace the content of an existing clip asset (or create it) with a sprite
        /// animation. Keeps controller references intact because the asset is reused.
        private static AnimationClip BakeClip(string clipName, Sprite[] frames, bool loop,
            float? forceLength = null, params (float frac, string func)[] events)
        {
            string path = $"{AnimDir}/{clipName}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip { name = clipName };
                AssetDatabase.CreateAsset(clip, path);
            }

            clip.ClearCurves();
            AnimationUtility.SetAnimationEvents(clip, new AnimationEvent[0]);
            clip.frameRate = Fps;

            float length = forceLength ?? Mathf.Max(frames.Length / Fps, 0.1f);
            if (frames.Length > 0)
            {
                var binding = new EditorCurveBinding
                {
                    path = "",
                    type = typeof(SpriteRenderer),
                    propertyName = "m_Sprite"
                };
                var keys = new ObjectReferenceKeyframe[frames.Length + 1];
                float step = length / frames.Length;
                for (int i = 0; i < frames.Length; i++)
                    keys[i] = new ObjectReferenceKeyframe { time = i * step, value = frames[i] };
                keys[frames.Length] = new ObjectReferenceKeyframe { time = length, value = frames[frames.Length - 1] };
                AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            }
            else
            {
                // No art for this move yet: keep a dummy curve so the state has length.
                clip.SetCurve("", typeof(Transform), "m_LocalScale.z", AnimationCurve.Constant(0f, length, 1f));
            }

            if (events.Length > 0)
            {
                AnimationUtility.SetAnimationEvents(clip,
                    events.Select(e => new AnimationEvent { time = e.frac * length, functionName = e.func }).ToArray());
            }

            var so = new SerializedObject(clip);
            so.FindProperty("m_AnimationClipSettings.m_LoopTime").boolValue = loop;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(clip);
            return clip;
        }

        /// Attack clip with 24fps action pacing: anticipation (first ~30%),
        /// strike frames snapped fast through the contact window, follow-through
        /// hold to the end. Hitbox/Fire events align to the strike window so the
        /// full swipe is active — not a sliver in the middle of the clip.
        /// Short strips (1-2 poses) get a windup pose prepended so every swing
        /// still reads as wind-up -> contact -> recovery.
        private static AnimationClip BakeAttackClip(string clipName, Sprite[] frames, Sprite[] windupPool,
            float length, bool projectile, bool slashWave = false)
        {
            string path = $"{AnimDir}/{clipName}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip { name = clipName };
                AssetDatabase.CreateAsset(clip, path);
            }

            clip.ClearCurves();
            AnimationUtility.SetAnimationEvents(clip, new AnimationEvent[0]);
            clip.frameRate = Fps;

            // Split into windup + strike.
            Sprite[] windup, strike;
            if (frames.Length >= 6)
            {
                windup = new[] { frames[0], frames[1] };
                strike = frames.Skip(2).ToArray();
            }
            else if (frames.Length >= 3)
            {
                windup = new[] { frames[0] };
                strike = frames.Skip(1).ToArray();
            }
            else if (frames.Length >= 1)
            {
                windup = windupPool != null && windupPool.Length > 0 ? new[] { windupPool[0] } : new Sprite[0];
                strike = frames;
            }
            else
            {
                windup = new Sprite[0];
                strike = new Sprite[0];
            }

            const float strikeStart = 0.32f;
            const float strikeEnd = 0.58f;

            if (strike.Length > 0)
            {
                var keys = new List<ObjectReferenceKeyframe>();
                for (int i = 0; i < windup.Length; i++)
                    keys.Add(new ObjectReferenceKeyframe
                    {
                        time = length * strikeStart * i / windup.Length,
                        value = windup[i]
                    });
                for (int i = 0; i < strike.Length; i++)
                    keys.Add(new ObjectReferenceKeyframe
                    {
                        time = length * (strikeStart + (strikeEnd - strikeStart) * i / strike.Length),
                        value = strike[i]
                    });
                // follow-through: hold the last strike pose to the end
                keys.Add(new ObjectReferenceKeyframe { time = length, value = strike[strike.Length - 1] });

                var binding = new EditorCurveBinding
                {
                    path = "",
                    type = typeof(SpriteRenderer),
                    propertyName = "m_Sprite"
                };
                AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());
            }
            else
            {
                clip.SetCurve("", typeof(Transform), "m_LocalScale.z", AnimationCurve.Constant(0f, length, 1f));
            }

            var events = new List<AnimationEvent>();
            if (projectile)
            {
                events.Add(new AnimationEvent { time = length * (strikeStart + 0.04f), functionName = "AnimEvent_Fire" });
            }
            else
            {
                events.Add(new AnimationEvent { time = length * strikeStart, functionName = "AnimEvent_HitboxOn" });
                if (slashWave)
                    events.Add(new AnimationEvent { time = length * (strikeStart + 0.06f), functionName = "AnimEvent_Fire" });
                events.Add(new AnimationEvent { time = length * (strikeEnd + 0.08f), functionName = "AnimEvent_HitboxOff" });
            }
            events.Add(new AnimationEvent { time = length * 0.93f, functionName = "AnimEvent_AttackEnd" });
            AnimationUtility.SetAnimationEvents(clip, events.ToArray());

            var so = new SerializedObject(clip);
            so.FindProperty("m_AnimationClipSettings.m_LoopTime").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(clip);
            return clip;
        }

        /// The demo-scene builder gives the placeholder Visual a (1,2,1) stretch.
        /// Real frames have correct proportions baked in, so any leftover
        /// non-uniform scale distorts every sprite. Normalize once real art lands.
        private static void NormalizePlayerVisual(string path = "Assets/Prefabs/Player.prefab", Sprite idleSprite = null)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var visual = root.transform.Find("Visual");
                if (visual == null) return;
                visual.localScale = Vector3.one;
                visual.localPosition = Vector3.zero;
                // real idle frame as the resting sprite: the hero is visible
                // even on the frame before the Animator first evaluates
                if (idleSprite != null)
                {
                    var sr = visual.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.sprite = idleSprite;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// Adds a Run state (MoveSpeed above walk top speed) to the player
        /// controller if it doesn't have one yet. Dash-hold running pushes
        /// MoveSpeed past 6, switching Walk -> Run.
        private static void EnsureRunState(AnimationClip runClip)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>($"{AnimDir}/PlayerAnimator.controller");
            if (controller == null) return;
            var sm = controller.layers[0].stateMachine;
            if (sm.states.Any(s => s.state.name == "Run")) return;

            var walkState = sm.states.FirstOrDefault(s => s.state.name == "Walk").state;
            var idleState = sm.states.FirstOrDefault(s => s.state.name == "Idle").state;
            if (walkState == null || idleState == null) return;

            var runState = sm.AddState("Run");
            runState.motion = runClip;

            var toRun = walkState.AddTransition(runState);
            toRun.AddCondition(AnimatorConditionMode.Greater, 6f, "MoveSpeed");
            toRun.hasExitTime = false;
            toRun.duration = 0f;

            var toWalk = runState.AddTransition(walkState);
            toWalk.AddCondition(AnimatorConditionMode.Less, 6f, "MoveSpeed");
            toWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "MoveSpeed");
            toWalk.hasExitTime = false;
            toWalk.duration = 0f;

            var toIdle = runState.AddTransition(idleState);
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "MoveSpeed");
            toIdle.hasExitTime = false;
            toIdle.duration = 0f;

            EditorUtility.SetDirty(controller);
        }

        // Move id -> extracted art folder for every sword / gun / dash /
        // sprint / teleport move. Unmapped moves fall back to slash poses.
        private static readonly (string move, string folder)[] PlayerArtMap =
        {
            ("BasicSlash1", "light1"), ("BasicSlash2", "light2"), ("BasicSlash3", "slash"),
            ("RisingSlash", "rising_slash"), ("OverheadSlash", "overhead_slash"),
            ("ForwardThrust", "forward_thrust"), ("LungeSlash", "lunge_slash"),
            ("HorizontalWide", "horizontal_wide"), ("SpinSlash", "spin_slash"),
            ("DoubleSpinSlash", "double_spin_slash"), ("DownwardStrike", "ground_slam"),
            ("DiagonalSlash", "diagonal_slash"), ("BackhandSlash", "backhand_slash"),
            ("CrescentSlash", "crescent_slash"), ("GreatCleave", "great_cleave"),
            ("ChargedSlash", "charged_slash"), ("FullChargeSlash", "full_charge_slash"),
            ("DashSlash", "dash_slash"), ("BlinkSlash", "blink_slash"),
            ("SpinningLunge", "spinning_lunge"), ("WhirlwindFinisher", "whirlwind"),
            ("GroundSlam", "ground_slam"), ("EarthSplitter", "earth_splitter"),
            ("WaveSlash", "wave_slash"), ("CrossSlash", "cross_slash"),
            ("SheathStrike", "sheath_strike"), ("TurningSlash", "turning_slash"),
            ("GuardBreak", "guard_break"), ("DisarmStrike", "disarm"),
            ("Execution", "execution"), ("SpecialFinisher1", "special_finisher1"),
            ("BladeStorm", "special_finisher2"), ("UppercutSlash", "rising_slash"),
            ("PistolWhip", "sheath_strike"), ("SlideSlash", "slide"),
            ("KickShot", "kick_shot"), ("MortarKick", "kick_shot"),
            ("QuickShot", "shooting"), ("RapidShot", "rapid_shot"), ("DualShot", "shooting"),
            ("TripleTap", "shooting"), ("RicochetShot", "shooting"), ("PiercingVolley", "shooting"),
            ("OverdriveShot", "rapid_shot"), ("BulletStorm", "rapid_shot"),
            ("SupportFire", "rapid_shot"), ("SupportBarrage", "bullet_rain"),
            ("BulletRain", "bullet_rain"), ("DragonShot", "dragon_slash"),
            ("Gunslinger", "gunslinger"), ("GunslingerFinisher", "gunslinger"),
            ("Shoot", "shooting"), ("ChargeUp", "charge_up"), ("PowerSlash", "power_slash"),
            ("SwordCharge", "sword_charge"), ("DualArcSlash", "dual_arc_slash"),
            ("Finisher", "finisher"), ("SpecialFinisher3", "special_finisher3"),
            ("ClassicSlash", "slash"),
            ("DashSlashAtk", "da_slash"), ("DashHeavyAtk", "da_heavy"), ("DashShootAtk", "da_shoot"),
            ("DashThrustAtk", "da_thrust"), ("DashUppercutAtk", "da_uppercut"), ("DashSpinAtk", "da_spin"),
            ("SprintSlashAtk", "sp_slash"), ("SprintHeavyAtk", "sp_heavy"), ("SprintShootAtk", "sp_shoot"),
            ("SprintThrustAtk", "sp_thrust"), ("SprintSpinAtk", "spin_slash"),
            ("TeleportStrikeAtk", "tp_strike"), ("TeleportHeavyAtk", "tp_strike_long"), ("TeleportShootAtk", "tp_shoot"),
            ("AirLightSlash", "air_jump_slash"), ("AirHeavySlash", "jumping_slash"),
            ("AirRisingSpin", "aerial_spin"), ("AirJumpSlash", "air_jump_slash"),
            ("JumpingSlash", "jumping_slash")
        };

        private static void BakePlayer()
        {
            var idle = LoadFrames($"{SilverDir}/idle");
            if (idle.Length == 0)
                Debug.LogError("SpriteBaker: Silver idle frames failed to load as sprites - " +
                               "Player clips would bake empty. Check texture import state.");
            NormalizePlayerVisual("Assets/Prefabs/Player.prefab", idle.Length > 0 ? idle[0] : null);
            var walk = LoadFrames($"{SilverDir}/walk");
            var run = LoadFrames($"{SilverDir}/run");
            var dash = LoadFrames($"{SilverDir}/dash");
            var jump = LoadFrames($"{SilverDir}/jump");
            var fall = LoadFrames($"{SilverDir}/fall");
            var land = LoadFrames($"{SilverDir}/land");
            var roll = LoadFrames($"{SilverDir}/roll");
            var tacticalRoll = LoadFrames($"{SilverDir}/tactical_roll");
            var guard = LoadFrames($"{SilverDir}/guard");
            var reload = LoadFrames($"{SilverDir}/reload");
            var hurt = LoadFrames($"{SilverDir}/hurt");
            var knockdown = LoadFrames($"{SilverDir}/knockdown");
            var getup = LoadFrames($"{SilverDir}/getup");
            var death = LoadFrames($"{SilverDir}/death");
            var victory = LoadFrames($"{SilverDir}/victory");
            var slash = LoadFrames($"{SilverDir}/basic_slash");
            if (slash.Length == 0) slash = LoadFrames($"{SilverDir}/slash");

            BakeClip("Player_Idle", idle, true, LoopLength(idle, IdleFps));
            var walkFrames = walk.Length > 0 ? walk : run;
            BakeClip("Player_Walk", walkFrames, true, LoopLength(walkFrames, WalkFps));
            // Lock-on backpedal: reversed walk cycle reads as retreating steps
            // (placeholder until a dedicated backstep sheet lands).
            var walkBack = walkFrames.Reverse().ToArray();
            BakeClip("Player_WalkBack", walkBack, true, LoopLength(walkBack, WalkFps));
            var runFrames = run.Length > 0 ? run : walk;
            EnsureRunState(BakeClip("Player_Run", runFrames, true, LoopLength(runFrames, 14f)));
            BakeClip("Player_DashAnim", dash.Length > 0 ? dash : run, false, 0.22f);
            BakeClip("Player_Hurt", hurt, false, 0.3f);
            var hurtHeavy = LoadFrames($"{SilverDir}/hurt_heavy");
            BakeClip("Player_HurtHeavy", hurtHeavy.Length > 0 ? hurtHeavy : hurt, false, 0.38f);
            BakeClip("Player_Knockdown", knockdown, false, 0.5f);
            BakeClip("Player_GetUp", getup, false, 0.4f);
            BakeClip("Player_Dead", death.Length > 0 ? death : knockdown, false,
                Mathf.Max(death.Length / Fps, 0.6f));
            BakeClip("Player_Jump", jump.Length > 0 ? jump
                : run.Length > 0 ? new[] { run[run.Length - 1] } : idle, false, 0.7f);
            BakeClip("Player_Fall", fall.Length > 0 ? fall : jump, false, 0.35f);
            BakeClip("Player_Land", land.Length > 0 ? land : idle, false, 0.28f);
            BakeClip("Player_Roll", roll.Length > 0 ? roll : tacticalRoll, false, 0.45f);
            BakeClip("Player_Guard", guard.Length > 0 ? guard : idle, false, 0.35f);
            BakeClip("Player_Reload", reload.Length > 0 ? reload : idle, false, 0.45f);
            BakeClip("Player_Victory", victory.Length > 0 ? victory : idle, false, 0.8f);

            // Windup poses for short strips: plain slash poses only — blue
            // charge frames are reserved for charge attacks and finishers.
            var windupPool = slash.Length > 0 ? new[] { slash[0] } : idle;

            // Bake every combat move from the move tables so events, lengths,
            // and projectile/wave flags always match the data the game runs on.
            foreach (var spec in SetupTools.AllPlayerMoveSpecs)
            {
                string folder = PlayerArtMap.FirstOrDefault(m => m.move == spec.id).folder;
                Sprite[] frames = folder != null ? LoadFrames($"{SilverDir}/{folder}") : new Sprite[0];
                if (frames.Length == 0)
                {
                    if (slash.Length == 0) continue;
                    frames = spec.projectile && idle.Length > 0 ? new[] { idle[0] } : slash;
                }

                BakeAttackClip("Player_" + spec.id, frames, windupPool, spec.length, spec.projectile, spec.slashWave);
            }

            BakeChargeClips(windupPool, idle);
        }

        /// Charge hold loops + the nine charged releases (chargedattacks.png).
        /// Folders come from the charged-attacks extraction; graceful fallbacks
        /// keep the states alive until the art lands.
        private static void BakeChargeClips(Sprite[] windupPool, Sprite[] idle)
        {
            Sprite[] FramesOr(string folder, params string[] fallbacks)
            {
                var frames = LoadFrames($"{SilverDir}/{folder}");
                foreach (var fb in fallbacks)
                {
                    if (frames.Length > 0) break;
                    frames = LoadFrames($"{SilverDir}/{fb}");
                }
                return frames.Length > 0 ? frames : idle;
            }

            var holdLight = FramesOr("charge_hold_light", "sword_charge", "charge_up");
            var holdHeavy = FramesOr("charge_hold_heavy", "sword_charge", "charge_up");
            var holdGun = FramesOr("charge_hold_gun", "charge_up", "sword_charge");
            BakeClip("Player_ChargeHoldLight", holdLight, true, LoopLength(holdLight, 10f));
            BakeClip("Player_ChargeHoldHeavy", holdHeavy, true, LoopLength(holdHeavy, 10f));
            BakeClip("Player_ChargeHoldGun", holdGun, true, LoopLength(holdGun, 10f));

            for (int level = 1; level <= 3; level++)
            {
                // durations must match PlayerController.BuildChargedMove
                BakeAttackClip("Player_ChargeLight" + level,
                    FramesOr($"charge_light_l{level}", "charged_slash"), windupPool,
                    0.38f + 0.08f * level, projectile: false);
                BakeAttackClip("Player_ChargeHeavy" + level,
                    FramesOr($"charge_heavy_l{level}", "full_charge_slash"), windupPool,
                    0.5f + 0.1f * level, projectile: false, slashWave: level >= 3);
                BakeAttackClip("Player_ChargeShot" + level,
                    FramesOr($"charge_shot_l{level}", "shooting"), windupPool,
                    0.42f + 0.07f * level, projectile: true);
            }
        }

        // Hilo move id -> extracted art folder (sprites/hilo sheets). Every
        // route in SetupTools' Hilo tables maps to a real strip.
        private static readonly (string move, string folder)[] HiloArtMap =
        {
            ("ClawJab", "light1"), ("PowerStraight", "heavy1"), ("HiloQuickShot", "quick_shot"),
            ("ClawCross", "light2"), ("RisingPalm", "rising_palm"), ("PhantomBeam", "phantom_beam"),
            ("FrontKick", "front_kick"), ("AxeKick", "axe_kick"), ("BurstCannon", "burst_cannon"),
            ("SideKick", "side_kick"), ("BackKick", "back_kick"), ("WideBeam", "wide_beam"),
            ("ClawFlurry", "light3"), ("ClawUppercut", "claw_uppercut"), ("EnergyWave", "energy_wave"),
            ("SnapKick", "snap_kick"), ("SpinningClaw", "spinning_claw"), ("HomingMissile", "homing_missile"),
            ("ClawSlash", "claw_slash"), ("ClawSpin", "claw_spin"), ("YinYangBurst", "yinyang_burst"),
            ("RoundhouseKick", "roundhouse_kick"), ("FlyingRoundhouse", "flying_roundhouse"), ("PhantomVolley", "phantom_beam"),
            ("DoubleRoundhouse", "roundhouse2"), ("HurricaneKick", "hurricane_kick"), ("EnergyClaw", "energy_claw"),
            ("ClawThrust", "claw_thrust"), ("CycloneKick", "cyclone_kick"), ("MissileBarrage", "homing_missile"),
            ("BionicComboA", "punch_combo_a"), ("FlyingSideKick", "flying_side_kick"), ("CannonVolley", "burst_cannon"),
            ("SpinningKick", "spinning_kick"), ("JumpKick", "jump_kick"), ("PhantomStrike", "phantom_strike"),
            ("BionicComboB", "punch_combo_b"), ("TwinClawCombo", "claw_combo_a"), ("OmniBeam", "wide_beam"),
            ("KillCombo", "kill_combo"), ("SavageClawFinisher", "claw_combo_b"), ("YinYangOverdrive", "yinyang_burst"),
            ("PhantomDance", "phantom_strike"), ("CycloneFinisher", "cyclone_kick"), ("ShadowCloneStrike", "shadow_clone"),
            ("HiloDashClaw", "claw_thrust"), ("HiloDashRush", "punch_combo_a"), ("HiloDashSpin", "claw_spin"),
            ("HiloDashKick", "flying_side_kick"), ("HiloDashShot", "quick_shot"),
            ("HiloSprintKick", "front_kick"), ("HiloSprintRush", "punch_combo_b"), ("HiloSprintCyclone", "cyclone_kick"),
            ("HiloSprintHurricane", "hurricane_kick"), ("HiloSprintCannon", "burst_cannon"),
            ("HiloShadowStrike", "phantom_strike"), ("HiloShadowClone", "shadow_clone"), ("HiloShadowBeam", "phantom_beam"),
            ("HiloAirKick", "air_jump_light"), ("HiloAirHeavyKick", "air_jump_heavy"),
            ("HiloAirClaw", "air_jump_claw"), ("HiloAirSpinKick", "air_spin_kick"),
            ("HiloAirDiveKick", "air_dive_kick"), ("HiloAirDownSlash", "air_downward_slash"),
            ("HiloAirBurst", "air_energy_burst"), ("HiloAerialCombo", "aerial_combo")
        };

        private static Sprite[] HiloFramesOr(Sprite[] fallback, params string[] folders)
        {
            foreach (var folder in folders)
            {
                var frames = LoadFrames($"{HiloDir}/{folder}");
                if (frames.Length > 0) return frames;
            }
            return fallback;
        }

        private static void BakeHilo()
        {
            var idle = LoadFrames($"{HiloDir}/idle");
            if (idle.Length == 0)
            {
                Debug.LogWarning("SpriteBaker: no Hilo frames found, skipping Hilo bake");
                return;
            }

            NormalizePlayerVisual("Assets/Prefabs/Hilo.prefab", idle[0]);

            var walk = LoadFrames($"{HiloDir}/walk");
            var run = LoadFrames($"{HiloDir}/run");
            var crouch = LoadFrames($"{HiloDir}/crouch");   // hurt / get-up / windup stand-in
            var slip = LoadFrames($"{HiloDir}/yin_slip");   // roll / knockdown stand-in

            BakeClip("Hilo_Idle", idle, true, LoopLength(idle, IdleFps));
            var walkFrames = walk.Length > 0 ? walk : run;
            BakeClip("Hilo_Walk", walkFrames, true, LoopLength(walkFrames, WalkFps));
            var hiloWalkBack = walkFrames.Reverse().ToArray();
            BakeClip("Hilo_WalkBack", hiloWalkBack, true, LoopLength(hiloWalkBack, WalkFps));
            var runFrames = run.Length > 0 ? run : walk;
            BakeClip("Hilo_Run", runFrames, true, LoopLength(runFrames, 14f));
            BakeClip("Hilo_DashAnim", HiloFramesOr(runFrames, "dash"), false, 0.22f);
            BakeClip("Hilo_Hurt", crouch.Length > 0 ? crouch : idle, false, 0.3f);
            BakeClip("Hilo_HurtHeavy", HiloFramesOr(crouch.Length > 0 ? crouch : idle, "yin_slip"), false, 0.38f);
            BakeClip("Hilo_Knockdown", slip.Length > 0 ? slip : crouch, false, 0.5f);
            BakeClip("Hilo_GetUp", crouch.Length > 0 ? crouch : idle, false, 0.4f);
            BakeClip("Hilo_Dead", slip.Length > 0 ? slip : crouch, false, 0.6f);
            // jump + double_jump strips concatenated: a full 11-frame arc
            var hiloJump = HiloFramesOr(idle, "jump")
                .Concat(LoadFrames($"{HiloDir}/double_jump")).ToArray();
            BakeClip("Hilo_Jump", hiloJump, false, 0.7f);
            BakeClip("Hilo_Fall", HiloFramesOr(idle, "fall", "jump"), false, 0.35f);
            BakeClip("Hilo_Land", HiloFramesOr(idle, "land"), false, 0.28f);
            BakeClip("Hilo_Roll", HiloFramesOr(idle, "yin_slip", "dash"), false, 0.45f);
            BakeClip("Hilo_Guard", HiloFramesOr(idle, "guard"), false, 0.35f);
            BakeClip("Hilo_Reload", HiloFramesOr(idle, "charge_bionic_l1", "crouch"), false, 0.45f);
            BakeClip("Hilo_Victory", HiloFramesOr(idle, "yinyang_burst"), false, 0.8f);

            var windupPool = crouch.Length > 0 ? crouch : idle;
            var basicClaw = LoadFrames($"{HiloDir}/claw_slash");

            foreach (var spec in SetupTools.AllHiloMoveSpecs)
            {
                string folder = HiloArtMap.FirstOrDefault(m => m.move == spec.id).folder;
                Sprite[] frames = folder != null ? LoadFrames($"{HiloDir}/{folder}") : new Sprite[0];
                if (frames.Length == 0)
                {
                    if (basicClaw.Length == 0) continue;
                    frames = spec.projectile ? new[] { idle[0] } : basicClaw;
                }

                BakeAttackClip("Hilo_" + spec.id, frames, windupPool, spec.length, spec.projectile, spec.slashWave);
            }

            BakeHiloChargeClips(windupPool, idle);
            BakeHiloAwakened(idle, crouch, slip);
        }

        /// Hilo's charge sheets run l1-l4: l1 poses loop as the hold, l2-l4 are
        /// the level 1-3 releases. Charged shots have their own l1-l3 strips.
        private static void BakeHiloChargeClips(Sprite[] windupPool, Sprite[] idle)
        {
            var holdLight = HiloFramesOr(idle, "charge_light_l1", "charge_claw_l1");
            var holdHeavy = HiloFramesOr(idle, "charge_heavy_l1", "charge_light_l1");
            var holdGun = HiloFramesOr(idle, "charge_bionic_l1", "charge_light_l1");
            BakeClip("Hilo_ChargeHoldLight", holdLight, true, LoopLength(holdLight, 10f));
            BakeClip("Hilo_ChargeHoldHeavy", holdHeavy, true, LoopLength(holdHeavy, 10f));
            BakeClip("Hilo_ChargeHoldGun", holdGun, true, LoopLength(holdGun, 10f));

            for (int level = 1; level <= 3; level++)
            {
                // durations must match PlayerController.BuildChargedMove
                BakeAttackClip("Hilo_ChargeLight" + level,
                    HiloFramesOr(idle, $"charge_claw_l{level + 1}", $"charge_light_l{level + 1}", "claw_slash"), windupPool,
                    0.38f + 0.08f * level, projectile: false);
                BakeAttackClip("Hilo_ChargeHeavy" + level,
                    HiloFramesOr(idle, $"charge_heavy_l{level + 1}", $"charge_bionic_l{level + 1}", "axe_kick"), windupPool,
                    0.5f + 0.1f * level, projectile: false, slashWave: level >= 3);
                BakeAttackClip("Hilo_ChargeShot" + level,
                    HiloFramesOr(idle, $"charge_shot_l{level}", "quick_shot"), windupPool,
                    0.42f + 0.07f * level, projectile: true);
            }
        }

        // Hilo's awakened "shadow" move id -> art folder. awk_attacks is the
        // dedicated awakened strip; the rest reuse her flashiest base arts.
        private static readonly (string move, string folder)[] HiloAwakenedArtMap =
        {
            ("ShadowClaw", "awk_attacks"), ("ShadowRush", "claw_combo_a"), ("PhantomBarrage", "phantom_strike"),
            ("ShadowAxe", "axe_kick"), ("ShadowCyclone", "cyclone_kick"), ("ShadowPierce", "claw_thrust"),
            ("YinYangPalm", "rising_palm"), ("ShadowBeam", "phantom_beam"), ("ShadowBurst", "yinyang_burst"),
            ("OmniCannon", "wide_beam"), ("CloneAssault", "shadow_clone"), ("MissileStorm", "homing_missile"),
            ("ShadowHurricane", "hurricane_kick"), ("ShadowWave", "energy_wave"),
            ("ShadowKillCombo", "kill_combo"), ("FinalYinYang", "yinyang_burst"), ("InfinityBeam", "wide_beam"),
            ("HiloShadowStrike", "phantom_strike"), ("HiloShadowClone", "shadow_clone"), ("HiloShadowBeam", "phantom_beam"),
            ("HiloAirDragonKick", "air_dragon_kick"), ("HiloAirBionicStrike", "air_bionic_strike")
        };

        private static void BakeHiloAwakened(Sprite[] baseIdle, Sprite[] crouch, Sprite[] slip)
        {
            var idle = HiloFramesOr(baseIdle, "awk_idle");
            var movement = HiloFramesOr(new Sprite[0], "awk_movement");
            var walkSrc = movement.Length > 0 ? movement : LoadFrames($"{HiloDir}/walk");
            var runSrc = movement.Length > 0 ? movement : LoadFrames($"{HiloDir}/run");
            var transform = HiloFramesOr(idle, "yinyang_burst");

            var idleClip = BakeClip("HiloAwakened_Idle", idle, true, LoopLength(idle, IdleFps));
            var walkClip = BakeClip("HiloAwakened_Walk", walkSrc, true, LoopLength(walkSrc, WalkFps));
            var runClip = BakeClip("HiloAwakened_Run", runSrc, true, LoopLength(runSrc, 14f));
            var hurtClip = BakeClip("HiloAwakened_Hurt", crouch.Length > 0 ? crouch : idle, false, 0.3f);
            var knockdownClip = BakeClip("HiloAwakened_Knockdown", slip.Length > 0 ? slip : crouch, false, 0.5f);
            var getUpClip = BakeClip("HiloAwakened_GetUp", crouch.Length > 0 ? crouch : idle, false, 0.4f);
            var deadClip = BakeClip("HiloAwakened_Dead", slip.Length > 0 ? slip : crouch, false, 0.6f);
            var transformClip = BakeClip("HiloAwakened_Transform", transform, false,
                Mathf.Clamp(transform.Length / 24f, 0.8f, 1.8f));

            string path = $"{AnimDir}/HiloAwakenedAnimator.controller";
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("BackPedal", AnimatorControllerParameterType.Bool);
            foreach (var trigger in new[] { "Hurt", "Knockdown", "GetUp", "Dead", "Jump", "Transform" })
                controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;
            var idleState = sm.AddState("Idle");
            idleState.motion = idleClip;
            sm.defaultState = idleState;
            var walkState = sm.AddState("Walk");
            walkState.motion = walkClip;
            var runState = sm.AddState("Run");
            runState.motion = runClip;

            var toWalk = idleState.AddTransition(walkState);
            toWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "MoveSpeed");
            toWalk.hasExitTime = false;
            toWalk.duration = 0f;
            var toIdle = walkState.AddTransition(idleState);
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "MoveSpeed");
            toIdle.hasExitTime = false;
            toIdle.duration = 0f;
            var walkToRun = walkState.AddTransition(runState);
            walkToRun.AddCondition(AnimatorConditionMode.Greater, 6f, "MoveSpeed");
            walkToRun.hasExitTime = false;
            walkToRun.duration = 0f;
            var runToWalk = runState.AddTransition(walkState);
            runToWalk.AddCondition(AnimatorConditionMode.Less, 6f, "MoveSpeed");
            runToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "MoveSpeed");
            runToWalk.hasExitTime = false;
            runToWalk.duration = 0f;
            var runToIdle = runState.AddTransition(idleState);
            runToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "MoveSpeed");
            runToIdle.hasExitTime = false;
            runToIdle.duration = 0f;

            void AddTriggerState(string trigger, AnimationClip clip, bool backToIdle = true)
            {
                var state = sm.AddState(trigger);
                state.motion = clip;
                var t = sm.AddAnyStateTransition(state);
                t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
                t.hasExitTime = false;
                t.duration = 0f;
                t.canTransitionToSelf = false;
                if (backToIdle)
                {
                    var back = state.AddTransition(idleState);
                    back.hasExitTime = true;
                    back.exitTime = 1f;
                    back.duration = 0f;
                }
            }

            AddTriggerState("Transform", transformClip);
            AddTriggerState("Hurt", hurtClip);
            controller.AddParameter("HurtHeavy", AnimatorControllerParameterType.Trigger);
            AddTriggerState("HurtHeavy", BakeClip("HiloAwakened_HurtHeavy",
                HiloFramesOr(crouch.Length > 0 ? crouch : idle, "yin_slip"), false, 0.38f));
            AddTriggerState("Dead", deadClip, backToIdle: false);
            AddTriggerState("Jump", BakeClip("HiloAwakened_Jump", HiloFramesOr(idle, "jump"), false, 0.6f));

            // awakened dash = shadow-step blink
            controller.AddParameter("Dash", AnimatorControllerParameterType.Trigger);
            var blink = HiloFramesOr(idle, "shadow_step", "dash");
            AddTriggerState("Dash", BakeClip("HiloAwakened_DashAnim", blink, false, 0.22f));

            var downState = sm.AddState("Knockdown");
            downState.motion = knockdownClip;
            var downT = sm.AddAnyStateTransition(downState);
            downT.AddCondition(AnimatorConditionMode.If, 0f, "Knockdown");
            downT.hasExitTime = false;
            downT.duration = 0f;
            var getUpState = sm.AddState("GetUp");
            getUpState.motion = getUpClip;
            var getUpT = downState.AddTransition(getUpState);
            getUpT.AddCondition(AnimatorConditionMode.If, 0f, "GetUp");
            getUpT.hasExitTime = false;
            getUpT.duration = 0f;
            var getUpBack = getUpState.AddTransition(idleState);
            getUpBack.hasExitTime = true;
            getUpBack.exitTime = 1f;
            getUpBack.duration = 0f;

            foreach (var move in SetupTools.HiloAwakenedMoves.Concat(SetupTools.HiloTeleportMoves)
                         .Concat(SetupTools.HiloAwakenedAirMoves))
            {
                if (controller.parameters.Any(p => p.name == move.trigger)) continue;
                controller.AddParameter(move.trigger, AnimatorControllerParameterType.Trigger);

                string folder = HiloAwakenedArtMap.FirstOrDefault(m => m.move == move.id).folder;
                var frames = folder != null ? LoadFrames($"{HiloDir}/{folder}") : new Sprite[0];
                if (frames.Length == 0) frames = new[] { idle[0] };

                var clip = BakeAttackClip("HiloAwakened_" + move.id, frames, idle, move.length,
                    move.projectile, move.slashWave);
                AddTriggerState(move.trigger, clip);
            }

            // Charge support while awakened: same trigger names as the base
            // controller so PlayerController stays controller-agnostic.
            var awkHoldFrames = HiloFramesOr(idle, "charge_bionic_l1", "charge_light_l1");
            var awkHold = BakeClip("HiloAwakened_ChargeHold", awkHoldFrames, true, LoopLength(awkHoldFrames, 10f));
            foreach (var holdTrigger in new[] { "ChargeHoldLight", "ChargeHoldHeavy", "ChargeHoldGun" })
            {
                controller.AddParameter(holdTrigger, AnimatorControllerParameterType.Trigger);
                AddTriggerState(holdTrigger, awkHold, backToIdle: false);
            }

            for (int level = 1; level <= 3; level++)
            {
                controller.AddParameter("ChargeLight" + level, AnimatorControllerParameterType.Trigger);
                AddTriggerState("ChargeLight" + level, BakeAttackClip("HiloAwakened_ChargeLight" + level,
                    HiloFramesOr(idle, $"charge_claw_l{level + 1}", "claw_slash"), idle,
                    0.38f + 0.08f * level, projectile: false));

                controller.AddParameter("ChargeHeavy" + level, AnimatorControllerParameterType.Trigger);
                AddTriggerState("ChargeHeavy" + level, BakeAttackClip("HiloAwakened_ChargeHeavy" + level,
                    HiloFramesOr(idle, $"charge_heavy_l{level + 1}", "axe_kick"), idle,
                    0.5f + 0.1f * level, projectile: false, slashWave: level >= 3));

                controller.AddParameter("ChargeShot" + level, AnimatorControllerParameterType.Trigger);
                AddTriggerState("ChargeShot" + level, BakeAttackClip("HiloAwakened_ChargeShot" + level,
                    HiloFramesOr(idle, $"charge_shot_l{level}", "quick_shot"), idle,
                    0.42f + 0.07f * level, projectile: true));
            }

            EditorUtility.SetDirty(controller);

            // Wire controller + move sets into the Hilo prefab.
            var awakenedSet = AssetDatabase.LoadAssetAtPath<Object>("Assets/Data/HiloAwakenedMoveSet.asset");
            var airSet = AssetDatabase.LoadAssetAtPath<Object>("Assets/Data/HiloAirMoveSet.asset");
            var awakenedAirSet = AssetDatabase.LoadAssetAtPath<Object>("Assets/Data/HiloAwakenedAirMoveSet.asset");
            var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Hilo.prefab");
            try
            {
                var player = root.GetComponent<Player.PlayerController>();
                var so = new SerializedObject(player);
                so.FindProperty("awakenedController").objectReferenceValue = controller;
                so.FindProperty("awakenedMoveSet").objectReferenceValue = awakenedSet;
                so.FindProperty("airMoveSet").objectReferenceValue = airSet;
                so.FindProperty("awakenedAirMoveSet").objectReferenceValue = awakenedAirSet;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Hilo.prefab");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Awakened move id -> extracted art folder (combo-set rows and the
        // standalone finishers from the awakened sheets).
        // Updated for the extraframes.png complete awakened set: full strips
        // (8-12 frames) replace the old single-pose combo cells where available.
        private static readonly (string move, string folder)[] AwakenedArtMap =
        {
            ("VoidSlash1", "awk_light1"),
            ("VoidSlash2", "awk_light2"),
            ("EchoTeleportCombo", "combo_a"),
            ("DimensionBreak", "awk_heavy1"),
            ("TeleportStrike", "awk_tp_strike_short"),
            ("ObliterationBurst", "awk_heavy2"),
            ("CrossDimensionCut", "combo_b"),
            ("TimeRiftSlash", "awk_heavy3"),
            ("VoidPiercer", "comboB_5"),
            ("AwakenedShot", "awk_shot_l1"),
            ("RapidVoidFire", "awk_shot_l2"),
            ("BladeStormBullet", "blade_storm_bullet"),
            ("SwordDrawShot", "quick_shot"),
            ("VoidSpearAssault", "comboC_4"),
            ("PhantomAssault", "combo_c"),
            ("TeleportFeint", "awk_tp_short"),
            ("TeleportImpact", "teleport_impact"),
            ("VoidSlasherFinisher", "combo_e"),
            ("InfiniteTeleportStorm", "combo_d"),
            ("FinalDomainVoidErasure", "fin_void_erasure"),
            ("EclipseSlash", "fin_eclipse_slash"),
            ("ApocalypseBlade", "fin_apocalypse_blade"),
            ("SilentRequiem", "fin_silent_requiem"),
            ("CelestialJudgment", "fin_celestial_judgment"),
            ("OmegaCut", "fin_omega_cut"),
            ("VoidErasure", "fin_void_erasure"),
            ("VoidAscension", "ultimate_void_ascension"),
            ("TeleportStrikeAtk", "awk_tp_strike_short"),
            ("TeleportHeavyAtk", "awk_tp_strike_long"),
            ("TeleportShootAtk", "awktp2_3"),
            ("AwakenedAirLight", "air_slash"),
            ("AwakenedAirHeavy", "air_spin_slash")
        };

        private static void BakeAwakened()
        {
            var idle = LoadFrames($"{AwakenedDir}/idle");
            var walk = LoadFrames($"{AwakenedDir}/walk");
            var run = LoadFrames($"{AwakenedDir}/run");
            var hurt = LoadFrames($"{AwakenedDir}/hurt");
            var knockdown = LoadFrames($"{AwakenedDir}/knockdown");
            var getup = LoadFrames($"{AwakenedDir}/getup");
            // Prefer the 47-frame extended strip from extraframes.png.
            var transform = LoadFrames($"{AwakenedDir}/transform_ext");
            if (transform.Length == 0)
                transform = new[]
                    {
                        "transform_phase1", "transform_phase2", "transform_phase3", "transform_phase4",
                        "transform_phase5", "transform_phase6", "transform_phase7"
                    }
                    .SelectMany(folder => LoadFrames($"{AwakenedDir}/{folder}"))
                    .ToArray();
            if (transform.Length == 0)
                transform = LoadFrames($"{AwakenedDir}/transform2a")
                    .Concat(LoadFrames($"{AwakenedDir}/transform2b")).ToArray();
            if (transform.Length == 0) transform = LoadFrames($"{AwakenedDir}/transform");
            if (idle.Length == 0)
            {
                Debug.LogWarning("SpriteBaker: no awakened frames found, skipping awakened bake");
                return;
            }

            var idleClip = BakeClip("Awakened_Idle", idle, true, LoopLength(idle, IdleFps));
            var walkSrc = walk.Length > 0 ? walk : run;
            var walkClip = BakeClip("Awakened_Walk", walkSrc, true, LoopLength(walkSrc, WalkFps));
            var awakenedRun = LoadFrames($"{AwakenedDir}/awkrun_2");
            var runSrc = awakenedRun.Length > 0 ? awakenedRun : run.Length > 0 ? run : walk;
            var runClip = BakeClip("Awakened_Run", runSrc, true, LoopLength(runSrc, 14f));
            var hurtClip = BakeClip("Awakened_Hurt", hurt, false, 0.3f);
            var knockdownClip = BakeClip("Awakened_Knockdown", knockdown, false, 0.5f);
            var getUpClip = BakeClip("Awakened_GetUp", getup, false, 0.4f);
            var deadClip = BakeClip("Awakened_Dead", knockdown, false, 0.6f);
            var transformClip = BakeClip("Awakened_Transform", transform, false,
                Mathf.Clamp(transform.Length / 24f, 0.8f, 1.8f));

            string path = $"{AnimDir}/AwakenedAnimator.controller";
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("BackPedal", AnimatorControllerParameterType.Bool); // lock-on flag
            foreach (var trigger in new[] { "Hurt", "Knockdown", "GetUp", "Dead", "Jump", "Transform" })
                controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;
            var idleState = sm.AddState("Idle");
            idleState.motion = idleClip;
            sm.defaultState = idleState;
            var walkState = sm.AddState("Walk");
            walkState.motion = walkClip;
            var runState = sm.AddState("Run");
            runState.motion = runClip;

            var toWalk = idleState.AddTransition(walkState);
            toWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "MoveSpeed");
            toWalk.hasExitTime = false;
            toWalk.duration = 0f;
            var toIdle = walkState.AddTransition(idleState);
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "MoveSpeed");
            toIdle.hasExitTime = false;
            toIdle.duration = 0f;
            var walkToRun = walkState.AddTransition(runState);
            walkToRun.AddCondition(AnimatorConditionMode.Greater, 6f, "MoveSpeed");
            walkToRun.hasExitTime = false;
            walkToRun.duration = 0f;
            var runToWalk = runState.AddTransition(walkState);
            runToWalk.AddCondition(AnimatorConditionMode.Less, 6f, "MoveSpeed");
            runToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "MoveSpeed");
            runToWalk.hasExitTime = false;
            runToWalk.duration = 0f;
            var runToIdle = runState.AddTransition(idleState);
            runToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "MoveSpeed");
            runToIdle.hasExitTime = false;
            runToIdle.duration = 0f;

            void AddTriggerState(string trigger, AnimationClip clip, bool backToIdle = true)
            {
                var state = sm.AddState(trigger);
                state.motion = clip;
                var t = sm.AddAnyStateTransition(state);
                t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
                t.hasExitTime = false;
                t.duration = 0f;
                t.canTransitionToSelf = false;
                if (backToIdle)
                {
                    var back = state.AddTransition(idleState);
                    back.hasExitTime = true;
                    back.exitTime = 1f;
                    back.duration = 0f;
                }
            }

            AddTriggerState("Transform", transformClip);
            AddTriggerState("Hurt", hurtClip);
            controller.AddParameter("HurtHeavy", AnimatorControllerParameterType.Trigger);
            var awkHurtHeavy = LoadFrames($"{AwakenedDir}/hurt_heavy");
            AddTriggerState("HurtHeavy", BakeClip("Awakened_HurtHeavy",
                awkHurtHeavy.Length > 0 ? awkHurtHeavy : hurt, false, 0.38f));
            AddTriggerState("Dead", deadClip, backToIdle: false);
            var jumpFrames = LoadFrames($"{AwakenedDir}/jump");
            if (jumpFrames.Length == 0) jumpFrames = run.Length > 0 ? run : idle;
            AddTriggerState("Jump", BakeClip("Awakened_Jump", jumpFrames, false, 0.6f));

            // awakened dash = blink teleport (vanish/transit/appear phases)
            controller.AddParameter("Dash", AnimatorControllerParameterType.Trigger);
            var blink = LoadFrames($"{AwakenedDir}/awk_tp_short");
            if (blink.Length == 0)
                blink = LoadFrames($"{AwakenedDir}/awktp2_1")
                    .Concat(LoadFrames($"{AwakenedDir}/awktp2_3")).ToArray();
            if (blink.Length == 0) blink = LoadFrames($"{AwakenedDir}/vanish");
            AddTriggerState("Dash", BakeClip("Awakened_DashAnim", blink, false, 0.22f));

            var downState = sm.AddState("Knockdown");
            downState.motion = knockdownClip;
            var downT = sm.AddAnyStateTransition(downState);
            downT.AddCondition(AnimatorConditionMode.If, 0f, "Knockdown");
            downT.hasExitTime = false;
            downT.duration = 0f;
            var getUpState = sm.AddState("GetUp");
            getUpState.motion = getUpClip;
            var getUpT = downState.AddTransition(getUpState);
            getUpT.AddCondition(AnimatorConditionMode.If, 0f, "GetUp");
            getUpT.hasExitTime = false;
            getUpT.duration = 0f;
            var getUpBack = getUpState.AddTransition(idleState);
            getUpBack.hasExitTime = true;
            getUpBack.exitTime = 1f;
            getUpBack.duration = 0f;

            // One state per awakened move (plus the teleport context attacks),
            // with art from the sheets.
            foreach (var move in SetupTools.AwakenedMoves.Concat(SetupTools.TeleportMoves)
                         .Concat(SetupTools.AwakenedAirMoves))
            {
                controller.AddParameter(move.trigger, AnimatorControllerParameterType.Trigger);

                string folder = AwakenedArtMap.FirstOrDefault(m => m.move == move.id).folder;
                var frames = folder != null ? LoadFrames($"{AwakenedDir}/{folder}") : new Sprite[0];
                if (frames.Length == 0) frames = new[] { idle[0] };

                var clip = BakeAttackClip("Awakened_" + move.id, frames, idle, move.length,
                    move.projectile, move.slashWave);
                AddTriggerState(move.trigger, clip);
            }

            // Charge support while awakened: same trigger names as the base
            // controller so PlayerController stays controller-agnostic.
            Sprite[] AwkFramesOr(params string[] folders)
            {
                foreach (var folder in folders)
                {
                    var f = LoadFrames($"{AwakenedDir}/{folder}");
                    if (f.Length > 0) return f;
                }
                return idle;
            }

            var awkHoldFrames = AwkFramesOr("crouch", "charge_awk_light");
            var awkHold = BakeClip("Awakened_ChargeHold", awkHoldFrames, true, LoopLength(awkHoldFrames, 10f));
            foreach (var holdTrigger in new[] { "ChargeHoldLight", "ChargeHoldHeavy", "ChargeHoldGun" })
            {
                controller.AddParameter(holdTrigger, AnimatorControllerParameterType.Trigger);
                AddTriggerState(holdTrigger, awkHold, backToIdle: false);
            }

            for (int level = 1; level <= 3; level++)
            {
                controller.AddParameter("ChargeLight" + level, AnimatorControllerParameterType.Trigger);
                AddTriggerState("ChargeLight" + level, BakeAttackClip("Awakened_ChargeLight" + level,
                    AwkFramesOr($"charge_awk_light_l{level}", "charge_awk_light"), idle,
                    0.38f + 0.08f * level, projectile: false));

                controller.AddParameter("ChargeHeavy" + level, AnimatorControllerParameterType.Trigger);
                AddTriggerState("ChargeHeavy" + level, BakeAttackClip("Awakened_ChargeHeavy" + level,
                    AwkFramesOr($"charge_awk_heavy_l{level}", "charge_awk_heavy"), idle,
                    0.5f + 0.1f * level, projectile: false, slashWave: level >= 3));

                controller.AddParameter("ChargeShot" + level, AnimatorControllerParameterType.Trigger);
                AddTriggerState("ChargeShot" + level, BakeAttackClip("Awakened_ChargeShot" + level,
                    AwkFramesOr($"charge_awk_gun_l{level}", $"awk_shot_l{level}", "charge_awk_gun"), idle,
                    0.42f + 0.07f * level, projectile: true));
            }

            EditorUtility.SetDirty(controller);

            // Wire controller + move set into the player prefab.
            var awakenedSet = AssetDatabase.LoadAssetAtPath<Object>("Assets/Data/AwakenedMoveSet.asset");
            var airSet = AssetDatabase.LoadAssetAtPath<Object>("Assets/Data/AirMoveSet.asset");
            var awakenedAirSet = AssetDatabase.LoadAssetAtPath<Object>("Assets/Data/AwakenedAirMoveSet.asset");
            var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player.prefab");
            try
            {
                var player = root.GetComponent<Player.PlayerController>();
                var so = new SerializedObject(player);
                so.FindProperty("awakenedController").objectReferenceValue = controller;
                so.FindProperty("awakenedMoveSet").objectReferenceValue = awakenedSet;
                so.FindProperty("airMoveSet").objectReferenceValue = airSet;
                so.FindProperty("awakenedAirMoveSet").objectReferenceValue = awakenedAirSet;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Player.prefab");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private class EnemyAnims
        {
            public string folder;
            public string prefab;
            public string[] attacks = { "attack" };  // melee variants -> Attack, Attack2, ...
            public string special = "attack2";
            public string shoot = "shoot";
            public string hurt = "hurt";
            public string hurtHeavy = "heavy_hit";
            public string knockdown = "knockdown";
            public string getup = "getup";
            public string death = "death";
        }

        // v2 extraction folder maps: every usable strip from the sheets gets a
        // state. Locomotion (idle/walk/run) is read by convention.
        private static readonly EnemyAnims[] Enemies =
        {
            new EnemyAnims { folder = "Werewolf", prefab = "Assets/Prefabs/Enemy_Werewolf.prefab",
                attacks = new[] { "claw_slash", "double_claw", "bite" }, special = "shred_burst",
                shoot = "spine_blade", hurt = "light_hit" },
            new EnemyAnims { folder = "Chimera", prefab = "Assets/Prefabs/Enemy_Chimera.prefab",
                attacks = new[] { "bite", "hyper_claw", "predator_pounce", "tail_whip" }, special = "tail_drill",
                shoot = "plasma_beam", hurt = "light_hit" },
            new EnemyAnims { folder = "Reaper", prefab = "Assets/Prefabs/Enemy_Reaper.prefab",
                attacks = new[] { "melee_slash", "dash_strike" }, special = "melee_slash",
                hurt = "take_damage" },
            new EnemyAnims { folder = "Sentinel", prefab = "Assets/Prefabs/Enemy_Sentinel.prefab",
                attacks = new[] { "punch_combo", "kick_combo", "slash_attack" }, special = "spin_slash",
                shoot = "rapid_fire", hurt = "light_hit" },
            new EnemyAnims { folder = "Samurai", prefab = "Assets/Prefabs/Enemy_Samurai.prefab",
                attacks = new[] { "slash1", "slash2", "thrust", "combo1", "combo2", "combo3" },
                special = "dragon_cut", shoot = "energy_wave", hurt = "light_hit",
                knockdown = "knocked_down", getup = "get_up", death = "death1" },
            new EnemyAnims { folder = "SamuraiDual", prefab = "Assets/Prefabs/Enemy_SamuraiDual.prefab",
                attacks = new[] { "slash1", "slash2", "thrust", "combo1", "combo2", "combo3" },
                special = "void_slash", shoot = "energy_wave", hurt = "light_hit",
                knockdown = "knocked_down", getup = "get_up", death = "death2" },
            new EnemyAnims { folder = "Guard", prefab = "Assets/Prefabs/Enemy_Guard.prefab",
                attacks = new[] { "melee" }, special = "special", hurt = "hit_big", hurtHeavy = "hurt_heavy", death = "death_big" },
            new EnemyAnims { folder = "Bruiser", prefab = "Assets/Prefabs/Enemy_Bruiser.prefab",
                attacks = new[] { "melee" }, special = "special", hurt = "hit_big", hurtHeavy = "hurt_heavy", death = "death_big" },
            new EnemyAnims { folder = "Titan", prefab = "Assets/Prefabs/Enemy_Titan.prefab",
                attacks = new[] { "melee" }, special = "special", hurt = "hit_big", hurtHeavy = "hurt_heavy", death = "death_big" }
        };

        private static void BakeEnemies()
        {
            foreach (var enemy in Enemies)
            {
                string dir = $"{EnemiesDir}/{enemy.folder}";
                var idle = LoadFrames($"{dir}/idle");
                if (idle.Length == 0)
                {
                    Debug.LogWarning($"SpriteBaker: no idle frames for {enemy.folder}, skipping");
                    continue;
                }
                var walk = LoadFrames($"{dir}/walk");
                var run = LoadFrames($"{dir}/run");
                if (walk.Length == 0) walk = run.Length > 0 ? run : idle;
                if (run.Length == 0) run = walk;

                var hurt = LoadFrames($"{dir}/{enemy.hurt}");
                if (hurt.Length == 0) hurt = idle;
                var hurtHeavy = LoadFrames($"{dir}/{enemy.hurtHeavy}");
                if (hurtHeavy.Length == 0) hurtHeavy = hurt;
                var knockdown = LoadFrames($"{dir}/{enemy.knockdown}");
                var death = LoadFrames($"{dir}/{enemy.death}");
                var getup = LoadFrames($"{dir}/{enemy.getup}");
                if (knockdown.Length == 0) knockdown = death.Length > 0 ? new[] { death[death.Length - 1] } : hurt;
                if (death.Length == 0) death = knockdown;
                if (getup.Length == 0) getup = hurt;

                var attackSets = enemy.attacks
                    .Select(folder => LoadFrames($"{dir}/{folder}"))
                    .Where(frames => frames.Length > 0)
                    .ToList();
                if (attackSets.Count == 0) attackSets.Add(idle);
                var special = LoadFrames($"{dir}/{enemy.special}");
                if (special.Length == 0) special = attackSets[attackSets.Count - 1];
                var shoot = LoadFrames($"{dir}/{enemy.shoot}");
                if (shoot.Length == 0) shoot = attackSets[0];

                string p = enemy.folder;
                var idleClip = BakeClip($"{p}_Idle", idle, true, LoopLength(idle, IdleFps));
                var walkClip = BakeClip($"{p}_Walk", walk, true, LoopLength(walk, WalkFps));
                var runClip = BakeClip($"{p}_Run", run, true, LoopLength(run, 14f));
                var attackClips = attackSets
                    .Select((frames, i) => BakeAttackClip($"{p}_Attack{(i == 0 ? "" : (i + 1).ToString())}",
                        frames, idle, 0.5f, projectile: false))
                    .ToArray();
                var specialClip = BakeAttackClip($"{p}_Special", special, idle, 0.65f, projectile: false);
                var shootClip = BakeAttackClip($"{p}_Shoot", shoot, idle, 0.5f, projectile: true);
                var hurtClip = BakeClip($"{p}_Hurt", hurt, false, 0.3f);
                var hurtHeavyClip = BakeClip($"{p}_HurtHeavy", hurtHeavy, false, 0.38f);
                var knockdownClip = BakeClip($"{p}_Knockdown", knockdown, false, 0.5f);
                var getUpClip = BakeClip($"{p}_GetUp", getup, false, 0.4f);
                var deadClip = BakeClip($"{p}_Dead", death, false, Mathf.Max(death.Length / Fps, 0.6f));

                var controller = BuildEnemyController(p, idleClip, walkClip, runClip, attackClips,
                    specialClip, shootClip, hurtClip, hurtHeavyClip, knockdownClip, getUpClip, deadClip);

                ApplyToPrefab(enemy.prefab, controller, idle[0], attackClips.Length);
            }
        }

        private static AnimatorController BuildEnemyController(string name, AnimationClip idle, AnimationClip walk,
            AnimationClip run, AnimationClip[] attacks, AnimationClip special, AnimationClip shoot,
            AnimationClip hurt, AnimationClip hurtHeavy, AnimationClip knockdown, AnimationClip getUp, AnimationClip dead)
        {
            string path = $"{AnimDir}/{name}Animator.controller";
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
            foreach (var trigger in new[] { "Attack", "Special", "Shoot", "Hurt", "Knockdown", "GetUp", "Dead" })
                controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;

            var idleState = sm.AddState("Idle");
            idleState.motion = idle;
            sm.defaultState = idleState;

            var walkState = sm.AddState("Walk");
            walkState.motion = walk;

            var toWalk = idleState.AddTransition(walkState);
            toWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "MoveSpeed");
            toWalk.hasExitTime = false;
            toWalk.duration = 0f;
            var toIdle = walkState.AddTransition(idleState);
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "MoveSpeed");
            toIdle.hasExitTime = false;
            toIdle.duration = 0f;

            // Run kicks in for fast movers (charges, lunges).
            var runState = sm.AddState("Run");
            runState.motion = run;
            var walkToRun = walkState.AddTransition(runState);
            walkToRun.AddCondition(AnimatorConditionMode.Greater, 4.4f, "MoveSpeed");
            walkToRun.hasExitTime = false;
            walkToRun.duration = 0f;
            var runToWalk = runState.AddTransition(walkState);
            runToWalk.AddCondition(AnimatorConditionMode.Less, 4.4f, "MoveSpeed");
            runToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "MoveSpeed");
            runToWalk.hasExitTime = false;
            runToWalk.duration = 0f;
            var runToIdle = runState.AddTransition(idleState);
            runToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "MoveSpeed");
            runToIdle.hasExitTime = false;
            runToIdle.duration = 0f;

            void AddTriggerState(string trigger, AnimationClip clip, bool backToIdle = true)
            {
                if (controller.parameters.All(par => par.name != trigger))
                    controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
                var state = sm.AddState(trigger);
                state.motion = clip;
                var t = sm.AddAnyStateTransition(state);
                t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
                t.hasExitTime = false;
                t.duration = 0f;
                t.canTransitionToSelf = false;
                if (backToIdle)
                {
                    var back = state.AddTransition(idleState);
                    back.hasExitTime = true;
                    back.exitTime = 1f;
                    back.duration = 0f;
                }
            }

            for (int i = 0; i < attacks.Length; i++)
                AddTriggerState(i == 0 ? "Attack" : "Attack" + (i + 1), attacks[i]);
            AddTriggerState("Special", special);
            AddTriggerState("Shoot", shoot);
            AddTriggerState("Hurt", hurt);
            AddTriggerState("HurtHeavy", hurtHeavy);
            AddTriggerState("Dead", dead, backToIdle: false);

            var downState = sm.AddState("Knockdown");
            downState.motion = knockdown;
            var downT = sm.AddAnyStateTransition(downState);
            downT.AddCondition(AnimatorConditionMode.If, 0f, "Knockdown");
            downT.hasExitTime = false;
            downT.duration = 0f;
            var getUpState = sm.AddState("GetUp");
            getUpState.motion = getUp;
            var getUpT = downState.AddTransition(getUpState);
            getUpT.AddCondition(AnimatorConditionMode.If, 0f, "GetUp");
            getUpT.hasExitTime = false;
            getUpT.duration = 0f;
            var getUpBack = getUpState.AddTransition(idleState);
            getUpBack.hasExitTime = true;
            getUpBack.exitTime = 1f;
            getUpBack.duration = 0f;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void ApplyToPrefab(string prefabPath, AnimatorController controller, Sprite idleSprite,
            int meleeVariants = 1)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) return;
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var visual = root.transform.Find("Visual");
                if (visual == null) return;
                visual.localScale = Vector3.one;
                visual.localPosition = Vector3.zero;
                var sr = visual.GetComponent<SpriteRenderer>();
                if (idleSprite != null) sr.sprite = idleSprite;
                visual.GetComponent<Animator>().runtimeAnimatorController = controller;

                var ai = root.GetComponent<Enemies.EnemyAI>();
                if (ai != null)
                {
                    var so = new SerializedObject(ai);
                    var prop = so.FindProperty("meleeVariants");
                    if (prop != null)
                    {
                        prop.intValue = meleeVariants;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AddBackgroundToScene()
        {
            // Copy the background into Assets if not already there.
            if (!Directory.Exists(BackgroundDir)) Directory.CreateDirectory(BackgroundDir);
            string destPath = $"{BackgroundDir}/city_ruins.png";
            if (!File.Exists(destPath))
            {
                var src = Directory.Exists("sprites/background")
                    ? Directory.GetFiles("sprites/background", "*.png").FirstOrDefault()
                    : null;
                if (src == null) return;
                File.Copy(src, destPath);
                AssetDatabase.ImportAsset(destPath, ImportAssetOptions.ForceSynchronousImport);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(destPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 85f;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            AssetDatabase.ImportAsset(destPath, ImportAssetOptions.ForceSynchronousImport);
            var bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(destPath);
            if (bgSprite == null)
            {
                Debug.LogError("SpriteBaker: background sprite failed to import at " + destPath);
                return;
            }

            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            var existing = GameObject.Find("CityBackground");
            if (existing != null) Object.DestroyImmediate(existing);

            var bgRoot = new GameObject("CityBackground");
            // Tile the backdrop along the level (player walks 0..40 units).
            for (int i = 0; i < 3; i++)
            {
                var tile = new GameObject("Tile" + i);
                tile.transform.SetParent(bgRoot.transform, false);
                var sr = tile.AddComponent<SpriteRenderer>();
                sr.sprite = bgSprite;
                sr.sortingOrder = -20000;
                float width = bgSprite.bounds.size.x;
                tile.transform.position = new Vector3(-5f + i * width, 0.8f, 0f);
            }

            EditorSceneManager.SaveScene(scene);
        }
    }
}
