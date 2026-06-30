using UnityEngine;

namespace SilverFang.Combat
{
    /// Impact material: Flesh bleeds (blood_splatter), Metal sparks (hit_spark).
    public enum HitMaterial { Flesh, Metal }

    /// Combat aura/style the attacker stamps on its impacts. Drives which family
    /// of effect the Impact VFX System plays and how it layers: Hilo's psionic
    /// blooms render BEHIND the target, Silver's blue aura and Lucas's electric
    /// arcs render ON TOP. Neutral = plain kinetic flesh/metal.
    public enum ImpactStyle { Neutral, SilverBlue, HiloPsionic, LucasGold, BigManRed }

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
        // Bloody Combat Engine: flesh combatants spray blood, metal ones spark.
        [SerializeField] protected HitMaterial hitMaterial = HitMaterial.Flesh;
        public HitMaterial Material => hitMaterial;
        public void SetMaterial(HitMaterial m) => hitMaterial = m;

        // Impact VFX System: the aura/style this combatant stamps on its hits.
        [SerializeField] protected ImpactStyle impactStyle = ImpactStyle.Neutral;
        public virtual ImpactStyle Style => impactStyle;
        public void SetStyle(ImpactStyle s) => impactStyle = s;
        /// Render order of this body, so impacts can layer relative to it.
        public int SortingOrder => sprite != null ? sprite.sortingOrder
            : -Mathf.RoundToInt(transform.position.y * 100f);

        // Combat Physics Engine: body weight tier (knockback resist, fall, accel).
        [SerializeField] protected Physics.WeightClass weight = Physics.WeightClass.Medium;
        public Physics.WeightClass Weight => weight;
        public void SetWeight(Physics.WeightClass w) => weight = w;

        /// Sticky-beam hold: pin the body where it stands and refresh hitstun so
        /// the enemy is locked in the beam path as it passes through.
        public void PinInPlace(float holdHitstun)
        {
            if (IsDead) return;
            if (body != null) body.linearVelocity = Vector2.zero;
            hitstunTimer = Mathf.Max(hitstunTimer, holdHitstun);
        }

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
        /// Modest global hitstun multiplier (kept small to protect balance).
        public const float GlobalHitstunScale = 1.15f;
        /// Extra hitstun an electric-style hit adds (~3 frames @ 24fps).
        public const float ElectricHitstunBonus = 3f / 24f;
        private float juggleHeight;
        private float juggleVelocity;
        private Vector3 visualBasePos;
        // Combat Fluidity Engine: bounce/extender state.
        private bool pendingGroundBounce;   // last hit flagged a ground bounce
        private int groundBounceCount;      // diminishing returns per knockdown
        private const float WallBounceRange = 1.1f; // wall search distance

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
                              && !Juggled && !Stunned && !Held && Status != StatusType.Frozen;
        public Health Health => health;

        // --- CQC Engine: stun meter. Stacks from stun damage taken; when it
        // tops out the body is knocked into a 2s looping STUN state. ---
        [SerializeField] protected float stunMax = 100f;
        [SerializeField] protected float stunDecay = 18f;  // meter bleeds off per second when not stunned
        private float stunMeter, stunTimer;
        public float StunMeter => stunMeter;
        public float StunMax => stunMax;
        public float StunFraction => stunMax > 0f ? Mathf.Clamp01(stunMeter / stunMax) : 0f;
        public bool Stunned => stunTimer > 0f;
        protected static readonly int HashStun = Animator.StringToHash("Stun");

        // --- CQC Engine: clinch hold. A grappled body is frozen and helpless
        // (the grappler refreshes this each frame while the clinch is held). ---
        private float heldTimer;
        public bool Held => heldTimer > 0f;
        public void SetHeld(float duration)
        {
            heldTimer = duration;
            if (duration > 0f)
            {
                InterruptAttack();
                hitstunTimer = 0f; knockdownTimer = 0f;
                juggleHeight = 0f; juggleVelocity = 0f;
                if (body != null) body.linearVelocity = Vector2.zero;
            }
        }

