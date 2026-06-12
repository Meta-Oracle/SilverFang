using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SilverFang.Progression
{
    /// Player level, XP, base stats, and skill tree state. Persisted via
    /// PlayerPrefs. Every stat has a baseline of BaseStat, grows automatically
    /// with level (+1 per 2 levels), and grows further with manually allocated
    /// stat points. Skill points buy tree nodes; all percent effects stack
    /// additively into GetMultiplier.
    public static class PlayerProgression
    {
        public const int MaxLevel = 99;
        public const int StatPointsPerLevel = 3;
        public const int SkillPointsPerLevel = 1;
        public const int BaseStat = 5;

        private static readonly int StatCount = Enum.GetValues(typeof(StatType)).Length;

        private static bool loaded;
        private static int level = 1;
        private static int xp;
        private static int statPoints;
        private static int skillPoints;
        private static int[] allocated;
        private static HashSet<string> owned;

        public static event Action OnChanged;
        public static event Action<int> OnLevelUp;
        public static event Action OnEnemyKilled;

        public static int Level { get { Load(); return level; } }
        public static int Xp { get { Load(); return xp; } }
        public static int UnspentStatPoints { get { Load(); return statPoints; } }
        public static int UnspentSkillPoints { get { Load(); return skillPoints; } }

        /// XP needed to go from `lvl` to `lvl + 1`.
        public static int XpToNext(int lvl) => 50 + 35 * lvl + 12 * lvl * lvl;

        /// Automatic bonus applied to every stat: +1 per 2 levels.
        public static int AutoStatBonus { get { Load(); return (level - 1) / 2; } }

        public static int AllocatedStat(StatType stat) { Load(); return allocated[(int)stat]; }
        public static int EffectiveStat(StatType stat) => BaseStat + AllocatedStat(stat) + AutoStatBonus;

        public static void AddXp(int amount)
        {
            Load();
            if (amount <= 0 || level >= MaxLevel) return;

            xp += Mathf.RoundToInt(amount * GetMultiplier(ModifierType.XpGain));
            bool leveled = false;
            while (level < MaxLevel && xp >= XpToNext(level))
            {
                xp -= XpToNext(level);
                level++;
                statPoints += StatPointsPerLevel;
                skillPoints += SkillPointsPerLevel;
                leveled = true;
            }
            if (level >= MaxLevel) xp = 0;

            Save();
            if (leveled) OnLevelUp?.Invoke(level);
            OnChanged?.Invoke();
        }

        public static void NotifyEnemyKilled() => OnEnemyKilled?.Invoke();

        public static bool AllocateStat(StatType stat)
        {
            Load();
            if (statPoints <= 0) return false;
            allocated[(int)stat]++;
            statPoints--;
            Save();
            OnChanged?.Invoke();
            return true;
        }

        public static bool OwnsNode(string id) { Load(); return owned.Contains(id); }

        public static int PointsSpentInPath(SkillPath path)
        {
            Load();
            return SkillTree.Nodes.Where(n => n.path == path && owned.Contains(n.id)).Sum(n => n.cost);
        }

        public static bool CanBuy(SkillNode node, out string reason)
        {
            Load();
            if (owned.Contains(node.id)) { reason = "owned"; return false; }
            int required = SkillTree.TierRequirement(node.tier);
            if (PointsSpentInPath(node.path) < required)
            {
                reason = $"needs {required}pts in path";
                return false;
            }
            if (skillPoints < node.cost) { reason = $"needs {node.cost}sp"; return false; }
            reason = "";
            return true;
        }

        public static bool BuyNode(string id)
        {
            var node = SkillTree.Find(id);
            if (node == null || !CanBuy(node, out _)) return false;
            owned.Add(id);
            skillPoints -= node.cost;
            Save();
            OnChanged?.Invoke();
            return true;
        }

        /// Refunds every spent stat and skill point.
        public static void RespecAll()
        {
            Load();
            statPoints += allocated.Sum();
            Array.Clear(allocated, 0, allocated.Length);
            skillPoints += SkillTree.Nodes.Where(n => owned.Contains(n.id)).Sum(n => n.cost);
            owned.Clear();
            Save();
            OnChanged?.Invoke();
        }

        /// Combined multiplier (1 = unchanged) for percent-based modifiers.
        public static float GetMultiplier(ModifierType type) => 1f + GetPercent(type) / 100f;

        /// Raw percent total (crit rate/damage, elemental resist readouts).
        public static float GetPercentOf(ModifierType type) => GetPercent(type);

        /// Flat bonuses: MaxHealth (HP) and HealOnKill (HP per kill).
        public static float GetFlat(ModifierType type)
        {
            Load();
            float flat = 0f;
            if (type == ModifierType.MaxHealth)
                flat += 6f * PointsAbove(StatType.Vitality);
            foreach (var node in OwnedNodes())
                foreach (var (effectType, value) in node.effects)
                    if (effectType == type && IsFlat(type)) flat += value;
            flat += ChipCore.GetFlat(type);
            return flat;
        }

        /// Fired by external progression systems (chip core) so listeners
        /// (max health, HUD) recompute.
        public static void NotifyExternal() => OnChanged?.Invoke();

        /// Damage-taken factor from Defense (1 = full damage, lower = tankier).
        public static float DamageTakenMult() =>
            Mathf.Clamp(1f - GetPercent(ModifierType.Defense) / 100f, 0.35f, 1f);

        private static float GetPercent(ModifierType type)
        {
            Load();
            float pct = type switch
            {
                ModifierType.SwordDamage => 4f * PointsAbove(StatType.Strength),
                ModifierType.GunDamage => 4f * PointsAbove(StatType.Marksmanship),
                ModifierType.MoveSpeed => 1.5f * PointsAbove(StatType.Agility),
                ModifierType.ChainWindow => 1f * PointsAbove(StatType.Agility),
                ModifierType.TeleportDistance => 3f * PointsAbove(StatType.Resonance),
                ModifierType.AwakenedDuration => 2f * PointsAbove(StatType.Resonance),
                ModifierType.AwakenedGain => 2f * PointsAbove(StatType.Resonance),
                ModifierType.AwakenedDamage => 1f * PointsAbove(StatType.Resonance),
                ModifierType.StatusDuration => 5f * PointsAbove(StatType.Alchemy),
                ModifierType.ScematicaGain => 1.5f * PointsAbove(StatType.Alchemy),
                ModifierType.XpGain => 1f * PointsAbove(StatType.Alchemy),
                ModifierType.Defense => 0.4f * PointsAbove(StatType.Vitality),
                // crit system: agility sharpens the eye, alchemy loads the luck
                ModifierType.CritRate => 5f + 1f * PointsAbove(StatType.Agility)
                                            + 0.5f * PointsAbove(StatType.Alchemy),
                ModifierType.CritDamage => 50f + 2f * PointsAbove(StatType.Strength),
                ModifierType.ElementalResist => 0.6f * PointsAbove(StatType.Vitality),
                _ => 0f
            };
            foreach (var node in OwnedNodes())
                foreach (var (effectType, value) in node.effects)
                    if (effectType == type && !IsFlat(type)) pct += value;
            pct += ChipCore.GetPercent(type);
            return pct;
        }

        private static bool IsFlat(ModifierType type) =>
            type == ModifierType.MaxHealth || type == ModifierType.HealOnKill;

        private static int PointsAbove(StatType stat) => EffectiveStat(stat) - BaseStat;

        private static IEnumerable<SkillNode> OwnedNodes() =>
            SkillTree.Nodes.Where(n => owned.Contains(n.id));

        // ── Persistence ──

        private const string KeyLevel = "sf_prog_level";
        private const string KeyXp = "sf_prog_xp";
        private const string KeyStatPts = "sf_prog_statpts";
        private const string KeySkillPts = "sf_prog_skillpts";
        private const string KeyAlloc = "sf_prog_alloc";
        private const string KeyNodes = "sf_prog_nodes";

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            allocated = new int[StatCount];
            owned = new HashSet<string>();

            level = Mathf.Clamp(PlayerPrefs.GetInt(KeyLevel, 1), 1, MaxLevel);
            xp = Mathf.Max(0, PlayerPrefs.GetInt(KeyXp, 0));
            statPoints = Mathf.Max(0, PlayerPrefs.GetInt(KeyStatPts, 0));
            skillPoints = Mathf.Max(0, PlayerPrefs.GetInt(KeySkillPts, 0));

            string alloc = PlayerPrefs.GetString(KeyAlloc, "");
            var parts = alloc.Split(',');
            for (int i = 0; i < StatCount && i < parts.Length; i++)
                if (int.TryParse(parts[i], out int v)) allocated[i] = Mathf.Max(0, v);

            foreach (var id in PlayerPrefs.GetString(KeyNodes, "").Split(';'))
                if (!string.IsNullOrEmpty(id) && SkillTree.Find(id) != null) owned.Add(id);
        }

        private static void Save()
        {
            PlayerPrefs.SetInt(KeyLevel, level);
            PlayerPrefs.SetInt(KeyXp, xp);
            PlayerPrefs.SetInt(KeyStatPts, statPoints);
            PlayerPrefs.SetInt(KeySkillPts, skillPoints);
            PlayerPrefs.SetString(KeyAlloc, string.Join(",", allocated));
            PlayerPrefs.SetString(KeyNodes, string.Join(";", owned));
        }
    }
}
