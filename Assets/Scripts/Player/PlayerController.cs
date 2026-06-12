using SilverFang.Combat;
using SilverFang.Progression;
using UnityEngine;

namespace SilverFang.Player
{
    public enum Stance
    {
        Sword,
        Gun
    }

    public class PlayerController : CharacterCombatant
    {
        [Header("Move Sets")]
        [SerializeField] private MoveSet swordMoveSet;
        [SerializeField] private MoveSet gunMoveSet;
        [SerializeField] private MoveSet dashMoveSet;
        [SerializeField] private MoveSet sprintMoveSet;
        [SerializeField] private MoveSet teleportMoveSet;
        [SerializeField] private MoveSet airMoveSet;
        [SerializeField] private MoveSet awakenedAirMoveSet;
        [SerializeField] private float chainWindow = 0.6f;

        [Header("Gun")]
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Transform firePoint;
        [SerializeField] private AmmoDefinition[] ammoTypes;

        [Header("Jump")]
        [SerializeField] private float jumpVelocity = 8.5f;
        [SerializeField] private float jumpGravity = -28f;
        [SerializeField] private float fallGravityMult = 1.55f; // faster fall = weighty arc
        [SerializeField] private int maxAirAttacks = 3; // room for 3-token air raves
        [SerializeField] private float airAttackLunge = 4.5f;
        [SerializeField] private float airAttackFloat = 0.12f;
        [SerializeField] private Transform visual;

        [Header("Charge")]
        [SerializeField] private float chargeStartDelay = 0.3f;   // hold this long to begin charging
        [SerializeField] private float chargeFullTime = 2f;       // full charge (sheet: 2.0s)
        [SerializeField] private float jumpChargeBonus = 0.8f;    // +80% jump velocity at full charge
        [SerializeField] private float heavyLandThreshold = 10f;  // fall speed that causes a landing impact

        [Header("Slash Wave")]
        [SerializeField] private Projectile slashWavePrefab;
        [SerializeField] private AmmoDefinition slashWaveAmmo;

        [Header("Dash / Run")]
        [SerializeField] private float dashSpeed = 13f;
        [SerializeField] private float dashDuration = 0.16f;
        [SerializeField] private float runMultiplier = 1.6f;
        [SerializeField] private float doubleTapWindow = 0.28f;

        [Header("Awakened")]
        [SerializeField] private float awakenedMax = 100f;
        [SerializeField] private float awakenedGainPerHit = 8f;
        [SerializeField] private float awakenedDuration = 12f;
        [SerializeField] private float awakenedDamageMultiplier = 1.5f;
        [SerializeField] private MoveSet awakenedMoveSet;
        [SerializeField] private RuntimeAnimatorController awakenedController;
        [SerializeField] private float teleportDashDistance = 3.5f;
        [SerializeField] private float teleportInvuln = 0.3f;
        [SerializeField] private float teleportBehindRange = 8f;

        private RuntimeAnimatorController baseController;

        private InputReader input;
        private ComboResolver resolver;
        private ComboResolver contextResolver;
        private MoveDefinition currentMove;
        private InputToken? bufferedToken;
        private float bufferedAt;
        private MoveSet activeContextMoveSet;
        private MoveSet chainOverrideMoveSet;

        private float height;
        private float verticalVelocity;
        private float visualBaseY;
        private bool airborne;
        private int airAttacksRemaining;
        private float airFloatTimer;
        private bool fallTriggered;

        private float dashTimer;
        private float dashDir;
        private bool running;
        private int lastTapDir;
        private float lastTapTime = -10f;

        // charge state
        private float lightHold, heavyHold, gunHold;
        private bool attackCharging;
        private InputToken chargeButton;
        private float chargeTime;
        private float chargePulseTimer;
        private bool jumpCharging;
        private float jumpChargeTime;
        private float landRecovery;

        public Stance CurrentStance { get; private set; } = Stance.Sword;
        public int AmmoIndex { get; private set; }
        public AmmoDefinition CurrentAmmo =>
            ammoTypes != null && ammoTypes.Length > 0 ? ammoTypes[AmmoIndex] : null;

        public float AwakenedMeter { get; private set; }
        public float AwakenedMaxMeter => awakenedMax;
        public bool AwakenedActive { get; private set; }
        private float awakenedTimer;

