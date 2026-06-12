using System.Collections.Generic;
using UnityEngine;

namespace SilverFang.VFX
{
    [CreateAssetMenu(menuName = "SilverFang/Vfx Library")]
    public class VfxLibrary : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public string id;
            public Sprite[] frames;
            public float fps = 15f;
            public float scale = 1f;
            public bool randomRotation;
        }

        public List<Entry> entries = new List<Entry>();

        public Entry Find(string id)
        {
            foreach (var e in entries)
                if (e.id == id) return e;
            return null;
        }
    }
}
