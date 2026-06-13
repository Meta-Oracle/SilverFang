namespace SilverFang.Core
{
    public enum PlayableCharacter
    {
        Silver,
        Hilo
    }

    /// Which hero the player picked on the start screen. Survives scene loads.
    public static class CharacterRoster
    {
        public static PlayableCharacter Selected = PlayableCharacter.Silver;
        public static bool SelectionMade;

        /// Set by the pause menu's Restart: on the next scene load the intro
        /// crawl and character select auto-advance straight into gameplay with
        /// the already-chosen hero. "Quit to Intro" clears it so the crawl and
        /// select screen play normally again.
        public static bool QuickRestart;
    }
}
