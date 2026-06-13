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
        private const float SpeedRealism = 3.2f;
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
        private Collider2D col;
        private SpriteRenderer sr;
        private readonly HashSet<Hurtbox> alreadyHit = new HashSet<Hurtbox>();
        private static readonly Collider2D[] Overlaps = new Collider2D[8];

        private void Awake()
        {
            col = GetComponent<Collider2D>();
            sr = GetComponentInChildren<SpriteRenderer>();
        }

        public void Fire(Team ownerTeam, AmmoDefinition ammoDef, float facing, float scale,
            float sizeScale = 1f, float speedMult = 1f, bool piercingOverride = false,
            Vector2? aimDirection = null)
        {
            team = ownerTeam;
            ammo = ammoDef;
            damageScale = scale;
            speedScale = speedMult;
            forcePiercing = piercingOverride;
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
                var hurtbox = Overlaps[i].GetComponent<Hurtbox>();
                if (hurtbox == null || hurtbox.Team == team) continue;
                if (!alreadyHit.Add(hurtbox)) continue;

                // deflection: a guarding/parrying/dashing defender bats the
                // round back at its sender instead of taking the hit.
                if (hurtbox.Owner != null && hurtbox.Owner.DeflectsProjectiles)
                {
                    Deflect(hurtbox.Team);
                    return false;
                }

                if (Hit(hurtbox)) return true;
            }
            return false;
        }

        private bool Hit(Hurtbox hurtbox)
        {
            var attack = ammo.attack.ScaledBy(damageScale);
            if (team == Team.Player && attack.damage > 0
                && Random.value * 100f < Progression.PlayerProgression.GetPercentOf(Progression.ModifierType.CritRate))
                attack = attack.ScaledBy(
                    1f + Progression.PlayerProgression.GetPercentOf(Progression.ModifierType.CritDamage) / 100f);
            VFX.VfxManager.Play("bullet_impact", transform.position, direction, 1f + attack.damage / 50f);
            HitStop.Do(0.03f);
            Core.CameraFollow.Instance?.Shake(0.04f, 0.07f);
            UI.DamageNumberUI.Spawn(transform.position, attack.damage,
                team == Team.Player ? new Color(1f, 0.95f, 0.8f) : new Color(1f, 0.35f, 0.3f));
            hurtbox.Owner?.ReceiveHit(attack, direction);
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