        public event System.Action<Stance> OnStanceChanged;
        public event System.Action<AmmoDefinition> OnAmmoChanged;

        private int baseMaxHealth;
        private float activeAwakenedDuration;

        protected override float DamageScale
        {
            get
            {
                if (AwakenedActive)
                    return awakenedDamageMultiplier * PlayerProgression.GetMultiplier(ModifierType.AwakenedDamage);
                return PlayerProgression.GetMultiplier(CurrentStance == Stance.Sword
                    ? ModifierType.SwordDamage
                    : ModifierType.GunDamage);
            }
        }

        protected override float SpeedScale => PlayerProgression.GetMultiplier(ModifierType.MoveSpeed);

        private float ChainWindow => chainWindow * PlayerProgression.GetMultiplier(ModifierType.ChainWindow);
        private float TeleportMult => PlayerProgression.GetMultiplier(ModifierType.TeleportDistance);

        private static readonly int HashJump = Animator.StringToHash("Jump");
        private static readonly int HashFall = Animator.StringToHash("Fall");
        private static readonly int HashLand = Animator.StringToHash("Land");
        private static readonly int HashReload = Animator.StringToHash("Reload");
        private static readonly int HashDash = Animator.StringToHash("Dash");

        private ComboTracker combo;

        protected override void Awake()
        {
            base.Awake();
            combo = GetComponent<ComboTracker>();
            if (combo == null) combo = gameObject.AddComponent<ComboTracker>();
            input = new InputReader();
            resolver = new ComboResolver(swordMoveSet);
            contextResolver = new ComboResolver(null);
            if (visual == null && sprite != null) visual = sprite.transform;
            if (visual != null) visualBaseY = visual.localPosition.y;
            if (hitbox != null) hitbox.OnHit += OnDealtHit;
            if (animator != null) baseController = animator.runtimeAnimatorController;

            baseMaxHealth = health.Max;
            PlayerProgression.OnChanged += ApplyProgression;
            PlayerProgression.OnEnemyKilled += OnEnemyKilled;
            ApplyProgression();
        }

        private void OnDestroy()
        {
            PlayerProgression.OnChanged -= ApplyProgression;
            PlayerProgression.OnEnemyKilled -= OnEnemyKilled;
        }

        private void ApplyProgression()
        {
            health.SetMax(baseMaxHealth + Mathf.RoundToInt(PlayerProgression.GetFlat(ModifierType.MaxHealth)));
        }

        private void OnEnemyKilled()
        {
            int heal = Mathf.RoundToInt(PlayerProgression.GetFlat(ModifierType.HealOnKill));
            if (heal > 0 && !IsDead) health.Heal(heal);
        }

        protected override void Update()
        {
            base.Update();
            if (IsDead) return;
            if (Core.GamePause.IsPaused) return;

            UpdateAwakened();
            UpdateJumpArc();
            TrackHolds();

            if (landRecovery > 0f)
            {
                // heavy landing: a beat of recovery before acting again
                landRecovery -= Time.deltaTime;
                AttackDrift(24f);
                return;
            }

            if (jumpCharging)
            {
                UpdateJumpCharge();
                return;
            }

            if (attackCharging)
            {
                UpdateAttackCharge();
                return;
            }

            if (input.StancePressed) ToggleStance();
            if (input.AmmoPressed) CycleAmmo();
            if (input.AwakenedPressed) TryActivateAwakened();

            var token = ReadToken();
            if (token.HasValue) HandleToken(token.Value);

            if (CanAct && !airborne && input.JumpPressed)
                BeginJumpCharge();

            if (CanAct && !airborne)
                TryBeginAttackCharge();

            if (CanAct)
            {
                var move = input.Move;
                HandleDoubleTap();

                if (dashTimer > 0f)
                {
                    dashTimer -= Time.deltaTime;
                    SetFacing(dashDir);
                    if (!AwakenedActive)
                        body.linearVelocity = new Vector2(dashDir * dashSpeed * SpeedScale, body.linearVelocity.y * 0.4f);
                    if (animator != null) animator.SetFloat(HashMoveSpeed, Mathf.Abs(body.linearVelocity.x));
                    // still holding the dash direction when the burst ends -> run
                    if (dashTimer <= 0f && Mathf.Abs(move.x) > 0.3f && Mathf.Sign(move.x) == dashDir)
                    {
                        running = true;
                        // awakened sprint opens with a long blink-teleport
                        if (AwakenedActive)
                            TeleportTo(transform.position + new Vector3(dashDir * 6f * TeleportMult, 0f, 0f));
                    }
                }
                else
                {
                    if (running && (move.magnitude < 0.3f || (move.x != 0f && Mathf.Sign(move.x) != dashDir)))
                        running = false;

                    UpdateLockOn();
                    if (lockTarget != null)
                    {
                        // facing pinned to the marked enemy; retreating reads
                        // as guarded backpedal steps, never a forward walk.
                        SetFacing(lockTarget.transform.position.x - transform.position.x);
                        bool back = Mathf.Abs(move.x) > 0.05f && Mathf.Sign(move.x) != Facing;
                        SetBackPedal(back);
                        running = false;
                        Move(move * (back ? 0.7f : 0.9f));
                    }
                    else
                    {
                        SetBackPedal(false);
                        SetFacing(move.x);
                        Move(running ? move * runMultiplier : move);
                    }
                }
            }
            else if (IsAttacking)
            {
                // lunge momentum bleeds off through the swing instead of stopping dead
                AttackDrift(IsHeavyAttack() ? 10f : 14f);
            }
        }

