using System.IO;
using System.Linq;
using SilverFang.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SilverFang.EditorTools
{
    public static class VfxBaker
    {
        private const string VfxDir = "Assets/Art/Sprites/VFX";
        private const string LibraryPath = "Assets/Data/VfxLibrary.asset";

        // id -> (fps, scale, randomRotation)
        private static readonly (string id, float fps, float scale, bool randomRot)[] Tuning =
        {
            ("dash_dust",      20f, 1.0f, false),
        ("slash_effect",   18f, 1.0f, false),
            ("hit_spark",      18f, 1.0f, false),
            ("blood_splatter", 14f, 1.0f, true),
            ("bullet_impact",  18f, 0.8f, true),
            ("muzzle_burst",   20f, 0.7f, false),
            ("gun_trail",      18f, 1.0f, false),
            ("rage_aura",      10f, 1.0f, false),
            ("howl_aura",      10f, 1.0f, false),
            ("overdrive_aura", 10f, 1.0f, false),
            ("sword_trail_red", 18f, 1.0f, false),
            ("sword_trail_blue", 18f, 1.0f, false),
            ("bullet",         12f, 1.0f, false),
            ("status_frozen",  10f, 1.0f, false),
            ("status_burning", 12f, 1.0f, false),
            ("status_radiated", 10f, 1.0f, false)
        };

        [MenuItem("SilverFang/Bake VFX")]
        public static void BakeAll()
        {
            ImportVfxSprites();
            var library = BuildLibrary();
            AddManagerToScene(library);
            SetProjectileSprite();
            AssetDatabase.SaveAssets();
            Debug.Log("VfxBaker: done");
        }

        private static void ImportVfxSprites()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { VfxDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.alphaIsTransparency = true;
                importer.spritePixelsPerUnit = 48f;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        private static VfxLibrary BuildLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<VfxLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<VfxLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            library.entries.Clear();
            foreach (var dir in Directory.GetDirectories(VfxDir))
            {
                string id = Path.GetFileName(dir);
                var frames = Directory.GetFiles(dir, "*.png")
                    .OrderBy(p => p)
                    .Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p.Replace('\\', '/')))
                    .Where(s => s != null)
                    .ToArray();
                if (frames.Length == 0) continue;

                var tune = Tuning.FirstOrDefault(t => t.id == id);
                library.entries.Add(new VfxLibrary.Entry
                {
                    id = id,
                    frames = frames,
                    fps = tune.fps > 0 ? tune.fps : 15f,
                    scale = tune.scale > 0 ? tune.scale : 1f,
                    randomRotation = tune.randomRot
                });
            }

            AddEntryFromDir(library, "Assets/Art/Sprites/Awakened/vanish", "vanish", 18f, 1f);
            AddEntryFromDir(library, "Assets/Art/Sprites/Awakened/reappear", "reappear", 18f, 1f);
            AddEntryFromDir(library, "Assets/Art/Sprites/Awakened/transform", "transform_burst", 12f, 1.2f);

            EditorUtility.SetDirty(library);
            return library;
        }

        private static void AddEntryFromDir(VfxLibrary library, string dir, string id, float fps, float scale)
        {
            if (!Directory.Exists(dir)) return;
            var frames = Directory.GetFiles(dir, "*.png")
                .OrderBy(p => p)
                .Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p.Replace('\\', '/')))
                .Where(s => s != null)
                .ToArray();
            if (frames.Length == 0) return;
            library.entries.RemoveAll(e => e.id == id);
            library.entries.Add(new VfxLibrary.Entry { id = id, frames = frames, fps = fps, scale = scale });
        }

        private static void AddManagerToScene(VfxLibrary library)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            var existing = GameObject.Find("VfxManager");
            if (existing != null) Object.DestroyImmediate(existing);

            var go = new GameObject("VfxManager");
            var manager = go.AddComponent<VfxManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("library").objectReferenceValue = library;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
        }

        private static void SetProjectileSprite()
        {
            var bullet = AssetDatabase.LoadAssetAtPath<Sprite>($"{VfxDir}/bullet/bullet_00.png");
            if (bullet == null) return;

            var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Projectile.prefab");
            try
            {
                var sr = root.GetComponentInChildren<SpriteRenderer>();
                sr.sprite = bullet;
                PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Projectile.prefab");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    public static class MasterBake
    {
        /// Single entry point for CI: sprites, then VFX (both touch the scene).
        public static void All()
        {
            SpriteBaker.BakeAll();
            VfxBaker.BakeAll();
        }
    }
}
