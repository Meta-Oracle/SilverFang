using System.Collections.Generic;
using UnityEngine;

namespace SilverFang.Combat
{
    /// Deals hits while active. Place on a child with a trigger Collider2D.
    /// Enable/disable from animation events via CharacterCombatant.
    /// The box is reshaped per attack from AttackData.rangeScale/heightScale,
    /// so wide swipes, long thrusts, and spins each cover their real arc.
    [RequireComponent(typeof(Collider2D))]
    public class Hitbox : MonoBehaviour
    {
        [SerializeField] private Team team;

        private Collider2D col;
        private BoxCollider2D box;
        private Vector2 baseSize;
        private Vector2 baseOffset;
        private Vector3 baseLocalPos;
        private AttackData currentAttack;
        private float facing = 1f;
        private float damageScale = 1f;
        private CharacterCombatant owner; // attacker, for impact style + element
        private readonly HashSet<Hurtbox> alreadyHit = new HashSet<Hurtbox>();

        public event System.Action<Hurtbox> OnHit;

        private void Awake()
        {
            owner = GetComponentInParent<CharacterCombatant>();
            col = GetComponent<Collider2D>();
            col.isTrigger = true;
            col.enabled = false;
            box = col as BoxCollider2D;
            if (box != null)
            {
                baseSize = box.size;
                baseOffset = box.offset;
            }
            baseLocalPos = transform.localPosition;
        }

        public void Activate(AttackData attack, float facingDirection, float scale = 1f)
        {
            currentAttack = attack;
            facing = facingDirection;
            damageScale = scale;
            alreadyHit.Clear();
            Shape(attack);
            col.enabled = true;
            // Slash trail rendered ACROSS the swing's active window — an energy/wind
            // trail on the blade, tinted to the attacker's aura, sized to the
            // hurtbox so the visual reads as the hurtbox's reach. Player swings only.
            if (team == Team.Player && owner != null)
            {
                float reach = Mathf.Max(col.bounds.size.x, col.bounds.size.y);
                VFX.CombatVfx.Slash(col.bounds.center, facing, owner.Style, 0.7f + reach * 0.35f);
            }
        }

        public void Deactivate()
        {
            col.enabled = false;
            currentAttack = null;
            if (box != null)
            {
                box.size = baseSize;
                box.offset = baseOffset;
            }
            transform.localPosition = baseLocalPos;
        }

        private void Shape(AttackData attack)
        {
            if (box == null || attack == null) return;

            float range = Mathf.Max(0.25f, attack.rangeScale);
            float height = Mathf.Max(0.25f, attack.heightScale);
            box.size = new Vector2(baseSize.x * range, baseSize.y * height);
            box.offset = baseOffset;

            if (attack.hitsBothSides)
            {
                // Cover the attacker on both sides: widen and center on the body.
                box.size = new Vector2(baseSize.x * range + baseLocalPos.x * 2f, box.size.y);
                transform.localPosition = new Vector3(0f, baseLocalPos.y, baseLocalPos.z);
            }
            else
            {
                // Grow forward from the body, not symmetrically around the pivot,
                // so longer range means longer reach instead of clipping backwards.
                float extraReach = (box.size.x - baseSize.x) * 0.5f;
                transform.localPosition = baseLocalPos + new Vector3(extraReach, 0f, 0f);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (currentAttack == null) return;

            var hurtbox = other.GetComponent<Hurtbox>();
            if (hurtbox == null || hurtbox.Team == team) return;
            if (!alreadyHit.Add(hurtbox)) return;

            var attack = currentAttack.ScaledBy(damageScale);

            // critical hits: agility/luck-driven roll. A crit lands for 3x the
            // current damage (base + leveled stats already folded in), with any
            // leveled crit-damage bonus stacked on top of that.
            bool crit = false;
            if (team == Team.Player && attack.damage > 0
                && Random.value * 100f < Progression.PlayerProgression.GetPercentOf(Progression.ModifierType.CritRate))
            {
                crit = true;
                attack = attack.ScaledBy(
                    3f + Progression.PlayerProgression.GetPercentOf(Progression.ModifierType.CritDamage) / 100f);
            }

            Vector3 contact = other.bounds.ClosestPoint(col.bounds.center);
            bool heavy = crit || attack.knocksDown || attack.launch > 0f;
            var style = owner != null ? owner.Style : ImpactStyle.Neutral;
            // (The swing's slash trail is spawned on hitbox Activate so it renders
            // across the whole active window; here we only resolve the impact.)
            if (attack.spawnsDebris)
            {
                // Standalone flying-debris burst (independent of any character clip).
                VFX.DebrisManager.Burst(contact, facing, (1f + attack.damage / 45f) * VFX.CombatVfx.ComboScale);
                VFX.VfxManager.Play("charge_debris", contact, facing, 0.9f + attack.damage / 60f); // impact flash
            }

            // Impact VFX System: style/element/material-aware, layered, combo-scaled.
            var mat = hurtbox.Owner != null ? hurtbox.Owner.Material : HitMaterial.Flesh;
            VFX.CombatVfx.Resolve(new VFX.CombatVfx.ImpactInfo
            {
                pos = contact, facing = facing, damage = attack.damage, crit = crit,
                isProjectile = false, material = mat, element = attack.damageType,
                style = style, target = hurtbox.Owner
            });
            HitStop.Do(VFX.CombatVfx.HitStopFor(crit, heavy));
            Core.CameraFollow.Instance?.Shake(VFX.CombatVfx.ShakeFor(heavy), heavy ? 0.14f : 0.1f);
            if (crit) Core.CameraFollow.Instance?.PunchIn(0.7f, 0.32f, contact);
            UI.DamageNumberUI.Spawn(contact, attack.damage,
                DamageColors.Resolve(attack.damageType, crit, team == Team.Player), heavy || crit);

            // electric styles tack on ~3 frames of hitstun
            if (style == ImpactStyle.LucasGold || style == ImpactStyle.BigManRed)
                attack = attack.WithBonusHitstun(CharacterCombatant.ElectricHitstunBonus);
            hurtbox.Owner?.ReceiveHit(attack, facing);
            OnHit?.Invoke(hurtbox);
        }
    }
}
