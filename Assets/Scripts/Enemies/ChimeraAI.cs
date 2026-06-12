using UnityEngine;

namespace SilverFang.Enemies
{
    /// X-7 Protocol: tanky quadruped mini-boss. Bites up close, spits acid at range.
    public class ChimeraAI : EnemyAI
    {
        [Header("Chimera")]
        [SerializeField] private float spitMinRange = 3f;
        [SerializeField] private float spitMaxRange = 9f;
        [SerializeField] private float spitCooldown = 3f;

        private float spitTimer;

        protected override void Update()
        {
            base.Update();
            if (spitTimer > 0f) spitTimer -= Time.deltaTime;
        }

        protected override void Tick()
        {
            float distX = Mathf.Abs(ToTarget.x);
            bool alignedY = Mathf.Abs(ToTarget.y) <= attackRangeY * 1.5f;

            if (spitTimer <= 0f && alignedY && distX >= spitMinRange && distX <= spitMaxRange)
            {
                spitTimer = spitCooldown;
                StartRangedAttack();
                return;
            }

            base.Tick();
        }
    }
}