        private bool IsHeavyAttack() =>
            currentAttack != null && (currentAttack.knocksDown || currentAttack.launch > 0f);

        private void TrackHolds()
        {
            float dt = Time.deltaTime;
            lightHold = input.LightHeld ? lightHold + dt : 0f;
            heavyHold = input.HeavyHeld ? heavyHold + dt : 0f;
            gunHold = input.GunHeld ? gunHold + dt : 0f;
        }

        private void HandleDoubleTap()
        {
            int tap = input.MoveTapDir;
            if (tap == 0) return;

            if (tap == lastTapDir && Time.time - lastTapTime <= doubleTapWindow && dashTimer <= 0f)
            {
                dashDir = tap;
                running = false;
                if (AwakenedActive)
                {
                    // awakened dash is a short blink-teleport
                    SetFacing(dashDir);
                    TeleportTo(transform.position + new Vector3(dashDir * 3.2f * TeleportMult, 0f, 0f));
                    dashTimer = 0.22f; // teleport-attack window
                }
                else
                {
                    dashTimer = dashDuration;
                    VFX.VfxManager.Play("dash_dust", transform.position + new Vector3(-0.4f * tap, 0.15f, 0f), tap, 0.9f);
                }
                if (animator != null) animator.SetTrigger(HashDash);
            }
            lastTapDir = tap;
            lastTapTime = Time.time;
        }

        private InputToken? ReadToken()
        {
            if (input.LightPressed) return InputToken.Light;
            if (input.HeavyPressed) return InputToken.Heavy;
            if (input.GunPressed) return InputToken.Gun;
            return null;
        }

        private void HandleToken(InputToken token)
        {
            if (airborne)
            {
                HandleAirToken(token);
                return;
            }

            if (CanAct)
            {
                // contextual moves: attacking out of a dash, sprint, or teleport
                var contextual = dashTimer > 0f
                    ? (AwakenedActive ? teleportMoveSet : dashMoveSet)
                    : running ? (AwakenedActive ? teleportMoveSet : sprintMoveSet) : null;
                if (contextual != null)
                {
                    if (TryStartContextualMove(contextual, token)) return;
                }

                var move = resolver.OnToken(token, Time.time, ChainWindow);
                if (move != null) StartMove(move);
            }
            else if (IsAttacking)
            {
                bufferedToken = token;
                bufferedAt = Time.time;
            }
        }

        // Air combo chain: tokens accumulate while airborne so multi-hit air
        // routes (LL claw, LH spin kick, HH dive kick, LLL aerial rave...) and
        // air projectiles resolve just like ground strings.
        private readonly System.Collections.Generic.List<InputToken> airChain =
            new System.Collections.Generic.List<InputToken>();
        private float airChainAt;

