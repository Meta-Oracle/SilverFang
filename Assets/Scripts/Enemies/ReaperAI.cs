using UnityEngine;

namespace SilverFang.Enemies
{
    /// M-09 Reaper: ranged rifle unit. Holds a firing band, retreats when crowded,
    /// falls back to a melee slash when cornered.
    public class ReaperAI : EnemyAI
    {
        [Header("Reaper")]
        [SerializeField] private float preferredMin = 4f;
        [SerializeField] private float preferredMax = 7f;

        protected override void Tick()
        {
            float distX = Mathf.Abs(ToTarget.x);
            bool alignedY = Mathf.Abs(ToTarget.y) <= attackRangeY;

            if (InMeleeRange)
            {
                Stop();
                if (cooldownTimer <= 0f) StartMeleeAttack(meleeAttack, HashAttack);
                return;
            }

            if (distX < preferredMin)
            {
                // Back away while keeping depth alignment for the next shot.
                Vector2 retreat = new Vector2(-Mathf.Sign(ToTarget.x), Mathf.Clamp(ToTarget.y, -1f, 1f) * 0.5f);
                Move(Vector2.ClampMagnitude(retreat, 1f));
                return;
            }

            if (distX <= preferredMax && alignedY)
            {
                Stop();
                if (cooldownTimer <= 0f) StartRangedAttack();
                return;
            }

            // Move toward the firing band / align depth.
            float desiredX = target.position.x - Mathf.Sign(ToTarget.x) * (preferredMin + preferredMax) * 0.5f;
            Vector2 move = new Vector2(desiredX - transform.position.x, ToTarget.y);
            Move(Vector2.ClampMagnitude(move, 1f));
        }
    }
}
