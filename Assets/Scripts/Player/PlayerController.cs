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
        [SerializeField] private float chainWindow = 0.85f; // wider: forgiving combo routing

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
        [SerializeField] private float airAttackFloat = 0.45f;   // hover time per air hit
        [SerializeField] private float hoverDrift = -1.0f;        // gentle descent while hovering
        [SerializeField] private float airHoverMax = 1.3f;        // hold-Jump hover budget per leap
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
        [SerializeField] private float teleportInvuln = 0.55f; // covers blink + strike active frames
        [SerializeField] private float teleportBehindRange = 8f;

        [Header("Impact Aura")]
        [SerializeField] private ImpactStyle baseStyle = ImpactStyle.Neutral;
        [SerializeField] private ImpactStyle awakenedStyle = ImpactStyle.Neutral;
        /// Effective impact aura — switches to the awakened/state style while active.
        public override ImpactStyle Style => AwakenedActive ? awakenedStyle : baseStyle;

        private RuntimeAnimatorController baseController;

        private InputReader input;
        private ComboResolver resolver;
        private ComboResolver contextResolver;
        private MoveDefinition currentMove;
        private InputToken? bufferedToken;
        private float comboCancelAt; // earliest time a buffered hit may cancel the current attack
        private float bufferedAt;
        private MoveSet activeContextMoveSet;
        private MoveSet chainOverrideMoveSet;

        private float height;
        private float verticalVelocity;
        private float visualBaseY;
        private bool airborne;
        private int airAttacksRemaining;
        private float airFloatTimer;
        private float airHoverBudget; // remaining hold-Jump hover time this leap
        private float airMomentumX;   // horizontal momentum carried through a leap (sprint-jump)
        private bool fallTriggered;

        private float dashTimer;
        private float dashDir;
        private bool running;
        private float sprintDustTimer; // spaces out the per-step sprint dust puffs
        private bool flurryActive;     // Jotaro flurry: front-armoured, forward-creeping
        private float flurryTimer;
        // CQC clinch state
        private bool clinchActive;
        private CharacterCombatant clinchEnemy;
        private float clinchTimer;
        [SerializeField] private float clinchRange = 1.7f;  // grab reach
        [SerializeField] private float clinchHold = 0.95f;  // distance the held foe is pinned in front
        private int lastTapDir;
        private float lastTapTime = -10f;

        // charge state
        private float lightHold, heavyHold, gunHold;
        private bool attackCharging;
        private InputToken? chargeLatch; // button that auto-fired at full charge; blocks re-charge until released
        private InputToken chargeButton;
        private float chargeTime;
        private float chargePulseTimer;
        private bool jumpCharging;
        private float jumpChargeTime;
        private float landRecovery;

        // Dash sweeps through gunfire batting rounds back; guard/parry will
        // extend this window once those mechanics land.
        // True-Aim: dash i-frames, guard/parry, AND active melee swing frames all
        // bat rounds aside (the swing's blade arc deflects). The power-class gate
        // in Projectile lets the top two classes punch through regardless.
        public override bool DeflectsProjectiles => dashTimer > 0f || guardDeflectTimer > 0f
            || (IsAttacking && currentMove != null && !currentMove.firesProjectile && !airborne);
        private float guardDeflectTimer;

        // --- Guard / Parry: hold the stance button (or Left-Ctrl) to block.
        // A hit in the first parryWindow is a perfect parry (no damage, big
        // feedback, deflects follow-up fire); otherwise it's a chip block. ---
        [SerializeField] private float parryWindow = 0.18f;
        [SerializeField] private float guardDamageMult = 0.25f;
        public bool Guarding { get; private set; }
        private float guardTime;
        private float jumpCancelTimer; // open after a landed hit (jump-cancel)

        private void UpdateGuard()
        {
            bool wantGuard = input.GuardHeld && !airborne && !IsDead
                             && !attackCharging && !jumpCharging && !IsAttacking
                             && !KnockedDown && !InHitstun && dashTimer <= 0f;
            if (wantGuard)
            {
                if (!Guarding)
                {
                    Guarding = true;
                    guardTime = 0f;
                    if (animator != null) animator.SetTrigger(HashGuard);
                }
                guardTime += Time.deltaTime;
                guardDeflectTimer = 0.1f; // guarding bats gunfire away
                Stop();
            }
            else
            {
                Guarding = false;
            }
        }

        // --- Revolver ammo: six chambers, gun-stance shots only (awakened runs
        // on energy, no ammo). Empties trigger an animated reload. ---
        public const int RevolverMax = 6;
        public int RevolverRounds { get; private set; } = RevolverMax;
        public bool UsesRevolver => CurrentStance == Stance.Gun && !AwakenedActive;
        public bool Reloading => reloadTimer > 0f;
        public event System.Action OnRevolverChanged;
        [SerializeField] private float reloadDuration = 0.7f;
        private float reloadTimer;

        private void StartReload()
        {
            if (reloadTimer > 0f || RevolverRounds >= RevolverMax) return;
            reloadTimer = reloadDuration;
            if (animator != null) animator.SetTrigger(HashReload);
            VFX.VfxManager.Play("dash_dust", transform.position + Vector3.up * 0.55f, Facing, 0.45f);
        }

        private void UpdateReload()
        {
            if (reloadTimer > 0f)
            {
                reloadTimer -= Time.deltaTime;
                if (reloadTimer <= 0f)
                {
                    RevolverRounds = RevolverMax;
                    OnRevolverChanged?.Invoke();
                }
            }
            // auto-reload when the cylinder runs dry and the hero is free
            else if (UsesRevolver && RevolverRounds <= 0 && CanAct)
                StartReload();
        }

        public Stance CurrentStance { get; private set; } = Stance.Sword;
        public int AmmoIndex { get; private set; }
        public AmmoDefinition CurrentAmmo =>
            ammoTypes != null && ammoTypes.Length > 0 ? ammoTypes[AmmoIndex] : null;

        /// The round actually loaded into a shot. In awakened form every round is
        /// overcharged into a blue psychic variant that keeps its base element.
        public AmmoDefinition FireAmmo => Overcharge(CurrentAmmo);

        private AmmoDefinition Overcharge(AmmoDefinition ammo) =>
            (AwakenedActive && ammo != null) ? ammo.AsAwakened() : ammo;

        // --- Lucas multi-weapon loadout (gated by multiWeapon) ---
        // Each weapon keeps its OWN ammo count; switching weapons RELOADS the one
        // you switch to (quick-rotation), and each has its own fire rate, pellet
        // count, spread, damage and a dual-wield option.
        [SerializeField] private bool multiWeapon;
        private struct Weapon { public string name; public int ammoMax; public float fireInterval; public int pellets; public float spreadDeg; public float dmgMult; public bool piercing; public bool dual; }
        private static readonly Weapon[] LucasWeapons =
        {
            new Weapon { name = "RIFLE",   ammoMax = 30, fireInterval = 0.09f, pellets = 1, spreadDeg = 2f,  dmgMult = 1.0f, piercing = false, dual = false },
            new Weapon { name = "SHOTGUN", ammoMax = 8,  fireInterval = 0.55f, pellets = 5, spreadDeg = 22f, dmgMult = 0.8f, piercing = false, dual = false },
            new Weapon { name = "SMG",     ammoMax = 45, fireInterval = 0.05f, pellets = 1, spreadDeg = 5f,  dmgMult = 0.7f, piercing = false, dual = false },
            new Weapon { name = "RAIL",    ammoMax = 5,  fireInterval = 0.70f, pellets = 1, spreadDeg = 0f,  dmgMult = 2.6f, piercing = true,  dual = false },
            new Weapon { name = "DUAL",    ammoMax = 24, fireInterval = 0.12f, pellets = 2, spreadDeg = 6f,  dmgMult = 0.9f, piercing = false, dual = true  }
        };
        private int[] weaponAmmo;
        private int currentWeapon;
        private float weaponFireTimer;
        public bool MultiWeapon => multiWeapon;
        public int CurrentWeaponAmmo => weaponAmmo != null && weaponAmmo.Length > 0 ? weaponAmmo[currentWeapon] : 0;
        public int CurrentWeaponMax => multiWeapon ? LucasWeapons[currentWeapon].ammoMax : 0;
        public string CurrentWeaponName => multiWeapon ? LucasWeapons[currentWeapon].name : "";

        private void EnsureWeapons()
        {
            if (weaponAmmo != null) return;
            weaponAmmo = new int[LucasWeapons.Length];
            for (int i = 0; i < weaponAmmo.Length; i++) weaponAmmo[i] = LucasWeapons[i].ammoMax;
        }

        private void CycleWeapon()
        {
            EnsureWeapons();
            currentWeapon = (currentWeapon + 1) % LucasWeapons.Length;
            weaponAmmo[currentWeapon] = LucasWeapons[currentWeapon].ammoMax; // switch reloads it
            weaponFireTimer = 0.15f;
            OnRevolverChanged?.Invoke();
            VFX.VfxManager.Play("dash_dust", transform.position + Vector3.up * 0.55f, Facing, 0.4f);
        }

        private void FireWeapon()
        {
            EnsureWeapons();
            if (weaponFireTimer > 0f) return;
            if (projectilePrefab == null || CurrentAmmo == null) return;
            var w = LucasWeapons[currentWeapon];
            if (weaponAmmo[currentWeapon] <= 0) { CycleWeapon(); return; } // dry -> rotate (reloads next)
            weaponAmmo[currentWeapon]--;
            weaponFireTimer = w.fireInterval;
            OnRevolverChanged?.Invoke();
            if (IsAttacking) InterruptAttack();

            Vector3 origin = MuzzleOrigin;
            VFX.VfxManager.Play("muzzle_burst", origin, Facing, w.pellets > 1 ? 1.2f : 1f);
            MuzzleBlast(origin);
            for (int p = 0; p < w.pellets; p++)
            {
                float ang = w.spreadDeg > 0f ? Random.Range(-w.spreadDeg, w.spreadDeg) : 0f;
                Vector2 aim = (Vector2)(Quaternion.Euler(0f, 0f, ang) * new Vector3(Facing, 0f, 0f));
                Vector3 o = w.dual ? origin + new Vector3(0f, p % 2 == 0 ? 0.28f : -0.28f, 0f) : origin;
                var proj = Instantiate(projectilePrefab, o, Quaternion.identity);
                proj.Fire(Team.Player, FireAmmo, Facing, DamageScale * w.dmgMult, 1f, 1f, w.piercing, aim, Style);
            }
            body.linearVelocity = new Vector2(-Facing * (w.pellets > 1 ? 2f : 1f), body.linearVelocity.y);
            if (animator != null) animator.SetTrigger(HashShoot);
            Core.CameraFollow.Instance?.Shake(0.03f, 0.05f);
        }

        // --- Lucas gadgets: gravity well, sonic stun, frag bomb, electric bomb ---
        private int currentGadget;
        private float gadgetCooldown;
        public string CurrentGadgetName => multiWeapon
            ? new[] { "GRAVITY", "SONIC", "FRAG", "ELECTRIC" }[currentGadget] : "";
        private static readonly Collider2D[] GadgetHits = new Collider2D[16];

        private void ThrowGadget()
        {
            if (gadgetCooldown > 0f) return;
            gadgetCooldown = 0.8f;
            Vector3 at = transform.position + new Vector3(Facing * 2.2f, 0.5f, 0f);
            if (animator != null) animator.SetTrigger(HashShoot);
            HitStop.Do(0.03f);
            Core.CameraFollow.Instance?.Shake(0.06f, 0.1f);
            switch (currentGadget)
            {
                case 0: Gadget_Gravity(at); break;
                case 1: Gadget_Sonic(at); break;
                case 2: Gadget_Frag(at); break;
                default: Gadget_Electric(at); break;
            }
            currentGadget = (currentGadget + 1) % 4; // quick rotation through the kit
            OnRevolverChanged?.Invoke();
        }

        private void Gadget_Gravity(Vector3 at)
        {
            VFX.VfxManager.Play("throw_impact", at, Facing, 1.6f);
            int n = Physics2D.OverlapCircleNonAlloc(at, 3.5f, GadgetHits);
            for (int i = 0; i < n; i++)
            {
                var hb = GadgetHits[i].GetComponent<Hurtbox>();
                if (hb == null || hb.Team == Team.Player || hb.Owner == null) continue;
                Vector2 pull = ((Vector2)at - (Vector2)hb.Owner.transform.position).normalized * 7f;
                hb.Owner.GetComponent<Rigidbody2D>()?.AddForce(pull, ForceMode2D.Impulse);
                hb.Owner.PinInPlace(0.15f);
            }
        }

        private void Gadget_Sonic(Vector3 at)
        {
            VFX.VfxManager.Play("throw_impact", at, Facing, 1.9f, null, new Color(0.6f, 0.9f, 1f));
            int n = Physics2D.OverlapCircleNonAlloc(at, 3.8f, GadgetHits);
            for (int i = 0; i < n; i++)
            {
                var hb = GadgetHits[i].GetComponent<Hurtbox>();
                if (hb == null || hb.Team == Team.Player || hb.Owner == null) continue;
                hb.Owner.AddStun(60f); // heavy stun build-up — sonic disorients
            }
        }

        private void Gadget_Frag(Vector3 at)
        {
            VFX.DebrisManager.Burst(at, Facing, 2f, VFX.DebrisSpec.Default(Team.Player, 2f));
            int n = Physics2D.OverlapCircleNonAlloc(at, 3f, GadgetHits);
            for (int i = 0; i < n; i++)
            {
                var hb = GadgetHits[i].GetComponent<Hurtbox>();
                if (hb == null || hb.Team == Team.Player || hb.Owner == null) continue;
                float dir = Mathf.Sign(hb.Owner.transform.position.x - at.x);
                if (dir == 0f) dir = Facing;
                hb.Owner.ReceiveHit(new AttackData { damage = 22, hitstun = 0.4f, knockback = new Vector2(7f, 1f), knocksDown = true, spawnsDebris = true }.ScaledBy(DamageScale), dir);
            }
        }

        private void Gadget_Electric(Vector3 at)
        {
            VFX.VfxManager.Play("electric_bigman", at, Facing, 1.8f);
            int n = Physics2D.OverlapCircleNonAlloc(at, 3.2f, GadgetHits);
            for (int i = 0; i < n; i++)
            {
                var hb = GadgetHits[i].GetComponent<Hurtbox>();
                if (hb == null || hb.Team == Team.Player || hb.Owner == null) continue;
                float dir = Mathf.Sign(hb.Owner.transform.position.x - at.x);
                if (dir == 0f) dir = Facing;
                hb.Owner.AddStun(35f);
                hb.Owner.ReceiveHit(new AttackData { damage = 14, hitstun = 0.5f, knockback = new Vector2(3f, 0.5f) }.ScaledBy(DamageScale), dir);
            }
        }

        /// World position of the revolver muzzle tip, mirrored to the way Silver
        /// faces so shots always leave the barrel on the correct side.
        private Vector3 MuzzleOrigin => firePoint != null
            ? transform.position + new Vector3(Mathf.Abs(firePoint.localPosition.x) * Facing,
                                               firePoint.localPosition.y, firePoint.localPosition.z)
            : transform.position + new Vector3(0.9f * Facing, 0.9f, 0f);

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
        private static readonly int HashStanceSwitch = Animator.StringToHash("StanceSwitch");
        private static readonly int HashGuard = Animator.StringToHash("Guard");
        private static readonly int HashParry = Animator.StringToHash("Parry");
        private static readonly int HashShoot = Animator.StringToHash("Shoot");
        private static readonly int HashSlashMirage = Animator.StringToHash("SlashMirage");

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

            input.Tick(Facing); // drives stance-tap/guard-hold + motion inputs

            // CQC clinch: while grappling, only clinch logic runs (throws/slams).
            if (clinchActive) { UpdateClinch(); return; }

            UpdateAwakened();
            UpdateJumpArc();
            UpdateReload();
            TrackHolds();

            // guard locks out other actions while held (release to act)
            UpdateGuard();
            if (Guarding) return;

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

            if (input.StanceTapped) ToggleStance();
            if (input.AmmoPressed) CycleAmmo();
            if (input.AwakenedPressed) TryActivateAwakened();

            // CQC grab: lock-on + Grab near a foe enters a close-quarters clinch.
            if (input.GrabPressed && input.LockOnHeld && CanAct && TryStartClinch()) return;

            // Lucas multi-weapon auto-fire + gadget throws (timers count down even
            // mid-action; hold the gun button to rip the current weapon).
            if (multiWeapon)
            {
                if (weaponFireTimer > 0f) weaponFireTimer -= Time.deltaTime;
                if (gadgetCooldown > 0f) gadgetCooldown -= Time.deltaTime;
                if (input.GadgetPressed && CanAct) ThrowGadget();
                if (input.GunHeld && !AwakenedActive && !Guarding && !airborne && CanAct)
                    FireWeapon();
            }

            // jump-cancel: after a landed hit you can cancel attack recovery
            // straight into a jump to chase a launched enemy skyward
            if (jumpCancelTimer > 0f) jumpCancelTimer -= Time.deltaTime;
            if (IsAttacking && !airborne && jumpCancelTimer > 0f && input.JumpPressed)
            {
                InterruptAttack();
                LaunchJump(0f);
                jumpCancelTimer = 0f;
            }

            var token = ReadToken();
            if (token.HasValue) HandleToken(token.Value);

            // combo fluidity: a buffered next-hit cancels the recovery ONLY once the
            // current swing has actually played far enough to read (its strike has
            // landed). Gated on the live clip progress so stretched clips still show
            // every swing instead of being cut off — fixes dropped/skipped swings.
            if (IsAttacking && bufferedToken.HasValue && Time.time >= comboCancelAt
                && AttackClipProgress() >= 0.6f)
                AnimEvent_AttackEnd();

            if (CanAct && !airborne && input.JumpPressed)
                BeginJumpCharge();

            if (CanAct && !airborne)
                TryBeginAttackCharge();

            if (CanAct)
            {
                var move = input.Move;
                HandleDoubleTap();

                if (airborne)
                {
                    ApplyAirMomentum(move);
                }
                else if (dashTimer > 0f)
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
                        // Dust at every major sprint step: a kicked-up puff behind
                        // the trailing foot, paced to the run cadence.
                        if (running && Mathf.Abs(move.x) > 0.3f)
                        {
                            sprintDustTimer -= Time.deltaTime;
                            if (sprintDustTimer <= 0f)
                            {
                                sprintDustTimer = 0.2f;
                                VFX.VfxManager.Play("dash_dust",
                                    transform.position + new Vector3(-Facing * 0.45f, 0.1f, 0f),
                                    -Facing, 0.55f);
                            }
                        }
                        else sprintDustTimer = 0f;
                    }
                }
            }
            else if (IsAttacking)
            {
                if (flurryActive)
                {
                    // Jotaro flurry: creep slowly FORWARD through the barrage (momentum),
                    // then stop dead during the wind-up pause before the cleave.
                    flurryTimer -= Time.deltaTime;
                    bool pausing = flurryTimer <= 0.46f && flurryTimer > 0.28f; // ~the pause window
                    body.linearVelocity = new Vector2(pausing ? 0f : Facing * 2.2f, body.linearVelocity.y);
                    if (animator != null) animator.SetFloat(HashMoveSpeed, 0f);
                }
                else
                {
                    // lunge momentum bleeds off through the swing instead of stopping dead
                    AttackDrift(IsHeavyAttack() ? 10f : 14f);
                }
            }
        }

        private bool IsHeavyAttack() =>
            currentAttack != null && (currentAttack.knocksDown || currentAttack.launch > 0f);

        /// 0..1 progress through the CURRENT attack clip (clip-length agnostic), so
        /// combo cancels wait for the real swing to read regardless of bake stretch.
        private float AttackClipProgress()
        {
            if (animator == null) return 1f;
            return Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f);
        }

        private void TrackHolds()
        {
            float dt = Time.deltaTime;
            lightHold = input.LightHeld ? lightHold + dt : 0f;
            heavyHold = input.HeavyHeld ? heavyHold + dt : 0f;
            gunHold = input.GunHeld ? gunHold + dt : 0f;
            // A full-charge auto-fire latches its button so holding it down can't
            // immediately re-charge in a loop; releasing the button clears it.
            if (chargeLatch == InputToken.Light && !input.LightHeld) chargeLatch = null;
            else if (chargeLatch == InputToken.Heavy && !input.HeavyHeld) chargeLatch = null;
            else if (chargeLatch == InputToken.Gun && !input.GunHeld) chargeLatch = null;
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
                else if (lockTarget != null)
                {
                    // teleport system (base): locked-on dash blinks instead of
                    // running — toward the target snaps behind it, away retreats
                    SetFacing(dashDir);
                    float toTarget = Mathf.Sign(lockTarget.transform.position.x - transform.position.x);
                    Vector3 dest = tap == toTarget
                        ? lockTarget.transform.position + new Vector3(dashDir * 1.6f, 0f, 0f)
                        : transform.position + new Vector3(dashDir * 4f * TeleportMult, 0f, 0f);
                    TeleportTo(dest);
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

        // Two-step motions (down -> forward / down -> back). The optional diagonal
        // is skipped so keyboard players, who can't hold a clean 3/1 diagonal,
        // can still execute every directional special — not just the teleport.
        private static readonly int[] Qcf = { 2, 6 }; // quarter-circle forward
        private static readonly int[] Qcb = { 2, 4 }; // quarter-circle back

        /// Semi-automatic revolver: one shot per trigger pull, no cooldown, no
        /// auto/hold fire. Cancels a prior shot's recovery so the player can
        /// mash to empty the cylinder as fast as they can tap.
        private void SemiAutoFire()
        {
            if (multiWeapon) { FireWeapon(); return; } // Lucas: fire the current loadout weapon
            if (Reloading) return;
            if (RevolverRounds <= 0) { StartReload(); return; }
            if (projectilePrefab == null || CurrentAmmo == null) return;
            if (IsAttacking) InterruptAttack();

            RevolverRounds--;
            OnRevolverChanged?.Invoke();
            Vector3 origin = MuzzleOrigin; // exactly at the revolver barrel tip
            VFX.VfxManager.Play("muzzle_burst", origin, Facing);
            MuzzleBlast(origin); // muzzle-flare hurtbox: point-blank flash damage
            var proj = Instantiate(projectilePrefab, origin, Quaternion.identity);
            proj.Fire(Team.Player, FireAmmo, Facing, DamageScale, style: Style);
            body.linearVelocity = new Vector2(-Facing * 1.2f, body.linearVelocity.y); // light recoil
            if (animator != null) animator.SetTrigger(HashShoot);
            Core.CameraFollow.Instance?.Shake(0.025f, 0.05f);
        }

        /// Muzzle-flare hurtbox: a brief point-blank hit at the barrel so firing
        /// into a foe at contact range singes them with the flash itself.
        private static readonly Collider2D[] MuzzleHits = new Collider2D[6];
        private void MuzzleBlast(Vector3 origin)
        {
            int n = Physics2D.OverlapCircleNonAlloc(origin, 0.55f, MuzzleHits);
            for (int i = 0; i < n; i++)
            {
                var hb = MuzzleHits[i].GetComponent<Hurtbox>();
                if (hb == null || hb.Team == Team.Player || hb.Owner == null) continue;
                hb.Owner.ReceiveHit(new AttackData { damage = 4, hitstun = 0.12f, knockback = new Vector2(2f, 0f) }, Facing);
            }
        }

        /// Slash Mirage: start-up slashes, then a hyper-accel barrage where
        /// Silver dissolves into after-images — fully invincible, no solid
        /// sprite (the clip is the mirage itself) — landing a storm of knock-
        /// around multi-hits before the mirage fades back in.
        [SerializeField] private float mirageDuration = 1.25f;

        private void StartSlashMirage()
        {
            var move = new MoveDefinition
            {
                id = "SlashMirage",
                animatorTrigger = "SlashMirage",
                duration = mirageDuration,
                multiHit = 14, // dense barrage of knock-around blows
                aoe = true,    // both sides — enemies get juggled in the storm
                attack = new AttackData
                {
                    damage = 9,
                    hitstun = 0.18f,
                    knockback = new Vector2(2.4f, 0.8f),
                    hitsBothSides = true,
                    rangeScale = 1.7f,
                    heightScale = 1.4f
                }
            };
            StartMove(move);
            // total invincibility for the whole vanish-and-barrage
            invulnTimer = mirageDuration;
            VFX.VfxManager.Play("dash_dust", transform.position + Vector3.up * 0.5f, Facing, 0.8f);
        }

        /// QCF + attack: blink onto the nearest foe and strike (teleport special).
        private bool TryTeleportAttack(InputToken token)
        {
            if (!CanAct) return false;
            var move = teleportMoveSet.Match(new[] { token });
            if (move == null) return false;

            var tgt = lockTarget ?? FindLockTarget();
            float dir = tgt != null ? Mathf.Sign(tgt.transform.position.x - transform.position.x) : Facing;
            if (dir == 0f) dir = Facing;
            SetFacing(dir);
            Vector3 dest = tgt != null
                ? tgt.transform.position - new Vector3(dir * 1.5f, 0f, 0f)
                : transform.position + new Vector3(dir * 4f * TeleportMult, 0f, 0f);
            TeleportTo(dest);
            StartMove(move, chainSet: teleportMoveSet);
            return true;
        }

        private void HandleToken(InputToken token)
        {
            if (airborne)
            {
                HandleAirToken(token);
                return;
            }

            // Semi-auto gun: in gun stance every Gun press is one immediate
            // shot (trigger-pull, not auto/hold) with no cooldown, so the
            // player can mash to empty the cylinder as fast as they can press.
            // The Gun button always fires the revolver with the SAME firing
            // animation in BOTH sword and gun stance (consistency) — only the
            // awakened energy form overrides it.
            if (token == InputToken.Gun && !AwakenedActive && !Guarding)
            {
                SemiAutoFire();
                return;
            }

            // Special motion inputs: QCF + attack = teleport strike (blink to
            // the foe and slash). The base of a growing special-move list.
            if ((token == InputToken.Light || token == InputToken.Heavy)
                && teleportMoveSet != null && input.ConsumeMotion(Qcf))
            {
                if (TryTeleportAttack(token)) return;
            }

            // QCB + Heavy = Slash Mirage: a hyper-accel barrage; the player
            // vanishes (the clip is pure mirage) and is fully invincible.
            if (token == InputToken.Heavy && CanAct && input.ConsumeMotion(Qcb))
            {
                StartSlashMirage();
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

        /// Directional air gunfire: aim from the stick/d-pad (diagonal-down
        /// plunge shots included), recoil-nudged, and refreshes the hover.
        private void AirGunFire()
        {
            if (projectilePrefab == null || CurrentAmmo == null) return;
            if (UsesRevolver)
            {
                if (Reloading) return;
                if (RevolverRounds <= 0) { StartReload(); return; }
                RevolverRounds--;
                OnRevolverChanged?.Invoke();
            }

            Vector2 m = input.Move;
            float h = Mathf.Abs(m.x) > 0.3f ? Mathf.Sign(m.x) : Facing;
            float v = m.y < -0.3f ? -1f : m.y > 0.3f ? 1f : 0f;
            Vector2 aim = v != 0f ? new Vector2(h * 0.85f, v).normalized : new Vector2(h, 0f);
            SetFacing(h);

            Vector3 origin = transform.position + (Vector3)(aim * 0.7f) + Vector3.up * 0.9f;
            VFX.VfxManager.Play("muzzle_burst", origin, h);
            var proj = Instantiate(projectilePrefab, origin, Quaternion.identity);
            proj.Fire(Team.Player, FireAmmo, h, DamageScale, 1f, 1f, false, aim, Style);

            // recoil + hover: firing hangs Silver aloft, drifting slowly down
            body.linearVelocity = new Vector2(-aim.x * 2.2f, body.linearVelocity.y);
            airFloatTimer = airAttackFloat;
            if (verticalVelocity < -1f) verticalVelocity = -1f;
            Core.CameraFollow.Instance?.Shake(0.03f, 0.06f);
        }

        private void HandleAirToken(InputToken token)
        {
            // Gun in the air = directional air fire (aim with the stick/d-pad,
            // including diagonal-DOWN plunging shots). Keeps you hovering.
            if (token == InputToken.Gun)
            {
                AirGunFire();
                return;
            }

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
            if (lightHold >= chargeStartDelay && chargeLatch != InputToken.Light) { button = InputToken.Light; hold = lightHold; }
            else if (heavyHold >= chargeStartDelay && chargeLatch != InputToken.Heavy) { button = InputToken.Heavy; hold = heavyHold; }
            else if (gunHold >= chargeStartDelay && chargeLatch != InputToken.Gun) { button = InputToken.Gun; hold = gunHold; }
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
            // Auto-fired at max? latch the button so it won't re-charge on hold.
            if (chargeTime >= chargeFullTime) chargeLatch = chargeButton;
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
                    // rapid multi-hit barrage: damage is split across the hits
                    multiHit = 4 + level * 2,
                    attack = new AttackData
                    {
                        damage = Mathf.Max(4, dmg[level] / (2 + level)),
                        hitstun = 0.18f,
                        knockback = new Vector2(1.5f + level, 0f),
                        knocksDown = false,
                        rangeScale = 1.4f + 0.25f * level,
                        heightScale = 1.2f
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
                    multiHit = 1 + level, // weighty slams, then a finishing blow
                    attack = new AttackData
                    {
                        damage = Mathf.Max(10, dmg[level] / (1 + level)),
                        hitstun = 0.45f,
                        knockback = new Vector2(6f + 2f * level, 0f),
                        knocksDown = true,
                        launch = level >= 3 ? 8.5f : 0f,
                        rangeScale = 1.35f + 0.3f * level,
                        heightScale = 1.4f,
                        hitsBothSides = level >= 3, // full charge = large AOE cleave
                        spawnsDebris = true          // heavy impacts kick up debris
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
            // Jotaro flurry: a grounded high-multiHit melee move runs the special
            // 1.5s flurry (front-armoured, forward-creeping, cleave finisher).
            flurryActive = !move.firesProjectile && move.multiHit >= 6;
            flurryTimer = flurryActive ? 1.55f : 0f;
            attackTimeout = (flurryActive ? 1.55f : move.duration) + 0.35f; // safety net if AttackEnd never fires
            // cancel point: after the strike lands a buffered next-hit may flow in
            comboCancelAt = Time.time + Mathf.Max(0.1f, move.duration * 0.55f);
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
            flurryActive = false;
            flurryTimer = 0f;
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

        // Jotaro flurry: a small, fast paced slash flash per blow (front of Silver).
        public override void AnimEvent_FlurryImpact()
        {
            Vector3 front = transform.position
                + new Vector3(Facing * (0.9f + Random.Range(0f, 0.5f)), 0.2f + Random.Range(-0.2f, 0.6f), 0f);
            VFX.CombatVfx.Slash(front, Facing, Style, 0.55f);
        }

        // Jotaro flurry finisher: after the wind-up pause, one explosive heavy
        // cleave — a strong AoE in front that spikes airborne foes / pops grounded
        // ones and GROUND-BOUNCES them, with debris, shake and hitstop.
        public override void AnimEvent_FinisherHit()
        {
            Vector3 center = transform.position + new Vector3(Facing * 1.2f, 0.5f, 0f);
            var cleave = new AttackData
            {
                damage = 34, hitstun = 0.45f, knockback = new Vector2(6f, -1f),
                launch = 5f, spike = true, groundBounce = true, spawnsDebris = true,
                rangeScale = 1.6f, heightScale = 1.5f
            }.ScaledBy(DamageScale);

            var seen = new System.Collections.Generic.HashSet<CharacterCombatant>();
            foreach (var c in Physics2D.OverlapCircleAll(center, 2.2f))
            {
                var hb = c.GetComponent<Hurtbox>();
                if (hb == null || hb.Team == Team.Player || hb.Owner == null || !seen.Add(hb.Owner)) continue;
                float dir = Mathf.Sign(hb.Owner.transform.position.x - transform.position.x);
                if (dir == 0f) dir = Facing;
                VFX.CombatVfx.Resolve(new VFX.CombatVfx.ImpactInfo
                {
                    pos = hb.Owner.transform.position, facing = dir, damage = cleave.damage, crit = true,
                    isProjectile = false, material = hb.Owner.Material, element = DamageType.Kinetic,
                    style = Style, target = hb.Owner
                });
                hb.Owner.ReceiveHit(cleave, Facing);
            }

            VFX.DebrisManager.Burst(center, Facing, 1.7f);
            VFX.VfxManager.Play("charge_debris", center, Facing, 1.2f);
            Core.CameraFollow.Instance?.Shake(0.18f, 0.2f);
            HitStop.Do(0.12f);
            body.linearVelocity = new Vector2(Facing * 3.5f, 0f); // drive into the cleave
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

            // revolver gating: a gun-stance shot needs a chambered round; empty
            // or mid-reload swallows the shot and (re)loads instead.
            bool gunShot = UsesRevolver && currentMove != null && currentMove.firesProjectile;
            if (gunShot)
            {
                if (Reloading) return;
                if (RevolverRounds <= 0) { StartReload(); return; }
                RevolverRounds--;
                OnRevolverChanged?.Invoke();
            }

            Vector3 origin = MuzzleOrigin;

            if (pendingChargeShot >= 0f)
            {
                float c = pendingChargeShot;
                pendingChargeShot = -1f;
                int lvl = Mathf.Clamp(pendingChargeLevel, 1, 3);
                // Charged revolver shot scales PER LEVEL: bigger round (hurtbox),
                // faster travel, more damage; level 3 pierces clean through.
                float dmgMult = 1f + 1.2f * lvl;      // x2.2 / x3.4 / x4.6
                float sizeMult = 0.95f + 0.2f * lvl;  // x1.15 / x1.35 / x1.55 (was oversized)
                float speedMult = 1.1f + 0.5f * lvl;  // x1.6 / x2.1 / x2.6
                VFX.VfxManager.Play("muzzle_burst", origin, Facing, 1f + c);
                var charged = Instantiate(projectilePrefab, origin, Quaternion.identity);
                charged.Fire(Team.Player, FireAmmo, Facing, DamageScale * dmgMult,
                    sizeMult, speedMult, lvl >= 3, null, Style);
                // recoil sells the shot's weight
                body.linearVelocity = new Vector2(-Facing * (1.5f + 2.5f * c), 0f);
                Core.CameraFollow.Instance?.Shake(0.05f + 0.07f * c, 0.12f);
                HitStop.Do(0.03f);
                return;
            }

            // Barrage / scatter / rain moves spray a fan of rounds in the spread
            // the animation shows; plain shots fire a single round forward. Ammo
            // (equipped element AND awakened psychic form) comes from FireAmmo, so
            // every pellet matches the state and projectile type.
            int pellets = 1; float spreadDeg = 0f;
            string moveId = currentMove != null ? currentMove.id ?? "" : "";
            if (moveId.Contains("Scatter")) { pellets = 5; spreadDeg = 54f; }
            else if (moveId.Contains("Rain")) { pellets = 4; spreadDeg = 80f; }
            else if (moveId.Contains("Barrage")) { pellets = 3; spreadDeg = 18f; }

            VFX.VfxManager.Play("muzzle_burst", origin, Facing, pellets > 1 ? 1.3f : 1f);
            if (pellets == 1)
            {
                var proj = Instantiate(projectilePrefab, origin, Quaternion.identity);
                proj.Fire(Team.Player, FireAmmo, Facing, DamageScale, style: Style);
            }
            else
            {
                float fwd = Mathf.Sign(Facing);
                for (int i = 0; i < pellets; i++)
                {
                    float t = pellets == 1 ? 0f : i / (float)(pellets - 1) - 0.5f;
                    float ang = t * spreadDeg * Mathf.Deg2Rad;
                    Vector2 dir = new Vector2(fwd * Mathf.Cos(ang), Mathf.Sin(ang));
                    var pellet = Instantiate(projectilePrefab, origin, Quaternion.identity);
                    pellet.Fire(Team.Player, FireAmmo, Facing, DamageScale, 1f, 1f, false, dir, Style);
                }
            }
        }

        private void FireSlashWave()
        {
            if (slashWavePrefab == null || slashWaveAmmo == null) return;
            Vector3 origin = transform.position + new Vector3(0.9f * Facing, 0.9f, 0f);
            float power = currentMove != null && currentMove.attack != null
                ? Mathf.Max(1f, currentMove.attack.damage / 24f)
                : 1f;
            var wave = Instantiate(slashWavePrefab, origin, Quaternion.identity);
            wave.Fire(Team.Player, Overcharge(slashWaveAmmo), Facing, DamageScale * power, 0.9f + 0.25f * power,
                1f, false, null, Style);
        }

        private void ToggleStance()
        {
            CurrentStance = CurrentStance == Stance.Sword ? Stance.Gun : Stance.Sword;
            // Awakened transcends stances: keep the awakened move set until it expires.
            if (!AwakenedActive)
                resolver.SetMoveSet(CurrentStance == Stance.Sword ? swordMoveSet : gunMoveSet);
            // Seamless flow: a stance swap mid-string keeps the combo alive (the
            // ComboTracker chain never drops) so sword strikes flow into gunfire
            // and back. Only the per-stance move resolver restarts its string.
            chainOverrideMoveSet = null;
            activeContextMoveSet = null;
            contextResolver.Reset();
            // quick draw/holster flourish; doesn't interrupt the chain window
            if (animator != null && !IsAttacking) animator.SetTrigger(HashStanceSwitch);
            VFX.VfxManager.Play("dash_dust", transform.position + Vector3.up * 0.6f, Facing, 0.5f);
            OnStanceChanged?.Invoke(CurrentStance);
        }

        private void CycleAmmo()
        {
            // Lucas: the ammo button rotates his weapon loadout (reloading the next).
            if (multiWeapon) { CycleWeapon(); return; }
            // in gun stance with spent chambers, the ammo button reloads instead
            if (UsesRevolver && RevolverRounds < RevolverMax) { StartReload(); return; }
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
                if (Core.CameraFollow.Instance != null) Core.CameraFollow.Instance.LockedOn = false;
                return;
            }

            if (lockTarget != null && (lockTarget.IsDead
                || Vector2.Distance(lockTarget.transform.position, transform.position) > lockOnRange * 1.25f))
                lockTarget = null;
            if (lockTarget == null) lockTarget = FindLockTarget();
            // CQC camera: enable the locked-on close-quarters zoom while a target is held.
            if (Core.CameraFollow.Instance != null) Core.CameraFollow.Instance.LockedOn = lockTarget != null;
        }

        // --- CQC Engine: grapple / clinch ---
        private enum ThrowKind { Forward, Slam, Back }

        private bool TryStartClinch()
        {
            CharacterCombatant best = null;
            float bestD = clinchRange;
            foreach (var e in FindObjectsByType<Enemies.EnemyAI>(FindObjectsInactive.Exclude))
            {
                if (e.IsDead || !e.gameObject.activeInHierarchy) continue;
                if (Mathf.Sign(e.transform.position.x - transform.position.x) != Mathf.Sign(Facing)) continue; // in front
                float d = Vector2.Distance(e.transform.position, transform.position);
                if (d < bestD) { bestD = d; best = e; }
            }
            if (best == null) return false;

            clinchActive = true;
            clinchEnemy = best;
            clinchTimer = 3f; // auto-release if no throw chosen
            SetFacing(best.transform.position.x - transform.position.x);
            Stop();
            best.SetHeld(0.3f);
            if (animator != null) animator.SetTrigger(HashGuard); // clinch hold pose
            Core.CameraFollow.Instance?.BeginClinch(transform);
            VFX.VfxManager.Play("hit_spark", best.transform.position + Vector3.up * 0.8f, Facing, 0.7f);
            return true;
        }

        private void UpdateClinch()
        {
            if (clinchEnemy == null || clinchEnemy.IsDead) { EndClinch(); return; }
            clinchTimer -= Time.deltaTime;

            // pin the foe in front of Silver (exact grapple placement)
            Vector3 spot = transform.position + new Vector3(Facing * clinchHold, 0f, 0f);
            clinchEnemy.transform.position = Vector3.Lerp(clinchEnemy.transform.position, spot, 0.5f);
            clinchEnemy.SetHeld(0.3f);
            body.linearVelocity = Vector2.zero;

            var token = ReadToken();
            if (token == InputToken.Light) ExecuteThrow(ThrowKind.Forward);
            else if (token == InputToken.Heavy) ExecuteThrow(ThrowKind.Slam);
            else if (token == InputToken.Gun) ExecuteThrow(ThrowKind.Back);
            else if (input.GrabPressed || clinchTimer <= 0f) EndClinch();
        }

        private void ExecuteThrow(ThrowKind kind)
        {
            var e = clinchEnemy;
            clinchActive = false;
            clinchEnemy = null;
            Core.CameraFollow.Instance?.EndClinch();
            if (e == null) return;
            e.SetHeld(0f);

            float dir = Facing;
            AttackData atk;
            switch (kind)
            {
                case ThrowKind.Slam: // drive them down -> spike + ground bounce
                    atk = new AttackData { damage = 28, hitstun = 0.5f, knockback = new Vector2(2f, 0f),
                        launch = 4f, spike = true, groundBounce = true, spawnsDebris = true };
                    if (animator != null) animator.SetTrigger("GroundSlam");
                    break;
                case ThrowKind.Back: // toss behind Silver
                    dir = -Facing;
                    atk = new AttackData { damage = 22, hitstun = 0.45f, knockback = new Vector2(9f, 1f), knocksDown = true };
                    if (animator != null) animator.SetTrigger("GreatCleave");
                    break;
                default: // Forward throw, into a wall if there is one
                    atk = new AttackData { damage = 24, hitstun = 0.45f, knockback = new Vector2(10f, 1.5f),
                        knocksDown = true, wallBounce = true };
                    if (animator != null) animator.SetTrigger("GreatCleave");
                    break;
            }

            Vector3 at = e.transform.position + Vector3.up * 0.8f;
            VFX.VfxManager.Play("throw_impact", at, dir, 1.2f);   // dedicated throw/slam VFX category
            VFX.DebrisManager.Burst(at, dir, 1.3f);
            VFX.CombatVfx.Resolve(new VFX.CombatVfx.ImpactInfo
            {
                pos = at, facing = dir, damage = atk.damage, crit = true,
                isProjectile = false, material = e.Material, element = DamageType.Kinetic,
                style = Style, target = e
            });
            e.ReceiveHit(atk.ScaledBy(DamageScale), dir);
            Core.CameraFollow.Instance?.Shake(0.16f, 0.18f);
            HitStop.Do(0.1f);
        }

        private void EndClinch()
        {
            clinchActive = false;
            Core.CameraFollow.Instance?.EndClinch();
            if (clinchEnemy != null) clinchEnemy.SetHeld(0f);
            clinchEnemy = null;
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
            jumpCancelTimer = 0.35f; // landed a hit -> jump-cancel window opens

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
            airHoverBudget = airHoverMax; // fresh hover budget each leap
            fallTriggered = false;
            // Sprint-jump: a leap out of a run keeps its full horizontal momentum.
            float sprintSpeed = moveSpeedX * runMultiplier * SpeedScale;
            airMomentumX = running
                ? Physics.CombatPhysics.SprintJumpMomentum(body.linearVelocity.x, sprintSpeed, Facing)
                : body.linearVelocity.x;
            verticalVelocity = jumpVelocity * (1f + jumpChargeBonus * charge01);
            // launch dust kicked off the ground at the feet
            VFX.CombatVfx.Dust(transform.position + Vector3.down * 0.85f, Facing, 0.5f + 0.4f * charge01);
            if (charge01 > 0.4f)
                Core.CameraFollow.Instance?.Shake(0.05f * charge01, 0.1f);
            if (animator != null) animator.SetTrigger(HashJump);
        }

        /// Airborne horizontal control: keep the carried sprint-jump momentum and
        /// allow light steering, instead of the grounded move snapping velocity to
        /// input (which would kill a running leap the instant you let go).
        private void ApplyAirMomentum(Vector2 move)
        {
            if (Mathf.Abs(move.x) > 0.25f)
            {
                float target = Mathf.Sign(move.x) * Mathf.Max(Mathf.Abs(airMomentumX), moveSpeedX * SpeedScale);
                airMomentumX = Physics.CombatPhysics.AirControl(airMomentumX, target, Time.deltaTime, 4f);
                SetFacing(move.x);
            }
            body.linearVelocity = new Vector2(airMomentumX, body.linearVelocity.y);
            if (animator != null) animator.SetFloat(HashMoveSpeed, Mathf.Abs(airMomentumX));
        }

        private void UpdateJumpArc()
        {
            if (!airborne || visual == null) return;
            // Hover triggers two ways: briefly after each air action (airFloatTimer)
            // and on demand by HOLDING Jump near/after the apex (spends a per-leap
            // budget). Either way Silver hangs and drifts slowly down instead of
            // falling, so air combos stay aloft and read as deliberate floating.
            bool actionHover = airFloatTimer > 0f;
            bool holdHover = input != null && input.JumpHeld && airHoverBudget > 0f
                             && verticalVelocity < 3f; // engages around the apex / on descent
            if (actionHover || holdHover)
            {
                if (actionHover) airFloatTimer -= Time.deltaTime;
                else airHoverBudget -= Time.deltaTime;
                verticalVelocity = Mathf.MoveTowards(verticalVelocity, hoverDrift, 45f * Time.deltaTime);
            }
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
                // every landing kicks up a dust puff at the feet; heavy landings
                // add a thud, bigger dust, camera shake and a beat of recovery
                Vector3 feet = transform.position + Vector3.down * 0.85f;
                if (impact > heavyLandThreshold)
                {
                    float over = Mathf.Clamp01((impact - heavyLandThreshold) / 10f);
                    landRecovery = 0.1f + 0.15f * over;
                    Core.CameraFollow.Instance?.Shake(0.06f + 0.08f * over, 0.14f);
                    HitStop.Do(0.02f + 0.02f * over);
                    VFX.CombatVfx.Dust(feet, Facing, 0.8f + over * 0.6f);
                    VFX.CombatVfx.Dust(feet + new Vector3(-Facing * 0.4f, 0f, 0f), -Facing, 0.5f + over * 0.4f);
                }
                else
                {
                    VFX.CombatVfx.Dust(feet, Facing, 0.4f);
                }
                if (animator != null) animator.SetTrigger(HashLand);
            }
            visual.localPosition = new Vector3(visual.localPosition.x, visualBaseY + height, visual.localPosition.z);
        }

        public override void ReceiveHit(AttackData attack, float attackerFacing)
        {
            // Jotaro flurry: the whirling blade wall makes Silver's FRONT nearly
            // invincible; only hits landing on his exposed BACK get through.
            if (flurryActive && !IsDead && attack != null
                && Mathf.Sign(attackerFacing) == -Mathf.Sign(Facing))
            {
                VFX.VfxManager.Play("hit_spark",
                    transform.position + new Vector3(Facing * 0.6f, 0.8f, 0f), -attackerFacing, 0.6f);
                return; // deflected on the armoured front
            }

            // Guard / parry intercept before any damage or combo break.
            if (Guarding && !IsDead && attack != null)
            {
                if (guardTime <= parryWindow)
                {
                    // perfect parry: no damage, keeps the combo, big feedback,
                    // deflects follow-up fire, refunds awakened meter
                    HitStop.Do(0.15f);
                    Core.CameraFollow.Instance?.Shake(0.12f, 0.16f);
                    VFX.VfxManager.Play("hit_spark", transform.position + Vector3.up * 0.9f, -attackerFacing, 1.5f);
                    if (animator != null) animator.SetTrigger(HashParry); // dedicated parry flash pose
                    guardDeflectTimer = 0.45f;
                    AwakenedMeter = Mathf.Min(awakenedMax, AwakenedMeter + awakenedGainPerHit * 1.5f);
                    return;
                }
                // chip block: heavy mitigation, no knockdown/launch, combo survives
                var soft = attack.ScaledBy(guardDamageMult * PlayerProgression.DamageTakenMult());
                soft.knocksDown = false;
                soft.launch = 0f;
                soft.knockback = new Vector2(attack.knockback.x * 0.4f, 0f);
                VFX.VfxManager.Play("hit_spark", transform.position + Vector3.up * 0.8f, -attackerFacing, 0.7f);
                base.ReceiveHit(soft, attackerFacing);
                return;
            }

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