        private void HandleAirToken(InputToken token)
        {
            if (CanAct && airAttacksRemaining > 0)
            {
                var set = AwakenedActive ? awakenedAirMoveSet : airMoveSet;
                if (set == null) return;

                if (airChain.Count > 0 && Time.time - airChainAt > ChainWindow)
                    airChain.Clear();
                airChain.Add(token);
                var move = set.Match(airChain.ToArray());
                if (move == null)
                {
                    // chain broke: restart the string from this token
                    airChain.Clear();
                    airChain.Add(token);
                    move = set.Match(airChain.ToArray());
                }
                if (move == null)
                {
                    airChain.Clear();
                    return;
                }

                airChainAt = Time.time;
                airAttacksRemaining--;
                StartMove(move, fromAir: true);
            }
            else if (IsAttacking)
            {
                bufferedToken = token;
                bufferedAt = Time.time;
            }
        }

        private bool TryStartContextualMove(MoveSet moveSet, InputToken token)
        {
            if (moveSet == null) return false;
            if (activeContextMoveSet != moveSet)
            {
                contextResolver.SetMoveSet(moveSet);
                activeContextMoveSet = moveSet;
            }

            var move = contextResolver.OnToken(token, Time.time, ChainWindow);
            if (move == null) return false;

            dashTimer = 0f;
            StartMove(move, chainSet: moveSet);
            return true;
        }

        // --- Charged attacks (hold light/heavy/gun; release to unleash; sheet: 2.0s full charge) ---

        private float pendingChargeShot = -1f;
        private int pendingChargeLevel;

        private void TryBeginAttackCharge()
        {
            if (attackCharging || jumpCharging) return;

            InputToken? button = null;
            float hold = 0f;
            if (lightHold >= chargeStartDelay) { button = InputToken.Light; hold = lightHold; }
            else if (heavyHold >= chargeStartDelay) { button = InputToken.Heavy; hold = heavyHold; }
            else if (gunHold >= chargeStartDelay) { button = InputToken.Gun; hold = gunHold; }
            if (!button.HasValue) return;

            attackCharging = true;
            chargeButton = button.Value;
            chargeTime = Mathf.Min(hold, chargeFullTime);
            chargePulseTimer = 0f;
            bufferedToken = null;
            Stop();
            if (animator != null) animator.SetTrigger(HoldTrigger(chargeButton));
        }

        private static string HoldTrigger(InputToken t) => t switch
        {
            InputToken.Light => "ChargeHoldLight",
            InputToken.Heavy => "ChargeHoldHeavy",
            _ => "ChargeHoldGun"
        };

        private void UpdateAttackCharge()
        {
            chargeTime = Mathf.Min(chargeTime + Time.deltaTime, chargeFullTime);
            AttackDrift(20f);

            chargePulseTimer -= Time.deltaTime;
            if (chargePulseTimer <= 0f)
            {
                chargePulseTimer = 0.45f;
                VFX.VfxManager.Play("hit_spark", transform.position + Vector3.up * 0.9f, Facing,
                    0.4f + 0.8f * (chargeTime / chargeFullTime));
            }

            bool stillHeld = chargeButton switch
            {
                InputToken.Light => input.LightHeld,
                InputToken.Heavy => input.HeavyHeld,
                _ => input.GunHeld
            };
            if (!stillHeld || chargeTime >= chargeFullTime)
                ReleaseChargedAttack();
        }

        private void ReleaseChargedAttack()
        {
            attackCharging = false;
            float charge01 = Mathf.Clamp01(chargeTime / chargeFullTime);
            int level = charge01 >= 0.65f ? 3 : charge01 >= 0.3f ? 2 : 1;
            pendingChargeShot = chargeButton == InputToken.Gun ? charge01 : -1f;
            pendingChargeLevel = level;
            if (level >= 3) Core.CameraFollow.Instance?.PunchIn(1f, 0.5f);
            StartMove(BuildChargedMove(chargeButton, level));
        }

