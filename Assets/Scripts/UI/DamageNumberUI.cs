using SilverFang.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SilverFang.UI
{
    /// Spawns floating damage numbers over world positions ("DAMAGE NUMBERS
    /// &amp; INDICATORS" on the HUD sheet). Lives on the HUD canvas.
    public class DamageNumberUI : MonoBehaviour
    {
        [SerializeField] private Font font;

        private static DamageNumberUI instance;

        private void Awake() => instance = this;
        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static void Spawn(Vector3 worldPos, int amount, Color color, bool big = false)
        {
            if (instance == null || Camera.main == null) return;

            var obj = new GameObject("Dmg");
            obj.transform.SetParent(instance.transform, false);
            var text = obj.AddComponent<Text>();
            text.font = instance.font;
            text.fontSize = big ? 24 : 17;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            // Display caps at 9999 even though the real damage applied to the
            // target (computed by the caller) can run far higher.
            text.text = Mathf.Min(amount, 9999).ToString();

            var rect = text.rectTransform;
            rect.sizeDelta = new Vector2(80f, 30f);
            rect.position = Camera.main.WorldToScreenPoint(worldPos + Vector3.up * 0.4f);

            obj.AddComponent<Floater>();
        }

        private class Floater : MonoBehaviour
        {
            private Text text;
            private float life;
            private float drift;

            private void Awake()
            {
                text = GetComponent<Text>();
                drift = Random.Range(-22f, 22f);
            }

            private void Update()
            {
                life += Time.unscaledDeltaTime;
                transform.position += new Vector3(drift, 80f, 0f) * Time.unscaledDeltaTime;
                if (text != null)
                {
                    var c = text.color;
                    c.a = Mathf.Clamp01(1.4f - life * 1.8f);
                    text.color = c;
                }
                if (life >= 0.85f) Destroy(gameObject);
            }
        }
    }
}
