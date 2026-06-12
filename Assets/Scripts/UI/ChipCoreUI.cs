using System.Text;
using SilverFang.Progression;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SilverFang.UI
{
    /// The Cybernetic Development Chip screen (G / L3). Renders interactive
    /// node widgets in eight rings over the level-tree artwork's circuit web,
    /// converging on the wolf core. A/D walk a ring, W/S step between rings,
    /// J installs the node, R resets the chip, G/Esc closes. All rails show
    /// live data; every node is a real modifier into the combat pipeline.
    public class ChipCoreUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private bool externalControl; // driven by CharacterHubUI
        [SerializeField] private RectTransform web;     // node container over the art
        [SerializeField] private Text pointsText;
        [SerializeField] private Text overviewText;
        [SerializeField] private Text previewText;
        [SerializeField] private Text summaryText;
        [SerializeField] private Text detailText;
        [SerializeField] private RectTransform levelMarker; // bottom SELECT LEVEL strip

        private static readonly Vector2 Center = new Vector2(0.475f, 0.52f);
        private const float OuterRadius = 0.295f;
        private const float RadiusStep = 0.0315f;

        private bool open;
        private int cursorRing;
        private int cursorIdx;
        private Image[] widgets;
        private RectTransform[] widgetRects;

        private void Start()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void BuildWidgets()
        {
            if (widgets != null || web == null) return;
            widgets = new Image[ChipCore.Nodes.Length];
            widgetRects = new RectTransform[ChipCore.Nodes.Length];
            for (int i = 0; i < ChipCore.Nodes.Length; i++)
            {
                var node = ChipCore.Nodes[i];
                var go = new GameObject(node.id);
                go.transform.SetParent(web, false);
                var img = go.AddComponent<Image>();
                var rect = img.rectTransform;
                Vector2 a = AnchorFor(node);
                rect.anchorMin = a;
                rect.anchorMax = a;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = node.ring == 8 ? new Vector2(26f, 26f) : new Vector2(11f, 11f);
                rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
                widgets[i] = img;
                widgetRects[i] = rect;
            }
        }

        private static Vector2 AnchorFor(ChipNode node)
        {
            if (node.ring == 8) return Center;
            float radius = OuterRadius - node.ring * RadiusStep;
            // stagger ring start angles so spokes interleave like the art
            float angle = (node.index + node.ring * 0.5f) / ChipCore.RingCounts[node.ring]
                          * Mathf.PI * 2f + Mathf.PI / 2f;
            return Center + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 1.42f);
        }

        /// Hub-driven visibility (no own pause ownership in external mode).
        public void SetVisible(bool show)
        {
            open = show;
            if (panel != null) panel.SetActive(show);
            if (show)
            {
                BuildWidgets();
                Refresh();
            }
        }

        private void Update()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            if (!externalControl)
            {
                bool toggle = (kb != null && kb.gKey.wasPressedThisFrame)
                           || (pad != null && pad.leftStickButton.wasPressedThisFrame);
                if (toggle || (open && kb != null && kb.escapeKey.wasPressedThisFrame))
                {
                    if (!open && Core.GamePause.TryAcquire(this)) open = true;
                    else if (open)
                    {
                        open = false;
                        Core.GamePause.Release(this);
                    }
                    if (panel != null) panel.SetActive(open);
                    if (open)
                    {
                        BuildWidgets();
                        Refresh();
                    }
                    return;
                }
            }
            if (!open) return;

            int ringDir = (Press(kb?.sKey) || Press(kb?.downArrowKey) || Press(pad?.dpad.down) ? 1 : 0)
                        - (Press(kb?.wKey) || Press(kb?.upArrowKey) || Press(pad?.dpad.up) ? 1 : 0);
            int idxDir = (Press(kb?.dKey) || Press(kb?.rightArrowKey) || Press(pad?.dpad.right) ? 1 : 0)
                       - (Press(kb?.aKey) || Press(kb?.leftArrowKey) || Press(pad?.dpad.left) ? 1 : 0);

            if (ringDir != 0)
            {
                cursorRing = Mathf.Clamp(cursorRing + ringDir, 0, 8);
                cursorIdx = Mathf.Clamp(cursorIdx, 0, RingSize(cursorRing) - 1);
                Refresh();
            }
            else if (idxDir != 0)
            {
                int size = RingSize(cursorRing);
                cursorIdx = (cursorIdx + idxDir + size) % size;
                Refresh();
            }
            else if (Press(kb?.jKey) || Press(kb?.enterKey) || Press(pad?.buttonSouth))
            {
                ChipCore.Buy(Selected().id);
                Refresh();
            }
            else if (kb != null && kb.rKey.wasPressedThisFrame)
            {
                ChipCore.ResetChip();
                Refresh();
            }
        }

        private static bool Press(UnityEngine.InputSystem.Controls.ButtonControl b) =>
            b != null && b.wasPressedThisFrame;

        private static int RingSize(int ring) => ring == 8 ? 1 : ChipCore.RingCounts[ring];

        private ChipNode Selected()
        {
            foreach (var n in ChipCore.Nodes)
                if (n.ring == cursorRing && n.index == cursorIdx) return n;
            return ChipCore.Core;
        }

        private void Refresh()
        {
            var selected = Selected();
            for (int i = 0; i < ChipCore.Nodes.Length; i++)
            {
                var node = ChipCore.Nodes[i];
                bool owns = ChipCore.Owns(node.id);
                bool can = !owns && ChipCore.CanBuy(node, out _);
                var img = widgets[i];
                img.color = owns ? new Color(0.55f, 0.95f, 1f, 1f)
                    : can ? new Color(1f, 0.85f, 0.4f, 0.95f)
                    : new Color(0.25f, 0.3f, 0.45f, 0.55f);
                bool isSel = node == selected;
                widgetRects[i].localScale = Vector3.one * (isSel ? 1.7f : 1f);
                if (isSel) img.color = Color.Lerp(img.color, Color.white, 0.45f);
            }

            if (pointsText != null) pointsText.text = $"{ChipCore.PointsRemaining:000}";

            if (overviewText != null)
            {
                int lvl = PlayerProgression.Level;
                overviewText.text = $"{lvl:00}\n\n{PlayerProgression.Xp}/{PlayerProgression.XpToNext(lvl)}"
                    + (lvl >= PlayerProgression.MaxLevel ? "\nMAX" : $"\n{PlayerProgression.XpToNext(lvl) - PlayerProgression.Xp} XP");
            }

            if (previewText != null)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"+{ChipCore.GetFlat(ModifierType.MaxHealth):0} HP");
                sb.AppendLine($"+{ChipCore.GetPercent(ModifierType.AwakenedGain):0}% energy");
                sb.AppendLine($"+{ChipCore.GetPercent(ModifierType.SwordDamage):0}% blade");
                sb.AppendLine($"+{ChipCore.GetPercent(ModifierType.GunDamage):0}% gun");
                sb.AppendLine($"+{ChipCore.GetPercent(ModifierType.Defense):0}% defense");
                sb.AppendLine($"+{ChipCore.GetPercent(ModifierType.MoveSpeed):0}% speed");
                sb.AppendLine($"+{ChipCore.GetPercent(ModifierType.ChainWindow):0}% chain");
                sb.AppendLine($"+{ChipCore.GetPercent(ModifierType.XpGain):0}% xp");
                sb.AppendLine($"+{ChipCore.GetPercent(ModifierType.AwakenedDamage):0}% void");
                sb.AppendLine($"+{ChipCore.GetPercent(ModifierType.StatusDuration):0}% status");
                sb.AppendLine($"+{ChipCore.GetPercent(ModifierType.CritRate):0.#}% crit rate");
                sb.AppendLine($"+{ChipCore.GetPercent(ModifierType.CritDamage):0}% crit dmg");
                sb.AppendLine($"+{ChipCore.GetPercent(ModifierType.ElementalResist):0}% resist");
                previewText.text = sb.ToString();
            }

            if (summaryText != null)
                summaryText.text = $"{ChipCore.PointsEarned:000}\n{ChipCore.PointsSpent:000}\n"
                    + $"{ChipCore.PointsRemaining:000}\n{PlayerProgression.MaxLevel}";

            if (detailText != null)
            {
                string effect = selected.ring == 8
                    ? "+15% void dmg, +10% weapon dmg, +50 HP"
                    : $"+{selected.value:0.#}{(selected.flat ? "" : "%")} {selected.type}";
                string state = ChipCore.Owns(selected.id) ? "INSTALLED"
                    : ChipCore.CanBuy(selected, out string reason) ? "J: INSTALL (1 pt)" : reason;
                string ringName = selected.ring == 8 ? "CORE" : ChipCore.RingNames[selected.ring];
                detailText.text = $"[{ringName}] {selected.label}\n{effect}\n{state}";
            }

            if (levelMarker != null)
            {
                // bottom strip stops: 1,10,20..90,99 across x 0.075..0.755
                int lvl = PlayerProgression.Level;
                int stop = lvl >= 99 ? 11 : lvl < 10 ? 0 : lvl / 10;
                float x = Mathf.Lerp(0.075f, 0.755f, stop / 11f);
                levelMarker.anchorMin = new Vector2(x, 0.025f);
                levelMarker.anchorMax = new Vector2(x, 0.025f);
            }
        }
    }
}