        private static MoveDefinition BuildChargedMove(InputToken button, int level)
        {
            if (button == InputToken.Light)
            {
                int[] dmg = { 0, 16, 26, 40 };
                return new MoveDefinition
                {
                    id = "ChargeLight" + level,
                    animatorTrigger = "ChargeLight" + level,
                    attack = new AttackData
                    {
                        damage = dmg[level],
                        hitstun = 0.32f + 0.1f * level,
                        knockback = new Vector2(4f + 2f * level, 0f),
                        knocksDown = level >= 2,
                        rangeScale = 1.2f + 0.25f * level,
                        heightScale = 1.15f
                    },
                    duration = 0.38f + 0.08f * level
                };
            }
            if (button == InputToken.Heavy)
            {
                int[] dmg = { 0, 28, 44, 64 };
                return new MoveDefinition
                {
                    id = "ChargeHeavy" + level,
                    animatorTrigger = "ChargeHeavy" + level,
                    attack = new AttackData
                    {
                        damage = dmg[level],
                        hitstun = 0.45f,
                        knockback = new Vector2(6f + 2f * level, 0f),
                        knocksDown = true,
                        launch = level >= 3 ? 8.5f : 0f,
                        rangeScale = 1.35f + 0.3f * level,
                        heightScale = 1.4f
                    },
                    firesSlashWave = level >= 3, // full-charge heavy releases a sword wave
                    duration = 0.5f + 0.1f * level
                };
            }
            return new MoveDefinition
            {
                id = "ChargeShot" + level,
                animatorTrigger = "ChargeShot" + level,
                firesProjectile = true,
                attack = new AttackData { damage = 6 },
                duration = 0.42f + 0.07f * level
            };
        }

        private void StartMove(MoveDefinition move)
        {
            StartMove(move, fromAir: false, chainSet: null);
        }

        private void StartMove(MoveDefinition move, bool fromAir = false, MoveSet chainSet = null)
        {
            if (move.teleport != TeleportKind.None && AwakenedActive)
                ExecuteMoveTeleport(move.teleport);

            currentMove = move;
            currentAttack = move.attack;
            IsAttacking = true;
            attackTimeout = move.duration + 0.35f; // safety net if AttackEnd never fires
            chainOverrideMoveSet = chainSet;

            if (fromAir)
            {
                float lunge = airAttackLunge + (move.attack != null ? move.attack.damage * 0.04f : 0f);
                body.linearVelocity = new Vector2(Facing * lunge, body.linearVelocity.y);
                airFloatTimer = airAttackFloat;
                if (verticalVelocity < -1f) verticalVelocity = -1f;
            }
            else
            {
                Stop();
                // forward lunge gives swings weight and drive
                if (!move.firesProjectile && move.attack != null)
                    body.linearVelocity = new Vector2(Facing * (3.2f + move.attack.damage * 0.05f), 0f);
            }

            if (animator != null && !string.IsNullOrEmpty(move.animatorTrigger))
                animator.SetTrigger(move.animatorTrigger);
        }

        private void ExecuteMoveTeleport(TeleportKind kind)
        {
            if (kind == TeleportKind.BehindTarget)
            {
                Enemies.EnemyAI nearest = null;
                float best = teleportBehindRange * TeleportMult;
                foreach (var enemy in FindObjectsByType<Enemies.EnemyAI>(FindObjectsInactive.Exclude))
                {
                    if (enemy.IsDead || !enemy.gameObject.activeInHierarchy) continue;
                    float dist = Vector2.Distance(enemy.transform.position, transform.position);
                    if (dist < best)
                    {
                        best = dist;
                        nearest = enemy;
                    }
                }

                if (nearest != null)
                {
                    Vector3 enemyPos = nearest.transform.position;
                    float side = Mathf.Sign(enemyPos.x - transform.position.x);
                    TeleportTo(new Vector3(enemyPos.x + side * 1.1f, enemyPos.y, enemyPos.z));
                    SetFacing(-side);
                    return;
                }
            }

            // ForwardDash, or BehindTarget with no enemy in range.
            TeleportTo(transform.position + new Vector3(teleportDashDistance * TeleportMult * Facing, 0f, 0f));
        }

        private void TeleportDash()
        {
            float distance = teleportDashDistance * TeleportMult;
            var move = input.Move;
            Vector3 dir = move.sqrMagnitude > 0.01f
                ? (Vector3)(move.normalized * distance)
                : new Vector3(distance * Facing, 0f, 0f);
            if (!Mathf.Approximately(dir.x, 0f)) SetFacing(dir.x);
            TeleportTo(transform.position + dir);
        }

