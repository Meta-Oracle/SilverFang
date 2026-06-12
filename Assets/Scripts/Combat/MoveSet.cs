using System.Collections.Generic;
using SilverFang.Player;
using UnityEngine;

namespace SilverFang.Combat
{
    public enum TeleportKind
    {
        None,
        BehindTarget,
        ForwardDash
    }

    [System.Serializable]
    public class MoveDefinition
    {
        public string id;
        public string animatorTrigger;
        public InputToken[] sequence;
        public AttackData attack;
        public bool firesProjectile;
        /// Launches a sword-wave projectile on the contact frame (in addition to the melee hit).
        public bool firesSlashWave;
        public TeleportKind teleport = TeleportKind.None;
        /// Animation clip length; used as a watchdog so a missed AttackEnd event can't lock the character.
        public float duration = 0.4f;
    }

    [CreateAssetMenu(menuName = "SilverFang/Move Set")]
    public class MoveSet : ScriptableObject
    {
        public List<MoveDefinition> moves = new List<MoveDefinition>();

        public MoveDefinition Match(IReadOnlyList<InputToken> buffer)
        {
            foreach (var move in moves)
            {
                if (move.sequence == null || move.sequence.Length != buffer.Count) continue;
                bool match = true;
                for (int i = 0; i < buffer.Count; i++)
                {
                    if (move.sequence[i] != buffer[i]) { match = false; break; }
                }
                if (match) return move;
            }
            return null;
        }

        /// True if some move's sequence starts with the buffer (a longer chain is still possible).
        public bool HasContinuation(IReadOnlyList<InputToken> buffer)
        {
            foreach (var move in moves)
            {
                if (move.sequence == null || move.sequence.Length <= buffer.Count) continue;
                bool prefix = true;
                for (int i = 0; i < buffer.Count; i++)
                {
                    if (move.sequence[i] != buffer[i]) { prefix = false; break; }
                }
                if (prefix) return true;
            }
            return false;
        }
    }
}
