using SilverFang.Combat;
using UnityEngine;

namespace SilverFang.Currency
{
    /// Awards Scematica to the player when this character dies. Attach to enemies.
    [RequireComponent(typeof(Health))]
    public class ScematicaBounty : MonoBehaviour
    {
        [SerializeField] private int amount = 10;

        private void Awake() => GetComponent<Health>().OnDeath += Award;

        private void Award() => ScematicaWallet.Earn(Mathf.RoundToInt(
            amount * Progression.PlayerProgression.GetMultiplier(Progression.ModifierType.ScematicaGain)
                   * (ComboTracker.Active != null ? ComboTracker.Active.PayoutMultiplier : 1f)));
    }
}