        /// Add stun damage. Topping the meter triggers the 2s stun state.
        public void AddStun(float amount)
        {
            if (IsDead || Stunned || amount <= 0f) return;
            stunMeter += amount;
            if (stunMeter >= stunMax) EnterStun();
        }

        private void EnterStun()
        {
            stunMeter = 0f;
            stunTimer = 2f;                 // 2 second stun
            InterruptAttack();
            hitstunTimer = 0f; knockdownTimer = 0f;
            juggleHeight = 0f; juggleVelocity = 0f;
            if (body != null) body.linearVelocity = Vector2.zero;
            if (animator != null && HasAnimatorParam("Stun")) animator.SetTrigger(HashStun);
            VFX.VfxManager.Play("hit_spark", transform.position + Vector3.up * 1.3f, Facing, 1f);
        }

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
            // CQC stun: count down the stun state, or bleed the meter off slowly.
            if (stunTimer > 0f)
            {
                stunTimer -= Time.deltaTime;
                if (body != null) body.linearVelocity = Vector2.zero;
                if (stunTimer <= 0f && !IsDead && animator != null) animator.SetTrigger(HashGetUp);
            }
            else if (stunMeter > 0f)
            {
                stunMeter = Mathf.MoveTowards(stunMeter, 0f, stunDecay * Time.deltaTime);
            }
            if (heldTimer > 0f)
            {
                heldTimer -= Time.deltaTime;
                if (body != null) body.linearVelocity = Vector2.zero;
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
                // Ground bounce: a flagged hit makes the target rebound off the
                // floor (diminishing each time) instead of settling into
                // knockdown — keeps the juggle alive for an extender.
                if (pendingGroundBounce && !IsDead && groundBounceCount < 2
                    && juggleVelocity < -3f)
                {
                    groundBounceCount++;
                    juggleHeight = 0.01f;
                    juggleVelocity = Mathf.Abs(juggleVelocity) * (0.55f - 0.12f * groundBounceCount);
                    pendingGroundBounce = false;
                    VFX.VfxManager.Play("dash_dust", transform.position, Facing, 0.8f);
                    Core.CameraFollow.Instance?.Shake(0.05f, 0.06f);
                    if (animator != null) animator.SetTrigger(HashHurt);
                    return;
                }

                juggleHeight = 0f;
                juggleVelocity = 0f;
                pendingGroundBounce = false;
                groundBounceCount = 0;
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
            // CQC stun build-up: every hit adds stun; heavy/knockdown hits add more.
            AddStun(attack.damage * 1.1f + (attack.knocksDown || attack.launch > 0f ? 18f : 0f));
            InterruptAttack();

            Vector3 bloodPos = transform.position + new Vector3(0f, 1f, 0f);
            VFX.VfxManager.Play("blood_splatter", bloodPos, attackerFacing, attack.knocksDown ? 1.4f : 1f);

            body.linearVelocity = Vector2.zero;
            // Combat Physics Engine: knockback is weight-scaled (heavy bodies
            // barely flinch, light ones fly).
            body.AddForce(Physics.CombatPhysics.Knockback(attack.knockback, attackerFacing, weight),
                          ForceMode2D.Impulse);

            if (attack.knocksDown || attack.launch > 0f)
                Core.CameraFollow.Instance?.Shake();

            if (IsDead) return;

            // --- Combat Fluidity Engine extenders ---
            // OTG pickup: scoop a downed target back into the air.
            if (attack.otg && KnockedDown)
            {
                knockdownTimer = 0f;
                juggleVelocity = attack.launch > 0f ? attack.launch : JuggleRehitPop;
                if (juggleHeight <= 0f) juggleHeight = 0.01f;
                pendingGroundBounce = attack.groundBounce;
                groundBounceCount = 0;
                if (animator != null) animator.SetTrigger(HashHurt);
                return;
            }
            // Spike: hammer an airborne target into the floor; the slam bounces.
            if (attack.spike && Juggled)
            {
                juggleVelocity = -Mathf.Max(8f, Mathf.Abs(attack.launch) + 6f);
                pendingGroundBounce = true;
                if (animator != null) animator.SetTrigger(HashHurt);
                return;
            }
            // Wall bounce: drive the target into a nearby wall, then off it.
            if (attack.wallBounce && TryWallBounce(attackerFacing))
            {
                if (animator != null) animator.SetTrigger(HashHurt);
                return;
            }

            // Juggle: launches pop the target airborne; hitting an airborne target keeps it up.
            if (attack.launch > 0f)
            {
                juggleVelocity = attack.launch;
                if (juggleHeight <= 0f) juggleHeight = 0.01f;
                hitstunTimer = 0f;
                knockdownTimer = 0f;
                pendingGroundBounce = attack.groundBounce;
                groundBounceCount = 0;
                if (animator != null) animator.SetTrigger(HashHurt);
                return;
            }
            if (Juggled)
            {
                juggleVelocity = Mathf.Max(juggleVelocity, JuggleRehitPop);
                if (attack.groundBounce) pendingGroundBounce = true;
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
                // Modest global hitstun bump for snappier, more reactive trades,
                // plus any style bonus (electric tacks on ~3 frames).
                hitstunTimer = (attack.hitstun + attack.bonusHitstun) * GlobalHitstunScale;
                // big non-knockdown hits use the dedicated heavy-flinch strip
                if (animator != null)
                    animator.SetTrigger(attack.damage >= 18 && hasHurtHeavy ? HashHurtHeavy : HashHurt);
            }
        }