        private void TeleportTo(Vector3 destination)
        {
            VFX.VfxManager.Play("vanish", transform.position + Vector3.up * 0.9f, Facing);
            body.position = destination;
            transform.position = destination;
            invulnTimer = teleportInvuln;
            VFX.VfxManager.Play("reappear", destination + Vector3.up * 0.9f, Facing);
        }

        public override void AnimEvent_AttackEnd()
        {
            bool wasGunMove = currentMove != null && currentMove.firesProjectile;
            currentMove = null;
            base.AnimEvent_AttackEnd();

            if (bufferedToken.HasValue && Time.time - bufferedAt <= ChainWindow)
            {
                var token = bufferedToken.Value;
                bufferedToken = null;
                if (chainOverrideMoveSet != null && TryStartContextualMove(chainOverrideMoveSet, token))
                    return;

                chainOverrideMoveSet = null;
                activeContextMoveSet = null;
                contextResolver.Reset();
                HandleToken(token);
            }
            else
            {
                bufferedToken = null;
                chainOverrideMoveSet = null;
                activeContextMoveSet = null;
                contextResolver.Reset();
            }
        }

        // Called from animation event on shooting / wave-release frames.
        public override void AnimEvent_Fire()
        {
            if (currentMove != null && currentMove.firesSlashWave)
            {
                FireSlashWave();
                if (!currentMove.firesProjectile) return;
            }

            if (projectilePrefab == null || CurrentAmmo == null) return;
            Vector3 origin = firePoint != null
                ? firePoint.position
                : transform.position + new Vector3(0.8f * Facing, 0.8f, 0f);

            if (pendingChargeShot >= 0f)
            {
                float c = pendingChargeShot;
                pendingChargeShot = -1f;
                VFX.VfxManager.Play("muzzle_burst", origin, Facing, 1f + c);
                var charged = Instantiate(projectilePrefab, origin, Quaternion.identity);
                charged.Fire(Team.Player, CurrentAmmo, Facing, DamageScale * (1.5f + 2.5f * c),
                    1.25f + 1.1f * c, 1.2f + 0.4f * c, pendingChargeLevel >= 3);
                // recoil sells the shot's weight
                body.linearVelocity = new Vector2(-Facing * (1.5f + 2.5f * c), 0f);
                Core.CameraFollow.Instance?.Shake(0.05f + 0.07f * c, 0.12f);
                HitStop.Do(0.03f);
                return;
            }

            VFX.VfxManager.Play("muzzle_burst", origin, Facing);
            var proj = Instantiate(projectilePrefab, origin, Quaternion.identity);
            proj.Fire(Team.Player, CurrentAmmo, Facing, DamageScale);
        }

        private void FireSlashWave()
        {
            if (slashWavePrefab == null || slashWaveAmmo == null) return;
            Vector3 origin = transform.position + new Vector3(0.9f * Facing, 0.9f, 0f);
            float power = currentMove != null && currentMove.attack != null
                ? Mathf.Max(1f, currentMove.attack.damage / 24f)
                : 1f;
            var wave = Instantiate(slashWavePrefab, origin, Quaternion.identity);
            wave.Fire(Team.Player, slashWaveAmmo, Facing, DamageScale * power, 0.9f + 0.25f * power);
        }

        private void ToggleStance()
        {
            CurrentStance = CurrentStance == Stance.Sword ? Stance.Gun : Stance.Sword;
            // Awakened transcends stances: keep the awakened move set until it expires.
            if (!AwakenedActive)
                resolver.SetMoveSet(CurrentStance == Stance.Sword ? swordMoveSet : gunMoveSet);
            bufferedToken = null;
            chainOverrideMoveSet = null;
            activeContextMoveSet = null;
            contextResolver.Reset();
            OnStanceChanged?.Invoke(CurrentStance);
        }

        private void CycleAmmo()
        {
            if (ammoTypes == null || ammoTypes.Length == 0) return;
            AmmoIndex = (AmmoIndex + 1) % ammoTypes.Length;
            if (animator != null) animator.SetTrigger(HashReload);
            OnAmmoChanged?.Invoke(CurrentAmmo);
        }

