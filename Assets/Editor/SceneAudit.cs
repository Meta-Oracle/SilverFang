using System.Linq;
using SilverFang.Combat;
using SilverFang.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SilverFang.EditorTools
{
    /// Batch-mode diagnostic: opens Main.unity and reports the structural
    /// health of everything the game needs to render heroes and enemies.
    public static class SceneAudit
    {
        [MenuItem("SilverFang/Audit Scene")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");

            foreach (var name in new[] { "Player", "Hilo" })
            {
                var go = FindAnyIncludingInactive(name);
                if (go == null) { Debug.Log($"AUDIT {name}: MISSING FROM SCENE"); continue; }
                AuditCharacter(name, go);
            }

            foreach (var ai in Object.FindObjectsByType<Enemies.EnemyAI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                AuditCharacter(ai.gameObject.name, ai.gameObject);

            // character select wiring
            var select = FindAnyIncludingInactive("CharacterSelect");
            if (select == null) Debug.Log("AUDIT CharacterSelect: MISSING");
            else
            {
                var ui = select.GetComponent<UI.CharacterSelectUI>();
                var so = new SerializedObject(ui);
                foreach (var field in new[] { "silverRoot", "hiloRoot", "silverHud", "hiloHud" })
                {
                    var p = so.FindProperty(field);
                    Debug.Log($"AUDIT select.{field}: " +
                        (p != null && p.objectReferenceValue != null ? "ok" : "NULL"));
                }
                var slicesProp = so.FindProperty("slices");
                Debug.Log("AUDIT select.slices: " + (slicesProp != null ? slicesProp.arraySize + " slices" : "NULL"));
            }

            var crawl = FindAnyIncludingInactive("IntroCrawl");
            Debug.Log("AUDIT IntroCrawl: " + (crawl != null ? $"present active={crawl.activeSelf}" : "MISSING"));

            var cam = Object.FindAnyObjectByType<Core.CameraFollow>(FindObjectsInactive.Include);
            Debug.Log("AUDIT CameraFollow: " + (cam != null ? "present" : "MISSING"));

            ValidateMoves();
        }

        /// Verifies every combo route end to end: each move in every move set
        /// must have its animator trigger AND a baked clip with sprite art.
        [MenuItem("SilverFang/Validate Moves")]
        public static void ValidateMoves()
        {
            CheckMoves("Player", "PlayerAnimator", new[] { "Sword", "Gun", "Dash", "Sprint", "Teleport", "Air" });
            CheckMoves("Awakened", "AwakenedAnimator", new[] { "Awakened", "Teleport", "AwakenedAir" });
            CheckMoves("Hilo", "HiloAnimator", new[] { "HiloCombo", "HiloEnergy", "HiloDash", "HiloSprint", "HiloTeleport", "HiloAir" });
            CheckMoves("HiloAwakened", "HiloAwakenedAnimator", new[] { "HiloAwakened", "HiloTeleport", "HiloAwakenedAir" });
            CheckMoves("Lucas", "LucasAnimator", new[] { "LucasCombo", "LucasGun", "LucasDash", "LucasSprint", "LucasTeleport", "LucasAir" });
        }

        private static void CheckMoves(string clipPrefix, string controllerName, string[] setNames)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                $"Assets/Art/Animations/{controllerName}.controller");
            if (controller == null)
            {
                Debug.LogError($"MOVES {clipPrefix}: controller {controllerName} MISSING");
                return;
            }
            var pars = new System.Collections.Generic.HashSet<string>(
                controller.parameters.Select(p => p.name));

            int ok = 0, missing = 0;
            foreach (var setName in setNames)
            {
                var set = AssetDatabase.LoadAssetAtPath<MoveSet>($"Assets/Data/{setName}MoveSet.asset");
                if (set == null)
                {
                    Debug.LogError($"MOVES {clipPrefix}: set {setName} MISSING");
                    continue;
                }
                foreach (var move in set.moves)
                {
                    bool hasParam = pars.Contains(move.animatorTrigger);
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                        $"Assets/Art/Animations/{clipPrefix}_{move.id}.anim");
                    bool hasArt = clip != null
                        && AnimationUtility.GetObjectReferenceCurveBindings(clip).Length > 0;
                    if (hasParam && hasArt) ok++;
                    else
                    {
                        missing++;
                        Debug.LogWarning($"MOVES {clipPrefix} {setName}/{move.id}: param={hasParam} art={hasArt}");
                    }
                }
            }
            Debug.Log($"MOVES {clipPrefix}: {ok} routes ok, {missing} missing");
        }

        private static GameObject FindAnyIncludingInactive(string name)
        {
            return Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(g => g.name == name && g.scene.isLoaded
                    && !EditorUtility.IsPersistent(g));
        }

        private static void AuditCharacter(string label, GameObject go)
        {
            var visual = go.transform.Find("Visual");
            var sr = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
            var animator = visual != null ? visual.GetComponent<Animator>() : null;
            var controller = animator != null ? animator.runtimeAnimatorController : null;

            string clipInfo = "no controller";
            if (controller != null)
            {
                var ac = controller as AnimatorController;
                var idleState = ac != null
                    ? ac.layers[0].stateMachine.states.FirstOrDefault(s => s.state.name == "Idle").state
                    : null;
                var clip = idleState != null ? idleState.motion as AnimationClip : null;
                if (clip == null) clipInfo = $"{controller.name}: NO IDLE CLIP";
                else
                {
                    var curves = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                    int keys = curves.Length > 0
                        ? AnimationUtility.GetObjectReferenceCurve(clip, curves[0])
                            .Count(k => k.value != null)
                        : 0;
                    clipInfo = $"{controller.name}, idle clip '{clip.name}' spriteKeys={keys}";
                }
            }

            Debug.Log($"AUDIT {label}: active={go.activeSelf} pos={go.transform.position} "
                + $"visualScale={(visual != null ? visual.localScale.ToString() : "NO VISUAL")} "
                + $"sprite={(sr != null && sr.sprite != null ? sr.sprite.name : "NULL")} "
                + $"srEnabled={(sr != null && sr.enabled)} order={(sr != null ? sr.sortingOrder : 0)} "
                + $"anim=[{clipInfo}]");
        }
    }
}