        /// Searches for a solid wall in the knockback direction; if found, kicks
        /// the target back off it and pops them up. Safe no-op when there's no
        /// wall in range (normal knockback then applies).
        private bool TryWallBounce(float attackerFacing)
        {
            float dir = Mathf.Sign(attackerFacing);
            var hit = Physics2D.Raycast(transform.position + Vector3.up * 0.8f,
                new Vector2(dir, 0f), WallBounceRange);
            if (hit.collider == null || hit.collider.isTrigger
                || hit.collider.GetComponentInParent<CharacterCombatant>() != null)
                return false;
            body.linearVelocity = new Vector2(-dir * 6f, 0f);   // peel off the wall
            juggleVelocity = JuggleRehitPop;
            if (juggleHeight <= 0f) juggleHeight = 0.01f;
            pendingGroundBounce = true;
            VFX.VfxManager.Play("hit_spark", hit.point, -dir, 1.2f);
            Core.CameraFollow.Instance?.Shake(0.06f, 0.07f);
            return true;
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

        /// Jotaro flurry: a small paced impact per blow (overridden by the player).
        public virtual void AnimEvent_FlurryImpact() { }

        /// Jotaro flurry: the explosive heavy cleave finisher after the wind-up
        /// pause — a strong AoE that spikes/ground-bounces (overridden by player).
        public virtual void AnimEvent_FinisherHit() { }

        protected virtual void OnDeath()
        {
            InterruptAttack();
            body.linearVelocity = Vector2.zero;
            VFX.VfxManager.Play("blood_splatter", transform.position + new Vector3(0f, 0.8f, 0f), Facing, 1.8f);
            if (animator != null) animator.SetTrigger(PickDeathPose());
            foreach (var col in GetComponentsInChildren<Collider2D>())
                col.enabled = false;
        }

        // Death Articulator Engine: any combatant whose animator carries extra
        // death poses (Dead2, Dead3, ...) gets a randomly chosen downed pose, so
        // deaths vary across a roster instead of repeating one canned slump. With
        // only the base "Dead" state present it behaves exactly as before.
        private int PickDeathPose()
        {
            if (animator == null) return HashDead;
            int variants = 1;
            for (int n = 2; n <= 20; n++)
                if (HasAnimatorParam("Dead" + n)) variants = n; else break;
            if (variants <= 1) return HashDead;
            int pick = Random.Range(1, variants + 1); // 1 = "Dead", 2.. = "DeadN"
            return pick == 1 ? HashDead : Animator.StringToHash("Dead" + pick);
        }

        private bool HasAnimatorParam(string name)
        {
            foreach (var p in animator.parameters)
                if (p.name == name) return true;
            return false;
        }
    }
}
