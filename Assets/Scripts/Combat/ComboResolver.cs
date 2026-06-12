using System.Collections.Generic;
using SilverFang.Player;

namespace SilverFang.Combat
{
    /// Matches sequences of input tokens against a MoveSet.
    /// Pressing L,L,H resolves a different move than H,L,L.
    public class ComboResolver
    {
        private readonly List<InputToken> buffer = new List<InputToken>();
        private MoveSet moveSet;
        private float lastTokenTime = float.MinValue;

        public IReadOnlyList<InputToken> CurrentString => buffer;

        public ComboResolver(MoveSet set) => moveSet = set;

        public void SetMoveSet(MoveSet set)
        {
            moveSet = set;
            Reset();
        }

        public void Reset() => buffer.Clear();

        public MoveDefinition OnToken(InputToken token, float time, float chainWindow)
        {
            if (moveSet == null) return null;

            if (buffer.Count > 0 && time - lastTokenTime > chainWindow)
                buffer.Clear();
            lastTokenTime = time;

            buffer.Add(token);
            var move = moveSet.Match(buffer);

            if (move == null && !moveSet.HasContinuation(buffer))
            {
                // Dead-end string: restart with this token as a fresh opener.
                buffer.Clear();
                buffer.Add(token);
                move = moveSet.Match(buffer);
                if (move == null && !moveSet.HasContinuation(buffer)) buffer.Clear();
            }

            // If the string completed a move that nothing longer extends, restart next press fresh.
            if (move != null && !moveSet.HasContinuation(buffer))
                buffer.Clear();

            return move;
        }
    }
}
