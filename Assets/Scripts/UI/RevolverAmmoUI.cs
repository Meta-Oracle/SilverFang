using SilverFang.Player;
using UnityEngine;
using UnityEngine.UI;

namespace SilverFang.UI
{
    /// Animated revolver cylinder HUD: shows the chambered-round count (0..6),
    /// flashes on each shot, and spins through the fill states while reloading.
    /// Swaps to the awakened (blue) cylinder art while awakened, base (gold)
    /// otherwise. Hidden unless the hero is in a gun/awakened state.
    public class RevolverAmmoUI : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private Image cylinder;
        [SerializeField] private Sprite[] baseStates;   // 0..6 rounds (gold)
        [SerializeField] private Sprite[] awkStates;    // 0..6 rounds (blue)
        [SerializeField] private Sprite[] baseFire;     // muzzle flash frames
        [SerializeField] private Sprite[] awkFire;
        [SerializeField] private Text countText;

        private int lastRounds = -1;
        private float flash;

        private void OnEnable()
        {
            if (player != null) player.OnRevolverChanged += OnChanged;
        }

        private void OnDisable()
        {
            if (player != null) player.OnRevolverChanged -= OnChanged;
        }

        private void OnChanged()
        {
            if (player.RevolverRounds < lastRounds) flash = 0.12f; // a shot left a chamber
            lastRounds = player.RevolverRounds;
        }

        private void Update()
        {
            if (player == null || cylinder == null) return;

            bool show = player.UsesRevolver || player.AwakenedActive;
            if (cylinder.enabled != show) cylinder.enabled = show;
            if (countText != null && countText.enabled != show) countText.enabled = show;
            if (!show) return;

            var states = player.AwakenedActive && awkStates != null && awkStates.Length > 0
                ? awkStates : baseStates;
            var fires = player.AwakenedActive && awkFire != null && awkFire.Length > 0
                ? awkFire : baseFire;
            if (states == null || states.Length == 0) return;

            Sprite chosen;
            if (flash > 0f)
            {
                flash -= Time.unscaledDeltaTime;
                chosen = fires != null && fires.Length > 0 ? fires[0] : states[states.Length - 1];
            }
            else if (player.Reloading)
            {
                // cylinder spin: race through the fill states while reloading
                int idx = Mathf.FloorToInt(Time.unscaledTime * 18f) % states.Length;
                chosen = states[idx];
            }
            else
            {
                int rounds = Mathf.Clamp(player.RevolverRounds, 0, states.Length - 1);
                chosen = states[rounds];
            }
            cylinder.sprite = chosen;

            if (countText != null)
                countText.text = player.AwakenedActive ? "∞"
                    : $"{player.RevolverRounds} / {PlayerController.RevolverMax}";
        }
    }
}
