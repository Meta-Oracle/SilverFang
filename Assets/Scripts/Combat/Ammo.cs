using UnityEngine;

namespace SilverFang.Combat
{
    public enum AmmoType
    {
        Standard,
        Nuclear,
        Ice,
        Incendiary,
        Piercing
    }

    [System.Serializable]
    public class AmmoDefinition
    {
        public AmmoType type = AmmoType.Standard;
        public AttackData attack = new AttackData();
        public Color tint = Color.white;
        public float speed = 14f;
        public bool piercing;
        public StatusType status = StatusType.None;
        public float statusDuration;
    }
}
