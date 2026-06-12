using UnityEngine;

namespace SilverFang.Combat
{
    /// Receives hits. Place on a child with a trigger Collider2D.
    [RequireComponent(typeof(Collider2D))]
    public class Hurtbox : MonoBehaviour
    {
        [SerializeField] private Team team;

        public Team Team => team;
        public CharacterCombatant Owner { get; private set; }

        private void Awake()
        {
            Owner = GetComponentInParent<CharacterCombatant>();
            GetComponent<Collider2D>().isTrigger = true;
        }
    }
}
