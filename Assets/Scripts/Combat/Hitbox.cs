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
        private readonly HashSet<Hurtbox> alreadyHit = new HashSet<Hurtbox>();

        public event System.Action<Hurtbox> OnHit;

        private void Awake()
        {
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

            // critical hits: agility/luck-driven roll, amped by crit damage
            bool crit = false;
            if (team == Team.Player && attack.damage > 0
                && Random.value * 100f < Progression.PlayerProgression.GetPercentOf(Progression.ModifierType.CritRate))
            {
                crit = true;
                attack = attack.ScaledBy(
                    1f + Progression.PlayerProgression.GetPercentOf(Progression.ModifierType.CritDamage) / 100f);
            }

            Vector3 contact = other.bounds.ClosestPoint(col.bounds.center);
            bool heavy = crit || attack.knocksDown || attack.launch > 0f;
            VFX.VfxManager.Play(team == Team.Player ? "slash_effect" : "hit_spark", contact, facing,
                (crit ? 1.4f : 1f) + attack.damage / 45f);
            HitStop.Do(crit ? 0.13f : heavy ? 0.11f : 0.05f);
            Core.CameraFollow.Instance?.Shake(heavy ? 0.12f : 0.05f, heavy ? 0.14f : 0.1f);
            if (crit) Core.CameraFollow.Instance?.PunchIn(0.7f, 0.32f, contact);
            UI.DamageNumberUI.Spawn(contact, attack.damage,
                crit ? new Color(1f, 0.78f, 0.25f)
                : team == Team.Player ? new Color(1f, 0.95f, 0.8f) : new Color(1f, 0.35f, 0.3f), heavy);

            hurtbox.Owner?.ReceiveHit(attack, facing);
            OnHit?.Invoke(hurtbox);
        }
    }
}
