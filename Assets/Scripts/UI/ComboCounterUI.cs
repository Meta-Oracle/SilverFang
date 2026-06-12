using SilverFang.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace SilverFang.UI
{
    /// Combo chain display using the dedicated glyph art (per-hero digit and
    /// HITS/MAX sprites from the combo-counter sheet): up to three digits laid
    /// right-to-left with the suffix beneath, punching in scale on every hit
    /// and lingering with the rank letter when the chain drops. Falls back to
    /// plain text if the glyph sprites aren't wired.
    public class ComboCounterUI : MonoBehaviour
    {
        [SerializeField] private ComboTracker tracker;
        [SerializeField] private Text countText;
        [SerializeField] private Text rankText;
        [SerializeField] private Color accent = new Color(0.55f, 0.8f, 1f);

        [Header("Glyph art (optional)")]
        [SerializeField] private Sprite[] digitSprites;   // 0-9
        [SerializeField] private Sprite hitsSprite;
        [SerializeField] private Sprite maxSprite;
        [SerializeField] private Image[] digitSlots;      // ones, tens, hundreds
        [SerializeField] private Image suffixSlot;
        [SerializeField] private float glyphHeight = 34f;

        private float punch;
        private float lingerTimer;
        private bool UseGlyphs => digitSprites != null && digitSprites.Length == 10
            && digitSlots != null && digitSlots.Length > 0;

        private void OnEnable()
        {
            if (tracker == null) return;
            tracker.OnHit += OnHit;
            tracker.OnDropped += OnDropped;
            Hide();
        }

        private void OnDisable()
        {
            if (tracker == null) return;
            tracker.OnHit -= OnHit;
            tracker.OnDropped -= OnDropped;
        }

        private void Hide()
        {
            if (countText != null) countText.text = "";
            if (rankText != null) rankText.text = "";
            if (digitSlots != null)
                foreach (var slot in digitSlots)
                    if (slot != null) slot.enabled = false;
            if (suffixSlot != null) suffixSlot.enabled = false;
        }

        private void ShowGlyphs(int count)
        {
            int remaining = count;
            for (int i = 0; i < digitSlots.Length; i++)
            {
                var slot = digitSlots[i];
                if (slot == null) continue;
                bool used = remaining > 0 || i == 0;
                slot.enabled = used;
                if (!used) continue;
                var sprite = digitSprites[remaining % 10];
                slot.sprite = sprite;
                float aspect = sprite.rect.width / sprite.rect.height;
                slot.rectTransform.sizeDelta = new Vector2(glyphHeight * aspect, glyphHeight);
                slot.color = Color.white;
                remaining /= 10;
            }

            if (suffixSlot != null)
            {
                var suffix = count >= 50 && maxSprite != null ? maxSprite : hitsSprite;
                suffixSlot.enabled = suffix != null && count >= 2;
                if (suffix != null)
                {
                    suffixSlot.sprite = suffix;
                    float aspect = suffix.rect.width / suffix.rect.height;
                    float h = glyphHeight * 0.55f;
                    suffixSlot.rectTransform.sizeDelta = new Vector2(h * aspect, h);
                    suffixSlot.color = Color.white;
                }
            }
        }

        private void OnHit(int count)
        {
            lingerTimer = 0f;
            if (UseGlyphs)
            {
                if (count >= 2) ShowGlyphs(count);
                if (countText != null) countText.text = "";
            }
            else if (countText != null)
            {
                countText.text = count >= 2 ? $"{count} HITS" : "";
                countText.color = accent;
            }

            if (rankText != null)
            {
                rankText.text = count >= 5 ? ComboTracker.RankFor(count) : "";
                rankText.color = Color.Lerp(accent, Color.white, 0.4f);
            }
            punch = 1f;
        }

        private void OnDropped(int finalCount, string rank)
        {
            if (finalCount < 5)
            {
                Hide();
                return;
            }
            // hold the result for a beat so the player reads their rank
            lingerTimer = 1.4f;
            if (UseGlyphs) ShowGlyphs(finalCount);
            else if (countText != null) countText.text = $"{finalCount} HITS";
            if (rankText != null) rankText.text = rank;
        }

        private void SetGlyphAlpha(float a)
        {
            if (digitSlots != null)
                foreach (var slot in digitSlots)
                    if (slot != null && slot.enabled)
                        slot.color = new Color(1f, 1f, 1f, a);
            if (suffixSlot != null && suffixSlot.enabled)
                suffixSlot.color = new Color(1f, 1f, 1f, a);
        }

        private void Update()
        {
            if (punch > 0f)
            {
                punch -= Time.unscaledDeltaTime * 6f;
                float s = 1f + 0.25f * Mathf.Max(0f, punch);
                var scaleTarget = UseGlyphs && digitSlots[0] != null
                    ? digitSlots[0].transform.parent
                    : countText != null ? countText.transform : null;
                if (scaleTarget != null) scaleTarget.localScale = Vector3.one * s;
                if (rankText != null) rankText.transform.localScale = Vector3.one * (1f + 0.4f * Mathf.Max(0f, punch));
            }

            if (lingerTimer > 0f)
            {
                lingerTimer -= Time.unscaledDeltaTime;
                float fade = Mathf.Clamp01(lingerTimer / 0.5f);
                if (UseGlyphs) SetGlyphAlpha(fade);
                else if (countText != null) countText.color = new Color(accent.r, accent.g, accent.b, fade);
                if (rankText != null)
                {
                    var c = rankText.color;
                    rankText.color = new Color(c.r, c.g, c.b, fade);
                }
                if (lingerTimer <= 0f) Hide();
            }
        }
    }
}
