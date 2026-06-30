using UnityEngine;

namespace SilverFang.VFX
{
    /// One-shot sprite flipbook. Destroys itself when finished (or after
    /// `loopDuration` seconds if looping). One-shots fade out and grow slightly
    /// over a guaranteed minimum lifetime, so even a single-still impact reads as
    /// a punchy, lively burst.
    [RequireComponent(typeof(SpriteRenderer))]
    public class VfxInstance : MonoBehaviour
    {
        private SpriteRenderer sr;
        private Sprite[] frames;
        private float fps;
        private float loopDuration;
        private float elapsed;
        private float life;          // one-shot total lifetime
        private Color baseColor = Color.white;
        private Vector3 baseScale = Vector3.one;
        private const float MinLife = 0.32f; // single-frame bursts still live long enough to read

        public void Play(Sprite[] animFrames, float framesPerSecond, float loopSeconds = 0f, Color? tint = null)
        {
            sr = GetComponent<SpriteRenderer>();
            frames = animFrames;
            fps = Mathf.Max(framesPerSecond, 1f);
            loopDuration = loopSeconds;
            elapsed = 0f;
            baseColor = tint ?? Color.white;
            baseScale = transform.localScale;
            sr.color = baseColor;
            life = Mathf.Max(MinLife, frames != null ? frames.Length / fps : MinLife);
            if (frames != null && frames.Length > 0) sr.sprite = frames[0];
        }

        private void Update()
        {
            if (frames == null || frames.Length == 0)
            {
                Destroy(gameObject);
                return;
            }

            elapsed += Time.deltaTime;

            if (loopDuration > 0f)
            {
                if (elapsed >= loopDuration) { Destroy(gameObject); return; }
                sr.sprite = frames[(int)(elapsed * fps) % frames.Length];
                return;
            }

            // one-shot: hold last frame across the lifetime, fade + grow to punch.
            if (elapsed >= life) { Destroy(gameObject); return; }
            int frame = Mathf.Min(frames.Length - 1, (int)(elapsed * fps));
            sr.sprite = frames[frame];

            float k = elapsed / life;                 // 0..1
            float a = k < 0.55f ? 1f : 1f - (k - 0.55f) / 0.45f;
            var c = baseColor; c.a = baseColor.a * Mathf.Clamp01(a); sr.color = c;
            transform.localScale = baseScale * (1f + 0.14f * k);
        }
    }
}
