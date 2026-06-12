using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SilverFang.Progression
{
    public class ChipNode
    {
        public string id;
        public int ring;       // 0 (outer, INITIATE) .. 7 (inner, TRANSCENDENT); 8 = the core
        public int index;      // position within the ring
        public ModifierType type;
        public bool flat;
        public float value;
        public string label;
    }

    /// The Cybernetic Development Chip (level-tree screen): eight concentric
    /// protocol rings of boost nodes converging on the wolf core. One chip
    /// point per character level; deeper rings demand the tier's level band
    /// AND half the previous ring lit. Effects feed PlayerProgression's
    /// modifier pipeline, so every node is a concrete combat change.
    public static class ChipCore
    {
        public static readonly int[] RingCounts = { 14, 12, 11, 10, 9, 8, 6, 2 };
        public static readonly int[] RingMinLevel = { 1, 10, 20, 30, 40, 50, 60, 80 };
        public static readonly string[] RingNames =
        {
            "INITIATE", "ENHANCED", "ADVANCED", "SUPERIOR",
            "ELITE", "MASTER", "ASCENDED", "TRANSCENDENT"
        };

        // effect wheel: ring depth scales magnitude (x1 outer .. x8 inner)
        private static readonly (ModifierType t, bool flat, float baseVal, string label)[] Wheel =
        {
            (ModifierType.SwordDamage, false, 2f, "Blade Output"),
            (ModifierType.GunDamage, false, 2f, "Ballistic Output"),
            (ModifierType.Defense, false, 1.2f, "Armor Lattice"),
            (ModifierType.MoveSpeed, false, 1f, "Servo Drive"),
            (ModifierType.MaxHealth, true, 8f, "Vital Cells"),
            (ModifierType.AwakenedGain, false, 2f, "Spirit Intake"),
            (ModifierType.ChainWindow, false, 2f, "Combat Clock"),
            (ModifierType.XpGain, false, 2f, "Neural Learner"),
            (ModifierType.StatusDuration, false, 2.5f, "Toxin Mixer"),
            (ModifierType.ScematicaGain, false, 2.5f, "Scrip Siphon"),
            (ModifierType.AwakenedDamage, false, 1.5f, "Void Amp"),
            (ModifierType.TeleportDistance, false, 2f, "Blink Coil"),
            (ModifierType.HealOnKill, true, 1f, "Reaper Cells"),
            (ModifierType.AwakenedDuration, false, 2f, "Form Stabilizer"),
            (ModifierType.CritRate, false, 0.8f, "Targeting Coprocessor"),
            (ModifierType.CritDamage, false, 4f, "Overkill Driver"),
            (ModifierType.ElementalResist, false, 2f, "Hazmat Weave"),
        };

        public static readonly ChipNode[] Nodes = Build();
        public static ChipNode Core => Nodes[Nodes.Length - 1];

        public static event System.Action OnChanged;

        private static HashSet<string> owned;

        private static ChipNode[] Build()
        {
            var list = new List<ChipNode>();
            for (int ring = 0; ring < RingCounts.Length; ring++)
                for (int i = 0; i < RingCounts[ring]; i++)
                {
                    var (t, flat, baseVal, label) = Wheel[(ring * 5 + i) % Wheel.Length];
                    list.Add(new ChipNode
                    {
                        id = $"chip_r{ring}_{i}",
                        ring = ring,
                        index = i,
                        type = t,
                        flat = flat,
                        value = baseVal * (ring + 1),
                        label = $"{label} {ToRoman(ring + 1)}",
                    });
                }

            // the wolf core: capstone perk
            list.Add(new ChipNode
            {
                id = "chip_core",
                ring = 8,
                index = 0,
                type = ModifierType.AwakenedDamage,
                flat = false,
                value = 15f,
                label = "WOLF CORE",
            });
            return list.ToArray();
        }

        private static string ToRoman(int n) =>
            new[] { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII" }[Mathf.Clamp(n, 0, 8)];

        // ── points ──

        public static int PointsEarned => PlayerProgression.Level;
        public static int PointsSpent { get { Load(); return owned.Count; } }
        public static int PointsRemaining => Mathf.Max(0, PointsEarned - PointsSpent);

        public static bool Owns(string id) { Load(); return owned.Contains(id); }

        public static int OwnedInRing(int ring)
        {
            Load();
            return Nodes.Count(n => n.ring == ring && owned.Contains(n.id));
        }

        public static bool CanBuy(ChipNode node, out string reason)
        {
            Load();
            reason = "";
            if (owned.Contains(node.id)) { reason = "Already installed."; return false; }
            if (PointsRemaining < 1) { reason = "No chip points (1 per level)."; return false; }

            if (node.ring == 8)
            {
                if (PlayerProgression.Level < 90) { reason = "Requires level 90."; return false; }
                if (OwnedInRing(7) < RingCounts[7]) { reason = "Light the full TRANSCENDENT ring."; return false; }
                return true;
            }

            if (PlayerProgression.Level < RingMinLevel[node.ring])
            {
                reason = $"Requires level {RingMinLevel[node.ring]} ({RingNames[node.ring]}).";
                return false;
            }
            if (node.ring > 0 && OwnedInRing(node.ring - 1) < (RingCounts[node.ring - 1] + 1) / 2)
            {
                reason = $"Light half the {RingNames[node.ring - 1]} ring first.";
                return false;
            }
            return true;
        }

        public static bool Buy(string id)
        {
            Load();
            var node = Nodes.FirstOrDefault(n => n.id == id);
            if (node == null || !CanBuy(node, out _)) return false;
            owned.Add(id);
            Save();
            OnChanged?.Invoke();
            PlayerProgression.NotifyExternal();
            return true;
        }

        public static void ResetChip()
        {
            Load();
            owned.Clear();
            Save();
            OnChanged?.Invoke();
            PlayerProgression.NotifyExternal();
        }

        // ── modifier contributions (summed into PlayerProgression) ──

        public static float GetPercent(ModifierType type)
        {
            Load();
            float pct = 0f;
            foreach (var n in Nodes)
                if (!n.flat && n.type == type && owned.Contains(n.id)) pct += n.value;
            // core perk: also boosts both weapon outputs
            if (owned.Contains("chip_core")
                && (type == ModifierType.SwordDamage || type == ModifierType.GunDamage)) pct += 10f;
            return pct;
        }

        public static float GetFlat(ModifierType type)
        {
            Load();
            float flat = 0f;
            foreach (var n in Nodes)
                if (n.flat && n.type == type && owned.Contains(n.id)) flat += n.value;
            if (owned.Contains("chip_core") && type == ModifierType.MaxHealth) flat += 50f;
            return flat;
        }

        // ── persistence ──

        private const string KeyOwned = "sf_chip_nodes";
        private static bool loaded;

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            owned = new HashSet<string>(
                PlayerPrefs.GetString(KeyOwned, "").Split(';').Where(s => s.Length > 0));
        }

        private static void Save()
        {
            PlayerPrefs.SetString(KeyOwned, string.Join(";", owned));
            PlayerPrefs.Save();
        }
    }
}
