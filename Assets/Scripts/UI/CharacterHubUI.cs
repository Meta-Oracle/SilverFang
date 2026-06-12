using SilverFang.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.UI;

namespace SilverFang.UI
{
    /// Character hub on the DualSense touchpad (T on keyboard): one pause
    /// menu hosting every character screen as tabs — ATTRIBUTES (art sheet),
    /// LEVELING (stats + skill paths), CHIP CORE (development chip). L1/R1
    /// (Q/E) switch tabs, the d-pad drives the active screen, touchpad/T/Esc
    /// closes.
    public class CharacterHubUI : MonoBehaviour
    {
        [SerializeField] private CharacterSheetUI attributes;
        [SerializeField] private LevelUpMenuUI leveling;
        [SerializeField] private ChipCoreUI chipCore;
        [SerializeField] private GameObject banner;
        [SerializeField] private Text tabsText;

        private static readonly string[] TabNames = { "ATTRIBUTES", "LEVELING", "CHIP CORE" };
        private bool open;
        private int tab;

        private void Start()
        {
            if (banner != null) banner.SetActive(false);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            var ds = pad as DualShockGamepad;

            bool toggle = (kb != null && kb.tKey.wasPressedThisFrame)
                       || (ds != null && ds.touchpadButton.wasPressedThisFrame)
                       || (open && kb != null && kb.escapeKey.wasPressedThisFrame);
            if (toggle)
            {
                if (!open && GamePause.TryAcquire(this)) open = true;
                else if (open)
                {
                    open = false;
                    GamePause.Release(this);
                }
                if (banner != null) banner.SetActive(open);
                ShowTab(open ? tab : -1);
                return;
            }
            if (!open) return;

            int dir = ((kb != null && kb.eKey.wasPressedThisFrame)
                       || (pad != null && pad.rightShoulder.wasPressedThisFrame) ? 1 : 0)
                    - ((kb != null && kb.qKey.wasPressedThisFrame)
                       || (pad != null && pad.leftShoulder.wasPressedThisFrame) ? 1 : 0);
            if (dir != 0)
            {
                tab = (tab + dir + TabNames.Length) % TabNames.Length;
                ShowTab(tab);
            }
        }

        private void ShowTab(int index)
        {
            if (attributes != null) attributes.SetVisible(index == 0);
            if (leveling != null) leveling.SetVisible(index == 1);
            if (chipCore != null) chipCore.SetVisible(index == 2);

            if (tabsText != null && index >= 0)
            {
                var parts = new string[TabNames.Length];
                for (int i = 0; i < TabNames.Length; i++)
                    parts[i] = i == index ? $"[ {TabNames[i]} ]" : TabNames[i];
                tabsText.text = string.Join("    ", parts) + "      L1/R1 SWITCH   TOUCHPAD CLOSE";
            }
        }
    }
}
