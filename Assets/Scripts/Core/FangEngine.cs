namespace SilverFang.Engine
{
    /// THE FANG ENGINE
    ///
    /// The integral integration layer of Silver Fang — the formal hub that ties
    /// every dedicated subsystem together and owns the data they share:
    ///
    ///   • Combat Physics Engine   (SilverFang.Physics.CombatPhysics) — weight,
    ///     knockback, gravity/float, momentum.
    ///   • True-Aim Engine         (SilverFang.Combat.CombatBallistics) — barrel
    ///     origin, power classes, projectile clashes, deflection.
    ///   • Debris Engine           (SilverFang.VFX.DebrisManager) — active shrapnel
    ///     fields (runs alongside True-Aim; both are projectile/shrapnel systems).
    ///   • Impact VFX Engine       (SilverFang.VFX.CombatVfx) — context-driven,
    ///     combo-scaled, RNG-pooled impact effects.
    ///   • Sprite Animation Engine (SilverFang.Animation.SpriteMotionEngine).
    ///   • Dynamic CQC Engine      (camera proximity/clinch + stun + grapple).
    ///
    /// It provides the ONE shared RNG (seedable for deterministic runs) and the
    /// single source of truth for global combat-feel tuning, so every engine's
    /// calculations, physics, and randomness flow through a single formalized core.
    public static class FangEngine
    {
        public const string Version = "1.0";

        // ---- Shared RNG (seedable; route engine randomness through here) ----
        private static System.Random rng = new System.Random();
        /// Seed for deterministic runs / replays / tests.
        public static void Seed(int seed) => rng = new System.Random(seed);
        public static float Value => (float)rng.NextDouble();
        public static float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);
        public static int RangeInt(int minInclusive, int maxExclusive) => rng.Next(minInclusive, maxExclusive);
        /// Roll a percent chance (0..100).
        public static bool Chance(float percent) => Value * 100f < percent;

        // ---- Global combat-feel tuning: single source of truth ----
        /// Modest universal hitstun multiplier (snappier trades; protects balance).
        public const float HitstunScale = 1.15f;
        /// Extra hitstun an electric-style hit tacks on (~3 frames @ 24fps).
        public const float ElectricHitstunBonus = 3f / 24f;
        /// VFX/feedback growth per combo tier (F..XXX).
        public const float ComboFeedbackStep = 0.08f;
        /// Projectile speed realism multiplier (tracer-fast, on-screen briefly).
        public const float ProjectileSpeedRealism = 5.0f;

        // ---- Cross-engine passthroughs (one call site for the common ops) ----
        public static float ProjectileStrength(SilverFang.Combat.AmmoDefinition ammo, float damageScale)
            => SilverFang.Combat.CombatBallistics.AutoStrength(ammo, damageScale);

        public static SilverFang.Combat.PowerClass PowerOf(float strength)
            => SilverFang.Combat.CombatBallistics.ClassOf(strength);
    }
}
