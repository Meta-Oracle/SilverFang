using SilverFang.Combat;
using UnityEngine;

namespace SilverFang.Enemies
{
    /// AX-7 Sentinel: hit-and-run android. Dashes in, lands a two-hit combo, backs off.
    public class SentinelAI : EnemyAI
    {
        [Header("Sentinel")]
        [SerializeField] private AttackData comboFinisher = new AttackData { damage = 12, hitstun = 0.35f, knockback = new Vector2(3f, 0f) };
        [SerializeField] private float dashSpeedMultiplier = 1.8f;
        [SerializeField] private float retreatDuration = 1.2f;
        [SerializeField] private float retreatDistance = 5f;

        private int comboStep;
        private float retreatTimer;

        protected override void Update()
        {
            base.Update();
            if (retreatTimer > 0f) retreatTimer -= Time.deltaTime;
        }

        protected override void Tick()
        {
            if (retreatTimer > 0f)
            {
                if (Mathf.Abs(ToTarget.x) < retreatDistance)
                {
                    Vector2 retreat = new Vector2(-Mathf.Sign(ToTarget.x), 0f);
                    Move(retreat);
                }
                else Stop();
                return;
            }

            if (InMeleeRange)
            {
                Stop();
                if (cooldownTimer <= 0f)
                {
                    if (comboStep == 0)
                    {
                        comboStep = 1;
                        cooldownTimer = 0.15f;
                        StartMeleeAttack(meleeAttack, HashAttack);
                    }
                    else
                    {
                        comboStep = 0;
                        retreatTimer = retreatDuration;
                        StartMeleeAttack(comboFinisher, HashSpecial);
                        cooldownTimer = attackCooldown;
                    }
                }
            }
            else
            {
                comboStep = 0;
                ApproachMelee(dashSpeedMultiplier);
            }
        }
    }
}
