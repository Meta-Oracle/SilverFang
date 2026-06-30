using SilverFang.Combat;
using UnityEngine;

namespace SilverFang.VFX
{
    /// Attributes a debris burst carries into the world. Drives how a debris
    /// FIELD behaves as an active combat object, not just decoration.
    public struct DebrisSpec
    {
        public Team team;            // who owns the field (whose enemies it hits)
        public int count;            // fragments thrown
        public float strength;       // ballistics force — clashes vs projectiles
        public int damage;           // per-fragment contact damage (0 = cosmetic)
        public float stunLock;       // hitstun seconds applied on contact
        public float duration;       // fragment lifetime
        public bool deflects;        // can deflect / stop opposing projectiles

        /// Sensible default heavy-hit debris field.
        public static DebrisSpec Default(Team team, float scale) => new DebrisSpec
        {
            team = team,
            count = Mathf.Clamp(Mathf.RoundToInt(5f * scale) + 1, 3, 12),
            strength = 14f * Mathf.Max(0.6f, scale),
            damage = Mathf.RoundToInt(4f * scale),
            stunLock = 0.18f,
            duration = 0.9f,
            deflects = true
        };
    }

    /// Standalone flying-debris ENGINE. A heavy/charged impact throws a field of
    /// independent debris fragments — each a real combat object: it arcs/spins
    /// with physics, DAMAGES + stun-locks enemies it touches, and (per its
    /// strength) DEFLECTS or STOPS opposing projectiles via the Ballistics Engine.
    /// Fully decoupled from character animation.
    public class DebrisManager : MonoBehaviour
    {
        [SerializeField] private Sprite[] debrisSprites;

        private static DebrisManager instance;
        private void Awake() => instance = this;
        private void OnDestroy() { if (instance == this) instance = null; }

        public void SetSprites(Sprite[] sprites) => debrisSprites = sprites;

        /// Cosmetic-compatible overload (defaults to a player-owned heavy field).
        public static void Burst(Vector3 pos, float facing, float scale = 1f, int count = 0)
        {
            var spec = DebrisSpec.Default(Team.Player, scale);
            if (count > 0) spec.count = count;
            Burst(pos, facing, scale, spec);
        }

        /// Throw a debris field at a world point, biased along `facing`.
        public static void Burst(Vector3 pos, float facing, float scale, DebrisSpec spec)
        {
            if (instance == null || instance.debrisSprites == null || instance.debrisSprites.Length == 0) return;
            float f = Mathf.Sign(facing == 0f ? 1f : facing);
            for (int i = 0; i < spec.count; i++) instance.SpawnFragment(pos, f, scale, spec);
        }

        private void SpawnFragment(Vector3 pos, float facing, float scale, DebrisSpec spec)
        {
            var go = new GameObject("Debris");
            go.transform.position = pos + (Vector3)(Random.insideUnitCircle * 0.15f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = debrisSprites[Random.Range(0, debrisSprites.Length)];
            sr.sortingOrder = 5200;
            go.transform.localScale = Vector3.one * (Random.Range(0.35f, 0.7f) * scale);

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.35f;

            var vel = new Vector2(facing * Random.Range(1f, 5f) + Random.Range(-2f, 2f), Random.Range(3.5f, 8.5f));
            go.AddComponent<DebrisFragment>().Init(vel, Random.Range(-560f, 560f), spec);
        }
    }

    /// One active debris chunk: ballistic arc + spin + fade, AND a combat object —
    /// it hits enemies (damage + stun-lock) and deflects/stops opposing rounds
    /// while it has the strength to (Ballistics Engine), losing strength each clash.
    public class DebrisFragment : MonoBehaviour
    {
        private Vector2 vel;
        private float spin, life, maxLife;
        private SpriteRenderer sr;
        private Color baseColor;
        private DebrisSpec spec;
        private CircleCollider2D col;
        private readonly System.Collections.Generic.HashSet<Hurtbox> hit = new System.Collections.Generic.HashSet<Hurtbox>();
        private static readonly Collider2D[] Overlaps = new Collider2D[8];

        public void Init(Vector2 v, float spinDeg, DebrisSpec s)
        {
            vel = v; spin = spinDeg; spec = s;
            maxLife = Mathf.Max(0.3f, s.duration);
            col = GetComponent<CircleCollider2D>();
            sr = GetComponent<SpriteRenderer>();
            if (sr != null) baseColor = sr.color;
        }

        private void Update()
        {
            life += Time.deltaTime;
            vel.y -= 24f * Time.deltaTime;                              // gravity
            vel.x = Mathf.MoveTowards(vel.x, 0f, 6f * Time.deltaTime);  // air drag
            transform.position += (Vector3)(vel * Time.deltaTime);
            transform.Rotate(0f, 0f, spin * Time.deltaTime);
            if (sr != null) { var c = baseColor; c.a = Mathf.Clamp01(1.3f - life / maxLife * 1.3f); sr.color = c; }

            Collide();

            if (life >= maxLife || spec.strength <= 0f) Destroy(gameObject);
        }

        private void Collide()
        {
            if (col == null) return;
            int n = Physics2D.OverlapCircleNonAlloc(transform.position, col.radius, Overlaps);
            for (int i = 0; i < n; i++)
            {
                // Deflect / stop opposing projectiles by ballistics strength.
                if (spec.deflects)
                {
                    var proj = Overlaps[i].GetComponentInParent<Projectile>();
                    if (proj != null && proj.Team != spec.team)
                    {
                        var r = CombatBallistics.Resolve(spec.strength, proj.Strength, out float leftover);
                        VfxManager.Play("hit_spark", transform.position, 1f, 0.7f);
                        if (r != CombatBallistics.ClashResult.Lose) Destroy(proj.gameObject); // stop it
                        spec.strength = r == CombatBallistics.ClashResult.Win ? leftover : 0f;
                        continue;
                    }
                }

                // Damage + stun-lock enemies on contact (once each).
                if (spec.damage > 0)
                {
                    var hb = Overlaps[i].GetComponent<Hurtbox>();
                    if (hb != null && hb.Team != spec.team && hb.Owner != null && hit.Add(hb))
                    {
                        hb.Owner.ReceiveHit(new AttackData
                        {
                            damage = spec.damage,
                            hitstun = spec.stunLock,
                            knockback = new Vector2(Mathf.Sign(vel.x == 0 ? 1f : vel.x) * 1.5f, 0.5f)
                        }, Mathf.Sign(vel.x == 0 ? 1f : vel.x));
                        VfxManager.Play("hit_spark", transform.position, 1f, 0.6f);
                    }
                }
            }
        }
    }
}
