using System.Collections.Generic;
using SilverFang.Currency;
using SilverFang.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace SilverFang.UI
{
    /// Top-right notification ticker ("ALERTS &amp; NOTIFICATIONS"):
    /// SCEMA payouts, level ups, awakening ready.
    public class NotificationUI : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private Player.PlayerController player;

        private readonly Queue<(string text, float until)> lines = new Queue<(string, float)>();
        private long lastBalance = -1;
        private bool awakenedReadyShown;

        private void OnEnable()
        {
            lastBalance = ScematicaWallet.Balance;
            ScematicaWallet.OnBalanceChanged += OnBalance;
            PlayerProgression.OnLevelUp += OnLevelUp;
        }

        private void OnDisable()
        {
            ScematicaWallet.OnBalanceChanged -= OnBalance;
            PlayerProgression.OnLevelUp -= OnLevelUp;
        }

        private void OnBalance(long balance)
        {
            long delta = balance - lastBalance;
            lastBalance = balance;
            if (delta > 0) Push($"+{delta} {ScematicaToken.Symbol}");
        }

        private void OnLevelUp(int level) => Push($"LEVEL UP — LV {level}  (press C)");

        private void Push(string text)
        {
            lines.Enqueue((text, Time.unscaledTime + 2.6f));
            while (lines.Count > 4) lines.Dequeue();
        }

        private void Update()
        {
            if (player != null)
            {
                bool ready = !player.AwakenedActive && player.AwakenedMeter >= player.AwakenedMaxMeter;
                if (ready && !awakenedReadyShown) Push("AWAKENING READY — press F");
                awakenedReadyShown = ready;
            }

            while (lines.Count > 0 && Time.unscaledTime > lines.Peek().until)
                lines.Dequeue();

            if (label == null) return;
            var sb = new System.Text.StringBuilder();
            foreach (var (text, _) in lines) sb.AppendLine(text);
            label.text = sb.ToString();
        }
    }
}