        // --- Lock-on (LT / Left Shift): hold to pin facing to the nearest enemy ---

        [SerializeField] private float lockOnRange = 11f;
        private Enemies.EnemyAI lockTarget;
        private bool backPedal;
        private static readonly int HashBackPedal = Animator.StringToHash("BackPedal");

        /// The enemy currently locked (marker UI hooks in here).
        public Enemies.EnemyAI LockTarget => lockTarget;

        private void UpdateLockOn()
        {
            if (!input.LockOnHeld || IsDead)
            {
                if (lockTarget != null)
                {
                    lockTarget = null;
                    SetBackPedal(false);
                }
                return;
            }

            if (lockTarget != null && (lockTarget.IsDead
                || Vector2.Distance(lockTarget.transform.position, transform.position) > lockOnRange * 1.25f))
                lockTarget = null;
            if (lockTarget == null) lockTarget = FindLockTarget();
        }

        private Enemies.EnemyAI FindLockTarget()
        {
            Enemies.EnemyAI best = null;
            float bestDist = lockOnRange;
            foreach (var enemy in Object.FindObjectsByType<Enemies.EnemyAI>())
            {
                if (enemy.IsDead) continue;
                float d = Vector2.Distance(enemy.transform.position, transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = enemy;
                }
            }
            return best;
        }

        private void SetBackPedal(bool value)
        {
            if (backPedal == value) return;
            backPedal = value;
            if (animator != null) animator.SetBool(HashBackPedal, value);
        }

        private void OnDealtHit(Hurtbox hurt)
        {
            combo?.RegisterHit();

            // dynamic camera: punch in on kills, finishers, and combo milestones
            var camFollow = Core.CameraFollow.Instance;
            if (camFollow != null)
            {
                if (combo != null && (combo.Count == 10 || combo.Count == 20
                    || combo.Count == 35 || combo.Count == 50))
                    camFollow.ComboPunch(combo.Count);

                Vector2 hitPos = hurt != null ? (Vector2)hurt.transform.position : (Vector2)transform.position;
                bool kill = hurt != null && hurt.Owner != null && hurt.Owner.IsDead;
                bool finisher = currentMove != null
                    && ((currentMove.sequence != null && currentMove.sequence.Length >= 4)
                        || (currentMove.attack != null && currentMove.attack.damage >= 34));

                if (kill && finisher) camFollow.FinisherPunch(hitPos);
                else if (kill) camFollow.KillPunch(hitPos);
                else if (finisher) camFollow.PunchIn(0.9f, 0.45f, hitPos);
            }

            if (!AwakenedActive)
            {
                float gain = awakenedGainPerHit * PlayerProgression.GetMultiplier(ModifierType.AwakenedGain);
                AwakenedMeter = Mathf.Min(awakenedMax, AwakenedMeter + gain);
            }
        }

        private static readonly int HashTransform = Animator.StringToHash("Transform");

        private void TryActivateAwakened()
        {
            if (AwakenedActive || AwakenedMeter < awakenedMax) return;
            AwakenedActive = true;
            activeAwakenedDuration = awakenedDuration * PlayerProgression.GetMultiplier(ModifierType.AwakenedDuration);
            awakenedTimer = activeAwakenedDuration;
            invulnTimer = 0.8f;
            InterruptAttack();
            bufferedToken = null;
            chainOverrideMoveSet = null;
            activeContextMoveSet = null;
            contextResolver.Reset();
            Stop();

            if (animator != null && awakenedController != null)
            {
                animator.runtimeAnimatorController = awakenedController;
                animator.SetTrigger(HashTransform);
            }
            if (awakenedMoveSet != null) resolver.SetMoveSet(awakenedMoveSet);
            VFX.VfxManager.PlayAttached("transform_burst", transform, 0.8f, 1.5f);
        }

        private void UpdateAwakened()
        {
            if (!AwakenedActive) return;
            awakenedTimer -= Time.deltaTime;
            AwakenedMeter = awakenedMax * Mathf.Max(0f, awakenedTimer / activeAwakenedDuration);
            if (awakenedTimer <= 0f) EndAwakened();
        }

