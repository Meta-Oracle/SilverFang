using System.Collections.Generic;
using System.Linq;
using System.Text;
using SilverFang.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SilverFang.Story
{
    /// Datashard codex (V key / gamepad R3). Lists found shards; W/S or
    /// d-pad selects, the right pane shows the full document. Pauses the game.
    public class CodexUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text listColumn;
        [SerializeField] private Text bodyColumn;

        private bool open;
        private int cursor;

        private List<LoreEntry> Found =>
            LoreDatabase.Entries.Where(e => StoryManager.IsCollected(e.id)).ToList();

        private void Start()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;

            bool toggle = (kb != null && kb.vKey.wasPressedThisFrame)
                       || (pad != null && pad.rightStickButton.wasPressedThisFrame)
                       || (open && kb != null && kb.escapeKey.wasPressedThisFrame);

            if (toggle) Toggle();
            if (!open) return;

            var found = Found;
            if (found.Count == 0) return;

            bool down = (kb != null && (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame))
                     || (pad != null && pad.dpad.down.wasPressedThisFrame);
            bool up = (kb != null && (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame))
                   || (pad != null && pad.dpad.up.wasPressedThisFrame);
            if (down) cursor = (cursor + 1) % found.Count;
            else if (up) cursor = (cursor - 1 + found.Count) % found.Count;
            else return;

            Render();
        }

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
            if (panel != null)
            {
                panel.SetActive(open);
                if (open) panel.transform.SetAsLastSibling(); // draw above HUD/menus
            }
        }

        private void Render()
        {
            var found = Found;
            if (cursor >= found.Count) cursor = Mathf.Max(0, found.Count - 1);

            if (listColumn != null)
            {
                var sb = new StringBuilder();
                sb.AppendLine("== DATASHARDS ==");
                sb.AppendLine($"({found.Count}/{LoreDatabase.Entries.Length} recovered)");
                sb.AppendLine();
                if (found.Count == 0)
                {
                    sb.AppendLine("No shards recovered.");
                    sb.AppendLine("Search the cull zones.");
                }
                for (int i = 0; i < found.Count; i++)
                    sb.AppendLine($"{(i == cursor ? "> " : "  ")}{found[i].title}");
                sb.AppendLine();
                sb.AppendLine("W/S=Select  V=Close");
                listColumn.text = sb.ToString();
            }

            if (bodyColumn != null)
                bodyColumn.text = found.Count == 0 ? "" : found[cursor].body;
        }
    }
}
