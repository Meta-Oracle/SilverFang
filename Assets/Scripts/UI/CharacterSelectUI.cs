using SilverFang.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SilverFang.UI
{
    /// Start screen, vertical-slice roster. Each hero is a tall masked slice of
    /// their dossier art; the highlighted one UNFOLDS — sliding open to reveal
    /// the full dossier — while the others stay narrow slivers. Built entirely
    /// from the dossier images (no live text/stat overlays).
    /// Controls: A/D or arrows / stick to move, J / Enter / South to confirm.
    public class CharacterSelectUI : MonoBehaviour
    {
        [SerializeField] private GameObject silverRoot;
        [SerializeField] private GameObject hiloRoot;
        [SerializeField] private GameObject lucasRoot;
        [SerializeField] private GameObject silverHud;
        [SerializeField] private GameObject hiloHud;
        [SerializeField] private GameObject lucasHud;
        [SerializeField] private GameObject panel;
        [SerializeField] private RectTransform[] slices;   // Silver, Hilo, (Lucas teaser)
        [SerializeField] private float sliceWidth = 150f;   // collapsed sliver width
        [SerializeField] private float dossierWidth = 1000f; // unfolded full-dossier width
        [SerializeField] private int selectableCount = 2;   // playable heroes; extras are view-only teasers
        [SerializeField] private float unfoldSpeed = 9f;

        private int index;
        private bool done;
        private float stickCooldown;
        private float[] curW;

        private void Start()
        {
            if (CharacterRoster.QuickRestart)
            {
                CharacterRoster.QuickRestart = false;
                Choose(CharacterRoster.Selected);
                return;
            }
            GamePause.TryAcquire(this);

            int n = slices != null ? slices.Length : 0;
            curW = new float[n];
            for (int i = 0; i < n; i++) curW[i] = sliceWidth;
            if (n > 0) curW[index] = dossierWidth;
            Layout(true);
        }

        private void Update()
        {
            if (done) return;

            int dir = ReadDirection();
            if (dir != 0 && slices != null && slices.Length > 0)
                index = Mathf.Clamp(index + dir, 0, slices.Length - 1);

            Layout(false);

            // Only playable heroes confirm; teaser slices just preview.
            if (ConfirmPressed() && index < selectableCount)
                Choose(index == 0 ? PlayableCharacter.Silver
                     : index == 1 ? PlayableCharacter.Hilo
                     : PlayableCharacter.Lucas);
        }

        /// Animate the slices toward their unfolded/collapsed widths and lay the
        /// row out centered, so the selected hero slides open.
        private void Layout(bool snap)
        {
            if (slices == null || slices.Length == 0 || curW == null) return;
            float t = snap ? 1f : Mathf.Clamp01(unfoldSpeed * Time.unscaledDeltaTime);

            float rowTotal = 0f;
            for (int i = 0; i < slices.Length; i++)
            {
                float target = i == index ? dossierWidth : sliceWidth;
                curW[i] = Mathf.Lerp(curW[i], target, t);
                rowTotal += curW[i];
            }

            float x = -rowTotal * 0.5f;
            for (int i = 0; i < slices.Length; i++)
            {
                var s = slices[i];
                if (s == null) continue;
                s.anchorMin = new Vector2(0.5f, 0f);
                s.anchorMax = new Vector2(0.5f, 1f);
                s.pivot = new Vector2(0f, 0.5f);
                s.anchoredPosition = new Vector2(x, 0f);
                s.sizeDelta = new Vector2(curW[i], 0f);
                x += curW[i];
            }
        }

        private int ReadDirection()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) return -1;
                if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) return 1;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.dpad.left.wasPressedThisFrame) return -1;
                if (pad.dpad.right.wasPressedThisFrame) return 1;
                stickCooldown -= Time.unscaledDeltaTime;
                float x = pad.leftStick.ReadValue().x;
                if (stickCooldown <= 0f && Mathf.Abs(x) > 0.6f)
                {
                    stickCooldown = 0.3f;
                    return x > 0f ? 1 : -1;
                }
            }
            return 0;
        }

        private bool ConfirmPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.jKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame
                || kb.spaceKey.wasPressedThisFrame)) return true;
            var pad = Gamepad.current;
            return pad != null && pad.buttonSouth.wasPressedThisFrame;
        }

        public void Choose(PlayableCharacter character)
        {
            if (done) return;
            done = true;

            CharacterRoster.Selected = character;
            CharacterRoster.SelectionMade = true;

            int pick = (int)character; // Silver=0, Hilo=1, Lucas=2
            var roots = new[] { silverRoot, hiloRoot, lucasRoot };
            var huds = new[] { silverHud, hiloHud, lucasHud };

            var hero = roots[pick];
            var hud = huds[pick];
            if (hero != null) hero.SetActive(true);
            if (hud != null) hud.SetActive(true);
            // bench (and free) every hero that wasn't chosen
            for (int i = 0; i < roots.Length; i++)
            {
                if (i == pick) continue;
                if (roots[i] != null) Destroy(roots[i]);
                if (huds[i] != null) Destroy(huds[i]);
            }

            if (hero != null && CameraFollow.Instance != null)
                CameraFollow.Instance.SetTarget(hero.transform);

            GamePause.Release(this);
            Destroy(gameObject);
        }
    }
}
