using SilverFang.Combat;
using SilverFang.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace SilverFang.UI
{
    /// Drives the portrait plate: health bar (smooth drain + damage flash),
    /// level tag, and XP strip, all overlaid on the plate art's blank slots.
    public class PlayerPlateUI : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private Image healthFill;
        [SerializeField] private Text healthText;
        [SerializeField] private Text levelText;
        [SerializeField] private Image xpFill;
        [SerializeField] private Text xpText;

        private static readonly Color HealthColor = Color.white;
        private static readonly Color FlashColor = new Color(1f, 0.85f, 0.85f, 1f);

        private float displayedHealth = 1f;
        private float targetHealth = 1f;
        private float flashTimer;

        private void OnEnable()
        {
            if (health != null)
            {
                health.OnHealthChanged += OnHealthChanged;
                OnHealthChanged(health.Current, health.Max);
                displayedHealth = targetHealth;
            }
            PlayerProgression.OnChanged += RefreshProgression;
            RefreshProgression();
        }

        private void OnDisable()
        {
            if (health != null) health.OnHealthChanged -= OnHealthChanged;
            PlayerProgression.OnChanged -= RefreshProgression;
        }

        private void OnHealthChanged(int current, int max)
        {
            float t = max > 0 ? (float)current / max : 0f;
            if (t < targetHealth) flashTimer = 0.25f;
            targetHealth = t;
            if (healthText != null) healthText.text = $"{current} / {max}";
        }

        private void RefreshProgression()
        {
            if (levelText != null)
            {
                int pts = PlayerProgression.UnspentStatPoints + PlayerProgression.UnspentSkillPoints;
                levelText.text = pts > 0 ? $"LV. {PlayerProgression.Level} (+{pts})" : $"LV. {PlayerProgression.Level}";
            }
            if (xpFill != null)
                xpFill.fillAmount = PlayerProgression.Level >= PlayerProgression.MaxLevel
                    ? 1f
                    : (float)PlayerProgression.Xp / PlayerProgression.XpToNext(PlayerProgression.Level);
            if (xpText != null)
                xpText.text = PlayerProgression.Level >= PlayerProgression.MaxLevel
                    ? "XP MAX"
                    : $"XP {PlayerProgression.Xp} / {PlayerProgression.XpToNext(PlayerProgression.Level)}";
        }

        private void Update()
        {
            if (healthFill == null) return;
            // drain eases toward the real value; gains snap (heals feel instant)
            displayedHealth = targetHealth > displayedHealth
                ? targetHealth
                : Mathf.MoveTowards(displayedHealth, targetHealth, 1.2f * Time.unscaledDeltaTime);
            healthFill.fillAmount = displayedHealth;

            if (flashTimer > 0f)
            {
                flashTimer -= Time.unscaledDeltaTime;
                healthFill.color = Color.Lerp(HealthColor, FlashColor, flashTimer / 0.25f * 2f % 1f);
                if (flashTimer <= 0f) healthFill.color = HealthColor;
            }
        }
    }
}
