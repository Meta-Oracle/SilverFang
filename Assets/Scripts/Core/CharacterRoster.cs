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
    }
}
