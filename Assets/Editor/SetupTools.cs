using System.Collections.Generic;
using System.IO;
using System.Linq;
using SilverFang.Combat;
using SilverFang.Core;
using SilverFang.Enemies;
using SilverFang.Player;
using SilverFang.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SilverFang.EditorTools
{
    public static class SetupTools
    {
        private const string SpritesDir = "Assets/Art/Sprites";
        private const string AnimDir = "Assets/Art/Animations";
        private const string PrefabDir = "Assets/Prefabs";
        private const string DataDir = "Assets/Data";
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string UiDir = "Assets/Art/UI";
        private const string AudioDir = "Assets/Audio/Music";

        public class MoveSpec
        {
            public string id;
            public string trigger;
            public InputToken[] seq;
            public AttackData attack;
            public bool projectile;
            public bool slashWave;
            public TeleportKind teleport = TeleportKind.None;
            public float length = 0.4f;
        }

        // (sequence, move name, damage, knocksDown, firesProjectile)
        // Every L/H/G string up to length 3, plus length-4 finishers: 51 sword combos.
        private static readonly (string seq, string name, int dmg, bool kd, bool proj)[] SwordTable =
        {
            ("L",    "BasicSlash1",        8,  false, false),
            ("H",    "OverheadSlash",      16, true,  false),
            ("G",    "QuickShot",          0,  false, true),

            ("LL",   "BasicSlash2",        10, false, false),
            ("LH",   "RisingSlash",        14, true,  false),
            ("LG",   "Gunslinger",         0,  false, true),
            ("HL",   "GuardBreak",         12, false, false),
            ("HH",   "GreatCleave",        24, true,  false),
            ("HG",   "KickShot",           14, true,  true),
            ("GL",   "PistolWhip",         8,  false, false),
            ("GH",   "SheathStrike",       18, true,  false),
            ("GG",   "RapidShot",          0,  false, true),

            ("LLL",  "BasicSlash3",        14, true,  false),
            ("LLH",  "SpinSlash",          20, true,  false),
            ("LLG",  "SupportFire",        0,  false, true),
            ("LHL",  "TurningSlash",       15, false, false),
            ("LHH",  "DoubleSpinSlash",    22, true,  false),
            ("LHG",  "BulletRain",         0,  false, true),
            ("LGL",  "CrossSlash",         16, false, false),
            ("LGH",  "CrescentSlash",      20, true,  false),
            ("LGG",  "DualShot",           0,  false, true),
            ("HLL",  "DiagonalSlash",      14, false, false),
            ("HLH",  "BackhandSlash",      18, true,  false),
            ("HLG",  "PiercingVolley",     0,  false, true),
            ("HHL",  "DownwardStrike",     20, false, false),
            ("HHH",  "EarthSplitter",      28, true,  false),
            ("HHG",  "MortarKick",         16, true,  true),
            ("HGL",  "DisarmStrike",       15, false, false),
            ("HGH",  "GroundSlam",         24, true,  false),
            ("HGG",  "OverdriveShot",      0,  false, true),
            ("GLL",  "ForwardThrust",      14, false, false),
            ("GLH",  "LungeSlash",         18, true,  false),
            ("GLG",  "TripleTap",          0,  false, true),
            ("GHL",  "WaveSlash",          17, false, false),
            ("GHH",  "HorizontalWide",     22, true,  false),
            ("GHG",  "RicochetShot",       0,  false, true),
            ("GGL",  "SlideSlash",         14, false, false),
            ("GGH",  "UppercutSlash",      20, true,  false),
            ("GGG",  "BulletStorm",        0,  false, true),

            ("LLLL", "WhirlwindFinisher",  30, true,  false),
            ("LLLH", "ChargedSlash",       32, true,  false),
            ("LLLG", "DragonShot",         18, true,  true),
            ("LLHH", "SpinningLunge",      28, true,  false),
            ("LHHH", "BladeStorm",         34, true,  false),
            ("LHLH", "BlinkSlash",         28, true,  false),
            ("HHHH", "Execution",          40, true,  false),
            ("HHHL", "FullChargeSlash",    36, true,  false),
            ("HHLL", "DashSlash",          26, false, false),
            ("HGHG", "SpecialFinisher1",   35, true,  false),
            ("GGGG", "SupportBarrage",     0,  false, true),
            ("GGGH", "GunslingerFinisher", 30, true,  false),

            // Extended routes: one playable route for every remaining Silver attack strip.
            ("LGLH", "DualArcSlash",       24, true,  false),
            ("LGHL", "Finisher",           38, true,  false),
            ("GHLH", "SpecialFinisher3",   38, true,  false),
            ("HHHG", "ChargeUp",           10, false, false),
            ("HHLH", "PowerSlash",         34, true,  false),
            ("HHGL", "SwordCharge",        28, false, false),
            ("GGHG", "ClassicSlash",       18, false, false)
        };

        private static readonly MoveSpec[] SwordMoves = BuildMoves(SwordTable);

        // Awakened: teleport-heavy moveset from the awakened sheets. T = teleport kind.
        private static readonly (string seq, string name, int dmg, bool kd, bool proj, TeleportKind tp)[] AwakenedTable =
        {
            ("L",    "VoidSlash1",             14, false, false, TeleportKind.None),
            ("LL",   "VoidSlash2",             18, false, false, TeleportKind.None),
            ("LLL",  "EchoTeleportCombo",      26, true,  false, TeleportKind.BehindTarget),
            ("H",    "DimensionBreak",         24, true,  false, TeleportKind.None),
            ("LH",   "TeleportStrike",         22, true,  false, TeleportKind.BehindTarget),
            ("HH",   "ObliterationBurst",      32, true,  false, TeleportKind.None),
            ("LLH",  "CrossDimensionCut",      28, true,  false, TeleportKind.None),
            ("HL",   "TimeRiftSlash",          24, false, false, TeleportKind.None),
            ("HLH",  "VoidPiercer",            28, true,  false, TeleportKind.ForwardDash),
            ("G",    "AwakenedShot",           0,  false, true,  TeleportKind.None),
            ("GG",   "RapidVoidFire",          0,  false, true,  TeleportKind.None),
            ("GGG",  "BladeStormBullet",       0,  false, true,  TeleportKind.None),
            ("LG",   "SwordDrawShot",          0,  false, true,  TeleportKind.None),
            ("HG",   "VoidSpearAssault",       26, true,  false, TeleportKind.ForwardDash),
            ("GH",   "PhantomAssault",         26, true,  false, TeleportKind.BehindTarget),
            ("LHL",  "TeleportFeint",          18, false, false, TeleportKind.ForwardDash),
            ("HHL",  "TeleportImpact",         26, true,  false, TeleportKind.None),
            ("GGH",  "VoidSlasherFinisher",    34, true,  false, TeleportKind.None),
            ("LLLL", "InfiniteTeleportStorm",  38, true,  false, TeleportKind.BehindTarget),
            ("HHHH", "FinalDomainVoidErasure", 50, true,  false, TeleportKind.None),
            // standalone finishers from the awakened combos sheet
            ("LLHH", "EclipseSlash",           36, true,  false, TeleportKind.None),
            ("HHLL", "ApocalypseBlade",        38, true,  false, TeleportKind.ForwardDash),
            ("LHHL", "SilentRequiem",          34, true,  false, TeleportKind.BehindTarget),
            ("HLLH", "CelestialJudgment",      40, true,  false, TeleportKind.None),
            ("GGHH", "OmegaCut",               42, true,  false, TeleportKind.ForwardDash),
            ("LGLG", "VoidErasure",            44, true,  false, TeleportKind.None),
            ("GLGL", "VoidAscension",          52, true,  false, TeleportKind.BehindTarget)
        };

        // contextual attacks fired out of a dash, sprint, or awakened teleport
        private static readonly (string seq, string name, int dmg, bool kd, bool proj)[] DashTable =
        {
            ("L", "DashSlashAtk", 12, false, false),
            ("LL", "DashThrustAtk", 14, false, false),
            ("LH", "DashUppercutAtk", 18, true, false),
            ("LLH", "DashSpinAtk", 20, true, false),
            ("H", "DashHeavyAtk", 18, true,  false),
            ("G", "DashShootAtk", 0,  false, true)
        };

        private static readonly (string seq, string name, int dmg, bool kd, bool proj)[] SprintTable =
        {
            ("L", "SprintSlashAtk", 14, false, false),
            ("LL", "SprintThrustAtk", 16, false, false),
            ("LH", "SprintSpinAtk", 22, true, false),
            ("H", "SprintHeavyAtk", 22, true,  false),
            ("G", "SprintShootAtk", 0,  false, true)
        };

        private static readonly (string seq, string name, int dmg, bool kd, bool proj)[] TeleportTable =
        {
            ("L", "TeleportStrikeAtk", 26, true, false),
            ("H", "TeleportHeavyAtk",  30, true, false),
            ("G", "TeleportShootAtk",  0,  false, true)
        };

        public static readonly MoveSpec[] DashMoves = BuildMoves(DashTable);
        public static readonly MoveSpec[] SprintMoves = BuildMoves(SprintTable);
        public static readonly MoveSpec[] TeleportMoves = BuildMoves(TeleportTable);
        public static readonly MoveSpec[] AirMoves =
        {
            new MoveSpec { id = "AirLightSlash", trigger = "AirLightSlash", seq = new[] { InputToken.Light },
                attack = new AttackData { damage = 12, hitstun = 0.28f, knockback = new Vector2(2.5f, 0.5f) }, length = 0.34f },
            new MoveSpec { id = "AirHeavySlash", trigger = "AirHeavySlash", seq = new[] { InputToken.Heavy },
                attack = new AttackData { damage = 22, hitstun = 0.42f, knockback = new Vector2(4.5f, -1f), knocksDown = true }, length = 0.48f },
            // air chains (aerial_spin / air_jump_slash / jumping_slash strips)
            new MoveSpec { id = "AirRisingSpin", trigger = "AirRisingSpin", seq = new[] { InputToken.Light, InputToken.Light },
                attack = new AttackData { damage = 16, hitstun = 0.32f, knockback = new Vector2(3f, 1f), hitsBothSides = true, rangeScale = 1.4f }, length = 0.4f },
            new MoveSpec { id = "AirJumpSlash", trigger = "AirJumpSlash", seq = new[] { InputToken.Light, InputToken.Heavy },
                attack = new AttackData { damage = 20, hitstun = 0.4f, knockback = new Vector2(4f, -0.5f) }, length = 0.42f },
            new MoveSpec { id = "JumpingSlash", trigger = "JumpingSlash", seq = new[] { InputToken.Heavy, InputToken.Heavy },
                attack = new AttackData { damage = 26, hitstun = 0.46f, knockback = new Vector2(5f, -2f), knocksDown = true, heightScale = 1.4f }, length = 0.5f }
        };
        public static readonly MoveSpec[] AwakenedAirMoves =
        {
            new MoveSpec { id = "AwakenedAirLight", trigger = "AwakenedAirLight", seq = new[] { InputToken.Light },
                attack = new AttackData { damage = 18, hitstun = 0.3f, knockback = new Vector2(3.5f, 0.6f) }, length = 0.34f },
            new MoveSpec { id = "AwakenedAirHeavy", trigger = "AwakenedAirHeavy", seq = new[] { InputToken.Heavy },
                attack = new AttackData { damage = 32, hitstun = 0.48f, knockback = new Vector2(6f, -1.2f), knocksDown = true }, length = 0.52f }
        };

        public static readonly MoveSpec[] AwakenedMoves = BuildAwakenedMoves();

        /// Every move that gets a state in the base PlayerAnimator, deduped by id
        /// (some moves appear in both the sword and gun tables).
        public static IEnumerable<MoveSpec> AllPlayerMoveSpecs =>
            SwordMoves.Concat(GunMoves).Concat(DashMoves).Concat(SprintMoves)
                .Concat(TeleportMoves).Concat(AirMoves)
                .GroupBy(m => m.id).Select(g => g.First());

        private static MoveSpec[] BuildAwakenedMoves()
        {
            var basic = BuildMoves(AwakenedTable.Select(t => (t.seq, t.name, t.dmg, t.kd, t.proj)).ToArray());
            for (int i = 0; i < basic.Length; i++)
                basic[i].teleport = AwakenedTable[i].tp;
            return basic;
        }

        private static MoveSpec[] BuildMoves((string seq, string name, int dmg, bool kd, bool proj)[] table)
        {
            var result = new MoveSpec[table.Length];
            for (int i = 0; i < table.Length; i++)
            {
                var (seq, name, dmg, kd, proj) = table[i];
                var tokens = new InputToken[seq.Length];
                for (int j = 0; j < seq.Length; j++)
                {
                    tokens[j] = seq[j] switch
                    {
                        'L' => InputToken.Light,
                        'H' => InputToken.Heavy,
                        _ => InputToken.Gun
                    };
                }

                // Rising/uppercut style moves launch into a juggle instead of knocking down.
                bool launches = name.Contains("Rising") || name.Contains("Uppercut");

                // Hitbox shape from the move's nature so swipes cover their real arc:
                // spins hit all around, thrusts reach far and thin, cleaves swing tall.
                bool spin = name.Contains("Spin") || name.Contains("Whirlwind") || name.Contains("Storm")
                            || name.Contains("Turning");
                bool thrust = name.Contains("Thrust") || name.Contains("Lunge") || name.Contains("Piercer")
                              || name.Contains("Slide");
                bool overhead = name.Contains("Overhead") || name.Contains("Slam") || name.Contains("Splitter")
                                || name.Contains("Cleave") || name.Contains("Downward") || name.Contains("Execution");
                bool wide = name.Contains("Wide") || name.Contains("Wave") || name.Contains("Crescent")
                            || name.Contains("Cross") || name.Contains("Arc");
                float range = thrust ? 1.9f : wide ? 1.6f : spin ? 1.5f : overhead ? 1.3f : 1.2f;
                float height = overhead ? 1.6f : spin ? 1.3f : thrust ? 0.9f : 1.15f;

                result[i] = new MoveSpec
                {
                    id = name,
                    trigger = name,
                    seq = tokens,
                    projectile = proj,
                    slashWave = name.Contains("Wave"),
                    length = Mathf.Clamp(0.3f + dmg * 0.008f + seq.Length * 0.02f, 0.3f, 0.75f),
                    attack = new AttackData
                    {
                        damage = dmg > 0 ? dmg : 6,
                        hitstun = Mathf.Clamp(0.2f + dmg * 0.008f, 0.2f, 0.55f),
                        knockback = new Vector2(Mathf.Clamp(dmg * (kd ? 0.28f : 0.2f), 1.5f, 9f), 0f),
                        knocksDown = kd && !launches,
                        launch = launches ? 7.5f : 0f,
                        rangeScale = range,
                        heightScale = height,
                        hitsBothSides = spin
                    }
                };
            }
            return result;
        }

        private static readonly (string seq, string name, int dmg, bool kd, bool proj)[] GunTable =
        {
            ("G",    "Shoot",              0,  false, true),
            ("L",    "PistolWhip",         8,  false, false),
            ("H",    "KickShot",           14, true,  true),
            ("GG",   "RapidShot",          0,  false, true),
            ("GL",   "TripleTap",          0,  false, true),
            ("GH",   "RicochetShot",       0,  false, true),
            ("LG",   "Gunslinger",         0,  false, true),
            ("HG",   "MortarKick",         16, true,  true),
            ("LL",   "SlideSlash",         14, false, false),
            ("LH",   "PiercingVolley",     0,  false, true),
            ("HH",   "DragonShot",         18, true,  true),
            ("GGG",  "SupportFire",        0,  false, true),
            ("GGL",  "DualShot",           0,  false, true),
            ("GGH",  "OverdriveShot",      0,  false, true),
            ("GLG",  "BulletRain",         0,  false, true),
            ("GHG",  "BulletStorm",        0,  false, true),
            ("LGG",  "SupportBarrage",     0,  false, true),
            ("HGG",  "GunslingerFinisher", 30, true,  false)
        };

        private static readonly MoveSpec[] GunMoves = BuildMoves(GunTable);

        // ---------------------------------------------------------------
        // Hilo: claw / martial-arts brawler with bionic-energy ranged moves.
        // Same combo grammar as Silver (L/H/G strings); every extracted
        // sprites/hilo folder gets at least one playable route.
        // ---------------------------------------------------------------
        private static readonly (string seq, string name, int dmg, bool kd, bool proj)[] HiloComboTable =
        {
            ("L",    "ClawJab",            8,  false, false),
            ("H",    "PowerStraight",      16, true,  false),
            ("G",    "HiloQuickShot",      0,  false, true),

            ("LL",   "ClawCross",          10, false, false),
            ("LH",   "RisingPalm",         14, true,  false),
            ("LG",   "PhantomBeam",        0,  false, true),
            ("HL",   "FrontKick",          12, false, false),
            ("HH",   "AxeKick",            24, true,  false),
            ("HG",   "BurstCannon",        0,  false, true),
            ("GL",   "SideKick",           10, false, false),
            ("GH",   "BackKick",           18, true,  false),
            ("GG",   "WideBeam",           0,  false, true),

            ("LLL",  "ClawFlurry",         14, true,  false),
            ("LLH",  "ClawUppercut",       20, true,  false),
            ("LLG",  "EnergyWave",         16, false, false),
            ("LHL",  "SnapKick",           15, false, false),
            ("LHH",  "SpinningClaw",       22, true,  false),
            ("LHG",  "HomingMissile",      0,  false, true),
            ("LGL",  "ClawSlash",          16, false, false),
            ("LGH",  "ClawSpin",           20, true,  false),
            ("LGG",  "YinYangBurst",       0,  false, true),
            ("HLL",  "RoundhouseKick",     14, false, false),
            ("HLH",  "FlyingRoundhouse",   18, true,  false),
            ("HLG",  "PhantomVolley",      0,  false, true),
            ("HHL",  "DoubleRoundhouse",   20, false, false),
            ("HHH",  "HurricaneKick",      28, true,  false),
            ("HHG",  "EnergyClaw",         18, true,  false),
            ("HGL",  "ClawThrust",         15, false, false),
            ("HGH",  "CycloneKick",        24, true,  false),
            ("HGG",  "MissileBarrage",     0,  false, true),
            ("GLL",  "BionicComboA",       14, false, false),
            ("GLH",  "FlyingSideKick",     18, true,  false),
            ("GLG",  "CannonVolley",       0,  false, true),
            ("GHL",  "SpinningKick",       17, false, false),
            ("GHH",  "JumpKick",           22, true,  false),
            ("GHG",  "PhantomStrike",      26, true,  false),
            ("GGL",  "BionicComboB",       14, false, false),
            ("GGH",  "TwinClawCombo",      20, true,  false),
            ("GGG",  "OmniBeam",           0,  false, true),

            ("LLLL", "KillCombo",          34, true,  false),
            ("HHHH", "SavageClawFinisher", 40, true,  false),
            ("GGGG", "YinYangOverdrive",   0,  false, true),
            ("LHLH", "PhantomDance",       28, true,  false),
            ("HHLL", "CycloneFinisher",    32, true,  false),
            ("GGHH", "ShadowCloneStrike",  36, true,  false)
        };

        private static readonly MoveSpec[] HiloComboMoves = BuildMoves(HiloComboTable);

        // Energy stance: bionic projectiles up front (gun-stance analog).
        private static readonly (string seq, string name, int dmg, bool kd, bool proj)[] HiloEnergyTable =
        {
            ("G",    "HiloQuickShot",    0,  false, true),
            ("L",    "ClawJab",          8,  false, false),
            ("H",    "AxeKick",          24, true,  false),
            ("GG",   "BurstCannon",      0,  false, true),
            ("GL",   "HomingMissile",    0,  false, true),
            ("GH",   "PhantomBeam",      0,  false, true),
            ("LG",   "EnergyWave",       16, false, false),
            ("HG",   "YinYangBurst",     0,  false, true),
            ("LL",   "ClawCross",        10, false, false),
            ("LH",   "RisingPalm",       14, true,  false),
            ("HH",   "CycloneKick",      24, true,  false),
            ("GGG",  "WideBeam",         0,  false, true),
            ("GGL",  "MissileBarrage",   0,  false, true),
            ("GGH",  "OmniBeam",         0,  false, true),
            ("GGGG", "YinYangOverdrive", 0,  false, true)
        };

        private static readonly MoveSpec[] HiloEnergyMoves = BuildMoves(HiloEnergyTable);

        private static readonly (string seq, string name, int dmg, bool kd, bool proj)[] HiloDashTable =
        {
            ("L",  "HiloDashClaw", 12, false, false),
            ("LL", "HiloDashRush", 14, false, false),
            ("LH", "HiloDashSpin", 20, true,  false),
            ("H",  "HiloDashKick", 18, true,  false),
            ("G",  "HiloDashShot", 0,  false, true)
        };

        private static readonly (string seq, string name, int dmg, bool kd, bool proj)[] HiloSprintTable =
        {
            ("L",  "HiloSprintKick",      14, false, false),
            ("LL", "HiloSprintRush",      16, false, false),
            ("LH", "HiloSprintCyclone",   22, true,  false),
            ("H",  "HiloSprintHurricane", 22, true,  false),
            ("G",  "HiloSprintCannon",    0,  false, true)
        };

        private static readonly (string seq, string name, int dmg, bool kd, bool proj)[] HiloTeleportTable =
        {
            ("L", "HiloShadowStrike", 26, true,  false),
            ("H", "HiloShadowClone",  30, true,  false),
            ("G", "HiloShadowBeam",   0,  false, true)
        };

        public static readonly MoveSpec[] HiloDashMoves = BuildMoves(HiloDashTable);
        public static readonly MoveSpec[] HiloSprintMoves = BuildMoves(HiloSprintTable);
        public static readonly MoveSpec[] HiloTeleportMoves = BuildMoves(HiloTeleportTable);

        public static readonly MoveSpec[] HiloAirMoves =
        {
            new MoveSpec { id = "HiloAirKick", trigger = "HiloAirKick", seq = new[] { InputToken.Light },
                attack = new AttackData { damage = 12, hitstun = 0.28f, knockback = new Vector2(2.5f, 0.5f) }, length = 0.34f },
            new MoveSpec { id = "HiloAirHeavyKick", trigger = "HiloAirHeavyKick", seq = new[] { InputToken.Heavy },
                attack = new AttackData { damage = 22, hitstun = 0.42f, knockback = new Vector2(4.5f, -1f), knocksDown = true }, length = 0.48f },
            // full air arsenal: claw, spin kick, dive kick, downward slash,
            // energy burst projectile, and the 11-frame aerial rave finisher
            new MoveSpec { id = "HiloAirClaw", trigger = "HiloAirClaw", seq = new[] { InputToken.Light, InputToken.Light },
                attack = new AttackData { damage = 15, hitstun = 0.3f, knockback = new Vector2(3f, 0.5f) }, length = 0.36f },
            new MoveSpec { id = "HiloAirSpinKick", trigger = "HiloAirSpinKick", seq = new[] { InputToken.Light, InputToken.Heavy },
                attack = new AttackData { damage = 19, hitstun = 0.36f, knockback = new Vector2(3.5f, 0.8f), hitsBothSides = true, rangeScale = 1.4f }, length = 0.42f },
            new MoveSpec { id = "HiloAirDiveKick", trigger = "HiloAirDiveKick", seq = new[] { InputToken.Heavy, InputToken.Heavy },
                attack = new AttackData { damage = 24, hitstun = 0.44f, knockback = new Vector2(4.5f, -2.5f), knocksDown = true }, length = 0.46f },
            new MoveSpec { id = "HiloAirDownSlash", trigger = "HiloAirDownSlash", seq = new[] { InputToken.Heavy, InputToken.Light },
                attack = new AttackData { damage = 18, hitstun = 0.36f, knockback = new Vector2(3.5f, -1.5f), heightScale = 1.4f }, length = 0.4f },
            new MoveSpec { id = "HiloAirBurst", trigger = "HiloAirBurst", seq = new[] { InputToken.Gun },
                attack = new AttackData { damage = 6, hitstun = 0.25f, knockback = new Vector2(2f, 0f) },
                projectile = true, length = 0.38f },
            new MoveSpec { id = "HiloAerialCombo", trigger = "HiloAerialCombo", seq = new[] { InputToken.Light, InputToken.Light, InputToken.Light },
                attack = new AttackData { damage = 30, hitstun = 0.5f, knockback = new Vector2(5f, -1f), knocksDown = true, hitsBothSides = true, rangeScale = 1.5f }, length = 0.6f }
        };

        public static readonly MoveSpec[] HiloAwakenedAirMoves =
        {
            new MoveSpec { id = "HiloAirDragonKick", trigger = "HiloAirDragonKick", seq = new[] { InputToken.Light },
                attack = new AttackData { damage = 18, hitstun = 0.3f, knockback = new Vector2(3.5f, 0.6f) }, length = 0.34f },
            new MoveSpec { id = "HiloAirBionicStrike", trigger = "HiloAirBionicStrike", seq = new[] { InputToken.Heavy },
                attack = new AttackData { damage = 32, hitstun = 0.48f, knockback = new Vector2(6f, -1.2f), knocksDown = true }, length = 0.52f }
        };

        // Hilo's awakened "shadow" form: yin-yang / phantom arts with the same
        // teleport-context flavor as Silver's awakened set.
        private static readonly (string seq, string name, int dmg, bool kd, bool proj, TeleportKind tp)[] HiloAwakenedTable =
        {
            ("L",    "ShadowClaw",      16, false, false, TeleportKind.None),
            ("LL",   "ShadowRush",      20, false, false, TeleportKind.ForwardDash),
            ("LLL",  "PhantomBarrage",  28, true,  false, TeleportKind.BehindTarget),
            ("H",    "ShadowAxe",       26, true,  false, TeleportKind.None),
            ("HH",   "ShadowCyclone",   32, true,  false, TeleportKind.None),
            ("LH",   "ShadowPierce",    24, true,  false, TeleportKind.BehindTarget),
            ("HL",   "YinYangPalm",     24, false, false, TeleportKind.None),
            ("G",    "ShadowBeam",      0,  false, true,  TeleportKind.None),
            ("GG",   "ShadowBurst",     0,  false, true,  TeleportKind.None),
            ("GGG",  "OmniCannon",      0,  false, true,  TeleportKind.None),
            ("HHL",  "CloneAssault",    28, true,  false, TeleportKind.ForwardDash),
            ("GH",   "MissileStorm",    0,  false, true,  TeleportKind.None),
            ("HG",   "ShadowHurricane", 30, true,  false, TeleportKind.ForwardDash),
            ("LG",   "ShadowWave",      22, false, false, TeleportKind.None),
            ("LLLL", "ShadowKillCombo", 42, true,  false, TeleportKind.BehindTarget),
            ("HHHH", "FinalYinYang",    52, true,  false, TeleportKind.None),
            ("GGGG", "InfinityBeam",    0,  false, true,  TeleportKind.None)
        };

        public static readonly MoveSpec[] HiloAwakenedMoves = BuildHiloAwakenedMoves();

        private static MoveSpec[] BuildHiloAwakenedMoves()
        {
            var basic = BuildMoves(HiloAwakenedTable.Select(t => (t.seq, t.name, t.dmg, t.kd, t.proj)).ToArray());
            for (int i = 0; i < basic.Length; i++)
                basic[i].teleport = HiloAwakenedTable[i].tp;
            return basic;
        }

        /// Every move that gets a state in the base HiloAnimator, deduped by id
        /// (combo and energy tables share several moves).
        public static IEnumerable<MoveSpec> AllHiloMoveSpecs =>
            HiloComboMoves.Concat(HiloEnergyMoves).Concat(HiloDashMoves).Concat(HiloSprintMoves)
                .Concat(HiloTeleportMoves).Concat(HiloAirMoves)
                .GroupBy(m => m.id).Select(g => g.First());

        private class EnemySpec
        {
            public string name;
            public System.Type aiType;
            public Color color;
            public int hp;
            public float speedX = 4f;
            public float speedY = 2.5f;
            public AttackData melee;
            public AmmoDefinition rangedAmmo;
            public Vector3 visualScale = new Vector3(1f, 2f, 1f);
            public float statusVfxScale = 1f;
        }

        private static readonly EnemySpec[] EnemySpecs =
        {
            new EnemySpec { name = "Werewolf", aiType = typeof(WerewolfAI), color = new Color(0.55f, 0.5f, 0.5f), hp = 60, speedX = 5.5f, speedY = 3.2f,
                melee = new AttackData { damage = 10, hitstun = 0.35f, knockback = new Vector2(2f, 0f) } },
            new EnemySpec { name = "Chimera", aiType = typeof(ChimeraAI), color = new Color(0.4f, 0.6f, 0.35f), hp = 150, speedX = 3f, speedY = 1.8f,
                melee = new AttackData { damage = 16, hitstun = 0.45f, knockback = new Vector2(4f, 0f), knocksDown = true },
                rangedAmmo = new AmmoDefinition { type = AmmoType.Nuclear, attack = new AttackData { damage = 12, hitstun = 0.4f, knockback = new Vector2(2f, 0f) }, tint = new Color(0.5f, 1f, 0.3f), speed = 9f },
                visualScale = new Vector3(2f, 1.6f, 1f), statusVfxScale = 1.15f },
            new EnemySpec { name = "Reaper", aiType = typeof(ReaperAI), color = new Color(0.35f, 0.35f, 0.4f), hp = 50, speedX = 4f, speedY = 2.5f,
                melee = new AttackData { damage = 8, hitstun = 0.3f, knockback = new Vector2(1.5f, 0f) },
                rangedAmmo = new AmmoDefinition { type = AmmoType.Standard, attack = new AttackData { damage = 7, hitstun = 0.25f, knockback = new Vector2(1f, 0f) }, tint = new Color(1f, 0.3f, 0.25f), speed = 13f } },
            new EnemySpec { name = "Sentinel", aiType = typeof(SentinelAI), color = new Color(0.75f, 0.25f, 0.25f), hp = 45, speedX = 5f, speedY = 3f,
                melee = new AttackData { damage = 7, hitstun = 0.3f, knockback = new Vector2(1f, 0f) } },
            new EnemySpec { name = "Samurai", aiType = typeof(SamuraiAI), color = new Color(0.4f, 0.45f, 0.7f), hp = 55, speedX = 5f, speedY = 3f,
                melee = new AttackData { damage = 6, hitstun = 0.3f, knockback = new Vector2(1f, 0f) }, statusVfxScale = 1.1f },
            // v2-sheet additions: dual-blade samurai + three extended units
            new EnemySpec { name = "SamuraiDual", aiType = typeof(SamuraiAI), color = new Color(0.3f, 0.5f, 0.85f), hp = 65, speedX = 5.2f, speedY = 3f,
                melee = new AttackData { damage = 9, hitstun = 0.32f, knockback = new Vector2(1.5f, 0f) }, statusVfxScale = 1.1f },
            new EnemySpec { name = "Guard", aiType = typeof(ReaperAI), color = new Color(0.6f, 0.45f, 0.3f), hp = 35, speedX = 3.8f, speedY = 2.4f,
                melee = new AttackData { damage = 6, hitstun = 0.25f, knockback = new Vector2(1f, 0f) },
                rangedAmmo = new AmmoDefinition { type = AmmoType.Standard, attack = new AttackData { damage = 6, hitstun = 0.22f, knockback = new Vector2(1f, 0f) }, tint = new Color(1f, 0.65f, 0.3f), speed = 12f } },
            new EnemySpec { name = "Bruiser", aiType = typeof(WerewolfAI), color = new Color(0.7f, 0.4f, 0.25f), hp = 80, speedX = 4.2f, speedY = 2.6f,
                melee = new AttackData { damage = 12, hitstun = 0.4f, knockback = new Vector2(3f, 0f), knocksDown = true } },
            new EnemySpec { name = "Titan", aiType = typeof(SentinelAI), color = new Color(0.5f, 0.55f, 0.6f), hp = 110, speedX = 2.8f, speedY = 1.8f,
                melee = new AttackData { damage = 16, hitstun = 0.5f, knockback = new Vector2(4f, 0f), knocksDown = true }, statusVfxScale = 1.2f }
        };

        [MenuItem("SilverFang/Build Demo Scene")]
        public static void BuildAll()
        {
            EnsureDirs();
            var playerSprite = CreatePlaceholderSprite("player_placeholder", new Color(0.2f, 0.6f, 1f), 32, 32);
            var groundSprite = CreatePlaceholderSprite("ground_placeholder", new Color(0.35f, 0.3f, 0.28f), 32, 32);
            var bulletSprite = CreatePlaceholderSprite("bullet_placeholder", new Color(1f, 0.9f, 0.4f), 8, 8);

            var swordSet = CreateMoveSet("SwordMoveSet", SwordMoves);
            var gunSet = CreateMoveSet("GunMoveSet", GunMoves);
            CreateMoveSet("AwakenedMoveSet", AwakenedMoves);
            CreateMoveSet("DashMoveSet", DashMoves);
            CreateMoveSet("SprintMoveSet", SprintMoves);
            CreateMoveSet("TeleportMoveSet", TeleportMoves);
            CreateMoveSet("AirMoveSet", AirMoves);
            CreateMoveSet("AwakenedAirMoveSet", AwakenedAirMoves);

            var hiloComboSet = CreateMoveSet("HiloComboMoveSet", HiloComboMoves);
            var hiloEnergySet = CreateMoveSet("HiloEnergyMoveSet", HiloEnergyMoves);
            CreateMoveSet("HiloAwakenedMoveSet", HiloAwakenedMoves);
            CreateMoveSet("HiloDashMoveSet", HiloDashMoves);
            CreateMoveSet("HiloSprintMoveSet", HiloSprintMoves);
            CreateMoveSet("HiloTeleportMoveSet", HiloTeleportMoves);
            CreateMoveSet("HiloAirMoveSet", HiloAirMoves);
            CreateMoveSet("HiloAwakenedAirMoveSet", HiloAwakenedAirMoves);

            // Regenerate the hero controllers whenever the move tables change.
            var playerAnimator = CreatePlayerAnimator();
            var hiloAnimator = CreateHiloAnimator();
            var enemyAnimator = CreateEnemyAnimator();

            var projectilePrefab = CreateProjectilePrefab(bulletSprite);
            var playerPrefab = CreateCharacterPrefab("Player", playerSprite, playerAnimator, Team.Player, true, swordSet, gunSet, projectilePrefab);
            var hiloSprite = CreatePlaceholderSprite("hilo_placeholder", new Color(0.75f, 0.45f, 0.95f), 32, 32);
            var hiloPrefab = CreateCharacterPrefab("Hilo", hiloSprite, hiloAnimator, Team.Player, true, hiloComboSet, hiloEnergySet, projectilePrefab, "Hilo");

            var enemyPrefabs = new System.Collections.Generic.List<GameObject>();
            foreach (var spec in EnemySpecs)
            {
                var sprite = CreatePlaceholderSprite($"enemy_{spec.name.ToLower()}_placeholder", spec.color, 32, 32);
                enemyPrefabs.Add(CreateEnemyPrefab(spec, sprite, enemyAnimator, projectilePrefab));
            }

            BuildScene(playerPrefab, hiloPrefab, enemyPrefabs, groundSprite);
            Debug.Log("SilverFang demo scene built at " + ScenePath);
        }

        [MenuItem("SilverFang/Rebuild Combat Assets")]
        public static void RebuildCombatAssets()
        {
            EnsureDirs();

            CreateMoveSet("SwordMoveSet", SwordMoves);
            CreateMoveSet("GunMoveSet", GunMoves);
            CreateMoveSet("AwakenedMoveSet", AwakenedMoves);
            CreateMoveSet("DashMoveSet", DashMoves);
            CreateMoveSet("SprintMoveSet", SprintMoves);
            CreateMoveSet("TeleportMoveSet", TeleportMoves);
            CreateMoveSet("AirMoveSet", AirMoves);
            CreateMoveSet("AwakenedAirMoveSet", AwakenedAirMoves);

            CreateMoveSet("HiloComboMoveSet", HiloComboMoves);
            CreateMoveSet("HiloEnergyMoveSet", HiloEnergyMoves);
            CreateMoveSet("HiloAwakenedMoveSet", HiloAwakenedMoves);
            CreateMoveSet("HiloDashMoveSet", HiloDashMoves);
            CreateMoveSet("HiloSprintMoveSet", HiloSprintMoves);
            CreateMoveSet("HiloTeleportMoveSet", HiloTeleportMoves);
            CreateMoveSet("HiloAirMoveSet", HiloAirMoves);
            CreateMoveSet("HiloAwakenedAirMoveSet", HiloAwakenedAirMoves);

            var playerAnimator = CreatePlayerAnimator();
            WireExistingHeroPrefab("Player", playerAnimator, "Sword", "Gun", "");
            var hiloAnimator = CreateHiloAnimator();
            WireExistingHeroPrefab("Hilo", hiloAnimator, "HiloCombo", "HiloEnergy", "Hilo");
            WireCommandListUiAssets();
            BuildMusicSystem();

            AssetDatabase.SaveAssets();
            Debug.Log("SetupTools: combat assets rebuilt");
        }

        private static void WireExistingHeroPrefab(string prefabName, AnimatorController heroAnimator,
            string swordSetName, string gunSetName, string setPrefix)
        {
            string path = $"{PrefabDir}/{prefabName}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var player = root.GetComponent<PlayerController>();
                if (player != null)
                {
                    SetPrivateField(player, "swordMoveSet", AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/{swordSetName}MoveSet.asset"));
                    SetPrivateField(player, "gunMoveSet", AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/{gunSetName}MoveSet.asset"));
                    SetPrivateField(player, "dashMoveSet", AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/{setPrefix}DashMoveSet.asset"));
                    SetPrivateField(player, "sprintMoveSet", AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/{setPrefix}SprintMoveSet.asset"));
                    SetPrivateField(player, "teleportMoveSet", AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/{setPrefix}TeleportMoveSet.asset"));
                    SetPrivateField(player, "airMoveSet", AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/{setPrefix}AirMoveSet.asset"));
                    SetPrivateField(player, "awakenedMoveSet", AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/{setPrefix}AwakenedMoveSet.asset"));
                    SetPrivateField(player, "awakenedAirMoveSet", AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/{setPrefix}AwakenedAirMoveSet.asset"));
                }

                var visual = root.transform.Find("Visual");
                if (visual != null)
                {
                    visual.localScale = Vector3.one;
                    visual.localPosition = Vector3.zero;
                    var animator = visual.GetComponent<Animator>();
                    if (animator != null) animator.runtimeAnimatorController = heroAnimator;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void WireCommandListUiAssets()
        {
            if (!File.Exists(ScenePath)) return;

            var scene = EditorSceneManager.OpenScene(ScenePath);
            foreach (var ui in Object.FindObjectsByType<UI.CommandListUI>(FindObjectsInactive.Exclude))
            {
                var so = new SerializedObject(ui);
                so.FindProperty("swordMoveSet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/SwordMoveSet.asset");
                so.FindProperty("gunMoveSet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/GunMoveSet.asset");
                so.FindProperty("awakenedMoveSet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/AwakenedMoveSet.asset");
                so.FindProperty("dashMoveSet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/DashMoveSet.asset");
                so.FindProperty("sprintMoveSet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/SprintMoveSet.asset");
                so.FindProperty("teleportMoveSet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/TeleportMoveSet.asset");
                so.FindProperty("airMoveSet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/AirMoveSet.asset");
                so.FindProperty("awakenedAirMoveSet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/AwakenedAirMoveSet.asset");
                WireHiloCommandListSets(so);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.SaveScene(scene);
        }

        private static void WireHiloCommandListSets(SerializedObject so)
        {
            so.FindProperty("hiloComboMoveSet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/HiloComboMoveSet.asset");
            so.FindProperty("hiloEnergyMoveSet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/HiloEnergyMoveSet.asset");
            so.FindProperty("hiloAwakenedMoveSet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/HiloAwakenedMoveSet.asset");
            so.FindProperty("hiloDashMoveSet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/HiloDashMoveSet.asset");
            so.FindProperty("hiloSprintMoveSet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/HiloSprintMoveSet.asset");
            so.FindProperty("hiloTeleportMoveSet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/HiloTeleportMoveSet.asset");
            so.FindProperty("hiloAirMoveSet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/HiloAirMoveSet.asset");
            so.FindProperty("hiloAwakenedAirMoveSet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/HiloAwakenedAirMoveSet.asset");
        }

        private static void BuildMusicSystem()
        {
            ImportMusicClips();
            if (!File.Exists(ScenePath)) return;

            var clips = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path)
                .Select(path => AssetDatabase.LoadAssetAtPath<AudioClip>(path))
                .Where(clip => clip != null)
                .ToArray();
            if (clips.Length == 0) return;

            var scene = EditorSceneManager.OpenScene(ScenePath);
            var existing = GameObject.Find("MusicPlayer");
            if (existing != null) Object.DestroyImmediate(existing);

            var go = new GameObject("MusicPlayer");
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = clips.Length == 1;
            source.spatialBlend = 0f;
            source.volume = 0.55f;

            var player = go.AddComponent<MusicPlayer>();
            var so = new SerializedObject(player);
            var list = so.FindProperty("playlist");
            list.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            so.FindProperty("volume").floatValue = 0.55f;
            so.FindProperty("loopPlaylist").boolValue = true;
            so.FindProperty("playOnAwake").boolValue = true;
            so.FindProperty("persistAcrossScenes").boolValue = true;
            so.FindProperty("fadeInSeconds").floatValue = 1.25f;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
        }

        private static void ImportMusicClips()
        {
            Directory.CreateDirectory(AudioDir);

            if (Directory.Exists("Audio"))
            {
                foreach (var src in Directory.GetFiles("Audio"))
                {
                    string ext = Path.GetExtension(src).ToLowerInvariant();
                    if (ext != ".mp3" && ext != ".ogg" && ext != ".wav"
                        && ext != ".aif" && ext != ".aiff" && ext != ".flac")
                        continue;

                    string dest = $"{AudioDir}/{Path.GetFileName(src)}";
                    if (!File.Exists(dest) || File.GetLastWriteTimeUtc(src) > File.GetLastWriteTimeUtc(dest))
                        File.Copy(src, dest, true);
                }
            }

            AssetDatabase.Refresh();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) continue;

                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.75f;
                importer.defaultSampleSettings = settings;
                importer.loadInBackground = true;
                importer.SaveAndReimport();
            }
        }

        private static void EnsureDirs()
        {
            foreach (var dir in new[] { SpritesDir, AnimDir, PrefabDir, DataDir, AudioDir, "Assets/Scenes" })
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }

        private static Sprite CreatePlaceholderSprite(string name, Color color, int w, int h)
        {
            string path = $"{SpritesDir}/{name}.png";
            if (!File.Exists(path))
            {
                var tex = new Texture2D(w, h);
                tex.SetPixels(Enumerable.Repeat(color, w * h).ToArray());
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 32;
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static MoveSet CreateMoveSet(string name, MoveSpec[] specs)
        {
            string path = $"{DataDir}/{name}.asset";
            var set = AssetDatabase.LoadAssetAtPath<MoveSet>(path);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<MoveSet>();
                AssetDatabase.CreateAsset(set, path);
            }

            set.moves = specs.Select(s => new MoveDefinition
            {
                id = s.id,
                animatorTrigger = s.trigger,
                sequence = s.seq,
                attack = s.attack,
                firesProjectile = s.projectile,
                firesSlashWave = s.slashWave,
                teleport = s.teleport,
                duration = s.length
            }).ToList();

            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            return set;
        }

        private static AnimationClip CreateClip(string name, float length, params (float time, string function)[] events)
        {
            string path = $"{AnimDir}/{name}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool isNew = clip == null;
            if (isNew)
            {
                clip = new AnimationClip { name = name };
            }
            else
            {
                // Existing clips keep stale lengths/events otherwise — refresh both
                // so retimed move tables actually land. SpriteBaker re-bakes art on top.
                clip.ClearCurves();
            }

            clip.SetCurve("", typeof(Transform), "m_LocalScale.z", AnimationCurve.Constant(0f, length, 1f));
            AnimationUtility.SetAnimationEvents(clip,
                events.Select(e => new AnimationEvent { time = e.time, functionName = e.function }).ToArray());

            if (isNew) AssetDatabase.CreateAsset(clip, path);
            else EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimatorController CreateBaseController(string path, string prefix)
        {
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Knockdown", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("GetUp", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;

            var idle = sm.AddState("Idle");
            idle.motion = CreateClip(prefix + "_Idle", 0.5f);
            sm.defaultState = idle;

            var walk = sm.AddState("Walk");
            walk.motion = CreateClip(prefix + "_Walk", 0.5f);

            var toWalk = idle.AddTransition(walk);
            toWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "MoveSpeed");
            toWalk.hasExitTime = false;
            toWalk.duration = 0f;

            var toIdle = walk.AddTransition(idle);
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "MoveSpeed");
            toIdle.hasExitTime = false;
            toIdle.duration = 0f;

            AddReactionStates(controller, sm, idle, prefix);
            return controller;
        }

        private static void AddReactionStates(AnimatorController controller, AnimatorStateMachine sm, AnimatorState idle, string prefix)
        {
            var hurt = sm.AddState("Hurt");
            hurt.motion = CreateClip(prefix + "_Hurt", 0.3f);
            var hurtT = sm.AddAnyStateTransition(hurt);
            hurtT.AddCondition(AnimatorConditionMode.If, 0f, "Hurt");
            hurtT.hasExitTime = false;
            hurtT.duration = 0f;
            var hurtBack = hurt.AddTransition(idle);
            hurtBack.hasExitTime = true;
            hurtBack.exitTime = 1f;
            hurtBack.duration = 0f;

            // heavy flinch for big non-knockdown hits (dedicated strip)
            controller.AddParameter("HurtHeavy", AnimatorControllerParameterType.Trigger);
            var hurtHeavy = sm.AddState("HurtHeavy");
            hurtHeavy.motion = CreateClip(prefix + "_HurtHeavy", 0.38f);
            var hurtHeavyT = sm.AddAnyStateTransition(hurtHeavy);
            hurtHeavyT.AddCondition(AnimatorConditionMode.If, 0f, "HurtHeavy");
            hurtHeavyT.hasExitTime = false;
            hurtHeavyT.duration = 0f;
            var hurtHeavyBack = hurtHeavy.AddTransition(idle);
            hurtHeavyBack.hasExitTime = true;
            hurtHeavyBack.exitTime = 1f;
            hurtHeavyBack.duration = 0f;

            var down = sm.AddState("Knockdown");
            down.motion = CreateClip(prefix + "_Knockdown", 0.5f);
            var downT = sm.AddAnyStateTransition(down);
            downT.AddCondition(AnimatorConditionMode.If, 0f, "Knockdown");
            downT.hasExitTime = false;
            downT.duration = 0f;

            var getUp = sm.AddState("GetUp");
            getUp.motion = CreateClip(prefix + "_GetUp", 0.4f);
            var getUpT = down.AddTransition(getUp);
            getUpT.AddCondition(AnimatorConditionMode.If, 0f, "GetUp");
            getUpT.hasExitTime = false;
            getUpT.duration = 0f;
            var getUpBack = getUp.AddTransition(idle);
            getUpBack.hasExitTime = true;
            getUpBack.exitTime = 1f;
            getUpBack.duration = 0f;

            var dead = sm.AddState("Dead");
            dead.motion = CreateClip(prefix + "_Dead", 0.6f);
            var deadT = sm.AddAnyStateTransition(dead);
            deadT.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            deadT.hasExitTime = false;
            deadT.duration = 0f;
        }

        private static AnimatorController CreatePlayerAnimator() =>
            CreateHeroAnimator("PlayerAnimator", "Player",
                SwordMoves.Concat(GunMoves).Concat(DashMoves).Concat(SprintMoves)
                    .Concat(TeleportMoves).Concat(AirMoves));

        private static AnimatorController CreateHiloAnimator() =>
            CreateHeroAnimator("HiloAnimator", "Hilo",
                HiloComboMoves.Concat(HiloEnergyMoves).Concat(HiloDashMoves).Concat(HiloSprintMoves)
                    .Concat(HiloTeleportMoves).Concat(HiloAirMoves));

        private static AnimatorController CreateHeroAnimator(string controllerName, string prefix,
            IEnumerable<MoveSpec> moves)
        {
            // always rebuild so new move states (dash/sprint/teleport) land
            string path = $"{AnimDir}/{controllerName}.controller";
            AssetDatabase.DeleteAsset(path);

            var controller = CreateBaseController(path, prefix);
            var sm = controller.layers[0].stateMachine;
            var idle = sm.defaultState;
            var walk = sm.states.FirstOrDefault(s => s.state.name == "Walk").state;

            if (walk != null)
            {
                var run = sm.AddState("Run");
                run.motion = CreateClip(prefix + "_Run", 0.5f);

                var walkToRun = walk.AddTransition(run);
                walkToRun.AddCondition(AnimatorConditionMode.Greater, 6f, "MoveSpeed");
                walkToRun.hasExitTime = false;
                walkToRun.duration = 0f;

                var runToWalk = run.AddTransition(walk);
                runToWalk.AddCondition(AnimatorConditionMode.Less, 6f, "MoveSpeed");
                runToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "MoveSpeed");
                runToWalk.hasExitTime = false;
                runToWalk.duration = 0f;

                var runToIdle = run.AddTransition(idle);
                runToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "MoveSpeed");
                runToIdle.hasExitTime = false;
                runToIdle.duration = 0f;

                // Lock-on backpedal: dedicated retreating-steps state so the
                // forward walk never plays while stepping away from a target.
                controller.AddParameter("BackPedal", AnimatorControllerParameterType.Bool);
                var walkBack = sm.AddState("WalkBack");
                walkBack.motion = CreateClip(prefix + "_WalkBack", 0.5f);

                foreach (var t in idle.transitions)
                    if (t.destinationState == walk)
                        t.AddCondition(AnimatorConditionMode.IfNot, 0f, "BackPedal");

                var idleToBack = idle.AddTransition(walkBack);
                idleToBack.AddCondition(AnimatorConditionMode.Greater, 0.1f, "MoveSpeed");
                idleToBack.AddCondition(AnimatorConditionMode.If, 0f, "BackPedal");
                idleToBack.hasExitTime = false;
                idleToBack.duration = 0f;

                var walkToBack = walk.AddTransition(walkBack);
                walkToBack.AddCondition(AnimatorConditionMode.If, 0f, "BackPedal");
                walkToBack.hasExitTime = false;
                walkToBack.duration = 0f;

                var runToBack = run.AddTransition(walkBack);
                runToBack.AddCondition(AnimatorConditionMode.If, 0f, "BackPedal");
                runToBack.hasExitTime = false;
                runToBack.duration = 0f;

                var backToWalk = walkBack.AddTransition(walk);
                backToWalk.AddCondition(AnimatorConditionMode.IfNot, 0f, "BackPedal");
                backToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "MoveSpeed");
                backToWalk.hasExitTime = false;
                backToWalk.duration = 0f;

                var backToIdle = walkBack.AddTransition(idle);
                backToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "MoveSpeed");
                backToIdle.hasExitTime = false;
                backToIdle.duration = 0f;
            }

            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            var jump = sm.AddState("Jump");
            jump.motion = CreateClip(prefix + "_Jump", 0.7f);
            var jumpT = sm.AddAnyStateTransition(jump);
            jumpT.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
            jumpT.hasExitTime = false;
            jumpT.duration = 0f;
            var jumpBack = jump.AddTransition(idle);
            jumpBack.hasExitTime = true;
            jumpBack.exitTime = 1f;
            jumpBack.duration = 0f;

            void AddSimpleTriggerState(string trigger, string clipName, float length)
            {
                if (controller.parameters.All(p => p.name != trigger))
                    controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
                var state = sm.AddState(trigger);
                state.motion = CreateClip(clipName, length);
                var t = sm.AddAnyStateTransition(state);
                t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
                t.hasExitTime = false;
                t.duration = 0f;
                t.canTransitionToSelf = false;
                var back = state.AddTransition(idle);
                back.hasExitTime = true;
                back.exitTime = 1f;
                back.duration = 0f;
            }

            AddSimpleTriggerState("Fall", prefix + "_Fall", 0.35f);
            AddSimpleTriggerState("Land", prefix + "_Land", 0.28f);
            AddSimpleTriggerState("Reload", prefix + "_Reload", 0.45f);
            AddSimpleTriggerState("Roll", prefix + "_Roll", 0.45f);
            AddSimpleTriggerState("Guard", prefix + "_Guard", 0.35f);
            AddSimpleTriggerState("Victory", prefix + "_Victory", 0.8f);

            controller.AddParameter("Dash", AnimatorControllerParameterType.Trigger);
            var dashState = sm.AddState("Dash");
            dashState.motion = CreateClip(prefix + "_DashAnim", 0.22f);
            var dashT = sm.AddAnyStateTransition(dashState);
            dashT.AddCondition(AnimatorConditionMode.If, 0f, "Dash");
            dashT.hasExitTime = false;
            dashT.duration = 0f;
            dashT.canTransitionToSelf = false;
            var dashBack = dashState.AddTransition(idle);
            dashBack.hasExitTime = true;
            dashBack.exitTime = 1f;
            dashBack.duration = 0f;

            foreach (var move in moves)
            {
                if (controller.parameters.Any(p => p.name == move.trigger)) continue;
                controller.AddParameter(move.trigger, AnimatorControllerParameterType.Trigger);

                var state = sm.AddState(move.id);
                // AttackEnd sits before the clip boundary: events at exactly the end
                // can be swallowed by the exit transition, locking the combo chain.
                state.motion = move.projectile
                    ? CreateClip(prefix + "_" + move.id, move.length,
                        (move.length * 0.3f, "AnimEvent_Fire"),
                        (move.length * 0.93f, "AnimEvent_AttackEnd"))
                    : move.slashWave
                        ? CreateClip(prefix + "_" + move.id, move.length,
                            (move.length * 0.22f, "AnimEvent_HitboxOn"),
                            (move.length * 0.3f, "AnimEvent_Fire"),
                            (move.length * 0.72f, "AnimEvent_HitboxOff"),
                            (move.length * 0.93f, "AnimEvent_AttackEnd"))
                        : CreateClip(prefix + "_" + move.id, move.length,
                            (move.length * 0.22f, "AnimEvent_HitboxOn"),
                            (move.length * 0.72f, "AnimEvent_HitboxOff"),
                            (move.length * 0.93f, "AnimEvent_AttackEnd"));

                var t = sm.AddAnyStateTransition(state);
                t.AddCondition(AnimatorConditionMode.If, 0f, move.trigger);
                t.hasExitTime = false;
                t.duration = 0f;
                t.canTransitionToSelf = false;

                var back = state.AddTransition(idle);
                back.hasExitTime = true;
                back.exitTime = 1f;
                back.duration = 0f;
            }

            AddChargeStates(controller, sm, idle, prefix);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        /// Charge mechanic states: three hold loops (one per button) and nine
        /// charged releases (chargedattacks.png sheet: levels 1-3 per light/heavy/gun).
        /// Durations must match PlayerController.BuildChargedMove.
        private static void AddChargeStates(AnimatorController controller, AnimatorStateMachine sm, AnimatorState idle,
            string prefix = "Player")
        {
            void Hold(string trigger)
            {
                controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
                var state = sm.AddState(trigger);
                var clip = CreateClip(prefix + "_" + trigger, 0.5f);
                var so = new SerializedObject(clip);
                so.FindProperty("m_AnimationClipSettings.m_LoopTime").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                state.motion = clip;
                var t = sm.AddAnyStateTransition(state);
                t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
                t.hasExitTime = false;
                t.duration = 0f;
                t.canTransitionToSelf = false;
            }

            Hold("ChargeHoldLight");
            Hold("ChargeHoldHeavy");
            Hold("ChargeHoldGun");

            void Release(string trigger, float length, bool projectile, bool alsoWave)
            {
                controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
                var state = sm.AddState(trigger);
                var events = projectile
                    ? new[] { (length * 0.3f, "AnimEvent_Fire"), (length * 0.93f, "AnimEvent_AttackEnd") }
                    : alsoWave
                        ? new[]
                        {
                            (length * 0.22f, "AnimEvent_HitboxOn"), (length * 0.3f, "AnimEvent_Fire"),
                            (length * 0.72f, "AnimEvent_HitboxOff"), (length * 0.93f, "AnimEvent_AttackEnd")
                        }
                        : new[]
                        {
                            (length * 0.22f, "AnimEvent_HitboxOn"), (length * 0.72f, "AnimEvent_HitboxOff"),
                            (length * 0.93f, "AnimEvent_AttackEnd")
                        };
                state.motion = CreateClip(prefix + "_" + trigger, length, events);
                var t = sm.AddAnyStateTransition(state);
                t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
                t.hasExitTime = false;
                t.duration = 0f;
                t.canTransitionToSelf = false;
                var back = state.AddTransition(idle);
                back.hasExitTime = true;
                back.exitTime = 1f;
                back.duration = 0f;
            }

            for (int level = 1; level <= 3; level++)
            {
                Release("ChargeLight" + level, 0.38f + 0.08f * level, false, false);
                Release("ChargeHeavy" + level, 0.5f + 0.1f * level, false, level >= 3);
                Release("ChargeShot" + level, 0.42f + 0.07f * level, true, false);
            }
        }

        private static AnimatorController CreateEnemyAnimator()
        {
            string path = $"{AnimDir}/EnemyAnimator.controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null) return existing;

            var controller = CreateBaseController(path, "Enemy");
            var sm = controller.layers[0].stateMachine;
            var idle = sm.defaultState;

            var attackSpecs = new (string trigger, float length, (float, string)[] events)[]
            {
                ("Attack", 0.5f, new[] { (0.17f, "AnimEvent_HitboxOn"), (0.36f, "AnimEvent_HitboxOff"), (0.47f, "AnimEvent_AttackEnd") }),
                ("Special", 0.65f, new[] { (0.22f, "AnimEvent_HitboxOn"), (0.48f, "AnimEvent_HitboxOff"), (0.61f, "AnimEvent_AttackEnd") }),
                ("Shoot", 0.5f, new[] { (0.2f, "AnimEvent_Fire"), (0.47f, "AnimEvent_AttackEnd") })
            };

            foreach (var (trigger, length, events) in attackSpecs)
            {
                controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
                var state = sm.AddState(trigger);
                state.motion = CreateClip("Enemy_" + trigger, length, events);
                var t = sm.AddAnyStateTransition(state);
                t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
                t.hasExitTime = false;
                t.duration = 0f;
                t.canTransitionToSelf = false;
                var back = state.AddTransition(idle);
                back.hasExitTime = true;
                back.exitTime = 1f;
                back.duration = 0f;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static Projectile CreateProjectilePrefab(Sprite bulletSprite)
        {
            string path = $"{PrefabDir}/Projectile.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<Projectile>();

            var root = new GameObject("Projectile");
            var col = root.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.15f;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = bulletSprite;
            sr.sortingOrder = 5000;

            root.AddComponent<Projectile>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<Projectile>();
        }

        /// Flying sword-wave: released by wave moves and full-charge heavy attacks.
        private static Projectile CreateSlashWavePrefab()
        {
            string path = $"{PrefabDir}/SlashWave.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<Projectile>();

            var sprite = CreatePlaceholderSprite("slashwave_placeholder", new Color(0.55f, 0.85f, 1f, 0.9f), 28, 12);
            var root = new GameObject("SlashWave");
            var col = root.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.95f, 0.5f);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 5000;

            root.AddComponent<Projectile>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<Projectile>();
        }

        private static GameObject CreateCharacterPrefab(string name, Sprite sprite, AnimatorController controller, Team team,
            bool isPlayer, MoveSet swordSet, MoveSet gunSet, Projectile projectilePrefab, string setPrefix = "")
        {
            string path = $"{PrefabDir}/{name}.prefab";

            var root = new GameObject(name);
            var body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            root.AddComponent<Health>();

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            visual.transform.localScale = new Vector3(1f, 2f, 1f);
            visual.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            visual.AddComponent<Animator>().runtimeAnimatorController = controller;
            visual.AddComponent<DepthSorter>();
            visual.AddComponent<AnimationEventRelay>();

            var feet = root.AddComponent<CapsuleCollider2D>();
            feet.size = new Vector2(0.6f, 0.3f);

            var hurtObj = new GameObject("Hurtbox");
            hurtObj.transform.SetParent(root.transform, false);
            hurtObj.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            var hurtCol = hurtObj.AddComponent<BoxCollider2D>();
            hurtCol.isTrigger = true;
            hurtCol.size = new Vector2(0.78f, 1.7f); // tight on the ~1.8u hero body
            var hurtbox = hurtObj.AddComponent<Hurtbox>();
            SetPrivateField(hurtbox, "team", team);

            // Base sword-reach box; Hitbox reshapes it per attack (range/height/both-sides).
            var hitObj = new GameObject("Hitbox");
            hitObj.transform.SetParent(root.transform, false);
            hitObj.transform.localPosition = new Vector3(1.05f, 0.9f, 0f);
            var hitCol = hitObj.AddComponent<BoxCollider2D>();
            hitCol.isTrigger = true;
            hitCol.enabled = false;
            hitCol.size = new Vector2(1.55f, 1.2f);
            var hitbox = hitObj.AddComponent<Hitbox>();
            SetPrivateField(hitbox, "team", team);

            var firePoint = new GameObject("FirePoint");
            firePoint.transform.SetParent(root.transform, false);
            firePoint.transform.localPosition = new Vector3(0.8f, 0.9f, 0f);

            CharacterCombatant combatant = isPlayer
                ? root.AddComponent<PlayerController>()
                : root.AddComponent<EnemyAI>();
            SetPrivateField(combatant, "hitbox", hitbox, typeof(CharacterCombatant));

            if (isPlayer)
            {
                root.AddComponent<ComboTracker>();
                var player = (PlayerController)combatant;
                SetPrivateField(player, "swordMoveSet", swordSet);
                SetPrivateField(player, "gunMoveSet", gunSet);
                SetPrivateField(player, "dashMoveSet", AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/{setPrefix}DashMoveSet.asset"));
                SetPrivateField(player, "sprintMoveSet", AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/{setPrefix}SprintMoveSet.asset"));
                SetPrivateField(player, "teleportMoveSet", AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/{setPrefix}TeleportMoveSet.asset"));
                SetPrivateField(player, "airMoveSet", AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/{setPrefix}AirMoveSet.asset"));
                SetPrivateField(player, "awakenedAirMoveSet", AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/{setPrefix}AwakenedAirMoveSet.asset"));
                SetPrivateField(player, "projectilePrefab", projectilePrefab);
                SetPrivateField(player, "firePoint", firePoint.transform);
                SetPrivateField(player, "visual", visual.transform);
                SetPrivateField(player, "slashWavePrefab", CreateSlashWavePrefab());
                SetPrivateField(player, "slashWaveAmmo", new AmmoDefinition
                {
                    type = AmmoType.Standard,
                    attack = new AttackData { damage = 14, hitstun = 0.3f, knockback = new Vector2(4f, 0f) },
                    tint = new Color(0.55f, 0.85f, 1f),
                    speed = 12f,
                    piercing = true
                });
                SetPrivateField(player, "ammoTypes", new[]
                {
                    new AmmoDefinition { type = AmmoType.Standard, attack = new AttackData { damage = 6, hitstun = 0.2f, knockback = new Vector2(1f, 0f) }, tint = Color.white, speed = 16f },
                    new AmmoDefinition { type = AmmoType.Nuclear, attack = new AttackData { damage = 20, hitstun = 0.5f, knockback = new Vector2(6f, 0f), knocksDown = true }, tint = new Color(0.4f, 1f, 0.3f), speed = 10f,
                        status = StatusType.Radiated, statusDuration = 5f },
                    new AmmoDefinition { type = AmmoType.Ice, attack = new AttackData { damage = 8, hitstun = 0.8f, knockback = Vector2.zero }, tint = new Color(0.5f, 0.8f, 1f), speed = 14f,
                        status = StatusType.Frozen, statusDuration = 2f },
                    new AmmoDefinition { type = AmmoType.Incendiary, attack = new AttackData { damage = 14, hitstun = 0.3f, knockback = new Vector2(2f, 0f) }, tint = new Color(1f, 0.5f, 0.2f), speed = 14f,
                        status = StatusType.Burning, statusDuration = 4f },
                    new AmmoDefinition { type = AmmoType.Piercing, attack = new AttackData { damage = 10, hitstun = 0.25f, knockback = new Vector2(1f, 0f) }, tint = new Color(0.8f, 0.5f, 1f), speed = 20f, piercing = true }
                });
            }

            var prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefabAsset;
        }

        private static GameObject CreateEnemyPrefab(EnemySpec spec, Sprite sprite, AnimatorController controller, Projectile projectilePrefab)
        {
            string path = $"{PrefabDir}/Enemy_{spec.name}.prefab";

            var root = new GameObject("Enemy_" + spec.name);
            var body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            var health = root.AddComponent<Health>();
            SetPrivateField(health, "maxHealth", spec.hp);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            visual.transform.localScale = spec.visualScale;
            visual.transform.localPosition = new Vector3(0f, spec.visualScale.y * 0.35f, 0f);
            visual.AddComponent<Animator>().runtimeAnimatorController = controller;
            visual.AddComponent<DepthSorter>();
            visual.AddComponent<AnimationEventRelay>();

            var feet = root.AddComponent<CapsuleCollider2D>();
            feet.size = new Vector2(0.6f, 0.3f);

            var hurtObj = new GameObject("Hurtbox");
            hurtObj.transform.SetParent(root.transform, false);
            hurtObj.transform.localPosition = new Vector3(0f, spec.visualScale.y * 0.35f, 0f);
            var hurtCol = hurtObj.AddComponent<BoxCollider2D>();
            hurtCol.isTrigger = true;
            hurtCol.size = new Vector2(spec.visualScale.x * 0.72f, spec.visualScale.y * 0.78f);
            var hurtbox = hurtObj.AddComponent<Hurtbox>();
            SetPrivateField(hurtbox, "team", Team.Enemy);

            var hitObj = new GameObject("Hitbox");
            hitObj.transform.SetParent(root.transform, false);
            hitObj.transform.localPosition = new Vector3(0.9f, 0.8f, 0f);
            var hitCol = hitObj.AddComponent<BoxCollider2D>();
            hitCol.isTrigger = true;
            hitCol.enabled = false;
            hitCol.size = new Vector2(1.15f, 0.95f);
            var hitbox = hitObj.AddComponent<Hitbox>();
            SetPrivateField(hitbox, "team", Team.Enemy);

            var firePoint = new GameObject("FirePoint");
            firePoint.transform.SetParent(root.transform, false);
            firePoint.transform.localPosition = new Vector3(0.8f, 0.9f, 0f);

            var ai = (EnemyAI)root.AddComponent(spec.aiType);
            SetPrivateField(ai, "hitbox", hitbox, typeof(CharacterCombatant));

            var bounty = root.AddComponent<Currency.ScematicaBounty>();
            SetPrivateField(bounty, "amount", Mathf.Max(5, spec.hp / 4));

            var xpBounty = root.AddComponent<Progression.XpBounty>();
            SetPrivateField(xpBounty, "xp", 10 + spec.hp / 2);

            var healthBar = root.AddComponent<EnemyHealthBar>();
            SetPrivateField(healthBar, "yOffset", spec.visualScale.y * 0.78f + 0.4f);
            SetPrivateField(ai, "moveSpeedX", spec.speedX, typeof(CharacterCombatant));
            SetPrivateField(ai, "moveSpeedY", spec.speedY, typeof(CharacterCombatant));
            SetPrivateField(ai, "statusVfxVariant", spec.name.ToLowerInvariant(), typeof(CharacterCombatant));
            SetPrivateField(ai, "statusVfxScale", spec.statusVfxScale, typeof(CharacterCombatant));
            SetPrivateField(ai, "statusVfxOffsetY", spec.visualScale.y * 0.35f, typeof(CharacterCombatant));
            SetPrivateField(ai, "meleeAttack", spec.melee, typeof(EnemyAI));
            if (spec.rangedAmmo != null)
            {
                SetPrivateField(ai, "projectilePrefab", projectilePrefab, typeof(EnemyAI));
                SetPrivateField(ai, "rangedAmmo", spec.rangedAmmo, typeof(EnemyAI));
                SetPrivateField(ai, "firePoint", firePoint.transform, typeof(EnemyAI));
            }

            var prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefabAsset;
        }

        private static void BuildScene(GameObject playerPrefab, GameObject hiloPrefab, System.Collections.Generic.List<GameObject> enemyPrefabs, Sprite groundSprite)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            var cam = camObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 4f;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            camObj.transform.position = new Vector3(0f, 0f, -10f);
            var follow = camObj.AddComponent<CameraFollow>();

            var ground = new GameObject("Ground");
            var gsr = ground.AddComponent<SpriteRenderer>();
            gsr.sprite = groundSprite;
            gsr.drawMode = SpriteDrawMode.Tiled;
            gsr.size = new Vector2(60f, 5f);
            gsr.sortingOrder = -10000;
            ground.transform.position = new Vector3(20f, -2.5f, 0f);

            CreateWall("BoundsTop", new Vector2(20f, -0.4f), new Vector2(60f, 0.2f));
            CreateWall("BoundsBottom", new Vector2(20f, -4.6f), new Vector2(60f, 0.2f));
            CreateWall("BoundsLeft", new Vector2(-6f, -2.5f), new Vector2(0.2f, 6f));
            CreateWall("BoundsRight", new Vector2(46f, -2.5f), new Vector2(0.2f, 6f));

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.position = new Vector3(-3f, -2.5f, 0f);
            var hilo = (GameObject)PrefabUtility.InstantiatePrefab(hiloPrefab);
            hilo.transform.position = new Vector3(-3f, -2.5f, 0f);

            // No target yet: the character-select screen retargets the camera
            // onto whichever hero gets picked.
            var followSo = new SerializedObject(follow);
            followSo.FindProperty("minX").floatValue = 0f;
            followSo.FindProperty("maxX").floatValue = 40f;
            followSo.ApplyModifiedPropertiesWithoutUndo();

            // Encounter 1: Werewolf + Sentinel + Samurai (melee wave)
            CreateEncounter("Encounter1", 8f, 10f, new[]
            {
                (enemyPrefabs[0], new Vector3(12f, -2f, 0f)),
                (enemyPrefabs[3], new Vector3(13.5f, -3.5f, 0f)),
                (enemyPrefabs[4], new Vector3(14f, -1.5f, 0f))
            }, "enc1_start", "enc1_clear");

            // Encounter 2: Reaper pair + Chimera mini-boss
            CreateEncounter("Encounter2", 24f, 26f, new[]
            {
                (enemyPrefabs[2], new Vector3(29f, -1.8f, 0f)),
                (enemyPrefabs[2], new Vector3(30f, -3.6f, 0f)),
                (enemyPrefabs[1], new Vector3(31f, -2.5f, 0f))
            }, "enc2_start", "enc2_clear");

            // Encounter 3: extended units - Guards lay fire, Bruiser rushes,
            // the dual-blade samurai flanks, Titan anchors the line.
            CreateEncounter("Encounter3", 35f, 37f, new[]
            {
                (enemyPrefabs[6], new Vector3(39f, -1.6f, 0f)),
                (enemyPrefabs[6], new Vector3(40.5f, -3.4f, 0f)),
                (enemyPrefabs[7], new Vector3(41.5f, -2.2f, 0f)),
                (enemyPrefabs[5], new Vector3(42.5f, -3.2f, 0f)),
                (enemyPrefabs[8], new Vector3(43.5f, -2.6f, 0f))
            });

            var silverHud = BuildPlayerPlate(player);
            var hiloHud = BuildHiloPlate(hilo);
            BuildScematicaUI();
            BuildCommandListUI();
            BuildCharacterSheetUI();
            BuildDialogueUI();
            BuildCodexUI();
            BuildLevelUpMenu();
            BuildChipCoreUI();
            BuildCharacterHub();
            BuildStoryObjects();
            var select = BuildCharacterSelect(player, hilo, silverHud, hiloHud);
            BuildIntroCrawl(select);

            EditorSceneManager.SaveScene(scene, ScenePath);
            BuildMusicSystem();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void CreateEncounter(string name, float triggerX, float lockX, (GameObject prefab, Vector3 pos)[] spawns,
            string startBeat = "", string clearBeat = "")
        {
            var zoneObj = new GameObject(name);
            zoneObj.transform.position = new Vector3(triggerX, -2.5f, 0f);
            var zoneCol = zoneObj.AddComponent<BoxCollider2D>();
            zoneCol.isTrigger = true;
            zoneCol.size = new Vector2(2f, 6f);
            var zone = zoneObj.AddComponent<EncounterZone>();

            var enemies = new System.Collections.Generic.List<EnemyAI>();
            foreach (var (prefab, pos) in spawns)
            {
                var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                enemy.transform.position = pos;
                enemies.Add(enemy.GetComponent<EnemyAI>());
            }

            var zoneSo = new SerializedObject(zone);
            var listProp = zoneSo.FindProperty("enemies");
            listProp.arraySize = enemies.Count;
            for (int i = 0; i < enemies.Count; i++)
                listProp.GetArrayElementAtIndex(i).objectReferenceValue = enemies[i];
            zoneSo.FindProperty("cameraLockX").floatValue = lockX;
            zoneSo.FindProperty("startBeat").stringValue = startBeat;
            zoneSo.FindProperty("clearBeat").stringValue = clearBeat;
            zoneSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Font uiFont;

        /// The builtin LegacyRuntime font fails to render on some installs
        /// (blank in-game text). Import Consolas from Windows as a real font
        /// asset; fall back to the builtin only if the copy fails.
        private static Font GetUiFont()
        {
            if (uiFont != null) return uiFont;
            string path = $"{UiDir}/Fonts/consola.ttf";
            if (AssetDatabase.LoadAssetAtPath<Font>(path) == null)
            {
                Directory.CreateDirectory($"{UiDir}/Fonts");
                const string src = @"C:\Windows\Fonts\consola.ttf";
                if (File.Exists(src))
                {
                    File.Copy(src, path, true);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                }
            }
            uiFont = AssetDatabase.LoadAssetAtPath<Font>(path);
            if (uiFont == null)
                uiFont = AssetDatabase.GetBuiltinExtraResource<Font>("LegacyRuntime.ttf");
            return uiFont;
        }

        private static Text MakeUiText(Transform parent, string name, Vector2 pos, Vector2 size,
            int fontSize, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = true)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var text = obj.AddComponent<Text>();
            text.font = GetUiFont();
            text.fontSize = fontSize;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return text;
        }

        private static Image MakeFill(Transform parent, string name, Sprite sprite, Color fallback,
            Vector2 pos, Vector2 size)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var img = obj.AddComponent<Image>();
            if (sprite != null) img.sprite = sprite;
            else img.color = fallback;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            var rect = img.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return img;
        }

        /// Full-stretch child of the canvas grouping one hero's HUD widgets so
        /// the character-select screen can flip the whole set on/off at once.
        private static GameObject HudGroup(Transform canvas, string name)
        {
            var group = new GameObject(name);
            group.transform.SetParent(canvas, false);
            var rect = group.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return group;
        }

        /// Glyph-art combo counter (digits right-to-left + HITS/MAX suffix)
        /// with rank letter, top-right under the notifications feed.
        private static void BuildComboCounter(GameObject group, GameObject hero, Color accent, string who)
        {
            var countText = MakeUiText(group.transform, "ComboCount", Vector2.zero, new Vector2(240f, 36f),
                16, accent, TextAnchor.MiddleRight);
            var countRect = countText.rectTransform;
            countRect.anchorMin = new Vector2(1f, 1f);
            countRect.anchorMax = new Vector2(1f, 1f);
            countRect.pivot = new Vector2(1f, 1f);
            countRect.anchoredPosition = new Vector2(-14f, -104f);

            var rankText = MakeUiText(group.transform, "ComboRank", Vector2.zero, new Vector2(240f, 46f),
                24, Color.white, TextAnchor.MiddleRight);
            var rankRect = rankText.rectTransform;
            rankRect.anchorMin = new Vector2(1f, 1f);
            rankRect.anchorMax = new Vector2(1f, 1f);
            rankRect.pivot = new Vector2(1f, 1f);
            rankRect.anchoredPosition = new Vector2(-14f, -140f);

            // digit container: slots laid right-to-left (ones, tens, hundreds)
            var digitsObj = new GameObject("ComboDigits");
            digitsObj.transform.SetParent(group.transform, false);
            var digitsRect = digitsObj.AddComponent<RectTransform>();
            digitsRect.anchorMin = new Vector2(1f, 1f);
            digitsRect.anchorMax = new Vector2(1f, 1f);
            digitsRect.pivot = new Vector2(1f, 1f);
            digitsRect.anchoredPosition = new Vector2(-14f, -104f);
            digitsRect.sizeDelta = new Vector2(120f, 40f);

            var slots = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var slotObj = new GameObject("Digit" + i);
                slotObj.transform.SetParent(digitsObj.transform, false);
                var img = slotObj.AddComponent<Image>();
                img.preserveAspect = true;
                img.enabled = false;
                var r = img.rectTransform;
                r.anchorMin = new Vector2(1f, 0.5f);
                r.anchorMax = new Vector2(1f, 0.5f);
                r.pivot = new Vector2(1f, 0.5f);
                r.anchoredPosition = new Vector2(-i * 18f, 0f);
                slots[i] = img;
            }

            var suffixObj = new GameObject("ComboSuffix");
            suffixObj.transform.SetParent(digitsObj.transform, false);
            var suffix = suffixObj.AddComponent<Image>();
            suffix.preserveAspect = true;
            suffix.enabled = false;
            var suffixRect = suffix.rectTransform;
            suffixRect.anchorMin = new Vector2(1f, 0.5f);
            suffixRect.anchorMax = new Vector2(1f, 0.5f);
            suffixRect.pivot = new Vector2(1f, 0.5f);
            suffixRect.anchoredPosition = new Vector2(0f, -22f);

            var comboUi = group.AddComponent<ComboCounterUI>();
            var comboSo = new SerializedObject(comboUi);
            comboSo.FindProperty("tracker").objectReferenceValue = hero.GetComponent<ComboTracker>();
            comboSo.FindProperty("countText").objectReferenceValue = countText;
            comboSo.FindProperty("rankText").objectReferenceValue = rankText;
            comboSo.FindProperty("accent").colorValue = accent;

            var digitsProp = comboSo.FindProperty("digitSprites");
            digitsProp.arraySize = 10;
            for (int i = 0; i < 10; i++)
                digitsProp.GetArrayElementAtIndex(i).objectReferenceValue =
                    ImportUiSprite($"{UiDir}/Combo/{who}_{i}.png");
            comboSo.FindProperty("hitsSprite").objectReferenceValue = ImportUiSprite($"{UiDir}/Combo/{who}_hits.png");
            comboSo.FindProperty("maxSprite").objectReferenceValue = ImportUiSprite($"{UiDir}/Combo/{who}_max.png");
            var slotsProp = comboSo.FindProperty("digitSlots");
            slotsProp.arraySize = 3;
            for (int i = 0; i < 3; i++)
                slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            comboSo.FindProperty("suffixSlot").objectReferenceValue = suffix;
            comboSo.FindProperty("glyphHeight").floatValue = 22f;
            comboSo.ApplyModifiedPropertiesWithoutUndo();
        }

        /// Combo level meter (F -> XXX ladder): frame chassis + tier glyph +
        /// progress fill + tier title, under the shard counter.
        private static void BuildComboLevelMeter(GameObject group, GameObject hero, string who)
        {
            var frameSprite = ImportUiSprite($"{UiDir}/ComboMeter/{who}_frame.png");
            var fillSprite = ImportUiSprite($"{UiDir}/ComboMeter/{who}_fill.png");
            const float S = 0.38f; // compact: 524x76 native -> ~199x29 on screen

            var meterObj = new GameObject("ComboLevelMeter");
            meterObj.transform.SetParent(group.transform, false);
            var frame = meterObj.AddComponent<Image>();
            if (frameSprite != null) frame.sprite = frameSprite;
            else frame.color = new Color(0f, 0f, 0f, 0.5f);
            var frameRect = frame.rectTransform;
            frameRect.anchorMin = new Vector2(0f, 1f);
            frameRect.anchorMax = new Vector2(0f, 1f);
            frameRect.pivot = new Vector2(0f, 1f);
            frameRect.anchoredPosition = new Vector2(12f, -126f);
            frameRect.sizeDelta = new Vector2(524f * S, 76f * S);

            Vector2 P(float x, float y) => new Vector2(x * S, -y * S);
            Vector2 Sz(float w, float h) => new Vector2(w * S, h * S);

            var fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(meterObj.transform, false);
            var fill = fillObj.AddComponent<Image>();
            if (fillSprite != null) fill.sprite = fillSprite;
            else fill.color = new Color(0.5f, 0.8f, 1f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;
            var fillRect = fill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 1f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 1f);
            fillRect.anchoredPosition = P(113, 31);
            fillRect.sizeDelta = Sz(349, 19);

            var letterObj = new GameObject("TierLetter");
            letterObj.transform.SetParent(meterObj.transform, false);
            var letter = letterObj.AddComponent<Image>();
            letter.preserveAspect = true;
            var letterRect = letter.rectTransform;
            letterRect.anchorMin = new Vector2(0f, 1f);
            letterRect.anchorMax = new Vector2(0f, 1f);
            letterRect.pivot = new Vector2(0f, 0.5f);
            letterRect.anchoredPosition = new Vector2(4f, -76f * S * 0.5f);

            var title = MakeUiText(meterObj.transform, "TierTitle", P(134, 6), Sz(280, 24),
                8, new Color(0.85f, 0.9f, 1f));

            meterObj.SetActive(false); // ComboLevelMeterUI shows it when a chain starts
            var ui = group.AddComponent<ComboLevelMeterUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("tracker").objectReferenceValue = hero.GetComponent<ComboTracker>();
            so.FindProperty("meterRoot").objectReferenceValue = meterObj;
            so.FindProperty("letterImage").objectReferenceValue = letter;
            so.FindProperty("fillImage").objectReferenceValue = fill;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("letterHeight").floatValue = 76f * S * 0.6f;
            var letters = so.FindProperty("letterSprites");
            letters.arraySize = 12;
            for (int i = 0; i < 12; i++)
                letters.GetArrayElementAtIndex(i).objectReferenceValue =
                    ImportUiSprite($"{UiDir}/ComboMeter/{who}_letter_{i}.png");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// World-space lock-on reticle marker, per-hero art.
        private static void WireLockReticle(GameObject hero, string who)
        {
            var ui = hero.GetComponent<LockOnReticleUI>();
            if (ui == null) ui = hero.AddComponent<LockOnReticleUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("reticleSprite").objectReferenceValue =
                ImportUiSprite($"{UiDir}/LockOn/{who}_reticle_0.png");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// Portrait plate (hud_player_plate art) with live health / awakened /
        /// XP fills overlaid on its blanked bar slots, plus the SCEMA, shard,
        /// and stance/ammo readouts beneath it. Returns Silver's HUD group.
        private static GameObject BuildPlayerPlate(GameObject player)
        {
            var canvasObj = new GameObject("Canvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            var group = HudGroup(canvasObj.transform, "SilverHud");

            var plateSprite = ImportUiSprite($"{UiDir}/HUD/hud_player_plate.png");
            var healthSprite = ImportUiSprite($"{UiDir}/HUD/hud_health_fill.png");
            var awakenedSprite = ImportUiSprite($"{UiDir}/HUD/hud_awakened_fill.png");
            var xpSprite = ImportUiSprite($"{UiDir}/HUD/hud_xp_fill.png");

            const float S = 0.5f; // compact: plate at 50% of native art size

            var plateObj = new GameObject("PlayerPlate");
            plateObj.transform.SetParent(group.transform, false);
            var plate = plateObj.AddComponent<Image>();
            if (plateSprite != null) plate.sprite = plateSprite;
            else plate.color = new Color(0f, 0f, 0f, 0.6f);
            var plateRect = plate.rectTransform;
            plateRect.anchorMin = new Vector2(0f, 1f);
            plateRect.anchorMax = new Vector2(0f, 1f);
            plateRect.pivot = new Vector2(0f, 1f);
            plateRect.anchoredPosition = new Vector2(10f, -10f);
            plateRect.sizeDelta = new Vector2(460f * S, 148f * S);

            Vector2 P(float x, float y) => new Vector2(x * S, -y * S);
            Vector2 Sz(float w, float h) => new Vector2(w * S, h * S);

            var healthFill = MakeFill(plateObj.transform, "HealthFill", healthSprite, new Color(0.85f, 0.15f, 0.15f), P(152, 43), Sz(300, 16));
            var xpFill = MakeFill(plateObj.transform, "XpFill", xpSprite, new Color(0.95f, 0.8f, 0.35f), P(152, 81), Sz(300, 16));
            var awakenedFill = MakeFill(plateObj.transform, "AwakenedFill", awakenedSprite, new Color(0.55f, 0.35f, 0.9f), P(228, 123), Sz(172, 22));

            var hpText = MakeUiText(plateObj.transform, "HpText", P(352, 22), Sz(170, 30), 10, new Color(0.95f, 0.85f, 0.85f));
            var lvText = MakeUiText(plateObj.transform, "LvText", P(352, 61), Sz(170, 30), 10, new Color(0.95f, 0.8f, 0.4f));
            var xpText = MakeUiText(plateObj.transform, "XpText", P(244, 99), Sz(330, 28), 9, new Color(0.95f, 0.85f, 0.55f));

            var plateUi = plateObj.AddComponent<PlayerPlateUI>();
            var so = new SerializedObject(plateUi);
            so.FindProperty("health").objectReferenceValue = player.GetComponent<Health>();
            so.FindProperty("healthFill").objectReferenceValue = healthFill;
            so.FindProperty("healthText").objectReferenceValue = hpText;
            so.FindProperty("levelText").objectReferenceValue = lvText;
            so.FindProperty("xpFill").objectReferenceValue = xpFill;
            so.FindProperty("xpText").objectReferenceValue = xpText;
            so.ApplyModifiedPropertiesWithoutUndo();

            var meterUi = plateObj.AddComponent<AwakenedMeterUI>();
            var meterSo = new SerializedObject(meterUi);
            meterSo.FindProperty("player").objectReferenceValue = player.GetComponent<PlayerController>();
            meterSo.FindProperty("fill").objectReferenceValue = awakenedFill;
            meterSo.ApplyModifiedPropertiesWithoutUndo();

            var stanceText = MakeUiText(group.transform, "StanceAmmo", new Vector2(12f, -96f), new Vector2(240f, 15f), 9, Color.white);
            var stanceUi = group.AddComponent<StanceAmmoUI>();
            var stanceSo = new SerializedObject(stanceUi);
            stanceSo.FindProperty("player").objectReferenceValue = player.GetComponent<PlayerController>();
            stanceSo.FindProperty("label").objectReferenceValue = stanceText;
            stanceSo.ApplyModifiedPropertiesWithoutUndo();

            var shardText = MakeUiText(canvasObj.transform, "ShardCounter", new Vector2(12f, -112f), new Vector2(240f, 14f), 8, new Color(0.6f, 0.9f, 1f));
            var shardUi = canvasObj.AddComponent<ShardCounterUI>();
            var shardSo = new SerializedObject(shardUi);
            shardSo.FindProperty("label").objectReferenceValue = shardText;
            shardSo.ApplyModifiedPropertiesWithoutUndo();

            var statusText = MakeUiText(group.transform, "PlayerStatus", new Vector2(244f, -24f), new Vector2(130f, 16f), 9, Color.white);
            var statusUi = group.AddComponent<PlayerStatusUI>();
            var statusSo = new SerializedObject(statusUi);
            statusSo.FindProperty("player").objectReferenceValue = player.GetComponent<PlayerController>();
            statusSo.FindProperty("label").objectReferenceValue = statusText;
            statusSo.ApplyModifiedPropertiesWithoutUndo();

            var notifText = MakeUiText(group.transform, "Notifications", new Vector2(0f, -12f), new Vector2(240f, 84f), 10, new Color(0.95f, 0.9f, 0.7f), TextAnchor.UpperRight);
            var notifRect = notifText.rectTransform;
            notifRect.anchorMin = new Vector2(1f, 1f);
            notifRect.anchorMax = new Vector2(1f, 1f);
            notifRect.pivot = new Vector2(1f, 1f);
            notifRect.anchoredPosition = new Vector2(-16f, -16f);
            var notifUi = group.AddComponent<NotificationUI>();
            var notifSo = new SerializedObject(notifUi);
            notifSo.FindProperty("label").objectReferenceValue = notifText;
            notifSo.FindProperty("player").objectReferenceValue = player.GetComponent<PlayerController>();
            notifSo.ApplyModifiedPropertiesWithoutUndo();

            BuildComboCounter(group, player, new Color(0.55f, 0.8f, 1f), "silver");
            BuildComboLevelMeter(group, player, "silver");
            WireLockReticle(player, "silver");

            var dmgUi = canvasObj.AddComponent<DamageNumberUI>();
            var dmgSo = new SerializedObject(dmgUi);
            dmgSo.FindProperty("font").objectReferenceValue = GetUiFont();
            dmgSo.ApplyModifiedPropertiesWithoutUndo();

            return group;
        }

        /// Hilo's banner plate (hilo_player_plate art, 636x116 native): health
        /// bar across the top, energy bar (awakened meter) and yin-yang orb row
        /// (XP) below, with the infinity medallion re-overlaid above the fills.
        /// Geometry comes from tools/extract_hilo_hud.py's blanked regions.
        private static GameObject BuildHiloPlate(GameObject hilo)
        {
            var canvasObj = GameObject.Find("Canvas");
            if (canvasObj == null) return null;

            var group = HudGroup(canvasObj.transform, "HiloHud");

            var plateSprite = ImportUiSprite($"{UiDir}/HiloHUD/hilo_player_plate.png");
            var emblemSprite = ImportUiSprite($"{UiDir}/HiloHUD/hilo_plate_emblem.png");
            var healthSprite = ImportUiSprite($"{UiDir}/HUD/hud_health_fill.png");
            var awakenedSprite = ImportUiSprite($"{UiDir}/HUD/hud_awakened_fill.png");
            var xpSprite = ImportUiSprite($"{UiDir}/HUD/hud_xp_fill.png");

            const float S = 0.36f; // compact: matches the smaller Silver plate

            var plateObj = new GameObject("HiloPlate");
            plateObj.transform.SetParent(group.transform, false);
            var plate = plateObj.AddComponent<Image>();
            if (plateSprite != null) plate.sprite = plateSprite;
            else plate.color = new Color(0f, 0f, 0f, 0.6f);
            var plateRect = plate.rectTransform;
            plateRect.anchorMin = new Vector2(0f, 1f);
            plateRect.anchorMax = new Vector2(0f, 1f);
            plateRect.pivot = new Vector2(0f, 1f);
            plateRect.anchoredPosition = new Vector2(10f, -10f);
            plateRect.sizeDelta = new Vector2(636f * S, 116f * S);

            Vector2 P(float x, float y) => new Vector2(x * S, -y * S);
            Vector2 Sz(float w, float h) => new Vector2(w * S, h * S);

            var healthFill = MakeFill(plateObj.transform, "HealthFill", healthSprite, new Color(0.85f, 0.15f, 0.15f), P(142, 40), Sz(481, 18));
            var awakenedFill = MakeFill(plateObj.transform, "EnergyFill", awakenedSprite, new Color(0.55f, 0.35f, 0.9f), P(110, 66), Sz(206, 20));
            var xpFill = MakeFill(plateObj.transform, "YinYangFill", xpSprite, new Color(0.95f, 0.8f, 0.35f), P(86, 90), Sz(230, 22));

            // medallion sits over the middle of the health bar, above the fill
            if (emblemSprite != null)
            {
                var emblemObj = new GameObject("Emblem");
                emblemObj.transform.SetParent(plateObj.transform, false);
                var emblem = emblemObj.AddComponent<Image>();
                emblem.sprite = emblemSprite;
                var emblemRect = emblem.rectTransform;
                emblemRect.anchorMin = new Vector2(0f, 1f);
                emblemRect.anchorMax = new Vector2(0f, 1f);
                emblemRect.pivot = new Vector2(0f, 1f);
                emblemRect.anchoredPosition = P(300, 2);
                emblemRect.sizeDelta = Sz(125, 66);
            }

            // annotation strip (right of the bars) was blanked by the extractor
            var hpText = MakeUiText(plateObj.transform, "HpText", P(524, 40), Sz(110, 22), 9, new Color(0.95f, 0.85f, 0.85f));
            var lvText = MakeUiText(plateObj.transform, "LvText", P(524, 64), Sz(110, 22), 9, new Color(0.95f, 0.8f, 0.4f));
            var xpText = MakeUiText(plateObj.transform, "XpText", P(524, 88), Sz(110, 22), 8, new Color(0.95f, 0.85f, 0.55f));

            var plateUi = plateObj.AddComponent<PlayerPlateUI>();
            var so = new SerializedObject(plateUi);
            so.FindProperty("health").objectReferenceValue = hilo.GetComponent<Health>();
            so.FindProperty("healthFill").objectReferenceValue = healthFill;
            so.FindProperty("healthText").objectReferenceValue = hpText;
            so.FindProperty("levelText").objectReferenceValue = lvText;
            so.FindProperty("xpFill").objectReferenceValue = xpFill;
            so.FindProperty("xpText").objectReferenceValue = xpText;
            so.ApplyModifiedPropertiesWithoutUndo();

            var meterUi = plateObj.AddComponent<AwakenedMeterUI>();
            var meterSo = new SerializedObject(meterUi);
            meterSo.FindProperty("player").objectReferenceValue = hilo.GetComponent<PlayerController>();
            meterSo.FindProperty("fill").objectReferenceValue = awakenedFill;
            meterSo.ApplyModifiedPropertiesWithoutUndo();

            var stanceText = MakeUiText(group.transform, "StanceAmmo", new Vector2(12f, -96f), new Vector2(240f, 15f), 9, Color.white);
            var stanceUi = group.AddComponent<StanceAmmoUI>();
            var stanceSo = new SerializedObject(stanceUi);
            stanceSo.FindProperty("player").objectReferenceValue = hilo.GetComponent<PlayerController>();
            stanceSo.FindProperty("label").objectReferenceValue = stanceText;
            stanceSo.ApplyModifiedPropertiesWithoutUndo();

            var statusText = MakeUiText(group.transform, "PlayerStatus", new Vector2(244f, -24f), new Vector2(130f, 16f), 9, Color.white);
            var statusUi = group.AddComponent<PlayerStatusUI>();
            var statusSo = new SerializedObject(statusUi);
            statusSo.FindProperty("player").objectReferenceValue = hilo.GetComponent<PlayerController>();
            statusSo.FindProperty("label").objectReferenceValue = statusText;
            statusSo.ApplyModifiedPropertiesWithoutUndo();

            var notifText = MakeUiText(group.transform, "Notifications", new Vector2(0f, -12f), new Vector2(240f, 84f), 10, new Color(0.95f, 0.9f, 0.7f), TextAnchor.UpperRight);
            var notifRect = notifText.rectTransform;
            notifRect.anchorMin = new Vector2(1f, 1f);
            notifRect.anchorMax = new Vector2(1f, 1f);
            notifRect.pivot = new Vector2(1f, 1f);
            notifRect.anchoredPosition = new Vector2(-16f, -16f);
            var notifUi = group.AddComponent<NotificationUI>();
            var notifSo = new SerializedObject(notifUi);
            notifSo.FindProperty("label").objectReferenceValue = notifText;
            notifSo.FindProperty("player").objectReferenceValue = hilo.GetComponent<PlayerController>();
            notifSo.ApplyModifiedPropertiesWithoutUndo();

            BuildComboCounter(group, hilo, new Color(0.78f, 0.5f, 1f), "hilo");
            BuildComboLevelMeter(group, hilo, "hilo");
            WireLockReticle(hilo, "hilo");

            return group;
        }

        /// Start screen: pick Silver or Hilo. Both heroes and both HUD groups
        /// are benched until CharacterSelectUI activates the chosen pair.
        /// Returns the (inactive) panel — the intro crawl activates it.
        private static GameObject BuildCharacterSelect(GameObject player, GameObject hilo,
            GameObject silverHud, GameObject hiloHud)
        {
            var canvas = GameObject.Find("Canvas");
            if (canvas == null) return null;

            player.SetActive(false);
            hilo.SetActive(false);
            if (silverHud != null) silverHud.SetActive(false);
            if (hiloHud != null) hiloHud.SetActive(false);

            var panelObj = new GameObject("CharacterSelect");
            panelObj.transform.SetParent(canvas.transform, false);
            var bg = panelObj.AddComponent<Image>();
            bg.color = new Color(0.01f, 0.012f, 0.03f, 1f);
            var panelRect = bg.rectTransform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // dedicated select-screen artwork (hero panels, roster, hints all
            // baked into the art), full height, centered
            var artObj = new GameObject("SelectArt");
            artObj.transform.SetParent(panelObj.transform, false);
            var art = artObj.AddComponent<Image>();
            art.sprite = ImportUiSprite($"{UiDir}/Screens/select_screen.png");
            art.preserveAspect = true;
            var artRect = art.rectTransform;
            artRect.anchorMin = new Vector2(0.5f, 0.02f);
            artRect.anchorMax = new Vector2(0.5f, 0.98f);
            artRect.pivot = new Vector2(0.5f, 0.5f);
            artRect.anchoredPosition = Vector2.zero;
            artRect.sizeDelta = new Vector2(540f, 0f);

            // selection highlight tints anchored over the two hero panels
            Image MakeTint(string slotName, float minX, float maxX)
            {
                var frameObj = new GameObject(slotName);
                frameObj.transform.SetParent(artObj.transform, false);
                var frame = frameObj.AddComponent<Image>();
                frame.color = new Color(0.35f, 0.35f, 0.4f, 0.16f);
                var rect = frame.rectTransform;
                rect.anchorMin = new Vector2(minX, 0.30f);
                rect.anchorMax = new Vector2(maxX, 0.95f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                return frame;
            }

            var silverFrame = MakeTint("SilverSlot", 0.03f, 0.485f);
            var hiloFrame = MakeTint("HiloSlot", 0.515f, 0.97f);

            var ui = panelObj.AddComponent<CharacterSelectUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("silverRoot").objectReferenceValue = player;
            so.FindProperty("hiloRoot").objectReferenceValue = hilo;
            so.FindProperty("silverHud").objectReferenceValue = silverHud;
            so.FindProperty("hiloHud").objectReferenceValue = hiloHud;
            so.FindProperty("panel").objectReferenceValue = panelObj;
            so.FindProperty("silverFrame").objectReferenceValue = silverFrame;
            so.FindProperty("hiloFrame").objectReferenceValue = hiloFrame;
            so.ApplyModifiedPropertiesWithoutUndo();

            panelObj.SetActive(false); // the intro crawl turns it on
            return panelObj;
        }

        // The official story, as the Corporation tells it. The reveal ladder
        // (Docs/STORY.md) contradicts this later — that's the point.
        private const string IntroCrawlText =
            "A.X. 2347\n\n" +
            "THE SPLICE PLAGUE\n\n\n" +
            "Three decades ago the first outbreak turned\n" +
            "the Meridian arcologies into hunting grounds.\n\n" +
            "The infected do not die.\n" +
            "They CHANGE.\n\n" +
            "Werewolves. Chimeras. Reapers.\n" +
            "Things with no names yet.\n\n\n" +
            "Civilization survives behind blast-walls,\n" +
            "and behind one name:\n\n" +
            "SCEMATICA DYNAMICS\n\n" +
            "The Corporation feeds the districts, mints\n" +
            "the SCEMA scrip that pays for clean water\n" +
            "and ammunition, and fields the only force\n" +
            "that can answer an outbreak:\n\n" +
            "THE GUN-HUNTERS\n\n" +
            "You are one of them.\n\n\n" +
            "Tonight, dispatch came down from Scematica\n" +
            "tower: District 9 has gone dark. Outbreak-\n" +
            "class signatures on every sensor.\n" +
            "Civilians still inside.\n\n" +
            "Your orders: descend into District 9,\n" +
            "purge the infestation, and recover\n" +
            "whatever the Corporation lost down there.\n\n\n" +
            "They did not say what it was.\n\n" +
            "They never do.";

        /// Skippable Star-Wars-style lore crawl shown before character select.
        private static void BuildIntroCrawl(GameObject select)
        {
            var canvas = GameObject.Find("Canvas");
            if (canvas == null) return;

            var panelObj = new GameObject("IntroCrawl");
            panelObj.transform.SetParent(canvas.transform, false);
            var bg = panelObj.AddComponent<Image>();
            bg.color = new Color(0.01f, 0.015f, 0.03f, 1f);
            var panelRect = bg.rectTransform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var crawlObj = new GameObject("CrawlText");
            crawlObj.transform.SetParent(panelObj.transform, false);
            var crawl = crawlObj.AddComponent<Text>();
            crawl.font = GetUiFont();
            crawl.fontSize = 24;
            crawl.fontStyle = FontStyle.Bold;
            crawl.color = new Color(0.88f, 0.94f, 1f);
            crawl.alignment = TextAnchor.UpperCenter;
            crawl.horizontalOverflow = HorizontalWrapMode.Wrap;
            crawl.verticalOverflow = VerticalWrapMode.Overflow;
            crawl.lineSpacing = 1.4f;
            crawl.text = IntroCrawlText;
            var crawlRect = crawl.rectTransform;
            crawlRect.anchorMin = new Vector2(0.5f, 0f);
            crawlRect.anchorMax = new Vector2(0.5f, 0f);
            crawlRect.pivot = new Vector2(0.5f, 1f);
            crawlRect.anchoredPosition = Vector2.zero; // top of text at screen bottom
            crawlRect.sizeDelta = new Vector2(760f, 0f);

            var hint = MakeUiText(panelObj.transform, "Hint", Vector2.zero, new Vector2(320f, 18f),
                11, new Color(0.5f, 0.55f, 0.65f), TextAnchor.MiddleRight);
            var hintRect = hint.rectTransform;
            hintRect.anchorMin = new Vector2(1f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(1f, 0f);
            hintRect.anchoredPosition = new Vector2(-18f, 12f);

            var ui = panelObj.AddComponent<IntroCrawlUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("crawlRect").objectReferenceValue = crawlRect;
            so.FindProperty("crawlText").objectReferenceValue = crawl;
            so.FindProperty("hintText").objectReferenceValue = hint;
            so.FindProperty("characterSelect").objectReferenceValue = select;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// First frame of an animation folder as a select-screen portrait.
        /// Forces sprite import: at scene-build time fresh art may still be
        /// typed as a plain texture (Sprite sub-asset doesn't exist yet).
        private static Sprite FirstFrameSprite(string folder)
        {
            if (!Directory.Exists(folder)) return null;
            var file = Directory.GetFiles(folder, "*.png").OrderBy(p => p).FirstOrDefault();
            return file == null ? null : ImportUiSprite(file.Replace('\\', '/'));
        }

        private static Sprite ImportUiSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void BuildScematicaUI()
        {
            var canvas = GameObject.Find("Canvas");
            if (canvas == null) return;

            var icon = ImportUiSprite($"{UiDir}/scematica_icon.png");
            ImportUiSprite($"{UiDir}/scematica_logo.png");

            var holder = new GameObject("ScematicaCounter");
            holder.transform.SetParent(canvas.transform, false);
            var holderRect = holder.AddComponent<RectTransform>();
            holderRect.anchorMin = new Vector2(0f, 1f);
            holderRect.anchorMax = new Vector2(0f, 1f);
            holderRect.pivot = new Vector2(0f, 1f);
            holderRect.anchoredPosition = new Vector2(12f, -82f);
            holderRect.sizeDelta = new Vector2(220f, 22f);

            var iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(holder.transform, false);
            var iconImage = iconObj.AddComponent<Image>();
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            var iconRect = iconImage.rectTransform;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(18f, 18f);

            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(holder.transform, false);
            var label = labelObj.AddComponent<Text>();
            label.font = GetUiFont();
            label.fontSize = 11;
            label.fontStyle = FontStyle.Bold;
            label.color = new Color(0.85f, 0.88f, 0.95f);
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(24f, 0f);
            labelRect.offsetMax = Vector2.zero;

            var ui = holder.AddComponent<ScematicaCounterUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("label").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildDialogueUI()
        {
            var canvas = GameObject.Find("Canvas");
            if (canvas == null) return;

            var panelObj = new GameObject("DialoguePanel");
            panelObj.transform.SetParent(canvas.transform, false);
            var bg = panelObj.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.03f, 0.05f, 0.94f);
            var panelRect = bg.rectTransform;
            panelRect.anchorMin = new Vector2(0.08f, 0f);
            panelRect.anchorMax = new Vector2(0.92f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 22f);
            panelRect.sizeDelta = new Vector2(0f, 118f);

            var speaker = MakeUiText(panelObj.transform, "Speaker", new Vector2(18f, -10f), new Vector2(300f, 22f), 15, new Color(0.95f, 0.55f, 0.45f));
            var body = MakeUiText(panelObj.transform, "Body", new Vector2(18f, -36f), new Vector2(620f, 70f), 14, new Color(0.92f, 0.94f, 0.98f), TextAnchor.UpperLeft, false);
            var bodyRect = body.rectTransform;
            bodyRect.anchorMin = new Vector2(0f, 1f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.sizeDelta = new Vector2(-36f, 70f);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;

            var hint = MakeUiText(panelObj.transform, "Hint", new Vector2(18f, -96f), new Vector2(300f, 16f), 10, new Color(0.55f, 0.6f, 0.7f));

            var uiObj = new GameObject("DialogueUI");
            uiObj.transform.SetParent(canvas.transform, false);
            var ui = uiObj.AddComponent<Story.DialogueUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("panel").objectReferenceValue = panelObj;
            so.FindProperty("speakerText").objectReferenceValue = speaker;
            so.FindProperty("bodyText").objectReferenceValue = body;
            so.FindProperty("hintText").objectReferenceValue = hint;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildCodexUI()
        {
            var canvas = GameObject.Find("Canvas");
            if (canvas == null) return;

            var panelObj = new GameObject("CodexPanel");
            panelObj.transform.SetParent(canvas.transform, false);
            var bg = panelObj.AddComponent<Image>();
            bg.color = new Color(0.015f, 0.03f, 0.045f, 0.95f);
            var panelRect = bg.rectTransform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = new Vector2(60f, 50f);
            panelRect.offsetMax = new Vector2(-60f, -50f);

            Text MakeColumn(string name, float minX, float maxX)
            {
                var obj = new GameObject(name);
                obj.transform.SetParent(panelObj.transform, false);
                var text = obj.AddComponent<Text>();
                text.font = GetUiFont();
                text.fontSize = 13;
                text.color = new Color(0.8f, 0.92f, 1f);
                text.alignment = TextAnchor.UpperLeft;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                var rect = text.rectTransform;
                rect.anchorMin = new Vector2(minX, 0f);
                rect.anchorMax = new Vector2(maxX, 1f);
                rect.offsetMin = new Vector2(16f, 14f);
                rect.offsetMax = new Vector2(-16f, -14f);
                return text;
            }

            var list = MakeColumn("List", 0f, 0.32f);
            var bodyCol = MakeColumn("Body", 0.32f, 1f);

            var uiObj = new GameObject("CodexUI");
            uiObj.transform.SetParent(canvas.transform, false);
            var ui = uiObj.AddComponent<Story.CodexUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("panel").objectReferenceValue = panelObj;
            so.FindProperty("listColumn").objectReferenceValue = list;
            so.FindProperty("bodyColumn").objectReferenceValue = bodyCol;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// Mid-combat level-up menu (touchpad / T): stats tab + three skill
        /// path tabs with cursor-driven point spending. Level cap 99.
        private static void BuildLevelUpMenu()
        {
            var canvas = GameObject.Find("Canvas");
            if (canvas == null) return;

            var panelObj = new GameObject("LevelUpPanel");
            panelObj.transform.SetParent(canvas.transform, false);
            var bg = panelObj.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.03f, 0.06f, 0.95f);
            var panelRect = bg.rectTransform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = new Vector2(70f, 50f);
            panelRect.offsetMax = new Vector2(-70f, -50f);

            var title = MakeUiText(panelObj.transform, "Title", new Vector2(18f, -12f), new Vector2(700f, 22f),
                14, new Color(0.95f, 0.9f, 0.7f));
            var tabs = MakeUiText(panelObj.transform, "Tabs", new Vector2(18f, -40f), new Vector2(700f, 20f),
                12, new Color(0.7f, 0.85f, 1f));

            Text MakeColumn(string name, float minX, float maxX)
            {
                var obj = new GameObject(name);
                obj.transform.SetParent(panelObj.transform, false);
                var text = obj.AddComponent<Text>();
                text.font = GetUiFont();
                text.fontSize = 12;
                text.color = new Color(0.85f, 0.92f, 1f);
                text.alignment = TextAnchor.UpperLeft;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                var rect = text.rectTransform;
                rect.anchorMin = new Vector2(minX, 0f);
                rect.anchorMax = new Vector2(maxX, 1f);
                rect.offsetMin = new Vector2(18f, 34f);
                rect.offsetMax = new Vector2(-12f, -66f);
                return text;
            }

            var list = MakeColumn("List", 0f, 0.55f);
            var detail = MakeColumn("Detail", 0.55f, 1f);

            var hint = MakeUiText(panelObj.transform, "Hint", new Vector2(18f, 0f), new Vector2(700f, 16f),
                9, new Color(0.55f, 0.6f, 0.7f));
            var hintRect = hint.rectTransform;
            hintRect.anchorMin = new Vector2(0f, 0f);
            hintRect.anchorMax = new Vector2(0f, 0f);
            hintRect.pivot = new Vector2(0f, 0f);
            hintRect.anchoredPosition = new Vector2(18f, 10f);

            var uiObj = new GameObject("LevelUpMenuUI");
            uiObj.transform.SetParent(canvas.transform, false);
            var ui = uiObj.AddComponent<LevelUpMenuUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("panel").objectReferenceValue = panelObj;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("tabsText").objectReferenceValue = tabs;
            so.FindProperty("listText").objectReferenceValue = list;
            so.FindProperty("detailText").objectReferenceValue = detail;
            so.FindProperty("hintText").objectReferenceValue = hint;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// Character hub: touchpad/T opens; L1/R1 tab between the attribute
        /// sheet, the leveling menu, and the chip core. Sub-screens flip to
        /// external control so only the hub owns the pause.
        private static void BuildCharacterHub()
        {
            var canvas = GameObject.Find("Canvas");
            if (canvas == null) return;

            var bannerObj = new GameObject("HubBanner");
            bannerObj.transform.SetParent(canvas.transform, false);
            var bannerBg = bannerObj.AddComponent<Image>();
            bannerBg.color = new Color(0.01f, 0.02f, 0.05f, 0.92f);
            var bannerRect = bannerBg.rectTransform;
            bannerRect.anchorMin = new Vector2(0f, 1f);
            bannerRect.anchorMax = new Vector2(1f, 1f);
            bannerRect.pivot = new Vector2(0.5f, 1f);
            bannerRect.anchoredPosition = Vector2.zero;
            bannerRect.sizeDelta = new Vector2(0f, 26f);

            var tabs = MakeUiText(bannerObj.transform, "Tabs", new Vector2(14f, -4f), new Vector2(900f, 18f),
                11, new Color(0.75f, 0.88f, 1f));

            var sheet = Object.FindAnyObjectByType<CharacterSheetUI>(FindObjectsInactive.Include);
            var levelUp = Object.FindAnyObjectByType<LevelUpMenuUI>(FindObjectsInactive.Include);
            var chip = Object.FindAnyObjectByType<ChipCoreUI>(FindObjectsInactive.Include);
            foreach (var component in new Component[] { sheet, levelUp, chip })
            {
                if (component == null) continue;
                var subSo = new SerializedObject(component);
                subSo.FindProperty("externalControl").boolValue = true;
                subSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var hubObj = new GameObject("CharacterHubUI");
            hubObj.transform.SetParent(canvas.transform, false);
            var hub = hubObj.AddComponent<CharacterHubUI>();
            var so = new SerializedObject(hub);
            so.FindProperty("attributes").objectReferenceValue = sheet;
            so.FindProperty("leveling").objectReferenceValue = levelUp;
            so.FindProperty("chipCore").objectReferenceValue = chip;
            so.FindProperty("banner").objectReferenceValue = bannerObj;
            so.FindProperty("tabsText").objectReferenceValue = tabs;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// Cybernetic Development Chip screen (G / L3): full-art chassis with
        /// runtime-generated interactive node rings and live data rails.
        private static void BuildChipCoreUI()
        {
            var canvas = GameObject.Find("Canvas");
            if (canvas == null) return;

            var panelObj = new GameObject("ChipCorePanel");
            panelObj.transform.SetParent(canvas.transform, false);
            var bg = panelObj.AddComponent<Image>();
            var artSprite = ImportUiSprite($"{UiDir}/Screens/leveltree_screen.png");
            if (artSprite != null) bg.sprite = artSprite;
            else bg.color = new Color(0.02f, 0.03f, 0.06f, 0.97f);
            var panelRect = bg.rectTransform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            Image Patch(string name, float minX, float minY, float maxX, float maxY, float alpha = 1f)
            {
                var obj = new GameObject(name);
                obj.transform.SetParent(panelObj.transform, false);
                var img = obj.AddComponent<Image>();
                img.color = new Color(0.015f, 0.025f, 0.06f, alpha);
                var rect = img.rectTransform;
                rect.anchorMin = new Vector2(minX, minY);
                rect.anchorMax = new Vector2(maxX, maxY);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                return img;
            }

            Text Zone(string name, float minX, float minY, float maxX, float maxY, int size,
                TextAnchor align = TextAnchor.UpperLeft)
            {
                var obj = new GameObject(name);
                obj.transform.SetParent(panelObj.transform, false);
                var text = obj.AddComponent<Text>();
                text.font = GetUiFont();
                text.fontSize = size;
                text.color = new Color(0.8f, 0.92f, 1f);
                text.alignment = align;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                var rect = text.rectTransform;
                rect.anchorMin = new Vector2(minX, minY);
                rect.anchorMax = new Vector2(maxX, maxY);
                rect.offsetMin = new Vector2(4f, 2f);
                rect.offsetMax = new Vector2(-4f, -2f);
                return text;
            }

            // node web container spans the circuit area
            var webObj = new GameObject("NodeWeb");
            webObj.transform.SetParent(panelObj.transform, false);
            var webRect = webObj.AddComponent<RectTransform>();
            webRect.anchorMin = Vector2.zero;
            webRect.anchorMax = Vector2.one;
            webRect.offsetMin = Vector2.zero;
            webRect.offsetMax = Vector2.zero;

            Patch("PointsPatch", 0.448f, 0.885f, 0.535f, 0.948f);
            var points = Zone("Points", 0.448f, 0.885f, 0.535f, 0.948f, 22, TextAnchor.MiddleCenter);

            Patch("OverviewPatch", 0.822f, 0.655f, 0.978f, 0.898f);
            var overview = Zone("Overview", 0.822f, 0.655f, 0.978f, 0.898f, 9);

            Patch("PreviewPatch", 0.822f, 0.27f, 0.978f, 0.612f);
            var preview = Zone("Preview", 0.822f, 0.27f, 0.978f, 0.612f, 9);

            Patch("SummaryPatch", 0.822f, 0.06f, 0.978f, 0.232f);
            var summary = Zone("Summary", 0.822f, 0.06f, 0.978f, 0.232f, 9);

            Patch("DetailPatch", 0.17f, 0.115f, 0.62f, 0.215f, 0.85f);
            var detail = Zone("Detail", 0.17f, 0.115f, 0.62f, 0.215f, 10);

            var markerObj = new GameObject("LevelMarker");
            markerObj.transform.SetParent(panelObj.transform, false);
            var marker = markerObj.AddComponent<Image>();
            marker.color = new Color(1f, 0.85f, 0.4f);
            var markerRect = marker.rectTransform;
            markerRect.anchorMin = new Vector2(0.075f, 0.025f);
            markerRect.anchorMax = new Vector2(0.075f, 0.025f);
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.sizeDelta = new Vector2(18f, 4f);

            var uiObj = new GameObject("ChipCoreUI");
            uiObj.transform.SetParent(canvas.transform, false);
            var ui = uiObj.AddComponent<ChipCoreUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("panel").objectReferenceValue = panelObj;
            so.FindProperty("web").objectReferenceValue = webRect;
            so.FindProperty("pointsText").objectReferenceValue = points;
            so.FindProperty("overviewText").objectReferenceValue = overview;
            so.FindProperty("previewText").objectReferenceValue = preview;
            so.FindProperty("summaryText").objectReferenceValue = summary;
            so.FindProperty("detailText").objectReferenceValue = detail;
            so.FindProperty("levelMarker").objectReferenceValue = markerRect;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // datashards placed across the strip: (entry id, x, y)
        private static readonly (string id, float x, float y)[] DemoShards =
        {
            ("shard_01", 4.5f, -1.6f),
            ("shard_02", 9.5f, -3.6f),
            ("shard_03", 16f, -2.2f),
            ("shard_04", 21f, -3.8f),
            ("shard_05", 28.5f, -1.5f),
            ("shard_09", 37.5f, -2.8f),
        };

        private static void BuildStoryObjects()
        {
            new GameObject("StoryDirector").AddComponent<Story.StoryDirector>();

            foreach (var (id, x, y) in DemoShards)
            {
                var shard = new GameObject("Shard_" + id);
                shard.transform.position = new Vector3(x, y, 0f);

                var sr = shard.AddComponent<SpriteRenderer>();
                int index = System.Array.FindIndex(Story.LoreDatabase.Entries, e => e.id == id) + 1;
                sr.sprite = ImportUiSprite($"{UiDir}/Collectibles/shard_{index:00}.png")
                            ?? AssetDatabase.LoadAssetAtPath<Sprite>($"{UiDir}/scematica_icon.png");
                sr.sortingOrder = 100;
                shard.transform.localScale = Vector3.one * 0.85f;

                var col = shard.AddComponent<CircleCollider2D>();
                col.isTrigger = true;
                col.radius = 0.45f;

                var collectible = shard.AddComponent<Story.LoreCollectible>();
                SetPrivateField(collectible, "entryId", id);
            }
        }

        /// Character attribute screen (C / Share): dedicated art chassis with
        /// per-hero portrait and live stat data rendered in the blank zones.
        private static void BuildCharacterSheetUI()
        {
            var canvas = GameObject.Find("Canvas");
            if (canvas == null) return;

            var font = GetUiFont();

            var panelObj = new GameObject("CharacterSheetPanel");
            panelObj.transform.SetParent(canvas.transform, false);
            var bg = panelObj.AddComponent<Image>();
            bg.color = new Color(0.01f, 0.015f, 0.03f, 0.98f);
            var panelRect = bg.rectTransform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // attribute-screen artwork, full height, centered; overlays anchor
            // into its blank zones
            var artObj = new GameObject("AttrArt");
            artObj.transform.SetParent(panelObj.transform, false);
            var art = artObj.AddComponent<Image>();
            art.sprite = ImportUiSprite($"{UiDir}/Screens/attributes_screen.png");
            art.preserveAspect = true;
            var artRect = art.rectTransform;
            artRect.anchorMin = new Vector2(0.5f, 0.01f);
            artRect.anchorMax = new Vector2(0.5f, 0.99f);
            artRect.pivot = new Vector2(0.5f, 0.5f);
            artRect.anchoredPosition = Vector2.zero;
            artRect.sizeDelta = new Vector2(560f, 0f);

            Text MakeZone(string name, float minX, float minY, float maxX, float maxY, int size)
            {
                var obj = new GameObject(name);
                obj.transform.SetParent(artObj.transform, false);
                var text = obj.AddComponent<Text>();
                text.font = font;
                text.fontSize = size;
                text.color = new Color(0.82f, 0.92f, 1f);
                text.alignment = TextAnchor.UpperLeft;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                var rect = text.rectTransform;
                rect.anchorMin = new Vector2(minX, minY);
                rect.anchorMax = new Vector2(maxX, maxY);
                rect.offsetMin = new Vector2(4f, 2f);
                rect.offsetMax = new Vector2(-4f, -2f);
                return text;
            }

            // portrait inside the CHARACTER OVERVIEW box
            var portObj = new GameObject("Portrait");
            portObj.transform.SetParent(artObj.transform, false);
            var portrait = portObj.AddComponent<Image>();
            portrait.preserveAspect = true;
            var portRect = portrait.rectTransform;
            portRect.anchorMin = new Vector2(0.035f, 0.635f);
            portRect.anchorMax = new Vector2(0.385f, 0.895f);
            portRect.offsetMin = Vector2.zero;
            portRect.offsetMax = Vector2.zero;

            var identity = MakeZone("Identity", 0.415f, 0.625f, 0.73f, 0.9f, 8);
            var levelZone = MakeZone("LevelZone", 0.755f, 0.625f, 0.985f, 0.9f, 8);
            var info = MakeZone("Info", 0.035f, 0.345f, 0.395f, 0.59f, 8);
            var vitals = MakeZone("Vitals", 0.415f, 0.345f, 0.985f, 0.59f, 8);
            var blade = MakeZone("Blade", 0.035f, 0.025f, 0.345f, 0.33f, 7);
            var gun = MakeZone("Gun", 0.355f, 0.025f, 0.665f, 0.33f, 7);
            var awakened = MakeZone("Awakened", 0.675f, 0.025f, 0.985f, 0.33f, 7);

            var uiObj = new GameObject("CharacterSheetUI");
            uiObj.transform.SetParent(canvas.transform, false);
            var ui = uiObj.AddComponent<CharacterSheetUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("panel").objectReferenceValue = panelObj;
            so.FindProperty("infoColumn").objectReferenceValue = info;
            so.FindProperty("bladeColumn").objectReferenceValue = blade;
            so.FindProperty("gunColumn").objectReferenceValue = gun;
            so.FindProperty("awakenedColumn").objectReferenceValue = awakened;
            so.FindProperty("portraitImage").objectReferenceValue = portrait;
            so.FindProperty("silverPortrait").objectReferenceValue = ImportUiSprite($"{UiDir}/Portraits/silver_select.png");
            so.FindProperty("hiloPortrait").objectReferenceValue = ImportUiSprite($"{UiDir}/Portraits/hilo_select.png");
            so.FindProperty("identityText").objectReferenceValue = identity;
            so.FindProperty("levelText").objectReferenceValue = levelZone;
            so.FindProperty("vitalsText").objectReferenceValue = vitals;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildCommandListUI()
        {
            var canvas = GameObject.Find("Canvas");
            if (canvas == null) return;

            var panelObj = new GameObject("CommandListPanel");
            panelObj.transform.SetParent(canvas.transform, false);
            var bg = panelObj.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.02f, 0.05f, 0.93f);
            var panelRect = bg.rectTransform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = new Vector2(30f, 30f);
            panelRect.offsetMax = new Vector2(-30f, -30f);

            var font = GetUiFont();

            Text MakeColumn(string name, float anchorMinX, float anchorMaxX)
            {
                var obj = new GameObject(name);
                obj.transform.SetParent(panelObj.transform, false);
                var text = obj.AddComponent<Text>();
                text.font = font;
                text.fontSize = 10;
                text.color = new Color(0.85f, 0.9f, 1f);
                text.alignment = TextAnchor.UpperLeft;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                var rect = text.rectTransform;
                rect.anchorMin = new Vector2(anchorMinX, 0f);
                rect.anchorMax = new Vector2(anchorMaxX, 1f);
                rect.offsetMin = new Vector2(12f, 40f);
                rect.offsetMax = new Vector2(-6f, -40f);
                return text;
            }

            var colA = MakeColumn("SwordA", 0f, 0.25f);
            var colB = MakeColumn("SwordB", 0.25f, 0.5f);
            var colGun = MakeColumn("Gun", 0.5f, 0.75f);
            var colAwk = MakeColumn("Awakened", 0.75f, 1f);

            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(panelObj.transform, false);
            var title = titleObj.AddComponent<Text>();
            title.font = font;
            title.fontSize = 22;
            title.fontStyle = FontStyle.Bold;
            title.color = new Color(0.95f, 0.95f, 1f);
            title.alignment = TextAnchor.MiddleCenter;
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = Vector2.zero;
            titleRect.sizeDelta = new Vector2(0f, 36f);

            var pauseIconObj = new GameObject("ScematicaIcon");
            pauseIconObj.transform.SetParent(panelObj.transform, false);
            var pauseIcon = pauseIconObj.AddComponent<Image>();
            pauseIcon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiDir}/scematica_icon.png");
            pauseIcon.preserveAspect = true;
            var pauseIconRect = pauseIcon.rectTransform;
            pauseIconRect.anchorMin = new Vector2(0f, 0f);
            pauseIconRect.anchorMax = new Vector2(0f, 0f);
            pauseIconRect.pivot = new Vector2(0f, 0f);
            pauseIconRect.anchoredPosition = new Vector2(12f, 8f);
            pauseIconRect.sizeDelta = new Vector2(24f, 24f);

            var balanceObj = new GameObject("ScematicaBalance");
            balanceObj.transform.SetParent(panelObj.transform, false);
            var balance = balanceObj.AddComponent<Text>();
            balance.font = font;
            balance.fontSize = 15;
            balance.fontStyle = FontStyle.Bold;
            balance.color = new Color(0.85f, 0.88f, 0.95f);
            balance.alignment = TextAnchor.MiddleLeft;
            balance.horizontalOverflow = HorizontalWrapMode.Overflow;
            var balanceRect = balance.rectTransform;
            balanceRect.anchorMin = new Vector2(0f, 0f);
            balanceRect.anchorMax = new Vector2(0f, 0f);
            balanceRect.pivot = new Vector2(0f, 0f);
            balanceRect.anchoredPosition = new Vector2(44f, 8f);
            balanceRect.sizeDelta = new Vector2(300f, 24f);

            var uiObj = new GameObject("CommandListUI");
            uiObj.transform.SetParent(canvas.transform, false);
            var ui = uiObj.AddComponent<UI.CommandListUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("swordMoveSet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/SwordMoveSet.asset");
            so.FindProperty("gunMoveSet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/GunMoveSet.asset");
            so.FindProperty("awakenedMoveSet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/AwakenedMoveSet.asset");
            so.FindProperty("dashMoveSet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/DashMoveSet.asset");
            so.FindProperty("sprintMoveSet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/SprintMoveSet.asset");
            so.FindProperty("teleportMoveSet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/TeleportMoveSet.asset");
            so.FindProperty("airMoveSet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/AirMoveSet.asset");
            so.FindProperty("awakenedAirMoveSet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MoveSet>($"{DataDir}/AwakenedAirMoveSet.asset");
            WireHiloCommandListSets(so);
            so.FindProperty("panel").objectReferenceValue = panelObj;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("swordColumnA").objectReferenceValue = colA;
            so.FindProperty("swordColumnB").objectReferenceValue = colB;
            so.FindProperty("gunColumn").objectReferenceValue = colGun;
            so.FindProperty("awakenedColumn").objectReferenceValue = colAwk;
            so.FindProperty("balanceText").objectReferenceValue = balance;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateWall(string name, Vector2 pos, Vector2 size)
        {
            var wall = new GameObject(name);
            wall.transform.position = pos;
            var col = wall.AddComponent<BoxCollider2D>();
            col.size = size;
        }

        private static void SetPrivateField(Object target, string fieldName, object value, System.Type declaringType = null)
        {
            var type = declaringType ?? target.GetType();
            var field = type.GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field == null)
            {
                Debug.LogError($"Field {fieldName} not found on {type.Name}");
                return;
            }
            field.SetValue(target, value);
            EditorUtility.SetDirty(target);
        }
    }
}
