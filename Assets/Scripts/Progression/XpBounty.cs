using SilverFang.Combat;
using UnityEngine;

namespace SilverFang.Progression
{
    /// Grants XP to the player when this character dies. Attach to enemies.
    [RequireComponent(typeof(Health))]
    public class XpBounty : MonoBehaviour
    {
        [SerializeField] private int xp = 20;

        private void Awake() => GetComponent<Health>().OnDeath += Award;

        private void Award()
        {
            // combo tier pays out: TRANSCENDENT chains earn 7.5x XP
            float mult = ComboTracker.Active != null ? ComboTracker.Active.PayoutMultiplier : 1f;
            PlayerProgression.AddXp(Mathf.RoundToInt(xp * mult));
            PlayerProgression.NotifyEnemyKilled();
        }
    }
}
