using System.Collections.Generic;
using SilverFang.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SilverFang.Story
{
    /// Bottom-screen dialogue box. Pauses the game while lines are shown;
    /// advance with J / Enter / gamepad Cross. Queues whole conversations,
    /// and waits politely if another menu currently owns the pause.
    public class DialogueUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text speakerText;
        [SerializeField] private Text bodyText;
        [SerializeField] private Text hintText;

        private static DialogueUI instance;

        private readonly Queue<DialogueLine> lines = new Queue<DialogueLine>();
        private bool showing;

        private void Awake() => instance = this;
        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Start()
        {
            if (panel != null) panel.SetActive(false);
            if (hintText != null) hintText.text = "J / Cross to continue";
        }

        /// Plays a beat from LoreDatabase once per save file.
        public static void PlayBeat(string beatId)
        {
            if (instance == null || string.IsNullOrEmpty(beatId)) return;
            if (!LoreDatabase.Beats.TryGetValue(beatId, out var beat)) return;
            if (!StoryManager.TryMarkBeatPlayed(beatId)) return;
            foreach (var line in beat) instance.lines.Enqueue(line);
        }

        /// Plays ad-hoc lines (e.g. datashard pickups). Always shown.
        public static void PlayLines(params DialogueLine[] adHoc)
        {
            if (instance == null) return;
            foreach (var line in adHoc) instance.lines.Enqueue(line);
        }

        private void Update()
        {
            if (!showing)
            {
                if (lines.Count == 0) return;
                // wait until we can own the pause (another menu may be open)
                if (!GamePause.TryAcquire(this)) return;
                showing = true;
                if (panel != null) panel.SetActive(true);
                ShowLine(lines.Dequeue());
                return;
            }

            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool advance = (kb != null && (kb.jKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                        || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            if (!advance) return;

            if (lines.Count > 0)
            {
                ShowLine(lines.Dequeue());
            }
            else
            {
                showing = false;
                if (panel != null) panel.SetActive(false);
                GamePause.Release(this);
            }
        }

        private void ShowLine(DialogueLine line)
        {
            if (speakerText != null) speakerText.text = line.speaker;
            if (bodyText != null) bodyText.text = line.text;
        }
    }
}
