using UnityEngine;

namespace SilverFang.Combat
{
    /// Shared hit-reaction, movement, and attack plumbing for player and enemies.
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public class CharacterCombatant : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] protected float moveSpeedX = 5f;
        [SerializeField] protected float moveSpeedY = 3f;
        // a touch of inertia: ramp up over ~0.2s, settle over ~0.13s, so starts
        // and stops read as body weight instead of instant velocity snaps
        [SerializeField] protected float acceleration = 26f;
        [SerializeField] protected float deceleration = 40f;

        [Header("Combat")]
        [SerializeField] protected Hitbox hitbox;
        [SerializeField] protected float knockdownDuration = 1f;

        protected Rigidbody2D body;
        protected Health health;
        protected Animator animator;
        protected SpriteRenderer sprite;

        [Header("Status Effects")]
        [SerializeField] private float burnDps = 4f;
        [SerializeField] private float radiationDps = 2.5f;
        [SerializeField] private float radiationSlowFactor = 0.6f;
        [Tooltip("Per-enemy VFX suffix, e.g. \"werewolf\" plays status_frozen_werewolf. Empty = universal overlay.")]
        [SerializeField] private string statusVfxVariant = "";
        [SerializeField] private float statusVfxScale = 1f;
        [SerializeField] private float statusVfxOffsetY = 0.8f;

        protected float hitstunTimer;
        protected float knockdownTimer;
        protected float invulnTimer;
        protected AttackData currentAttack;
        // Watchdog: animation events at clip boundaries can be swallowed by
        // transitions. If AttackEnd never arrives the character would be stuck
        // attacking forever, so attacks force-end shortly after their clip length.
        protected float attackTimeout;

        private const float JuggleGravity = -22f;
        private const float JuggleRehitPop = 4.5f;
        private float juggleHeight;
        private float juggleVelocity;
        private Vector3 visualBasePos;

        private float statusTimer;
        private float dotAccumulator;
        private GameObject statusVfx;

        public StatusType Status { get; private set; } = StatusType.None;
        public float Facing { get; protected set; } = 1f;
        public bool InHitstun => hitstunTimer > 0f;
        public bool KnockedDown => knockdownTimer > 0f;
        public bool Juggled => juggleHeight > 0f || juggleVelocity > 0f;
        public bool IsDead => health.IsDead;
        public bool IsAttacking { get; protected set; }
        public bool CanAct => !InHitstun && !KnockedDown && !IsDead && !IsAttacking
                              && !Juggled && Status != StatusType.Frozen;
        public Health Health => health;

        /// When true, incoming enemy projectiles are deflected back instead of
        /// landing (set during dash i-frames, guard, and parry windows).
        public virtual bool DeflectsProjectiles => false;

        protected static readonly int HashMoveSpeed = Animator.StringToHash("MoveSpeed");
        protected static readonly int HashHurt = Animator.StringToHash("Hurt");
        protected static readonly int HashKnockdown = Animator.StringToHash("Knockdown");
        protected static readonly int HashGetUp = Animator.StringToHash("GetUp");
        protected static readonly int HashDead = Animator.StringToHash("Dead");

        protected virtual void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            animator = GetComponentInChildren<Animator>();
            sprite = GetComponentInChildren<SpriteRenderer>();
            CacheHurtHeavy();

            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.linearDamping = 4f; // knockback bleeds off instead of stopping dead

            health.OnDeath += OnDeath;
            if (sprite != null) visualBasePos = sprite.transform.localPosition;
        }

        protected virtual void Update()
        {
            if (invulnTimer > 0f) invulnTimer -= Time.deltaTime;
            if (hitstunTimer > 0f) hitstunTimer -= Time.deltaTime;
            if (IsAttacking && attackTimeout > 0f)
            {
                attackTimeout -= Time.deltaTime;
                if (attackTimeout <= 0f) AnimEvent_AttackEnd();
            }
            if (knockdownTimer > 0f)
            {
                knockdownTimer -= Time.deltaTime;
                if (knockdownTimer <= 0f && !IsDead && animator != null)
                    animator.SetTrigger(HashGetUp);
            }

            UpdateStatus();
            UpdateJuggle();
        }

        private void UpdateStatus()
        {
            if (Status == StatusType.None || IsDead) return;

            statusTimer -= Time.deltaTime;

            if (Status == StatusType.Burning) TickDot(burnDps);
            else if (Status == StatusType.Radiated) TickDot(radiationDps);
            else if (Status == StatusType.Frozen) body.linearVelocity = Vector2.zero;

            if (statusTimer <= 0f) ClearStatus();
        }

        private void TickDot(float dps)
        {
            dotAccumulator += dps * Time.deltaTime;
            if (dotAccumulator >= 1f)
            {
                int dmg = Mathf.FloorToInt(dotAccumulator);
                dotAccumulator -= dmg;
                health.TakeDamage(dmg);
                if (IsDead) ClearStatus();
            }
        }

        public void ApplyStatus(StatusType type, float duration)
        {
            // elemental resist (chip/skills/vitality) shortens hero statuses
            if (this is Player.PlayerController)
                duration *= Mathf.Clamp01(1f -
                    Progression.PlayerProgression.GetPercentOf(Progression.ModifierType.ElementalResist) / 100f);
            if (IsDead || type == StatusType.None || duration <= 0f) return;

            ClearStatus();
            Status = type;
            statusTimer = duration;
            dotAccumulator = 0f;

            switch (type)
            {
                case StatusType.Frozen:
                    if (animator != null) animator.speed = 0f;
                    if (sprite != null) sprite.color = new Color(0.55f, 0.8f, 1f);
                    InterruptAttack();
                    body.linearVelocity = Vector2.zero;
                    statusVfx = PlayStatusVfx("status_frozen", duration);
                    break;
                case StatusType.Burning:
                    if (sprite != null) sprite.color = new Color(1f, 0.6f, 0.4f);
                    statusVfx = PlayStatusVfx("status_burning", duration);
                    break;
                case StatusType.Radiated:
                    if (sprite != null) sprite.color = new Color(0.6f, 1f, 0.5f);
                    statusVfx = PlayStatusVfx("status_radiated", duration);
                    break;
            }
        }

        /// Per-enemy status overlay when a variant exists, universal otherwise.
        private GameObject PlayStatusVfx(string baseId, float duration)
        {
            if (!string.IsNullOrEmpty(statusVfxVariant))
            {
                var variant = VFX.VfxManager.PlayAttached(
                    $"{baseId}_{statusVfxVariant}", transform, duration, statusVfxScale, statusVfxOffsetY);
                if (variant != null) return variant;
            }
            return VFX.VfxManager.PlayAttached(baseId, transform, duration, statusVfxScale, statusVfxOffsetY);
        }

        private void ClearStatus()
        {
            if (Status == StatusType.Frozen && animator != null) animator.speed = 1f;
            if (sprite != null) sprite.color = Color.white;
            Status = StatusType.None;
            statusTimer = 0f;
            if (statusVfx != null)
            {
                Destroy(statusVfx);
                statusVfx = null;
            }
        }

        private void UpdateJuggle()
        {
            if (!Juggled) return;

            juggleVelocity += JuggleGravity * Time.deltaTime;
            juggleHeight += juggleVelocity * Time.deltaTime;

            if (juggleHeight <= 0f)
            {
                juggleHeight = 0f;
                juggleVelocity = 0f;
                if (sprite != null) sprite.transform.localPosition = visualBasePos;
                if (!IsDead)
                {
                    knockdownTimer = knockdownDuration;
                    hitstunTimer = 0f;
                    if (animator != null) animator.SetTrigger(HashKnockdown);
                }
                return;
            }

            if (sprite != null)
                sprite.transform.localPosition = visualBasePos + new Vector3(0f, juggleHeight, 0f);
        }

        public virtual void ReceiveHit(AttackData attack, float attackerFacing)
        {
            if (IsDead || invulnTimer > 0f) return;

            health.TakeDamage(attack.damage);
            InterruptAttack();

            Vector3 bloodPos = transform.position + new Vector3(0f, 1f, 0f);
            VFX.VfxManager.Play("blood_splatter", bloodPos, attackerFacing, attack.knocksDown ? 1.4f : 1f);

            body.linearVelocity = Vector2.zero;
            body.AddForce(new Vector2(attack.knockback.x * attackerFacing, attack.knockback.y), ForceMode2D.Impulse);

            if (attack.knocksDown || attack.launch > 0f)
                Core.CameraFollow.Instance?.Shake();

            if (IsDead) return;

            // Juggle: launches pop the target airborne; hitting an airborne target keeps it up.
            if (attack.launch > 0f)
            {
                juggleVelocity = attack.launch;
                if (juggleHeight <= 0f) juggleHeight = 0.01f;
                hitstunTimer = 0f;
                knockdownTimer = 0f;
                if (animator != null) animator.SetTrigger(HashHurt);
                return;
            }
            if (Juggled)
            {
                juggleVelocity = Mathf.Max(juggleVelocity, JuggleRehitPop);
                if (animator != null) animator.SetTrigger(HashHurt);
                return;
            }

            if (attack.knocksDown)
            {
                knockdownTimer = knockdownDuration;
                hitstunTimer = 0f;
                if (animator != null) animator.SetTrigger(HashKnockdown);
            }
            else
            {
                hitstunTimer = attack.hitstun;
                // big non-knockdown hits use the dedicated heavy-flinch strip
                if (animator != null)
                    animator.SetTrigger(attack.damage >= 18 && hasHurtHeavy ? HashHurtHeavy : HashHurt);
            }
        }

        protected static readonly int HashHurtHeavy = Animator.StringToHash("HurtHeavy");
        private bool hasHurtHeavy;

        protected void CacheHurtHeavy()
        {
            hasHurtHeavy = false;
            if (animator == null || animator.runtimeAnimatorController == null) return;
            foreach (var p in animator.parameters)
                if (p.name == "HurtHeavy") { hasHurtHeavy = true; break; }
        }

        protected void SetFacing(float direction)
        {
            if (Mathf.Approximately(direction, 0f)) return;
            Facing = Mathf.Sign(direction);
            var scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * Facing;
            transform.localScale = scale;
        }

        protected void Move(Vector2 input)
        {
            float slow = Status == StatusType.Radiated ? radiationSlowFactor : 1f;
            slow *= SpeedScale;
            Vector2 target = new Vector2(input.x * moveSpeedX * slow, input.y * moveSpeedY * slow);
            float rate = target.sqrMagnitude > body.linearVelocity.sqrMagnitude ? acceleration : deceleration;
            body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, target, rate * Time.deltaTime);
            if (animator != null) animator.SetFloat(HashMoveSpeed, body.linearVelocity.magnitude);
        }

        protected void Stop()
        {
            body.linearVelocity = Vector2.zero;
            if (animator != null) animator.SetFloat(HashMoveSpeed, 0f);
        }

        /// Bleed velocity off smoothly instead of stopping dead, so attack
        /// lunges carry through the swing and give it weight.
        protected void AttackDrift(float deceleration = 14f)
        {
            body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, Vector2.zero,
                deceleration * Time.deltaTime);
            if (animator != null) animator.SetFloat(HashMoveSpeed, 0f);
        }

        protected void InterruptAttack()
        {
            IsAttacking = false;
            currentAttack = null;
            attackTimeout = 0f;
            if (hitbox != null) hitbox.Deactivate();
        }

        protected virtual float DamageScale => 1f;
        protected virtual float SpeedScale => 1f;

        // --- Animation events ---
        public void AnimEvent_HitboxOn()
        {
            if (hitbox != null && currentAttack != null)
                hitbox.Activate(currentAttack, Facing, DamageScale);
        }

        public void AnimEvent_HitboxOff()
        {
            if (hitbox != null) hitbox.Deactivate();
        }

        public virtual void AnimEvent_AttackEnd()
        {
            InterruptAttack();
        }

        public virtual void AnimEvent_Fire() { }

        protected virtual void OnDeath()
        {
            InterruptAttack();
            body.linearVelocity = Vector2.zero;
            VFX.VfxManager.Play("blood_splatter", transform.position + new Vector3(0f, 0.8f, 0f), Facing, 1.8f);
            if (animator != null) animator.SetTrigger(HashDead);
            foreach (var col in GetComponentsInChildren<Collider2D>())
                col.enabled = false;
        }
    }
}
