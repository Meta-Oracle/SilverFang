using UnityEngine;

namespace SilverFang.Core
{
    /// Single owner of Time.timeScale-based pausing so two menus can't fight
    /// over it. Gameplay input should early-out while IsPaused.
    public static class GamePause
    {
        private static object owner;

        public static bool IsPaused => owner != null;

        public static bool TryAcquire(object requester)
        {
            if (owner != null && owner != requester) return false;
            owner = requester;
            Time.timeScale = 0f;
            return true;
        }

        public static void Release(object requester)
        {
            if (owner != requester) return;
            owner = null;
            Time.timeScale = 1f;
        }
    }
}
