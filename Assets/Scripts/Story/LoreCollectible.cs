using UnityEngine;

namespace SilverFang.Story
{
    /// World pickup for a datashard. Bobs in place; on player contact it is
    /// logged to the codex, pays a small SCEMA finder's fee, and shows the
    /// shard's excerpt. Already-collected shards do not respawn.
    [RequireComponent(typeof(Collider2D))]
    public class LoreCollectible : MonoBehaviour
    {
        [SerializeField] private string entryId;
        [SerializeField] private float bobAmplitude = 0.12f;
        [SerializeField] private float bobSpeed = 2.5f;

        private Vector3 basePos;

        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
            basePos = transform.position;
        }

        private void Start()
        {
            if (StoryManager.IsCollected(entryId)) Destroy(gameObject);
        }

        private void Update()
        {
            transform.position = basePos + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobAmplitude);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<Player.PlayerController>() == null) return;
            if (!StoryManager.Collect(entryId)) return;

            var entry = LoreDatabase.FindEntry(entryId);
            DialogueUI.PlayLines(
                new DialogueLine("DATASHARD", $"\"{entry.title}\" — {entry.excerpt}"),
                new DialogueLine("DATASHARD", $"Logged to codex ({StoryManager.CollectedCount}/{LoreDatabase.Entries.Length}). Press V to read."));

            VFX.VfxManager.Play("hit_spark", transform.position, 1f, 0.8f);
            Destroy(gameObject);
        }
    }
}
