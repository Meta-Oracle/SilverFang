using UnityEngine;

namespace SilverFang.Physics
{
    /// Body mass tier. Heavier bodies shrug off pushback, fall harder, and ramp
    /// momentum slower — the backbone of how weight reads in combat.
    public enum WeightClass { Light, Medium, Heavy, Titan }

    /// Silver Fang's dedicated Combat Physics Engine.
    ///
    /// One authoritative rule set for every body in the game: character weight,
    /// melee knockback, projectile pushback, gravity / float, locomotion inertia,
    /// and momentum carry. It runs ALONGSIDE Unity's Rigidbody2D (it computes the
    /// impulses/targets; the rigidbody integrates them) so it layers onto the
    /// existing foundation instead of replacing it. Coupled with the Sprite
    /// Animation Engine, which reads these same body states to drive squash,
    /// lean, and float on the art.
    public static class CombatPhysics
    {
        // ---- global feel constants (single source of truth) ----
        public const float BaseGravity = -38f;     // up-arc gravity
        public const float FallGravityMult = 1.5f; // falling pulls harder for a weighty arc
        public const float HoverDrift = -1.0f;     // gentle descent while hovering
        public const float GroundFriction = 40f;   // how fast loose momentum bleeds off

        /// Knockback multiplier by weight — light bodies fly, titans barely flinch.
        public static float KnockbackResist(WeightClass w) => w switch
        {
            WeightClass.Light => 1.3f,
            WeightClass.Medium => 1f,
            WeightClass.Heavy => 0.55f,
            WeightClass.Titan => 0.3f,
            _ => 1f
        };

        /// Relative mass — drives fall speed and accel ramp.
        public static float Mass(WeightClass w) => w switch
        {
            WeightClass.Light => 0.7f,
            WeightClass.Medium => 1f,
            WeightClass.Heavy => 1.8f,
            WeightClass.Titan => 3f,
            _ => 1f
        };

        /// Weight-scaled melee knockback impulse from a base knockback + facing.
        public static Vector2 Knockback(Vector2 baseKnockback, float attackerFacing, WeightClass targetWeight)
        {
            float r = KnockbackResist(targetWeight);
            return new Vector2(Mathf.Abs(baseKnockback.x) * Mathf.Sign(attackerFacing) * r,
                               baseKnockback.y * r);
        }

        /// Projectile pushback impulse (weight-scaled; supports angled beams/rounds).
        public static Vector2 ProjectilePush(float force, Vector2 dir, WeightClass targetWeight)
            => dir.sqrMagnitude < 0.0001f ? Vector2.zero
             : dir.normalized * force * KnockbackResist(targetWeight);

        /// Sprint-jump momentum: a leap launched out of a sprint keeps its full
        /// horizontal drive instead of stalling, so running jumps carry across
        /// gaps. Returns the horizontal velocity the jump should preserve.
        public static float SprintJumpMomentum(float currentVx, float sprintSpeed, float facing)
        {
            float carried = Mathf.Abs(currentVx) > sprintSpeed * 0.55f ? currentVx : sprintSpeed * Mathf.Sign(facing);
            // a touch of extra drive so the running jump feels committed
            return carried * 1.08f;
        }

        /// Air-control blend toward a desired horizontal velocity while keeping
        /// most of the carried momentum (so you steer, not teleport, mid-leap).
        public static float AirControl(float currentVx, float desiredVx, float dt, float authority = 6f)
            => Mathf.Lerp(currentVx, desiredVx, Mathf.Clamp01(authority * dt));
    }
}
