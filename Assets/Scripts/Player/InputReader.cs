using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SilverFang.Player
{
    /// Polls keyboard + gamepad.
    /// Gamepad: West=Light, North=Heavy, East=Gun, South=Jump,
    ///          LB=stance, RB=ammo, LT=Lock-On (hold), RT=Awakened.
    /// Keyboard: J=Light, K=Heavy, L=Gun, Space=Jump, Q=stance, E=ammo,
    ///           F=Awakened, LeftShift=Lock-On (hold), WASD/arrows=move.
    public class InputReader
    {
        private const float TriggerThreshold = 0.5f;
        private bool triggerHeld;
        private bool stickNeutral = true;

        public Vector2 Move
        {
            get
            {
                Vector2 v = Vector2.zero;
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1f;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1f;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1f;
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1f;
                }
                var pad = Gamepad.current;
                if (pad != null && v == Vector2.zero)
                {
                    v = pad.leftStick.ReadValue();
                    if (v.magnitude < 0.2f) v = pad.dpad.ReadValue();
                }
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        /// Horizontal tap this frame: -1 / 0 / +1. Keyboard presses, d-pad
        /// presses, or the stick crossing out of neutral. Poll once per frame
        /// (the stick edge-detect is stateful).
        public int MoveTapDir
        {
            get
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) return 1;
                    if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) return -1;
                }
                var pad = Gamepad.current;
                if (pad != null)
                {
                    if (pad.dpad.right.wasPressedThisFrame) return 1;
                    if (pad.dpad.left.wasPressedThisFrame) return -1;
                    float x = pad.leftStick.ReadValue().x;
                    if (stickNeutral && Mathf.Abs(x) > 0.6f)
                    {
                        stickNeutral = false;
                        return x > 0f ? 1 : -1;
                    }
                    if (Mathf.Abs(x) < 0.25f) stickNeutral = true;
                }
                return 0;
            }
        }

        public bool LightPressed => KeyDown(Keyboard.current?.jKey) || PadDown(Gamepad.current?.buttonWest);
        public bool HeavyPressed => KeyDown(Keyboard.current?.kKey) || PadDown(Gamepad.current?.buttonNorth);
        public bool GunPressed => KeyDown(Keyboard.current?.lKey) || PadDown(Gamepad.current?.buttonEast);
        public bool JumpPressed => KeyDown(Keyboard.current?.spaceKey) || PadDown(Gamepad.current?.buttonSouth);

        // Held states drive the charge mechanics (charge jump / charged attacks).
        public bool LightHeld => KeyHeld(Keyboard.current?.jKey) || PadHeld(Gamepad.current?.buttonWest);
        public bool HeavyHeld => KeyHeld(Keyboard.current?.kKey) || PadHeld(Gamepad.current?.buttonNorth);
        public bool GunHeld => KeyHeld(Keyboard.current?.lKey) || PadHeld(Gamepad.current?.buttonEast);
        public bool JumpHeld => KeyHeld(Keyboard.current?.spaceKey) || PadHeld(Gamepad.current?.buttonSouth);
        public bool AmmoPressed => KeyDown(Keyboard.current?.eKey) || PadDown(Gamepad.current?.rightShoulder);
        /// CQC grapple/clinch trigger (V / right-stick click).
        public bool GrabPressed => KeyDown(Keyboard.current?.vKey) || PadDown(Gamepad.current?.rightStickButton);
        /// Lucas gadget throw (B / left-stick click) — rotates through the 4 gadgets.
        public bool GadgetPressed => KeyDown(Keyboard.current?.bKey) || PadDown(Gamepad.current?.leftStickButton);

        // Stance button is dual-use: a quick TAP switches stance, a HOLD guards
        // (Q / L1). Keyboard Left-Ctrl is a dedicated guard too. Tick() drives
        // the tap-vs-hold split; read StanceTapped / GuardHeld after.
        private const float TapWindow = 0.2f;
        private float qHeld, l1Held;
        public bool StanceTapped { get; private set; }
        public bool GuardHeld { get; private set; }

        // Fighting-game motion buffer: forward-relative numpad directions with
        // timestamps, so QCF (236) / QCB (214) specials can be detected.
        private readonly System.Collections.Generic.List<(int dir, float t)> motion
            = new System.Collections.Generic.List<(int, float)>();
        private const float MotionWindow = 0.55f; // forgiving directional-special window

        public void Tick(float facing = 1f)
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool qNow = kb != null && kb.qKey.isPressed;
            bool l1Now = pad != null && pad.leftShoulder.isPressed;
            StanceTapped = false;

            if (qNow) qHeld += Time.unscaledDeltaTime;
            else { if (qHeld > 0f && qHeld < TapWindow) StanceTapped = true; qHeld = 0f; }
            if (l1Now) l1Held += Time.unscaledDeltaTime;
            else { if (l1Held > 0f && l1Held < TapWindow) StanceTapped = true; l1Held = 0f; }

            GuardHeld = (kb != null && kb.leftCtrlKey.isPressed)
                        || qHeld >= TapWindow || l1Held >= TapWindow;

            int dir = CurrentNumpad(facing);
            if (motion.Count == 0 || motion[motion.Count - 1].dir != dir)
                motion.Add((dir, Time.unscaledTime));
            motion.RemoveAll(m => Time.unscaledTime - m.t > MotionWindow);
        }

        /// Forward-relative numpad direction (6 = toward facing, 4 = away).
        private int CurrentNumpad(float facing)
        {
            Vector2 m = Move;
            int h = m.x * Mathf.Sign(facing) > 0.4f ? 1 : m.x * Mathf.Sign(facing) < -0.4f ? -1 : 0;
            int v = m.y > 0.4f ? 1 : m.y < -0.4f ? -1 : 0;
            if (v < 0) return h > 0 ? 3 : h < 0 ? 1 : 2;
            if (v > 0) return h > 0 ? 9 : h < 0 ? 7 : 8;
            return h > 0 ? 6 : h < 0 ? 4 : 5;
        }

        /// True if the recent direction history contains the motion in order,
        /// ending now (consumes the buffer so it can't double-fire).
        public bool ConsumeMotion(params int[] seq)
        {
            int si = 0;
            foreach (var m in motion)
            {
                if (m.dir == seq[si])
                {
                    si++;
                    if (si == seq.Length) { motion.Clear(); return true; }
                }
            }
            return false;
        }

        public bool AwakenedPressed
        {
            get
            {
                if (KeyDown(Keyboard.current?.fKey)) return true;

                var pad = Gamepad.current;
                if (pad == null) return false;
                bool held = pad.rightTrigger.ReadValue() > TriggerThreshold;
                bool pressed = held && !triggerHeld;
                triggerHeld = held;
                return pressed;
            }
        }

        /// Hold to stay locked onto the nearest enemy (LT / Left Shift).
        public bool LockOnHeld =>
            KeyHeld(Keyboard.current?.leftShiftKey)
            || (Gamepad.current != null && Gamepad.current.leftTrigger.ReadValue() > TriggerThreshold);

        private static bool KeyDown(KeyControl key) => key != null && key.wasPressedThisFrame;
        private static bool PadDown(UnityEngine.InputSystem.Controls.ButtonControl b) => b != null && b.wasPressedThisFrame;
        private static bool KeyHeld(KeyControl key) => key != null && key.isPressed;
        private static bool PadHeld(UnityEngine.InputSystem.Controls.ButtonControl b) => b != null && b.isPressed;
    }
}
