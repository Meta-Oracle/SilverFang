using SilverFang.Combat;
using SilverFang.Player;
using UnityEngine;
using UnityEngine.UI;

namespace SilverFang.UI
{
    /// Shows the current stance and loaded ammo, tinted to the ammo type,
    /// with a small scale-pop whenever either changes.
    public class StanceAmmoUI : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private Text label;

        private float pop;

        private void OnEnable()
        {
            if (player == null) return;
            player.OnStanceChanged += OnStance;
            player.OnAmmoChanged += OnAmmo;
            Refresh();
        }

        private void OnDisable()
        {
            if (player == null) return;
            player.OnStanceChanged -= OnStance;
            player.OnAmmoChanged -= OnAmmo;
        }

        private void OnStance(Stance _) { Refresh(); pop = 1f; }
        private void OnAmmo(AmmoDefinition _) { Refresh(); pop = 1f; }

        private void Refresh()
        {
            if (label == null || player == null) return;
            var ammo = player.CurrentAmmo;
            string ammoName = ammo != null ? ammo.type.ToString().ToUpperInvariant() : "—";
            label.text = player.CurrentStance == Stance.Sword
                ? $"SWORD  ·  {ammoName}"
                : $"GUN  ·  {ammoName}";
            label.color = ammo != null ? Color.Lerp(Color.white, ammo.tint, 0.65f) : Color.white;
        }

        private void Update()
        {
            if (label == null || pop <= 0f) return;
            pop = Mathf.Max(0f, pop - Time.unscaledDeltaTime * 4f);
            label.transform.localScale = Vector3.one * (1f + pop * 0.18f);
        }
    }
}
