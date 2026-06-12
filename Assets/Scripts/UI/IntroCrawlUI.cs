using SilverFang.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SilverFang.UI
{
    /// Skippable opening lore crawl: scrolls the Scematica Dynamics mission
    /// briefing up the screen, then hands off to the character-select screen.
    /// Holds the game pause while visible. J / Enter / Space / South skips.
    public class IntroCrawlUI : MonoBehaviour
    {
        [SerializeField] private RectTransform crawlRect;
        [SerializeField] private Text crawlText;
        [SerializeField] private Text hintText;
        [SerializeField] private GameObject characterSelect;
        [SerializeField] private float scrollSpeed = 22f;
        [SerializeField] private float endPause = 1.6f;

        private float endTimer = -1f;
        private bool done;

        private void Start()
        {
            GamePause.TryAcquire(this);
            if (hintText != null)
                hintText.text = "J / ENTER / Ⓐ  SKIP";
        }

        private void Update()
        {
            if (done) return;

            if (SkipPressed())
            {
                Finish();
                return;
            }

            if (crawlRect != null)
            {
                crawlRect.anchoredPosition += Vector2.up * (scrollSpeed * Time.unscaledDeltaTime);

                // crawl is done once the whole text has scrolled past the top
                float total = Screen.height + (crawlText != null ? crawlText.preferredHeight : 0f);
                if (endTimer < 0f && crawlRect.anchoredPosition.y >= total)
                    endTimer = endPause;
            }

            if (endTimer >= 0f)
            {
                endTimer -= Time.unscaledDeltaTime;
                if (endTimer <= 0f) Finish();
            }
        }

        private bool SkipPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.jKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame
                || kb.spaceKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) return true;
            var pad = Gamepad.current;
            return pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame);
        }

        private void Finish()
        {
            done = true;
            GamePause.Release(this);
            if (characterSelect != null) characterSelect.SetActive(true);
            Destroy(gameObject);
        }
    }
}
