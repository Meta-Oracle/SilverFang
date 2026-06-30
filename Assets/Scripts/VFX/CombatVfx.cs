using SilverFang.Combat;
using UnityEngine;

namespace SilverFang.VFX
{
    /// Reactive Combat VFX Engine + Impact VFX System.
    ///
    /// Every physical impact routes through Resolve(), which picks the effect
    /// family from CONTEXT — attacker aura/style, damage element, hurt material,
    /// melee vs projectile, crit, and the live combo tier — and LAYERS it
    /// (Hilo psionic BEHIND the target; Silver blue aura + Lucas electric ON TOP).
    ///
    /// Variety: each category (blood / spark / psi / electric / blue / slash) is a
    /// POOL of sprite variants; every hit picks one at RANDOM so the same sprite
    /// never repeats back-to-back. Sizes are kept subtle-but-noticeable.
    public static class CombatVfx
    {
        private static int Level => ComboTracker.Active != null ? ComboTracker.Active.Level : 0;

        /// Combo-tier size multiplier (F..XXX -> ~1.0 .. ~1.9x), subtle ramp.
        public static float ComboScale => 1f + Level * 0.08f;

        // --- RNG variant pools (folders in Assets/Art/Sprites/VFX) ---
        private static readonly string[] Blood    = { "blood_0", "blood_1", "blood_2", "blood_3", "blood_4" };
        private static readonly string[] Spark    = { "spark_0", "spark_1", "spark_2", "spark_3", "spark_4" };
        private static readonly string[] Psi      = { "psi_0", "psi_1", "psi_2", "psi_3" };
        private static readonly string[] Electric = { "electric_0", "electric_1", "electric_2", "electric_3" };
        private static readonly string[] Blue     = { "blue_0", "blue_1", "blue_2", "blue_3" };
        private static readonly string[] SlashPool = { "slash_0", "slash_1", "slash_2", "slash_3", "slash_4", "slash_5", "slash_6" };

        private static string Pick(string[] pool) => pool[Random.Range(0, pool.Length)];

        private static readonly Color NuclearTint = new Color(0.55f, 1f, 0.45f);

        // Slash tint per attacker aura (slash pool sprites are neutral white).
        private static Color SlashTint(ImpactStyle s) => s switch
        {
            ImpactStyle.SilverBlue  => new Color(0.6f, 0.8f, 1f),
            ImpactStyle.HiloPsionic => new Color(0.75f, 0.45f, 1f),
            ImpactStyle.LucasGold   => new Color(1f, 0.85f, 0.4f),
            ImpactStyle.BigManRed   => new Color(1f, 0.45f, 0.35f),
            _                       => new Color(0.85f, 0.92f, 1f) // neutral steel-white
        };

        public struct ImpactInfo
        {
            public Vector3 pos;
            public float facing;
            public int damage;
            public bool crit;
            public bool isProjectile;
            public HitMaterial material;
            public DamageType element;
            public ImpactStyle style;
            public CharacterCombatant target; // for behind/on-top layering (may be null)
        }

        /// Full context-driven impact. The heart of the Impact VFX System.
        public static void Resolve(in ImpactInfo info)
        {
            int level = Level;
            float combo = 1f + level * 0.08f;
            float baseScale = Mathf.Clamp(0.5f + info.damage / 36f + (info.crit ? 0.4f : 0f), 0.5f, 1.7f);
            float scale = baseScale * combo;

            // Exact placement: prefer the target's centre so the impact lands ON
            // the body, not at a stray collider point.
            Vector3 at = info.target != null ? info.target.transform.position + Vector3.up * 0.9f : info.pos;
            int? behind = info.target != null ? info.target.SortingOrder - 2 : (int?)null;
            int? onTop = info.target != null ? info.target.SortingOrder + 5 : (int?)null;

            // 1) Primary family by attacker style (aura), then element, then plain.
            switch (info.style)
            {
                case ImpactStyle.HiloPsionic:
                    VfxManager.Play(Pick(Psi), at, info.facing, scale, behind);          // bloom BEHIND
                    break;
                case ImpactStyle.SilverBlue:
                    VfxManager.Play(Pick(Blue), at, info.facing, scale, onTop);           // aura ON TOP
                    break;
                case ImpactStyle.LucasGold:
                case ImpactStyle.BigManRed:
                    VfxManager.Play(Pick(Electric), at, info.facing, scale, onTop);
                    if (info.target != null)
                        VfxManager.PlayAttached("electric_lingering", info.target.transform,
                            0.5f, scale * 0.85f, 0.6f, onTop, null);
                    break;
                default:
                    PlayElemental(info, at, scale);
                    break;
            }

            // 2) Bloody Combat Engine: a random blood/spark splatter sized by the hit.
            MaterialSpray(info, at, scale, level);

            // 3) Top-tier flourish: a small restrained radial accent at the very top
            //    of the ladder (X+), random variants, kept subtle.
            if (level >= 9)
            {
                string[] pool = info.material == HitMaterial.Metal ? Spark : Blood;
                int spokes = level >= 11 ? 5 : 3;
                for (int s = 0; s < spokes; s++)
                {
                    float a = s / (float)spokes * Mathf.PI * 2f;
                    Vector3 o = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (0.3f + 0.03f * level) * scale;
                    VfxManager.Play(Pick(pool), at + o, Mathf.Cos(a) >= 0f ? info.facing : -info.facing, scale * 0.4f);
                }
            }
        }

