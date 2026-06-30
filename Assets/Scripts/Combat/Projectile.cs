using System.Collections.Generic;
using UnityEngine;

namespace SilverFang.Combat
{
    [RequireComponent(typeof(Collider2D))]
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float lifetime = 2f;

        // Realistic-but-visible: bullets cross the screen fast. The global
        // multiplier turns the readable design speeds (~14) into tracer-fast
        // velocities (~45+) while staying on-screen for a couple frames.
        private const float SpeedRealism = 5.0f; // bullets/beams cross fast and true
        // Movement is swept in sub-steps no longer than this, so a bullet never
        // tunnels through a thin hurtbox between frames at high speed.
        private const float MaxStep = 0.22f;

        private Team team;
        private AmmoDefinition ammo;
        private float direction = 1f;          // horizontal sign (knockback/crit facing)
        private Vector2 velocityDir = Vector2.right; // world travel direction (supports diagonals)
        private float damageScale = 1f;
        private float speedScale = 1f;
        private bool forcePiercing;
        private float strength;                 // ballistics: clash force
        private ImpactStyle projStyle = ImpactStyle.Neutral; // shooter aura for impact VFX
        public Team Team => team;
        public float Strength => strength;
        private Collider2D col;
        private SpriteRenderer sr;
        private readonly HashSet<Hurtbox> alreadyHit = new HashSet<Hurtbox>();
        private readonly Dictionary<Hurtbox, float> beamNextHit = new Dictionary<Hurtbox, float>();
        private static readonly Collider2D[] Overlaps = new Collider2D[8];

        private void Awake()
        {
            col = GetComponent<Collider2D>();
            sr = GetComponentInChildren<SpriteRenderer>();
        }

