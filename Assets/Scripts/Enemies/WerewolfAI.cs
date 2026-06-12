using SilverFang.Combat;
using UnityEngine;

namespace SilverFang.Enemies
{
    /// K-9X Protocol: melee bruiser. Pounces from mid-range, enrages below 40% HP.
    public class WerewolfAI : EnemyAI
    {
        [Header("Werewolf")]
        [SerializeField] private AttackData pounceAttack = new AttackData { damage = 14, hitstun = 0.4f, knockback = new Vector2(4f, 0f), knocksDown = true };
        [SerializeField] private float pounceMinRange = 3f;
        [SerializeField] private float pounceMaxRange = 6f;
        [SerializeField] private float pounceSpeed = 12f;
        [SerializeField] private float rageHealthFraction = 0.4f;
        [SerializeField] private float rageSpeedMultiplier = 1.4f;
        [SerializeField] private Color rageTint = new Color(1f, 0.4f, 0.4f);

        private bool pouncing;
        private bool enraged;

        protected override float DamageScale => enraged ? 1.3f : 1f;

        protected override void Update()
        {
            base.Update();
            if (!enraged && !IsDead && health.Current <= health.Max * rageHealthFraction)
            {
                enraged = true;
                moveSpeedX *= rageSpeedMultiplier;
                moveSpeedY *= rageSpeedMultiplier;
                attackCooldown *= 0.6f;
                if (sprite != null) sprite.color = rageTint;
                VFX.VfxManager.PlayAttached("rage_aura", transform, 1.5f, 1.6f);
            }

            if (pouncing)
            {
                body.linearVelocity = new Vector2(pounceSpeed * Facing, body.linearVelocity.y);
                if (InMeleeRange || Mathf.Abs(ToTarget.x) < attackRangeX)
                {
                    pouncing = false;
                    StartMeleeAttack(pounceAttack, HashSpecial);
                }
            }
        }

        protected override void Tick()
        {
            if (pouncing) return;

            float distX = Mathf.Abs(ToTarget.x);
            bool alignedY = Mathf.Abs(ToTarget.y) <= attackRangeY;

            if (cooldownTimer <= 0f && alignedY && distX >= pounceMinRange && distX <= pounceMaxRange)
            {
                pouncing = true;
                cooldownTimer = attackCooldown;
                return;
            }

            base.Tick();
        }

        public override void ReceiveHit(AttackData attack, float attackerFacing)
        {
            pouncing = false;
            base.ReceiveHit(attack, attackerFacing);
        }
    }
}
