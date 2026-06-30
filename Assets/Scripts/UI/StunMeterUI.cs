using SilverFang.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace SilverFang.UI
{
    /// CQC Engine stun meter. Fills as the hero takes stun damage and flashes
    /// hard when fully stunned (the 2s stun state). Hidden while empty.
    public class StunMeterUI : MonoBehaviour
    {
        [SerializeField] private CharacterCombatant target;
        [SerializeField] private Image fill;
        [SerializeField] private GameObject root; // shown only while there's stun to display

        private static readonly Color Low   = new Color(1f, 0.82f, 0.3f);  // amber
        private static readonly Color High  = new Color(1f, 0.35f, 0.2f);  // red-hot
        private static readonly Color Stun  = new Color(1f, 0.95f, 0.5f);  // flashing when stunned

        private void Update()
        {
            if (target == null || fill == null) return;
            float t = target.StunFraction;
            bool show = t > 0.01f || target.Stunned;
            if (root != null && root.activeSelf != show) root.SetActive(show);

            fill.fillAmount = target.Stunned ? 1f : t;
            fill.color = target.Stunned
                ? Color.Lerp(High, Stun, Mathf.PingPong(Time.unscaledTime * 6f, 1f))
                : Color.Lerp(Low, High, t);
        }
    }
}
