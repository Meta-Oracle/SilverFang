using SilverFang.Story;
using UnityEngine;
using UnityEngine.UI;

namespace SilverFang.UI
{
    /// HUD count of recovered datashards.
    public class ShardCounterUI : MonoBehaviour
    {
        [SerializeField] private Text label;

        private void OnEnable()
        {
            StoryManager.OnChanged += Refresh;
            Refresh();
        }

        private void OnDisable() => StoryManager.OnChanged -= Refresh;

        private void Refresh()
        {
            if (label != null)
                label.text = $"SHARDS {StoryManager.CollectedCount}/{LoreDatabase.Entries.Length}  (V)";
        }
    }
}
