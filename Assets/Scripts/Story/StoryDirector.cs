using UnityEngine;

namespace SilverFang.Story
{
    /// Kicks off the opening dialogue. Beats only ever play once per save
    /// (StoryManager), so this is safe across scene reloads.
    public class StoryDirector : MonoBehaviour
    {
        [SerializeField] private string openingBeat = "intro";
        [SerializeField] private string hiloOpeningBeat = "hilo_intro";
        [SerializeField] private float delay = 0.75f;

        private void Start() => Invoke(nameof(PlayOpening), delay);

        private void PlayOpening()
        {
            // Hilo's story runs in parallel from the start: she gets her own
            // opening beat when she's the selected hero.
            bool hilo = Core.CharacterRoster.Selected == Core.PlayableCharacter.Hilo;
            DialogueUI.PlayBeat(hilo ? hiloOpeningBeat : openingBeat);
        }
    }
}
