using UnityEngine;

namespace SilverFang.VFX
{
    /// Scene singleton. Spawns one-shot effects from the VfxLibrary.
    /// All calls are safe no-ops when no manager/library exists.
    public class VfxManager : MonoBehaviour
    {
        [SerializeField] private VfxLibrary library;

        private static VfxManager instance;

        private void Awake() => instance = this;
        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static void Play(string id, Vector3 position, float facing = 1f, float scaleMultiplier = 1f)
        {
            Spawn(id, position, facing, scaleMultiplier, null, 0f);
        }

        /// Looping effect that follows a target (e.g. rage aura) for `duration` seconds.
        /// Returns the spawned object (null if no manager/library/entry) so callers
        /// can destroy it when the effect ends early.
        public static GameObject PlayAttached(string id, Transform parent, float duration, float scaleMultiplier = 1f, float yOffset = 0f)
        {
            return Spawn(id, parent.position + Vector3.up * yOffset, 1f, scaleMultiplier, parent, duration);
        }

        private static GameObject Spawn(string id, Vector3 position, float facing, float scaleMultiplier, Transform parent, float loopSeconds)
        {
            if (instance == null || instance.library == null) return null;
            var entry = instance.library.Find(id);
            if (entry == null || entry.frames == null || entry.frames.Length == 0) return null;

            var go = new GameObject("Vfx_" + id);
            go.transform.position = position;
            if (parent != null) go.transform.SetParent(parent, true);

            float scale = entry.scale * scaleMultiplier;
            go.transform.localScale = new Vector3(scale * Mathf.Sign(facing == 0 ? 1f : facing), scale, 1f);
            if (entry.randomRotation)
                go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = -Mathf.RoundToInt(position.y * 100f) + 200;

            go.AddComponent<VfxInstance>().Play(entry.frames, entry.fps, loopSeconds);
            return go;
        }
    }
}
