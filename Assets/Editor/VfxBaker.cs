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
            ("dash_dust",      20f, 0.6f, false),
            ("charge_debris",  20f, 1.3f, true),
            ("hilo_beam_fire", 22f, 1.1f, false),
            ("hilo_beam_impact", 20f, 1.2f, true),
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
            ("status_radiated", 10f, 1.0f, false),

            // --- Impact VFX System library (vfxtotaloverhaul) ---
            // Scales cut to ~40% (−60%) for grounded, realistically-sized hits.
            // Bloody Combat Engine tiers (flesh blood / metal sparks)
            ("blood_light",    14f, 0.36f, true),
            ("blood_heavy",    14f, 0.48f, true),
            ("blood_crit",     14f, 0.60f, true),
            ("spark_light",    16f, 0.36f, true),
            ("spark_heavy",    16f, 0.48f, true),
            ("spark_crit",     16f, 0.60f, true),
            // Psionic (Hilo) — rendered behind the target
            ("psi_impact",     16f, 0.48f, true),
            ("psi_behind",     14f, 0.60f, true),
            ("psi_crit",       14f, 0.68f, true),
            // Electric (Lucas / Big Man) — overlaid on the target
            ("electric_impact",   18f, 0.44f, true),
            ("electric_lingering",16f, 0.48f, false),
            ("electric_bigman",   18f, 0.56f, true),
            // Silver blue aura — overlaid on top
            ("blue_aura_impact", 16f, 0.48f, true),
            ("blue_aura_slash",  18f, 0.44f, false),
            ("blue_aura_crit",   14f, 0.64f, true),
            // Elemental projectile impacts
            ("impact_ballistic",  18f, 0.36f, true),
            ("impact_nuclear",    16f, 0.52f, true),
            ("impact_incendiary", 16f, 0.48f, true),
            ("impact_ice",        16f, 0.44f, true),
            // CQC throws/slams — a powerful shockwave (dedicated category)
            ("throw_impact",      20f, 1.1f, false)
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
                float fps = tune.fps > 0 ? tune.fps : 15f;
                float scale = tune.scale > 0 ? tune.scale : 1f;
                bool randomRot = tune.randomRot;
                // RNG variant pools (blood_N / spark_N / psi_N / electric_N /
                // blue_N / slash_N) get subtle, category-based defaults.
                if (tune.id == null)
                {
                    if (id.StartsWith("blood_"))    { fps = 14f; scale = 0.45f; randomRot = true; }
                    else if (id.StartsWith("spark_")){ fps = 16f; scale = 0.45f; randomRot = true; }
                    else if (id.StartsWith("psi_"))  { fps = 16f; scale = 0.50f; randomRot = true; }
                    else if (id.StartsWith("electric_")) { fps = 18f; scale = 0.50f; randomRot = true; }
                    else if (id.StartsWith("blue_")) { fps = 16f; scale = 0.50f; randomRot = true; }
                    else if (id.StartsWith("slash_")){ fps = 18f; scale = 0.55f; randomRot = false; }
                }
                library.entries.Add(new VfxLibrary.Entry
                {
                    id = id,
                    frames = frames,
                    fps = fps,
                    scale = scale,
                    randomRotation = randomRot
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

            // Feed the standalone flying-debris system its sprite frames.
            var debris = GameObject.Find("DebrisManager");
            if (debris != null)
            {
                var frames = System.IO.Directory.Exists($"{VfxDir}/charge_debris")
                    ? System.IO.Directory.GetFiles($"{VfxDir}/charge_debris", "*.png")
                        .OrderBy(p => p)
                        .Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p.Replace('\\', '/')))
                        .Where(s => s != null).ToArray()
                    : new Sprite[0];
                var dso = new SerializedObject(debris.GetComponent<SilverFang.VFX.DebrisManager>());
                var arr = dso.FindProperty("debrisSprites");
                arr.arraySize = frames.Length;
                for (int i = 0; i < frames.Length; i++)
                    arr.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                dso.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.SaveScene(scene);
        }

        private static void SetProjectileSprite()
        {
            // NOTE: the main bullet keeps the NEUTRAL WHITE placeholder from
            // CreateProjectilePrefab so Projectile.Fire's ammo tint paints the
            // true round colour (white kinetic, green nuclear, cyan ice, etc.).
            // We do NOT override it with a coloured tracer sprite — that made
            // every shot read red regardless of ammo type.

            // Hilo's beam round uses the real arm-cannon beam sprite.
            var beam = AssetDatabase.LoadAssetAtPath<Sprite>($"{VfxDir}/hilo_beam/hilo_beam_00.png");
            string beamPath = "Assets/Prefabs/HiloBeam.prefab";
            if (beam != null && AssetDatabase.LoadAssetAtPath<GameObject>(beamPath) != null)
            {
                var broot = PrefabUtility.LoadPrefabContents(beamPath);
                try
                {
                    var bsr = broot.GetComponentInChildren<SpriteRenderer>();
                    bsr.sprite = beam;
                    PrefabUtility.SaveAsPrefabAsset(broot, beamPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(broot); }
            }

            // Silver's launched slash projectile uses the real crescent slash VFX.
            var slash = AssetDatabase.LoadAssetAtPath<Sprite>($"{VfxDir}/blue_aura_slash/blue_aura_slash_00.png");
            string slashPath = "Assets/Prefabs/SlashWave.prefab";
            if (slash != null && AssetDatabase.LoadAssetAtPath<GameObject>(slashPath) != null)
            {
                var sroot = PrefabUtility.LoadPrefabContents(slashPath);
                try
                {
                    var ssr = sroot.GetComponentInChildren<SpriteRenderer>();
                    ssr.sprite = slash;
                    ssr.color = Color.white; // ammo tint applied at Fire()
                    PrefabUtility.SaveAsPrefabAsset(sroot, slashPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(sroot); }
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