        public void Fire(Team ownerTeam, AmmoDefinition ammoDef, float facing, float scale,
            float sizeScale = 1f, float speedMult = 1f, bool piercingOverride = false,
            Vector2? aimDirection = null, ImpactStyle style = ImpactStyle.Neutral)
        {
            team = ownerTeam;
            ammo = ammoDef;
            damageScale = scale;
            speedScale = speedMult;
            forcePiercing = piercingOverride;
            projStyle = style;
            strength = CombatBallistics.AutoStrength(ammoDef, scale);
            alreadyHit.Clear();
            if (!Mathf.Approximately(sizeScale, 1f))
                transform.localScale *= sizeScale;

            // angled fire (air diagonal-down / directional) vs plain horizontal
            bool angled = aimDirection.HasValue && aimDirection.Value.sqrMagnitude > 0.01f;
            velocityDir = angled ? aimDirection.Value.normalized : new Vector2(Mathf.Sign(facing), 0f);
            direction = velocityDir.x >= 0f ? 1f : -1f;

            if (col == null) col = GetComponent<Collider2D>();
            if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
            if (angled)
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocityDir.y, velocityDir.x) * Mathf.Rad2Deg);
            if (sr != null)
            {
                sr.color = ammoDef.tint;
                if (!angled) sr.flipX = facing < 0f;
                // motion streak: stretch the tracer along travel by its speed
                float streak = Mathf.Clamp(ammoDef.speed / 16f, 1f, 2.6f);
                var s = sr.transform.localScale;
                sr.transform.localScale = new Vector3(Mathf.Abs(s.x) * streak, s.y, s.z);
            }
            col.isTrigger = true;
            Destroy(gameObject, lifetime);
        }

        private float aliveTime;

        private void Update()
        {
            if (ammo == null) return;

            // phantom slash: bleed speed off and fade the sprite as it dissipates
            if (ammo.decelerates)
            {
                aliveTime += Time.deltaTime;
                speedScale = Mathf.MoveTowards(speedScale, 0.15f, 2.4f * Time.deltaTime);
                if (sr != null && aliveTime > lifetime * 0.4f)
                {
                    float fade = Mathf.InverseLerp(lifetime, lifetime * 0.4f, aliveTime);
                    var c = sr.color; c.a = fade; sr.color = c;
                }
            }

            float dist = ammo.speed * speedScale * SpeedRealism * Time.deltaTime;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(dist) / MaxStep));
            float stepDist = dist / steps;
            for (int i = 0; i < steps; i++)
            {
                transform.position += (Vector3)(velocityDir * stepDist); // world-space travel (diagonals)
                if (SweepHit()) return;
            }
        }

        /// Manual swept overlap so high-speed rounds always register.
        private bool SweepHit()
        {
            if (col == null) return false;
            var b = col.bounds;
            int n = Physics2D.OverlapBoxNonAlloc(b.center, b.size, 0f, Overlaps);
            for (int i = 0; i < n; i++)
            {
                // Ballistics clash: opposing rounds collide. The lower-id side
                // resolves it once for both (stronger punches through, equal
                // annihilate). Beams have high strength so they shove through fire.
                var otherProj = Overlaps[i].GetComponentInParent<Projectile>();
                if (otherProj != null && otherProj != this && otherProj.team != team
                    && GetInstanceID() < otherProj.GetInstanceID())
                {
                    if (ResolveClash(otherProj)) return true;
                    continue;
                }

                var hurtbox = Overlaps[i].GetComponent<Hurtbox>();
                if (hurtbox == null || hurtbox.Team == team) continue;

                // True-Aim deflection: a guarding/parrying/dashing/SWINGING
                // defender bats the round back — but only up to Heavy class. The
                // top two power classes (Beam, Apex) punch straight through a swing.
                if (hurtbox.Owner != null && hurtbox.Owner.DeflectsProjectiles
                    && CombatBallistics.SwordCanDeflect(CombatBallistics.ClassOf(strength)))
                {
                    Deflect(hurtbox.Team);
                    return false;
                }

                if (ammo.isBeam)
                {
                    // Beam pierces and RE-HITS each enemy on an interval, locking
                    // sticky targets in the beam path. Never destroys on contact.
                    if (beamNextHit.TryGetValue(hurtbox, out float next) && Time.time < next) continue;
                    beamNextHit[hurtbox] = Time.time + Mathf.Max(0.05f, ammo.beamTickInterval);
                    Hit(hurtbox);
                    continue;
                }

                if (!alreadyHit.Add(hurtbox)) continue;
                if (Hit(hurtbox)) return true;
            }
            return false;
        }

        /// Two opposing rounds meet. Returns true if THIS round is destroyed.
        private bool ResolveClash(Projectile other)
        {
            Vector3 mid = (transform.position + other.transform.position) * 0.5f;
            VFX.VfxManager.Play("hit_spark", mid, direction, 1.3f);
            HitStop.Do(0.03f);
            var result = CombatBallistics.Resolve(strength, other.strength, out float leftover);
            switch (result)
            {
                case CombatBallistics.ClashResult.Win:
                    strength = leftover;            // punches through, weakened
                    Destroy(other.gameObject);
                    return false;
                case CombatBallistics.ClashResult.Lose:
                    Destroy(gameObject);
                    return true;
                default:                            // mutual annihilation
                    Destroy(other.gameObject);
                    Destroy(gameObject);
                    return true;
            }
        }

        private bool Hit(Hurtbox hurtbox)
        {
            var attack = ammo.attack.ScaledBy(damageScale);
            // The round's attribute (nuclear green, awakened psychic blue, etc.)
            // colours the number. Kept local so the shared ammo def isn't mutated.
            DamageType dtype = ammo.ResolveDamageType();
            bool crit = false;
            if (team == Team.Player && attack.damage > 0
                && Random.value * 100f < Progression.PlayerProgression.GetPercentOf(Progression.ModifierType.CritRate))
            {
                crit = true;
                attack = attack.ScaledBy(
                    3f + Progression.PlayerProgression.GetPercentOf(Progression.ModifierType.CritDamage) / 100f);
            }
            VFX.VfxManager.Play("bullet_impact", transform.position, direction, 1f + attack.damage / 50f);
            HitStop.Do(crit ? 0.07f : 0.03f);
            Core.CameraFollow.Instance?.Shake(crit ? 0.07f : 0.04f, 0.07f);
            UI.DamageNumberUI.Spawn(transform.position, attack.damage,
                DamageColors.Resolve(dtype, crit, team == Team.Player), crit);
            // electric shooter styles add ~3 frames of hitstun (before the hit lands)
            if (projStyle == ImpactStyle.LucasGold || projStyle == ImpactStyle.BigManRed)
                attack = attack.WithBonusHitstun(CharacterCombatant.ElectricHitstunBonus);
            hurtbox.Owner?.ReceiveHit(attack, direction);
            // Impact VFX System on projectile hits: shooter style + ammo element,
            // material-aware and layered (Hilo beams bloom behind, etc.).
            if (hurtbox.Owner != null)
            {
                VFX.CombatVfx.Resolve(new VFX.CombatVfx.ImpactInfo
                {
                    pos = transform.position, facing = direction, damage = attack.damage, crit = crit,
                    isProjectile = true, material = hurtbox.Owner.Material, element = dtype,
                    style = projStyle, target = hurtbox.Owner
                });
                // sticky beam pins the caught enemy in the beam path each tick
                if (ammo.isBeam && ammo.sticky)
                    hurtbox.Owner.PinInPlace(ammo.beamTickInterval * 2.2f);
            }
            if (team == Team.Player) ComboTracker.Active?.RegisterHit();
            if (ammo.status != StatusType.None && hurtbox.Owner != null)
            {
                float duration = ammo.statusDuration;
                if (team == Team.Player)
                    duration *= Progression.PlayerProgression.GetMultiplier(Progression.ModifierType.StatusDuration);
                hurtbox.Owner.ApplyStatus(ammo.status, duration);
            }

            return !ammo.piercing && !forcePiercing;
        }

        /// Reflect the round back the way it came, now owned by the defender.
        private void Deflect(Team defenderTeam)
        {
            team = defenderTeam;
            velocityDir = -velocityDir;
            direction = velocityDir.x >= 0f ? 1f : -1f;
            speedScale *= 1.25f; // snappier on the return
            alreadyHit.Clear();
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocityDir.y, velocityDir.x) * Mathf.Rad2Deg);
            if (sr != null)
                sr.color = Color.Lerp(sr.color, new Color(0.7f, 0.95f, 1f), 0.5f);
            VFX.VfxManager.Play("hit_spark", transform.position, direction, 1.1f);
            HitStop.Do(0.04f);
        }
    }
}
