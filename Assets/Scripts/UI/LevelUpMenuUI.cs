using System.Linq;
using System.Text;
using SilverFang.Progression;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.UI;

namespace SilverFang.UI
{
    /// Mid-combat level-up menu (DualSense touchpad / T). Pauses the game and
    /// offers four tabs: STATS (allocate stat points) and the three skill
    /// paths (buy nodes up to tier 8). Q/E or LB/RB switch tabs, W/S or d-pad
    /// move the cursor, J/Enter/South spends, touchpad/T/Esc closes.
    public class LevelUpMenuUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private bool externalControl; // driven by CharacterHubUI
        [SerializeField] private Text titleText;
        [SerializeField] private Text tabsText;
        [SerializeField] private Text listText;
        [SerializeField] private Text detailText;
        [SerializeField] private Text hintText;

        private static readonly string[] TabNames = { "STATS", "BLADE", "GUN", "AWAKENED" };
        private int tab;
        private int cursor;

        private void Start()
        {
            if (panel != null) panel.SetActive(false);
            if (hintText != null)
                hintText.text = "Q/E TABS   W/S SELECT   J SPEND   T/TOUCHPAD CLOSE";
        }

        private bool open;

        /// Hub-driven visibility (no own pause ownership in external mode).
        public void SetVisible(bool show)
        {
            open = show;
            if (panel != null) panel.SetActive(show);
            if (show) { tab = 0; cursor = 0; Refresh(); }
        }

        private void Update()
        {
            if (!externalControl && TogglePressed())
            {
                if (!open && Core.GamePause.TryAcquire(this)) open = true;
                else if (open)
                {
                    open = false;
                    Core.GamePause.Release(this);
                }
                if (panel != null) panel.SetActive(open);
                if (open) Refresh();
                return;
            }
            if (!open) return;

            var kb = Keyboard.current;
            var pad = Gamepad.current;
            // inside the hub: A/D + d-pad left/right switch this screen's own
            // tabs (L1/R1 belong to the hub)
            int tabDir = externalControl
                ? (Down(kb?.dKey) || Down(pad?.dpad.right) ? 1 : 0)
                  - (Down(kb?.aKey) || Down(pad?.dpad.left) ? 1 : 0)
                : (Down(kb?.eKey) || Down(pad?.rightShoulder) ? 1 : 0)
                  - (Down(kb?.qKey) || Down(pad?.leftShoulder) ? 1 : 0);
            int curDir = (Down(kb?.sKey) || Down(kb?.downArrowKey) || Down(pad?.dpad.down) ? 1 : 0)
                       - (Down(kb?.wKey) || Down(kb?.upArrowKey) || Down(pad?.dpad.up) ? 1 : 0);
            if (tabDir != 0)
            {
                tab = (tab + tabDir + TabNames.Length) % TabNames.Length;
                cursor = 0;
                Refresh();
            }
            else if (curDir != 0)
            {
                cursor = Mathf.Clamp(cursor + curDir, 0, RowCount() - 1);
                Refresh();
            }
            else if (Down(kb?.jKey) || Down(kb?.enterKey) || Down(pad?.buttonSouth))
            {
                Spend();
                Refresh();
            }
            else if (!externalControl && Down(kb?.escapeKey))
            {
                open = false;
                Core.GamePause.Release(this);
                if (panel != null) panel.SetActive(false);
            }
        }

        private static bool Down(UnityEngine.InputSystem.Controls.ButtonControl b) =>
            b != null && b.wasPressedThisFrame;

        private bool TogglePressed()
        {
            if (Down(Keyboard.current?.tKey)) return true;
            var ds = Gamepad.current as DualShockGamepad;
            return ds != null && Down(ds.touchpadButton);
        }

        private SkillPath TabPath => (SkillPath)(tab - 1);

        private SkillNode[] TabNodes() =>
            SkillTree.Nodes.Where(n => n.path == TabPath)
                .OrderBy(n => n.tier).ThenBy(n => n.id).ToArray();

        private int RowCount() =>
            tab == 0 ? System.Enum.GetValues(typeof(StatType)).Length : TabNodes().Length;

        private void Spend()
        {
            if (tab == 0) PlayerProgression.AllocateStat((StatType)cursor);
            else
            {
                var nodes = TabNodes();
                if (cursor < nodes.Length) PlayerProgression.BuyNode(nodes[cursor].id);
            }
        }

        private void Refresh()
        {
            if (titleText != null)
                titleText.text = $"== LEVEL {PlayerProgression.Level} ==   " +
                    $"XP {PlayerProgression.Xp}/{PlayerProgression.XpToNext(PlayerProgression.Level)}   " +
                    $"STAT PTS {PlayerProgression.UnspentStatPoints}   SKILL PTS {PlayerProgression.UnspentSkillPoints}";

            if (tabsText != null)
                tabsText.text = string.Join("   ",
                    TabNames.Select((n, i) => i == tab ? $"[ {n} ]" : n));

            var list = new StringBuilder();
            var detail = new StringBuilder();

            if (tab == 0)
            {
                var stats = (StatType[])System.Enum.GetValues(typeof(StatType));
                for (int i = 0; i < stats.Length; i++)
                {
                    list.AppendLine($"{(i == cursor ? "> " : "  ")}{stats[i],-12} " +
                        $"{PlayerProgression.EffectiveStat(stats[i]),3}  (+{PlayerProgression.AllocatedStat(stats[i])})");
                }
                detail.AppendLine("Spend stat points to raise a stat.");
                detail.AppendLine($"\nAuto bonus from level: +{PlayerProgression.AutoStatBonus} to all stats.");
            }
            else
            {
                var nodes = TabNodes();
                int spent = PlayerProgression.PointsSpentInPath(TabPath);
                detail.AppendLine($"Points in path: {spent}");
                for (int i = 0; i < nodes.Length; i++)
                {
                    var n = nodes[i];
                    bool ownedNode = PlayerProgression.OwnsNode(n.id);
                    string mark = ownedNode ? "*" : SkillTree.TierRequirement(n.tier) > spent ? "x" : " ";
                    list.AppendLine($"{(i == cursor ? "> " : "  ")}[{mark}] T{n.tier} {n.name} ({n.cost}sp)");
                    if (i == cursor)
                    {
                        detail.AppendLine($"\n{n.name}\n{n.description}");
                        if (ownedNode) detail.AppendLine("OWNED");
                        else if (PlayerProgression.CanBuy(n, out string reason)) detail.AppendLine("Press J to learn.");
                        else detail.AppendLine(reason);
                    }
                }
            }

            if (listText != null) listText.text = list.ToString();
            if (detailText != null) detailText.text = detail.ToString();
        }
    }
}
