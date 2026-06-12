using UnityEngine;

namespace SilverFang.Combat
{
    [RequireComponent(typeof(Collider2D))]
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float lifetime = 2f;

        private Team team;
        private AmmoDefinition ammo;
        private float direction = 1f;
        private float damageScale = 1f;
        private float speedScale = 1f;
        private bool forcePiercing;

        public void Fire(Team ownerTeam, AmmoDefinition ammoDef, float facing, float scale,
            float sizeScale = 1f, float speedMult = 1f, bool piercingOverride = false)
        {
            team = ownerTeam;
            ammo = ammoDef;
            direction = facing;
            damageScale = scale;
            speedScale = speedMult;
            forcePiercing = piercingOverride;
            if (!Mathf.Approximately(sizeScale, 1f))
                transform.localScale *= sizeScale;

            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = ammoDef.tint;
                sr.flipX = facing < 0f;
            }
            GetComponent<Collider2D>().isTrigger = true;
            Destroy(gameObject, lifetime);
        }

        private void Update()
        {
            if (ammo == null) return;
            transform.Translate(Vector3.right * (direction * ammo.speed * speedScale * Time.deltaTime));
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (ammo == null) return;
            var hurtbox = other.GetComponent<Hurtbox>();
            if (hurtbox == null || hurtbox.Team == team) return;

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

            if (!ammo.piercing && !forcePiercing) Destroy(gameObject);
        }
    }
}
