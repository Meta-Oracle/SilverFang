using SilverFang.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace SilverFang.UI
{
    /// Shows the player's active status effect next to the plate, tinted.
    public class PlayerStatusUI : MonoBehaviour
    {
        [SerializeField] private CharacterCombatant player;
        [SerializeField] private Text label;

        private void Update()
        {
            if (label == null || player == null) return;
            switch (player.Status)
            {
                case StatusType.Frozen:
                    label.text = "FROZEN";
                    label.color = new Color(0.55f, 0.85f, 1f);
                    break;
                case StatusType.Burning:
                    label.text = "BURNING";
                    label.color = new Color(1f, 0.55f, 0.3f);
                    break;
                case StatusType.Radiated:
                    label.text = "RADIATED";
                    label.color = new Color(0.6f, 1f, 0.45f);
                    break;
                default:
                    label.text = "";
                    break;
            }
        }
    }
}
