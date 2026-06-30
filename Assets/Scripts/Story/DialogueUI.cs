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
                if (panel != null)
                {
                    panel.SetActive(true);
                    panel.transform.SetAsLastSibling(); // pop above any open HUD/menu
                }
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

        private UnityEngine.UI.Image panelImage;

        private void ShowLine(DialogueLine line)
        {
            Color c = SpeakerColor(line.speaker);
            if (speakerText != null) speakerText.color = c;
            // Body stays bright/readable but tinted faintly toward the speaker.
            if (bodyText != null) bodyText.color = Color.Lerp(new Color(0.94f, 0.96f, 1f), c, 0.25f);
            // The dialogue box itself takes the speaker's color (dark, so text reads).
            if (panelImage == null && panel != null) panelImage = panel.GetComponent<UnityEngine.UI.Image>();
            if (panelImage != null) panelImage.color = new Color(c.r * 0.13f, c.g * 0.13f, c.b * 0.16f, 0.95f);

            if (speakerText != null) speakerText.text = line.speaker;
            if (bodyText != null) bodyText.text = line.text;
        }

        /// Designated dialogue-box colour per speaker, so each voice reads at a
        /// glance: Silver steel, Vesper machine-cyan, Senn warm amber, Voss
        /// imperial gold, Apium hive-violet, the fading clone washed-grey, Dark
        /// Fang crimson, future-Silver deep blue, Lucas lime, Hilo violet.
        private static Color SpeakerColor(string speaker)
        {
            switch (speaker)
            {
                case "SILVER":          return new Color(0.82f, 0.90f, 1.00f);
                case "VESPER":          return new Color(0.45f, 0.92f, 0.95f);
                case "DR. SENN":        return new Color(1.00f, 0.72f, 0.40f);
                case "CHAIRMAN VOSS":   return new Color(1.00f, 0.85f, 0.42f);
                case "APIUM GESTALT":   return new Color(0.66f, 0.55f, 1.00f);
                case "S-1L/C":          return new Color(0.60f, 0.66f, 0.78f);
                case "DARK FANG":       return new Color(1.00f, 0.35f, 0.35f);
                case "SILVER (FUTURE)": return new Color(0.58f, 0.78f, 1.00f);
                case "LUCAS":           return new Color(0.60f, 0.95f, 0.48f);
                case "HILO":            return new Color(0.82f, 0.50f, 1.00f);
                case "SIX":             return new Color(0.80f, 0.80f, 0.88f); // moonlit silver-grey
                case "A.o.C":           return new Color(0.85f, 0.20f, 0.30f); // blood-crimson authority
                case "THE MASTER":      return new Color(0.95f, 0.78f, 0.45f); // aged gold
                case "STERLING":        return new Color(0.90f, 0.92f, 0.96f); // polished silver (Silver's heir)
                case "LYRA":            return new Color(0.62f, 0.72f, 0.95f); // cosmic gravitic blue
                default:                return new Color(0.70f, 0.72f, 0.80f); // "???" / unknown
            }
        }
    }
}
