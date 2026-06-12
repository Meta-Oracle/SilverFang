using UnityEngine;

namespace SilverFang.Combat
{
    /// Brief global slow-motion on impact — the backbone of punchy combat.
    /// Defers to GamePause (never fights a menu for timeScale).
    public static class HitStop
    {
        private static Runner runner;

        public static void Do(float seconds)
        {
            if (Core.GamePause.IsPaused) return;
            if (runner == null)
            {
                var go = new GameObject("HitStop");
                Object.DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideInHierarchy;
                runner = go.AddComponent<Runner>();
            }
            runner.Begin(seconds);
        }

        private class Runner : MonoBehaviour
        {
            private float timer;

            public void Begin(float seconds)
            {
                timer = Mathf.Max(timer, seconds);
                if (!Core.GamePause.IsPaused) Time.timeScale = 0.05f;
            }

            private void Update()
            {
                if (timer <= 0f) return;
                timer -= Time.unscaledDeltaTime;
                if (timer <= 0f && !Core.GamePause.IsPaused) Time.timeScale = 1f;
            }
        }
    }
}
