using UnityEngine;

namespace SilverFang.Combat
{
    /// Animation events only reach components on the Animator's own GameObject.
    /// This sits next to the Animator (Visual child) and forwards to the combatant on the root.
    public class AnimationEventRelay : MonoBehaviour
    {
        private CharacterCombatant combatant;

        private void Awake()
        {
            combatant = GetComponentInParent<CharacterCombatant>();
        }

        public void AnimEvent_HitboxOn() => combatant?.AnimEvent_HitboxOn();
        public void AnimEvent_HitboxOff() => combatant?.AnimEvent_HitboxOff();
        public void AnimEvent_AttackEnd() => combatant?.AnimEvent_AttackEnd();
        public void AnimEvent_Fire() => combatant?.AnimEvent_Fire();
    }
}