        private void EndAwakened()
        {
            AwakenedActive = false;
            AwakenedMeter = 0f;
            InterruptAttack();
            bufferedToken = null;
            chainOverrideMoveSet = null;
            activeContextMoveSet = null;
            contextResolver.Reset();

            if (animator != null && baseController != null)
                animator.runtimeAnimatorController = baseController;
            resolver.SetMoveSet(CurrentStance == Stance.Sword ? swordMoveSet : gunMoveSet);
            VFX.VfxManager.Play("vanish", transform.position + Vector3.up * 0.9f, Facing);
        }

        private void BeginJumpCharge()
        {
            jumpCharging = true;
            jumpChargeTime = 0f;
            Stop();
        }

        private void UpdateJumpCharge()
        {
            jumpChargeTime = Mathf.Min(jumpChargeTime + Time.deltaTime, chargeFullTime);
            Stop();

            // crouch squash while charging so the wind-up reads on screen
            float c = jumpChargeTime / chargeFullTime;
            if (visual != null)
                visual.localScale = new Vector3(1f + 0.08f * c, 1f - 0.14f * c, 1f);

            if (!input.JumpHeld || jumpChargeTime >= chargeFullTime)
                LaunchJump(c);
        }

        private void LaunchJump(float charge01)
        {
            jumpCharging = false;
            if (visual != null) visual.localScale = Vector3.one;

            airborne = true;
            airAttacksRemaining = maxAirAttacks;
            airChain.Clear();
            airFloatTimer = 0f;
            fallTriggered = false;
            verticalVelocity = jumpVelocity * (1f + jumpChargeBonus * charge01);
            if (charge01 > 0.4f)
            {
                VFX.VfxManager.Play("hit_spark", transform.position + Vector3.up * 0.2f, Facing, 0.7f + charge01);
                Core.CameraFollow.Instance?.Shake(0.05f * charge01, 0.1f);
            }
            if (animator != null) animator.SetTrigger(HashJump);
        }

        private void UpdateJumpArc()
        {
            if (!airborne || visual == null) return;
            if (airFloatTimer > 0f)
                airFloatTimer -= Time.deltaTime;
            else
            {
                // falling pulls harder than rising: snappy, weighty arc
                float g = verticalVelocity <= 0f ? jumpGravity * fallGravityMult : jumpGravity;
                verticalVelocity += g * Time.deltaTime;
            }

            if (!fallTriggered && verticalVelocity <= 0f)
            {
                fallTriggered = true;
                if (animator != null) animator.SetTrigger(HashFall);
            }

            height += verticalVelocity * Time.deltaTime;
            if (height <= 0f)
            {
                float impact = -verticalVelocity;
                height = 0f;
                verticalVelocity = 0f;
                airborne = false;
                airAttacksRemaining = 0;
                airChain.Clear();
                airFloatTimer = 0f;
                fallTriggered = false;
                if (impact > heavyLandThreshold)
                {
                    // heavy landing: thud, dust, and a beat of recovery
                    float over = Mathf.Clamp01((impact - heavyLandThreshold) / 10f);
                    landRecovery = 0.1f + 0.15f * over;
                    Core.CameraFollow.Instance?.Shake(0.06f + 0.08f * over, 0.14f);
                    HitStop.Do(0.02f + 0.02f * over);
                    VFX.VfxManager.Play("dash_dust", transform.position + Vector3.up * 0.1f, Facing, 0.9f + over);
                }
                if (animator != null) animator.SetTrigger(HashLand);
            }
            visual.localPosition = new Vector3(visual.localPosition.x, visualBaseY + height, visual.localPosition.z);
        }

        public override void ReceiveHit(AttackData attack, float attackerFacing)
        {
            float taken = PlayerProgression.DamageTakenMult();
            if (taken < 1f) attack = attack.ScaledBy(taken);

            combo?.Break();

            // getting hit cancels any charge in progress
            attackCharging = false;
            jumpCharging = false;
            pendingChargeShot = -1f;
            if (visual != null) visual.localScale = Vector3.one;

            base.ReceiveHit(attack, attackerFacing);
            resolver.Reset();
            contextResolver.Reset();
            bufferedToken = null;
            chainOverrideMoveSet = null;
            activeContextMoveSet = null;
            currentMove = null;
        }
    }
}
