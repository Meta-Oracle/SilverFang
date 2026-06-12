using SilverFang.Currency;
using UnityEngine;
using UnityEngine.UI;

namespace SilverFang.UI
{
    /// HUD counter showing the Scematica balance next to the token icon,
    /// with a scale-pop when the balance changes.
    public class ScematicaCounterUI : MonoBehaviour
    {
        [SerializeField] private Text label;

        private bool first = true;
        private float pop;

        private void OnEnable()
        {
            ScematicaWallet.OnBalanceChanged += Refresh;
            first = true;
            Refresh(ScematicaWallet.Balance);
        }

        private void OnDisable() => ScematicaWallet.OnBalanceChanged -= Refresh;

        private void Refresh(long balance)
        {
            if (label != null)
                label.text = $"{balance:N0} {ScematicaToken.Symbol}";
            if (!first) pop = 1f;
            first = false;
        }

        private void Update()
        {
            if (label == null || pop <= 0f) return;
            pop = Mathf.Max(0f, pop - Time.unscaledDeltaTime * 4f);
            label.transform.localScale = Vector3.one * (1f + pop * 0.2f);
        }
    }
}
