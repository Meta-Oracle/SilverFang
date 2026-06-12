using UnityEngine;

namespace SilverFang.Story
{
    /// Kicks off the opening dialogue. Beats only ever play once per save
    /// (StoryManager), so this is safe across scene reloads.
    public class StoryDirector : MonoBehaviour
    {
        [SerializeField] private string openingBeat = "intro";
        [SerializeField] private float delay = 0.75f;

        private void Start() => Invoke(nameof(PlayOpening), delay);

        private void PlayOpening() => DialogueUI.PlayBeat(openingBeat);
    }
}
