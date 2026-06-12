using System;
using UnityEngine;

namespace SilverFang.Combat
{
    public class Health : MonoBehaviour
    {
        [SerializeField] private int maxHealth = 100;

        public int Max => maxHealth;
        public int Current { get; private set; }
        public bool IsDead => Current <= 0;

        public event Action<int, int> OnHealthChanged; // current, max
        public event Action OnDeath;

        private void Awake() => Current = maxHealth;

        public void TakeDamage(int amount)
        {
            if (IsDead) return;
            Current = Mathf.Max(0, Current - amount);
            OnHealthChanged?.Invoke(Current, maxHealth);
            if (IsDead) OnDeath?.Invoke();
        }

        public void Heal(int amount)
        {
            if (IsDead) return;
            Current = Mathf.Min(maxHealth, Current + amount);
            OnHealthChanged?.Invoke(Current, maxHealth);
        }

        /// Changes max health; increases also heal by the gained amount.
        public void SetMax(int newMax)
        {
            if (newMax <= 0 || newMax == maxHealth || IsDead) return;
            int delta = newMax - maxHealth;
            maxHealth = newMax;
            Current = delta > 0 ? Current + delta : Mathf.Min(Current, maxHealth);
            OnHealthChanged?.Invoke(Current, maxHealth);
        }
    }
}
