using SilverFang.Combat;
using UnityEngine;

namespace SilverFang.Enemies
{
    /// Samurai Swordslinger: fast duelist with a three-hit slash string.
    public class SamuraiAI : EnemyAI
    {
        [Header("Samurai")]
        [SerializeField] private AttackData slash2 = new AttackData { damage = 8, hitstun = 0.3f, knockback = new Vector2(1.5f, 0f) };
        [SerializeField] private AttackData slash3 = new AttackData { damage = 14, hitstun = 0.45f, knockback = new Vector2(4f, 0f), knocksDown = true };
        [SerializeField] private float stringGap = 0.1f;

        private int stringStep;

        protected override void Tick()
        {
            if (InMeleeRange)
            {
                Stop();
                if (cooldownTimer <= 0f)
                {
                    switch (stringStep)
                    {
                        case 0:
                            stringStep = 1;
                            cooldownTimer = stringGap;
                            StartMeleeAttack(meleeAttack, HashAttack);
                            break;
                        case 1:
                            stringStep = 2;
                            cooldownTimer = stringGap;
                            StartMeleeAttack(slash2, HashAttack);
                            break;
                        default:
                            stringStep = 0;
                            StartMeleeAttack(slash3, HashSpecial);
                            cooldownTimer = attackCooldown;
                            break;
                    }
                }
            }
            else
            {
                stringStep = 0;
                ApproachMelee(1.2f);
            }
        }

        public override void ReceiveHit(AttackData attack, float attackerFacing)
        {
            stringStep = 0;
            base.ReceiveHit(attack, attackerFacing);
        }
    }
}
