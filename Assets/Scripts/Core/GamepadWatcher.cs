using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;

namespace SilverFang.Core
{
    /// Logs gamepad connect/disconnect and confirms DualSense (PS5) recognition.
    /// On a recognized DualShock/DualSense pad, tints the light bar silver so
    /// recognition is visible on the controller itself.
    public static class GamepadWatcher
    {
        private static readonly Color LightBarColor = new Color(0.75f, 0.75f, 0.8f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            InputSystem.onDeviceChange += OnDeviceChange;

            foreach (var pad in Gamepad.all)
                Report(pad, "connected");
        }

        private static void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (!(device is Gamepad pad)) return;

            switch (change)
            {
                case InputDeviceChange.Added:
                case InputDeviceChange.Reconnected:
                    Report(pad, change.ToString().ToLowerInvariant());
                    break;
                case InputDeviceChange.Removed:
                case InputDeviceChange.Disconnected:
                    Debug.Log($"[Input] Gamepad {change}: {pad.displayName}");
                    break;
            }
        }

        private static void Report(Gamepad pad, string evt)
        {
            string kind = pad.GetType().Name;
#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_WSA
            if (pad is DualSenseGamepadHID) kind = "DualSense (PS5)";
            else
#endif
            if (pad is DualShockGamepad) kind = "DualShock (PS4)";

            Debug.Log($"[Input] Gamepad {evt}: {pad.displayName} — recognized as {kind} (layout '{pad.layout}')");

            if (pad is DualShockGamepad ds)
                ds.SetLightBarColor(LightBarColor);
        }
    }
}
