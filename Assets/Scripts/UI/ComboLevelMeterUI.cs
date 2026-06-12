using SilverFang.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace SilverFang.UI
{
    /// Combo level meter (combolevelmeters sheet): tier glyph F -> XXX, bar
    /// filling toward the next tier, and the tier title. The bar doubles as
    /// the combo-window timer warning: as the (tier-shortened) window runs
    /// out it burns toward red, so high tiers visibly demand faster play.
    public class ComboLevelMeterUI : MonoBehaviour
    {
        [SerializeField] private ComboTracker tracker;
        [SerializeField] private GameObject meterRoot; // hidden until a chain starts
        [SerializeField] private Image letterImage;
        [SerializeField] private Image fillImage;
        [SerializeField] private Text titleText;
        [SerializeField] private Sprite[] letterSprites; // 12 tiers
        [SerializeField] private float letterHeight = 30f;

        private static readonly Color WarnColor = new Color(1f, 0.4f, 0.3f);
        private float punch;
        private float shownFill;

        private void OnEnable()
        {
            if (tracker == null) return;
            tracker.OnLevelChanged += OnLevelChanged;
            tracker.OnDropped += OnDropped;
            ApplyTier(tracker.Level);
        }

        private void OnDisable()
        {
            if (tracker == null) return;
            tracker.OnLevelChanged -= OnLevelChanged;
            tracker.OnDropped -= OnDropped;
        }

        private void ApplyTier(int level)
        {
            if (letterImage != null && letterSprites != null
                && level < letterSprites.Length && letterSprites[level] != null)
            {
                var sprite = letterSprites[level];
                letterImage.sprite = sprite;
                letterImage.enabled = true;
                float aspect = sprite.rect.width / sprite.rect.height;
                letterImage.rectTransform.sizeDelta = new Vector2(letterHeight * aspect, letterHeight);
            }
            if (titleText != null)
                titleText.text = ComboTracker.TierTitles[Mathf.Clamp(level, 0, ComboTracker.TierTitles.Length - 1)];
        }

        private void OnLevelChanged(int level)
        {
            ApplyTier(level);
            punch = 1f;
        }

        private void OnDropped(int finalCount, string rank) => ApplyTier(0);

        private void Update()
        {
            if (tracker == null) return;

            // only on screen while a chain is alive — no static chrome
            if (meterRoot != null && meterRoot.activeSelf != tracker.Count > 0)
            {
                meterRoot.SetActive(tracker.Count > 0);
                if (tracker.Count > 0)
                {
                    shownFill = 0f;
                    ApplyTier(tracker.Level);
                }
            }
            if (meterRoot != null && !meterRoot.activeSelf) return;

            if (fillImage != null)
            {
                shownFill = Mathf.MoveTowards(shownFill, tracker.LevelProgress01,
                    Time.unscaledDeltaTime * 2.5f);
                fillImage.fillAmount = shownFill;

                // window urgency: burn red and pulse as the leash runs out
                float left = tracker.WindowLeft01;
                if (tracker.Count > 0 && left < 0.45f)
                {
                    float urgency = 1f - left / 0.45f;
                    float flicker = 0.75f + 0.25f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 9f));
                    fillImage.color = Color.Lerp(Color.white, WarnColor * flicker, urgency);
                }
                else
                {
                    fillImage.color = Color.white;
                }
            }

            if (punch > 0f && letterImage != null)
            {
                punch -= Time.unscaledDeltaTime * 4f;
                letterImage.transform.localScale = Vector3.one * (1f + 0.45f * Mathf.Max(0f, punch));
            }
        }
    }
}
