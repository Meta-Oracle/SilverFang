using System;
using System.Linq;
using System.Text;
using SilverFang.Core;
using SilverFang.Progression;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SilverFang.UI
{
    /// Character sheet / skill tree menu (C key, gamepad Select-Share).
    /// Pauses the game. Navigate with W/S or d-pad, spend points with
    /// Enter or Cross, R respecs everything.
    public class CharacterSheetUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private bool externalControl; // driven by CharacterHubUI
        [SerializeField] private Text infoColumn;
        [SerializeField] private Text bladeColumn;
        [SerializeField] private Text gunColumn;
        [SerializeField] private Text awakenedColumn;

        [Header("Attribute screen overlays")]
        [SerializeField] private Image portraitImage;
        [SerializeField] private Sprite silverPortrait;
        [SerializeField] private Sprite hiloPortrait;
        [SerializeField] private Text identityText;
        [SerializeField] private Text levelText;
        [SerializeField] private Text vitalsText;

        private static readonly StatType[] Stats = (StatType[])Enum.GetValues(typeof(StatType));
        private bool open;
        private int cursor;

        private int ItemCount => Stats.Length + SkillTree.Nodes.Length;

        private void Start()
        {
            if (panel != null) panel.SetActive(false);
        }

        /// Hub-driven visibility (no own pause ownership in external mode).
        public void SetVisible(bool show)
        {
            open = show;
            if (panel != null) panel.SetActive(show);
            if (show) { cursor = 0; Render(); }
        }

        private void Update()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;

            if (!externalControl)
            {
                bool togglePressed = (kb != null && kb.cKey.wasPressedThisFrame)
                                  || (pad != null && pad.selectButton.wasPressedThisFrame);
                if (togglePressed) Toggle();
            }
            if (!open) return;

            if (NavPressed(kb?.sKey, kb?.downArrowKey, pad?.dpad.down))
                cursor = (cursor + 1) % ItemCount;
            else if (NavPressed(kb?.wKey, kb?.upArrowKey, pad?.dpad.up))
                cursor = (cursor - 1 + ItemCount) % ItemCount;
            else if (NavPressed(kb?.enterKey, kb?.numpadEnterKey, pad?.buttonSouth))
                Spend();
            else if (kb != null && kb.rKey.wasPressedThisFrame)
                PlayerProgression.RespecAll();
            else
                return;

            Render();
        }

        private static bool NavPressed(params UnityEngine.InputSystem.Controls.ButtonControl[] buttons)
            => buttons.Any(b => b != null && b.wasPressedThisFrame);

        private void Toggle()
        {
            if (!open)
            {
                if (!GamePause.TryAcquire(this)) return;
                open = true;
                cursor = 0;
                Render();
            }
            else
            {
                open = false;
                GamePause.Release(this);
            }
            if (panel != null) panel.SetActive(open);
        }

        private void Spend()
        {
            if (cursor < Stats.Length) PlayerProgression.AllocateStat(Stats[cursor]);
            else PlayerProgression.BuyNode(SkillTree.Nodes[cursor - Stats.Length].id);
        }

        private void Render()
        {
            RenderIdentity();
            if (infoColumn != null) infoColumn.text = BuildInfo();
            RenderPath(bladeColumn, SkillPath.Blade, "PATH OF THE BLADE");
            RenderPath(gunColumn, SkillPath.Gun, "PATH OF THE GUN");
            RenderPath(awakenedColumn, SkillPath.Awakened, "PATH OF THE AWAKENED");
        }

        /// Identity / level / live-vitals overlays on the attribute-screen art.
        private void RenderIdentity()
        {
            bool hilo = CharacterRoster.Selected == PlayableCharacter.Hilo;

            if (portraitImage != null)
            {
                var sprite = hilo ? hiloPortrait : silverPortrait;
                portraitImage.sprite = sprite;
                portraitImage.enabled = sprite != null;
            }

            if (identityText != null)
                identityText.text = hilo
                    ? "NAME\n  HILO\n\nTITLE\n  Cyber-Geneticist of Thread C\n\nORIGIN\n  A timeline with no Scematica —\n  gutted by the Hemarex Virus\n\nROLE\n  Bionic Striker / seeker of the\n  pure-birth cure (built her own arm)"
                    : "NAME\n  SILVER\n\nTITLE\n  The Wolf Fang Protocol\n\nCLAN / FACTION\n  Scematica Gun-Hunters\n\nROLE\n  Vanguard Hunter (S-1L) — pure-birth";

            if (levelText != null)
            {
                int lvl = PlayerProgression.Level;
                levelText.text = $"LEVEL\n  {lvl}{(lvl >= PlayerProgression.MaxLevel ? " (MAX)" : "")}\n\n" +
                    $"XP\n  {PlayerProgression.Xp}/{PlayerProgression.XpToNext(lvl)}\n\n" +
                    $"COMBAT RANK\n  {Combat.ComboTracker.RankFor(PlayerProgression.Level * 2)}";
            }

            if (vitalsText != null)
            {
                var hero = FindAnyObjectByType<Player.PlayerController>();
                var health = hero != null ? hero.GetComponent<Combat.Health>() : null;
                var sb = new StringBuilder();
                sb.AppendLine("COMBAT STATS");
                sb.AppendLine($"  Attack (sword) x{PlayerProgression.GetMultiplier(ModifierType.SwordDamage):0.00}");
                sb.AppendLine($"  Attack (gun)   x{PlayerProgression.GetMultiplier(ModifierType.GunDamage):0.00}");
                sb.AppendLine($"  Awakened dmg   x{PlayerProgression.GetMultiplier(ModifierType.AwakenedDamage):0.00}");
                sb.AppendLine($"  Damage taken   x{PlayerProgression.DamageTakenMult():0.00}");
                sb.AppendLine($"  Crit rate      {PlayerProgression.GetPercentOf(ModifierType.CritRate):0.#}%");
                sb.AppendLine($"  Crit damage    +{PlayerProgression.GetPercentOf(ModifierType.CritDamage):0}%");
                sb.AppendLine($"  Elem resist    {PlayerProgression.GetPercentOf(ModifierType.ElementalResist):0.#}%");
                sb.AppendLine();
                sb.AppendLine("VITALITY");
                if (health != null) sb.AppendLine($"  Health  {health.Current}/{health.Max}");
                if (hero != null) sb.AppendLine($"  Energy  {hero.AwakenedMeter:0}/{hero.AwakenedMaxMeter:0}");
                sb.AppendLine();
                sb.AppendLine("MOBILITY");
                sb.AppendLine($"  Move speed x{PlayerProgression.GetMultiplier(ModifierType.MoveSpeed):0.00}");
                sb.AppendLine($"  Teleport   x{PlayerProgression.GetMultiplier(ModifierType.TeleportDistance):0.00}");
                vitalsText.text = sb.ToString();
            }
        }

        private string BuildInfo()
        {
            // core attributes zone: stat list + spend cursor (level and combat
            // multipliers render in their own art zones)
            var sb = new StringBuilder();
            sb.AppendLine($"PTS  STAT {PlayerProgression.UnspentStatPoints} / SKILL {PlayerProgression.UnspentSkillPoints}   (+{PlayerProgression.AutoStatBonus} auto)");
            for (int i = 0; i < Stats.Length; i++)
            {
                var stat = Stats[i];
                string marker = cursor == i ? "> " : "  ";
                sb.AppendLine($"{marker}{stat,-12} {PlayerProgression.EffectiveStat(stat),3}  {StatHint(stat)}");
            }
            sb.AppendLine();
            sb.AppendLine("W/S move  Enter spend  R respec  C close");
            return sb.ToString();
        }

        private static string StatHint(StatType stat) => stat switch
        {
            StatType.Vitality => "HP, defense",
            StatType.Strength => "sword dmg",
            StatType.Marksmanship => "gun dmg",
            StatType.Agility => "speed, combos",
            StatType.Resonance => "awakened, teleport",
            StatType.Alchemy => "status, SCEMA, XP",
            _ => ""
        };

        private void RenderPath(Text column, SkillPath path, string title)
        {
            if (column == null) return;
            var sb = new StringBuilder();
            sb.AppendLine($"== {title} ==");
            sb.AppendLine($"({PlayerProgression.PointsSpentInPath(path)} pts spent)");
            sb.AppendLine();

            int index = Stats.Length;
            foreach (var node in SkillTree.Nodes)
            {
                if (node.path != path) { index++; continue; }

                string marker = cursor == index ? "> " : "  ";
                string state;
                if (PlayerProgression.OwnsNode(node.id)) state = "[X]";
                else if (PlayerProgression.CanBuy(node, out _)) state = $"[{node.cost}]";
                else
                {
                    PlayerProgression.CanBuy(node, out string reason);
                    state = $"[-] ({reason})";
                }

                sb.AppendLine($"{marker}{state} {node.name}");
                sb.AppendLine($"      {node.description}");
                index++;
            }
            column.text = sb.ToString();
        }
    }
}
