using UnityEngine;

namespace SilverFang.VFX
{
    /// One-shot sprite flipbook. Destroys itself when finished
    /// (or after `loopDuration` seconds if looping).
    [RequireComponent(typeof(SpriteRenderer))]
    public class VfxInstance : MonoBehaviour
    {
        private SpriteRenderer sr;
        private Sprite[] frames;
        private float fps;
        private float loopDuration;
        private float elapsed;

        public void Play(Sprite[] animFrames, float framesPerSecond, float loopSeconds = 0f)
        {
            sr = GetComponent<SpriteRenderer>();
            frames = animFrames;
            fps = Mathf.Max(framesPerSecond, 1f);
            loopDuration = loopSeconds;
            elapsed = 0f;
            if (frames.Length > 0) sr.sprite = frames[0];
        }

        private void Update()
        {
            if (frames == null || frames.Length == 0)
            {
                Destroy(gameObject);
                return;
            }

            elapsed += Time.deltaTime;
            int frame = (int)(elapsed * fps);

            if (loopDuration > 0f)
            {
                if (elapsed >= loopDuration)
                {
                    Destroy(gameObject);
                    return;
                }
                sr.sprite = frames[frame % frames.Length];
            }
            else
            {
                if (frame >= frames.Length)
                {
                    Destroy(gameObject);
                    return;
                }
                sr.sprite = frames[frame];
            }
        }
    }
}
