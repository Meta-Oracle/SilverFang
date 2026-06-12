using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilverFang.Story
{
    /// Persistent narrative state: which dialogue beats have played and
    /// which datashards have been collected. PlayerPrefs-backed, like the
    /// wallet and progression systems.
    public static class StoryManager
    {
        private const string KeyBeats = "sf_story_beats";
        private const string KeyShards = "sf_story_shards";
        private const int ShardScemaReward = 15;

        private static bool loaded;
        private static HashSet<string> playedBeats;
        private static HashSet<string> collectedShards;

        public static event Action OnChanged;

        /// True the first time a beat is requested; false on replays.
        public static bool TryMarkBeatPlayed(string beatId)
        {
            Load();
            if (string.IsNullOrEmpty(beatId) || playedBeats.Contains(beatId)) return false;
            playedBeats.Add(beatId);
            Save();
            return true;
        }

        public static bool IsCollected(string shardId)
        {
            Load();
            return collectedShards.Contains(shardId);
        }

        public static int CollectedCount { get { Load(); return collectedShards.Count; } }

        public static bool Collect(string shardId)
        {
            Load();
            if (LoreDatabase.FindEntry(shardId) == null || collectedShards.Contains(shardId)) return false;
            collectedShards.Add(shardId);
            Save();
            Currency.ScematicaWallet.Earn(ShardScemaReward);
            OnChanged?.Invoke();
            return true;
        }

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            playedBeats = Split(PlayerPrefs.GetString(KeyBeats, ""));
            collectedShards = Split(PlayerPrefs.GetString(KeyShards, ""));
        }

        private static HashSet<string> Split(string csv)
        {
            var set = new HashSet<string>();
            foreach (var id in csv.Split(';'))
                if (!string.IsNullOrEmpty(id)) set.Add(id);
            return set;
        }

        private static void Save()
        {
            PlayerPrefs.SetString(KeyBeats, string.Join(";", playedBeats));
            PlayerPrefs.SetString(KeyShards, string.Join(";", collectedShards));
        }
    }
}
