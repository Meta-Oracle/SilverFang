using UnityEngine;

namespace SilverFang.Combat
{
    /// Counts the player's uninterrupted hit chain and the combo LEVEL ladder
    /// (F -> E -> D -> C -> B -> A -> S -> SS -> SSS -> X -> XX -> XXX, per the
    /// combo-level-meters sheet). Higher tiers multiply SCEMA/XP payouts but
    /// shrink the combo window, so god-tier chains demand relentless pressure.
    /// Hits (melee or projectile) extend the chain; taking damage or letting
    /// the window lapse drops it. Sits on the player; UI subscribes.
    public class ComboTracker : MonoBehaviour
    {
        // tier ladder straight from the sheet legend
        public static readonly string[] TierNames =
            { "F", "E", "D", "C", "B", "A", "S", "SS", "SSS", "X", "XX", "XXX" };
        public static readonly string[] TierTitles =
        {
            "FRAUD", "EXCEPTIONAL", "DECIMATING", "CATASTROPHIC", "BRUTAL",
            "AWE-INSPIRING", "SUPREME", "SUPERIOR", "PEERLESS", "PRODIGIOUS",
            "UNSTOPPABLE", "TRANSCENDENT"
        };
        public static readonly int[] TierHits =
            { 0, 5, 10, 16, 23, 31, 40, 50, 62, 75, 90, 110 };
        public static readonly float[] TierPayout =
            { 1f, 1.1f, 1.25f, 1.5f, 1.75f, 2f, 2.5f, 3f, 4f, 5f, 6f, 7.5f };

        [SerializeField] private float comboWindow = 2.6f;
        [SerializeField] private float windowDecayPerTier = 0.92f; // each tier shortens the leash

        public int Count { get; private set; }
        public int Level { get; private set; }

        /// Seconds allowed between hits at the current tier.
        public float CurrentWindow => comboWindow * Mathf.Pow(windowDecayPerTier, Level);
        public float WindowLeft01 => Count > 0 ? timer / CurrentWindow : 0f;

        /// 0..1 progress from the current tier toward the next.
        public float LevelProgress01 =>
            Level >= TierHits.Length - 1
                ? 1f
                : Mathf.Clamp01((Count - TierHits[Level])
                    / (float)(TierHits[Level + 1] - TierHits[Level]));

        /// SCEMA / XP multiplier for kills landed at the current tier.
        public float PayoutMultiplier => TierPayout[Level];

        public event System.Action<int> OnHit;        // fired on every increment
        public event System.Action<int> OnLevelChanged;
        public event System.Action<int, string> OnDropped; // final count + rank

        /// The selected hero's tracker (projectiles and bounties read this).
        public static ComboTracker Active { get; private set; }

        private float timer;

        private void OnEnable() => Active = this;

        private void OnDisable()
        {
            if (Active == this) Active = null;
        }

        public static string RankFor(int hits)
        {
            int tier = 0;
            for (int i = 0; i < TierHits.Length; i++)
                if (hits >= TierHits[i]) tier = i;
            return TierNames[tier];
        }

        public string Rank => RankFor(Count);

        public void RegisterHit()
        {
            Count++;
            while (Level + 1 < TierHits.Length && Count >= TierHits[Level + 1])
            {
                Level++;
                OnLevelChanged?.Invoke(Level);
            }
            timer = CurrentWindow;
            OnHit?.Invoke(Count);
        }

        /// Drop the chain (player got hit). Counts under 2 vanish silently.
        public void Break()
        {
            if (Count == 0) return;
            int final = Count;
            Count = 0;
            timer = 0f;
            if (Level != 0)
            {
                Level = 0;
                OnLevelChanged?.Invoke(0);
            }
            OnDropped?.Invoke(final, RankFor(final));
        }

        private void Update()
        {
            if (Count == 0) return;
            timer -= Time.deltaTime;
            if (timer <= 0f) Break();
        }
    }
}
