using SilverFang.Player;
using UnityEngine;
using UnityEngine.UI;

namespace SilverFang.UI
{
    /// HUD bar for the Awakened / transformation meter. Pulses when full, drains
    /// while active. Tinted to the hero's AURA colour (Silver blue, Hilo purple,
    /// Lucas red) so each fighter's meter reads as their own energy.
    public class AwakenedMeterUI : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private Image fill;
        /// Hero aura colour — set per HUD when the meter is built.
        [SerializeField] private Color aura = new Color(0.55f, 0.7f, 1f); // default: Silver blue

        public void SetAura(Color c) => aura = c;

        private Color Charging => Color.Lerp(aura, Color.black, 0.35f); // dim while filling
        private Color Ready => Color.Lerp(aura, Color.white, 0.35f);    // bright when ready/active

        private void Update()
        {
            if (player == null || fill == null) return;
            float t = player.AwakenedMaxMeter > 0f ? player.AwakenedMeter / player.AwakenedMaxMeter : 0f;
            fill.fillAmount = t;

            bool ready = !player.AwakenedActive && t >= 1f;
            fill.color = ready
                ? Color.Lerp(Charging, Ready, Mathf.PingPong(Time.unscaledTime * 2f, 1f))
                : (player.AwakenedActive ? Ready : Charging);
        }
    }
}
