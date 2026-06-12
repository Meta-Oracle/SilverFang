using UnityEngine;

namespace SilverFang.UI
{
    /// World-space lock-on reticle: spins and pulses over the marked enemy
    /// while LT / Left Shift is held. Per-hero art (Silver blue, Hilo purple)
    /// from the lock-on UI sheet. Lives on the hero root next to
    /// PlayerController; manages its own detached marker object.
    public class LockOnReticleUI : MonoBehaviour
    {
        [SerializeField] private Sprite reticleSprite;
        [SerializeField] private float spinSpeed = 70f;
        [SerializeField] private float worldScale = 1.1f;
        [SerializeField] private float heightOffset = 0.9f;

        private Player.PlayerController player;
        private Transform marker;
        private SpriteRenderer sr;

        private void Awake()
        {
            player = GetComponent<Player.PlayerController>();
            var go = new GameObject("LockReticle");
            marker = go.transform;
            sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = reticleSprite;
            sr.sortingOrder = 6000;
            go.SetActive(false);
        }

        private void OnDestroy()
        {
            if (marker != null) Destroy(marker.gameObject);
        }

        private void LateUpdate()
        {
            var target = player != null ? player.LockTarget : null;
            if (target == null || target.IsDead || reticleSprite == null)
            {
                if (marker.gameObject.activeSelf) marker.gameObject.SetActive(false);
                return;
            }

            marker.position = target.transform.position + Vector3.up * heightOffset;
            marker.Rotate(0f, 0f, spinSpeed * Time.unscaledDeltaTime);
            float pulse = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 6f);
            marker.localScale = Vector3.one * (worldScale * pulse);
            if (!marker.gameObject.activeSelf) marker.gameObject.SetActive(true);
        }
    }
}
