using SilverFang.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SilverFang.UI
{
    /// Start screen: pick Silver or Hilo. Owns the game pause until a hero is
    /// chosen, then activates that hero + their HUD, retargets the camera, and
    /// removes itself (and the unused hero) from the scene.
    /// Controls: A/D or arrows / stick to highlight, J / Enter / South to confirm.
    public class CharacterSelectUI : MonoBehaviour
    {
        [SerializeField] private GameObject silverRoot;
        [SerializeField] private GameObject hiloRoot;
        [SerializeField] private GameObject silverHud;
        [SerializeField] private GameObject hiloHud;
        [SerializeField] private GameObject panel;
        [SerializeField] private Image silverFrame;
        [SerializeField] private Image hiloFrame;
        [SerializeField] private Text hintText;

        // translucent: these tint the baked select-screen art panels
        private static readonly Color Highlight = new Color(1f, 0.92f, 0.55f, 0.22f);
        private static readonly Color Dim = new Color(0.1f, 0.1f, 0.16f, 0.05f);

        private int index; // 0 = Silver, 1 = Hilo
        private bool done;
        private float stickCooldown;

        private void Start()
        {
            GamePause.TryAcquire(this);
            ApplyHighlight();
            if (hintText != null)
                hintText.text = "A / D  CHOOSE      J / ENTER / Ⓐ  CONFIRM";
        }

        private void Update()
        {
            if (done) return;

            int dir = ReadDirection();
            if (dir != 0)
            {
                index = Mathf.Clamp(index + dir, 0, 1);
                ApplyHighlight();
            }

            if (ConfirmPressed())
                Choose(index == 0 ? PlayableCharacter.Silver : PlayableCharacter.Hilo);
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

        private void ApplyHighlight()
        {
            if (silverFrame != null) silverFrame.color = index == 0 ? Highlight : Dim;
            if (hiloFrame != null) hiloFrame.color = index == 1 ? Highlight : Dim;
        }

        public void Choose(PlayableCharacter character)
        {
            if (done) return;
            done = true;

            CharacterRoster.Selected = character;
            CharacterRoster.SelectionMade = true;

            var hero = character == PlayableCharacter.Silver ? silverRoot : hiloRoot;
            var bench = character == PlayableCharacter.Silver ? hiloRoot : silverRoot;
            var hud = character == PlayableCharacter.Silver ? silverHud : hiloHud;
            var benchHud = character == PlayableCharacter.Silver ? hiloHud : silverHud;

            if (hero != null) hero.SetActive(true);
            if (bench != null) Destroy(bench);
            if (hud != null) hud.SetActive(true);
            if (benchHud != null) Destroy(benchHud);

            if (hero != null && CameraFollow.Instance != null)
                CameraFollow.Instance.SetTarget(hero.transform);

            GamePause.Release(this);
            Destroy(gameObject);
        }
    }
}
