using System.Collections.Generic;
using System.Linq;
using SilverFang.Enemies;
using UnityEngine;

namespace SilverFang.Core
{
    /// Locks the camera when the player enters, activates enemies, unlocks when all are dead.
    [RequireComponent(typeof(Collider2D))]
    public class EncounterZone : MonoBehaviour
    {
        [SerializeField] private List<EnemyAI> enemies = new List<EnemyAI>();
        [SerializeField] private float cameraLockX;
        [SerializeField] private int scematicaClearBonus = 25;
        [SerializeField] private int xpClearBonus = 40;
        [SerializeField] private string startBeat;
        [SerializeField] private string clearBeat;

        private CameraFollow cam;
        private bool triggered;

        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
            cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            foreach (var e in enemies.Where(e => e != null))
                e.gameObject.SetActive(false);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (triggered) return;
            if (other.GetComponentInParent<Player.PlayerController>() == null) return;

            triggered = true;
            if (cam != null) cam.LockAt(cameraLockX);
            foreach (var e in enemies.Where(e => e != null))
                e.gameObject.SetActive(true);
            Story.DialogueUI.PlayBeat(startBeat);
        }

        private void Update()
        {
            if (!triggered) return;
            if (enemies.Any(e => e != null && !e.IsDead)) return;

            if (cam != null) cam.Unlock();
            Currency.ScematicaWallet.Earn(scematicaClearBonus);
            Progression.PlayerProgression.AddXp(xpClearBonus);
            Story.DialogueUI.PlayBeat(clearBeat);
            enabled = false;
        }
    }
}
