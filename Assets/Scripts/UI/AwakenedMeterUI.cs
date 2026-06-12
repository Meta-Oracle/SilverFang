using SilverFang.Player;
using UnityEngine;
using UnityEngine.UI;

namespace SilverFang.UI
{
    /// HUD bar for the Awakened meter. Pulses when full, drains while active.
    public class AwakenedMeterUI : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private Image fill;

        private static readonly Color Charging = new Color(0.55f, 0.35f, 0.9f);
        private static readonly Color Ready = new Color(0.8f, 0.55f, 1f);

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
