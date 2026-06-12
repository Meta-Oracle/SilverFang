using UnityEngine;

namespace SilverFang.Combat
{
    /// Tiny world-space health bar above an enemy ("TARGETING &amp; ENEMY INFO").
    /// Built from runtime sprites; appears on first damage, fades when idle.
    [RequireComponent(typeof(Health))]
    public class EnemyHealthBar : MonoBehaviour
    {
        [SerializeField] private float yOffset = 1.9f;
        [SerializeField] private float width = 1.1f;
        [SerializeField] private float visibleSeconds = 4f;

        private Health health;
        private Transform barRoot;
        private Transform fill;
        private SpriteRenderer fillSr;
        private SpriteRenderer bgSr;
        private float showTimer;

        private void Awake()
        {
            health = GetComponent<Health>();
            health.OnHealthChanged += OnHealthChanged;
            health.OnDeath += () => { if (barRoot != null) barRoot.gameObject.SetActive(false); };

            var sprite = Sprite.Create(Texture2D.whiteTexture,
                new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                new Vector2(0f, 0.5f), Texture2D.whiteTexture.width);

            barRoot = new GameObject("HealthBar").transform;
            barRoot.SetParent(transform, false);
            barRoot.localPosition = new Vector3(0f, yOffset, 0f);

            var bg = new GameObject("BG");
            bg.transform.SetParent(barRoot, false);
            bg.transform.localPosition = new Vector3(-width / 2f, 0f, 0f);
            bg.transform.localScale = new Vector3(width, 0.12f, 1f);
            bgSr = bg.AddComponent<SpriteRenderer>();
            bgSr.sprite = sprite;
            bgSr.color = new Color(0f, 0f, 0f, 0.7f);
            bgSr.sortingOrder = 5000;

            var fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(barRoot, false);
            fillObj.transform.localPosition = new Vector3(-width / 2f + 0.015f, 0f, 0f);
            fillObj.transform.localScale = new Vector3(width - 0.03f, 0.09f, 1f);
            fillSr = fillObj.AddComponent<SpriteRenderer>();
            fillSr.sprite = sprite;
            fillSr.color = new Color(0.95f, 0.25f, 0.2f);
            fillSr.sortingOrder = 5001;
            fill = fillObj.transform;

            barRoot.gameObject.SetActive(false);
        }

        private void OnHealthChanged(int current, int max)
        {
            if (health.IsDead || barRoot == null) return;
            barRoot.gameObject.SetActive(true);
            showTimer = visibleSeconds;
            float t = max > 0 ? (float)current / max : 0f;
            fill.localScale = new Vector3((width - 0.03f) * t, 0.09f, 1f);
        }

        private void Update()
        {
            if (barRoot == null || !barRoot.gameObject.activeSelf) return;
            // bar must not mirror when the enemy flips to face the player
            float parentSign = Mathf.Sign(transform.lossyScale.x);
            barRoot.localScale = new Vector3(parentSign, 1f, 1f);

            showTimer -= Time.deltaTime;
            if (showTimer <= 0f) barRoot.gameObject.SetActive(false);
        }
    }
}