        /// Elemental / ballistic primary for plain (Neutral-style) hits.
        private static void PlayElemental(in ImpactInfo info, Vector3 at, float scale)
        {
            switch (info.element)
            {
                case DamageType.Nuclear:    VfxManager.Play("impact_nuclear", at, info.facing, scale, null, NuclearTint); break;
                case DamageType.Fire:       VfxManager.Play("impact_incendiary", at, info.facing, scale, null); break;
                case DamageType.Ice:        VfxManager.Play("impact_ice", at, info.facing, scale, null); break;
                case DamageType.Psychic:    VfxManager.Play(Pick(Blue), at, info.facing, scale, null); break;
                default:
                    if (info.isProjectile) VfxManager.Play("impact_ballistic", at, info.facing, scale, null);
                    break;
            }
        }

        /// Bloody Combat Engine spray: a random splatter variant sized by the hit.
        private static void MaterialSpray(in ImpactInfo info, Vector3 at, float scale, int level)
        {
            string[] pool = info.material == HitMaterial.Metal ? Spark : Blood;
            VfxManager.Play(Pick(pool), at, info.facing, scale);

            // A droplet/spark or two extra on crits and big hits (not an explosion).
            int extra = (info.crit ? 1 : 0) + (info.damage >= 28 ? 1 : 0);
            for (int i = 0; i < extra; i++)
            {
                float ang = Random.value * Mathf.PI * 2f;
                Vector3 off = new Vector3(Mathf.Cos(ang), Mathf.Abs(Mathf.Sin(ang)) * 0.7f, 0f)
                              * Random.Range(0.2f, 0.45f) * scale;
                VfxManager.Play(Pick(pool), at + off, info.facing, scale * Random.Range(0.4f, 0.7f));
            }
        }

        /// Sword-swing slash flash: a RANDOM slash shape from the pool, tinted to
        /// the attacker's aura, placed EXACTLY at the contact point and kept small.
        public static void Slash(Vector3 contact, float facing, ImpactStyle style, float sizeMul = 1f)
            => VfxManager.Play(Pick(SlashPool), contact, facing, 0.7f * sizeMul * ComboScale, null, SlashTint(style));

        /// Back-compat simple entry (Neutral, kinetic, no target layering).
        public static void Impact(Vector3 pos, float facing, int damage, HitMaterial material, bool crit)
            => Resolve(new ImpactInfo
            {
                pos = pos, facing = facing, damage = damage, crit = crit,
                material = material, element = DamageType.Kinetic, style = ImpactStyle.Neutral
            });

        /// Combo-scaled hitstop: heavier freeze as the combo climbs.
        public static float HitStopFor(bool crit, bool heavy)
            => (crit ? 0.13f : heavy ? 0.11f : 0.05f) * (1f + Level * 0.05f);

        /// Combo-scaled camera shake amplitude.
        public static float ShakeFor(bool heavy)
            => (heavy ? 0.12f : 0.05f) * (1f + Level * 0.06f);

        /// Movement scuff/dust puff (dashes, sprint steps, bounces, landings).
        public static void Dust(Vector3 pos, float facing, float scale = 1f)
            => VfxManager.Play("dash_dust", pos, facing, scale);
    }
}
