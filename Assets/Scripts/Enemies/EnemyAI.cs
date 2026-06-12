using SilverFang.Combat;
using UnityEngine;

namespace SilverFang.Enemies
{
    /// Base belt-scroller enemy: chases the player and uses a melee attack.
    /// Subclasses override Tick() for per-type behavior.
    public class EnemyAI : CharacterCombatant
    {
        [Header("AI")]
        [SerializeField] protected AttackData meleeAttack = new AttackData();
        [SerializeField] protected float attackRangeX = 1.2f;
        [SerializeField] protected float attackRangeY = 0.4f;
        [SerializeField] protected float attackCooldown = 1.5f;
        [SerializeField] protected float aggroRange = 10f;

        [Header("Ranged (optional)")]
        [SerializeField] protected Projectile projectilePrefab;
        [SerializeField] protected AmmoDefinition rangedAmmo;
        [SerializeField] protected Transform firePoint;

        protected Transform target;
        protected float cooldownTimer;

        protected static readonly int HashAttack = Animator.StringToHash("Attack");
        protected static readonly int HashSpecial = Animator.StringToHash("Special");
        protected static readonly int HashShoot = Animator.StringToHash("Shoot");

        protected Vector2 ToTarget => target != null
            ? (Vector2)(target.position - transform.position)
            : Vector2.zero;

        protected bool InMeleeRange =>
            Mathf.Abs(ToTarget.x) <= attackRangeX && Mathf.Abs(ToTarget.y) <= attackRangeY;

        protected virtual void Start()
        {
            var player = FindAnyObjectByType<Player.PlayerController>();
            if (player != null) target = player.transform;
        }

        protected override void Update()
        {
            base.Update();
            if (IsDead) return;
            if (target == null)
            {
                // hero may activate after Start (character select screen)
                var player = FindAnyObjectByType<Player.PlayerController>();
                if (player == null) return;
                target = player.transform;
            }
            if (Core.GamePause.IsPaused) return;

            if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;
            if (!CanAct) return;

            if (ToTarget.magnitude > aggroRange)
            {
                Stop();
                return;
            }

            SetFacing(ToTarget.x);
            Tick();
        }

        /// Per-type behavior. Default: walk into melee range and swing.
        protected virtual void Tick()
        {
            if (InMeleeRange)
            {
                Stop();
                if (cooldownTimer <= 0f) StartMeleeAttack(meleeAttack, PickMeleeTrigger());
            }
            else
            {
                ApproachMelee();
            }
        }

        // Number of baked melee variant states (Attack, Attack2, Attack3...).
        // SpriteBaker sets this to however many attack strips the sheet had,
        // so enemies cycle through their full move arsenal.
        [SerializeField] protected int meleeVariants = 1;

        protected int PickMeleeTrigger()
        {
            if (meleeVariants <= 1) return HashAttack;
            int pick = Random.Range(0, meleeVariants);
            return pick == 0 ? HashAttack : Animator.StringToHash("Attack" + (pick + 1));
        }

        protected void ApproachMelee(float speedScale = 1f)
        {
            var to = ToTarget;
            float desiredX = target.position.x - Mathf.Sign(to.x) * attackRangeX * 0.8f;
            Vector2 move = new Vector2(desiredX - transform.position.x, to.y);
            Move(Vector2.ClampMagnitude(move, 1f) * speedScale);
        }

        protected void StartMeleeAttack(AttackData attack, int animHash)
        {
            currentAttack = attack;
            IsAttacking = true;
            attackTimeout = 1.1f;
            cooldownTimer = attackCooldown;
            Stop();
            // small step into the swing so enemy attacks read as committed
            body.linearVelocity = new Vector2(Facing * 1.6f, 0f);
            if (animator != null) animator.SetTrigger(animHash);
        }

        protected void StartRangedAttack()
        {
            IsAttacking = true;
            attackTimeout = 1.1f;
            cooldownTimer = attackCooldown;
            Stop();
            if (animator != null) animator.SetTrigger(HashShoot);
        }

        public override void AnimEvent_Fire()
        {
            if (projectilePrefab == null || rangedAmmo == null) return;
            Vector3 origin = firePoint != null
                ? firePoint.position
                : transform.position + new Vector3(0.8f * Facing, 0.8f, 0f);
            VFX.VfxManager.Play("muzzle_burst", origin, Facing);
            var proj = Instantiate(projectilePrefab, origin, Quaternion.identity);
            proj.Fire(Team.Enemy, rangedAmmo, Facing, DamageScale);
        }
    }
}
