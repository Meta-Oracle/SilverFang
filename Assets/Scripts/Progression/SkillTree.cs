using System.Linq;

namespace SilverFang.Progression
{
    public enum SkillPath
    {
        Blade,
        Gun,
        Awakened
    }

    public class SkillNode
    {
        public string id;
        public string name;
        public string description;
        public SkillPath path;
        public int tier;  // 1..8; tier N unlocks after (N-1)*2 points spent in the path
        public int cost;
        public (ModifierType type, float value)[] effects;
    }

    /// Static skill tree definition: three paths, eight tiers each.
    /// Percent effects stack additively with stat bonuses.
    public static class SkillTree
    {
        public static int TierRequirement(int tier) => (tier - 1) * 2;

        public static readonly SkillNode[] Nodes =
        {
            // ── Path of the Blade ──
            N("blade_edge1", SkillPath.Blade, 1, 1, "Honed Edge", "+10% sword damage",
                (ModifierType.SwordDamage, 10f)),
            N("blade_flow", SkillPath.Blade, 1, 1, "Flowing Strikes", "+15% chain window",
                (ModifierType.ChainWindow, 15f)),
            N("blade_hide", SkillPath.Blade, 1, 1, "Iron Hide", "-6% damage taken",
                (ModifierType.Defense, 6f)),
            N("blade_edge2", SkillPath.Blade, 2, 2, "Razor Edge", "+15% sword damage",
                (ModifierType.SwordDamage, 15f)),
            N("blade_feast", SkillPath.Blade, 2, 2, "Predator Instinct", "Heal 2 HP per kill",
                (ModifierType.HealOnKill, 2f)),
            N("blade_trance", SkillPath.Blade, 3, 2, "Battle Trance", "+8% move speed",
                (ModifierType.MoveSpeed, 8f)),
            N("blade_edge3", SkillPath.Blade, 3, 2, "Master Swordsman", "+20% sword damage",
                (ModifierType.SwordDamage, 20f)),
            N("blade_cap", SkillPath.Blade, 4, 3, "Fang of the Silver Wolf", "+25% sword, +10% awakened damage",
                (ModifierType.SwordDamage, 25f), (ModifierType.AwakenedDamage, 10f)),
            N("blade_hunger", SkillPath.Blade, 4, 2, "Wolf's Hunger", "Heal 4 HP per kill",
                (ModifierType.HealOnKill, 4f)),
            N("blade_edge4", SkillPath.Blade, 5, 3, "Moonlit Edge", "+30% sword damage",
                (ModifierType.SwordDamage, 30f)),
            N("blade_sprint", SkillPath.Blade, 5, 2, "Hunter's Stride", "+12% move speed",
                (ModifierType.MoveSpeed, 12f)),
            N("blade_bulwark", SkillPath.Blade, 6, 3, "Lupine Bulwark", "-12% damage taken",
                (ModifierType.Defense, 12f)),
            N("blade_tempo", SkillPath.Blade, 6, 2, "Perfect Tempo", "+25% chain window",
                (ModifierType.ChainWindow, 25f)),
            N("blade_lethal", SkillPath.Blade, 6, 2, "Lethal Precision", "+25% crit damage",
                (ModifierType.CritDamage, 25f)),
            N("blade_edge5", SkillPath.Blade, 7, 3, "Severance", "+35% sword damage",
                (ModifierType.SwordDamage, 35f)),
            N("blade_feast2", SkillPath.Blade, 7, 3, "Bloodfeast", "Heal 6 HP per kill",
                (ModifierType.HealOnKill, 6f)),
            N("blade_zenith", SkillPath.Blade, 8, 4, "Zenith of the Hunt", "+40% sword, +10% speed, -10% damage taken",
                (ModifierType.SwordDamage, 40f), (ModifierType.MoveSpeed, 10f), (ModifierType.Defense, 10f)),

            // ── Path of the Gun ──
            N("gun_aim1", SkillPath.Gun, 1, 1, "Steady Aim", "+10% gun damage",
                (ModifierType.GunDamage, 10f)),
            N("gun_loads", SkillPath.Gun, 1, 1, "Potent Loads", "+15% status duration",
                (ModifierType.StatusDuration, 15f)),
            N("gun_bounty", SkillPath.Gun, 1, 1, "Bounty Hunter", "+15% Scematica gain",
                (ModifierType.ScematicaGain, 15f)),
            N("gun_aim2", SkillPath.Gun, 2, 2, "Deadeye", "+15% gun damage",
                (ModifierType.GunDamage, 15f)),
            N("gun_toxins", SkillPath.Gun, 2, 2, "Lingering Toxins", "+25% status duration",
                (ModifierType.StatusDuration, 25f)),
            N("gun_aim3", SkillPath.Gun, 3, 2, "Gun Saint", "+20% gun damage",
                (ModifierType.GunDamage, 20f)),
            N("gun_vet", SkillPath.Gun, 3, 2, "Veteran Instincts", "+15% XP gain",
                (ModifierType.XpGain, 15f)),
            N("gun_cap", SkillPath.Gun, 4, 3, "One True Shot", "+25% gun damage, +15% status duration",
                (ModifierType.GunDamage, 25f), (ModifierType.StatusDuration, 15f)),
            N("gun_fence", SkillPath.Gun, 4, 2, "Fence Network", "+25% Scematica gain",
                (ModifierType.ScematicaGain, 25f)),
            N("gun_aim4", SkillPath.Gun, 5, 3, "Executioner Rounds", "+30% gun damage",
                (ModifierType.GunDamage, 30f)),
            N("gun_scholar", SkillPath.Gun, 5, 2, "Field Scholar", "+25% XP gain",
                (ModifierType.XpGain, 25f)),
            N("gun_killer", SkillPath.Gun, 5, 2, "Killer Instinct", "+6% crit rate",
                (ModifierType.CritRate, 6f)),
            N("gun_plague", SkillPath.Gun, 6, 3, "Plaguebearer", "+35% status duration",
                (ModifierType.StatusDuration, 35f)),
            N("gun_magnate", SkillPath.Gun, 6, 2, "Scrip Magnate", "+35% Scematica gain",
                (ModifierType.ScematicaGain, 35f)),
            N("gun_aim5", SkillPath.Gun, 7, 3, "Deathmark", "+35% gun damage",
                (ModifierType.GunDamage, 35f)),
            N("gun_sage", SkillPath.Gun, 7, 3, "War Sage", "+35% XP gain",
                (ModifierType.XpGain, 35f)),
            N("gun_apex", SkillPath.Gun, 8, 4, "Apex Marksman", "+40% gun, +25% status, +25% Scematica",
                (ModifierType.GunDamage, 40f), (ModifierType.StatusDuration, 25f), (ModifierType.ScematicaGain, 25f)),

            // ── Path of the Awakened ──
            N("awk_conduit", SkillPath.Awakened, 1, 1, "Spirit Conduit", "+15% awakened meter gain",
                (ModifierType.AwakenedGain, 15f)),
            N("awk_step", SkillPath.Awakened, 1, 1, "Void Step", "+15% teleport distance",
                (ModifierType.TeleportDistance, 15f)),
            N("awk_sustain", SkillPath.Awakened, 1, 1, "Sustained Form", "+10% awakened duration",
                (ModifierType.AwakenedDuration, 10f)),
            N("awk_edge", SkillPath.Awakened, 2, 2, "Void Edge", "+10% awakened damage",
                (ModifierType.AwakenedDamage, 10f)),
            N("awk_phase", SkillPath.Awakened, 2, 2, "Phase Walker", "+20% teleport distance",
                (ModifierType.TeleportDistance, 20f)),
            N("awk_eternal", SkillPath.Awakened, 3, 2, "Eternal Form", "+20% awakened duration",
                (ModifierType.AwakenedDuration, 20f)),
            N("awk_overflow", SkillPath.Awakened, 3, 2, "Overflowing Spirit", "+20% awakened meter gain",
                (ModifierType.AwakenedGain, 20f)),
            N("awk_cap", SkillPath.Awakened, 4, 3, "Silver Apotheosis", "+20% awakened dmg, +15% duration, +15% teleport",
                (ModifierType.AwakenedDamage, 20f), (ModifierType.AwakenedDuration, 15f), (ModifierType.TeleportDistance, 15f)),
            N("awk_well", SkillPath.Awakened, 4, 2, "Spirit Well", "+25% awakened meter gain",
                (ModifierType.AwakenedGain, 25f)),
            N("awk_edge2", SkillPath.Awakened, 5, 3, "Void Razor", "+25% awakened damage",
                (ModifierType.AwakenedDamage, 25f)),
            N("awk_blink", SkillPath.Awakened, 5, 2, "Far Blink", "+30% teleport distance",
                (ModifierType.TeleportDistance, 30f)),
            N("awk_endless", SkillPath.Awakened, 6, 3, "Endless Form", "+30% awakened duration",
                (ModifierType.AwakenedDuration, 30f)),
            N("awk_flood", SkillPath.Awakened, 6, 2, "Spirit Flood", "+30% awakened meter gain",
                (ModifierType.AwakenedGain, 30f)),
            N("awk_edge3", SkillPath.Awakened, 7, 3, "Dimension Render", "+35% awakened damage",
                (ModifierType.AwakenedDamage, 35f)),
            N("awk_anchor", SkillPath.Awakened, 7, 3, "World Anchor", "+30% awakened duration",
                (ModifierType.AwakenedDuration, 30f)),
            N("awk_godhood", SkillPath.Awakened, 8, 4, "Voidborn Godhood", "+40% awakened dmg, +30% gain, +30% teleport",
                (ModifierType.AwakenedDamage, 40f), (ModifierType.AwakenedGain, 30f), (ModifierType.TeleportDistance, 30f))
        };

        public static SkillNode Find(string id) => Nodes.FirstOrDefault(n => n.id == id);

        private static SkillNode N(string id, SkillPath path, int tier, int cost, string name,
            string description, params (ModifierType, float)[] effects)
        {
            return new SkillNode
            {
                id = id, path = path, tier = tier, cost = cost,
                name = name, description = description, effects = effects
            };
        }
    }
}
