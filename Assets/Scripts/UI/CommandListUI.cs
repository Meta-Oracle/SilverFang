using System.Text;
using SilverFang.Combat;
using SilverFang.Core;
using SilverFang.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SilverFang.UI
{
    /// Multilayered pause menu (Esc / Tab / gamepad Options). Freezes the game.
    ///   Layer 1: option menu  - Resume, Command List, Restart, Quit to Intro.
    ///   Layer 2: command list - a scrollable reference split into two pages,
    ///            NORMAL ATTACKS and SPECIAL INPUT moves, each broken
    ///            into labelled sections. L1/R1 (or A/D) flip pages; W/S (or the
    ///            stick) scrolls. Content lives in a clipped ScrollRect so text
    ///            never bleeds outside its box.
    public class CommandListUI : MonoBehaviour
    {
        [SerializeField] private MoveSet swordMoveSet;
        [SerializeField] private MoveSet gunMoveSet;
        [SerializeField] private MoveSet awakenedMoveSet;
        [SerializeField] private MoveSet dashMoveSet;
        [SerializeField] private MoveSet sprintMoveSet;
        [SerializeField] private MoveSet teleportMoveSet;
        [SerializeField] private MoveSet airMoveSet;
        [SerializeField] private MoveSet awakenedAirMoveSet;
        [SerializeField] private MoveSet hiloComboMoveSet;
        [SerializeField] private MoveSet hiloEnergyMoveSet;
        [SerializeField] private MoveSet hiloAwakenedMoveSet;
        [SerializeField] private MoveSet hiloDashMoveSet;
        [SerializeField] private MoveSet hiloSprintMoveSet;
        [SerializeField] private MoveSet hiloTeleportMoveSet;
        [SerializeField] private MoveSet hiloAirMoveSet;
        [SerializeField] private MoveSet hiloAwakenedAirMoveSet;
        [SerializeField] private GameObject panel;          // command-list reference panel
        [SerializeField] private GameObject menuPanel;      // option menu
        [SerializeField] private Text menuText;
        [SerializeField] private Text titleText;
        [SerializeField] private Text tabText;              // "[ BASIC ]  SPECIAL"
        [SerializeField] private ScrollRect scrollRect;     // viewport for bodyText
        [SerializeField] private Text bodyText;             // scroll content
        [SerializeField] private Text balanceText;

        private static readonly string[] Options = { "RESUME", "COMMAND LIST", "RESTART", "QUIT TO INTRO" };
        private static readonly string[] Pages = { "NORMAL ATTACKS", "SPECIAL INPUT" };
        private enum Mode { Closed, Menu, CommandList }
        private Mode mode = Mode.Closed;
        private int cursor;
        private int page;
        private float repeat;

        private void Start()
        {
            if (panel != null) panel.SetActive(false);
            if (menuPanel != null) menuPanel.SetActive(false);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool pausePressed = (kb != null && (kb.tabKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
                             || (pad != null && pad.startButton.wasPressedThisFrame);

            switch (mode)
            {
                case Mode.Closed:
                    if (pausePressed && Core.GamePause.TryAcquire(this)) OpenMenu();
                    break;

                case Mode.CommandList:
                    if (pausePressed || Back(kb, pad)) { ShowMenu(); break; }
                    // L1/R1 or A/D flip pages
                    int pageDir = (Down(kb?.dKey) || Down(kb?.rightArrowKey) || Down(pad?.rightShoulder) ? 1 : 0)
                                - (Down(kb?.aKey) || Down(kb?.leftArrowKey) || Down(pad?.leftShoulder) ? 1 : 0);
                    if (pageDir != 0)
                    {
                        page = (page + pageDir + Pages.Length) % Pages.Length;
                        BuildText();
                        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f; // top
                    }
                    Scroll(kb, pad);
                    break;

                case Mode.Menu:
                    if (pausePressed) { Resume(); break; }
                    int dir = (Down(kb?.sKey) || Down(kb?.downArrowKey) || Down(pad?.dpad.down) ? 1 : 0)
                            - (Down(kb?.wKey) || Down(kb?.upArrowKey) || Down(pad?.dpad.up) ? 1 : 0);
                    if (dir != 0)
                    {
                        cursor = (cursor + dir + Options.Length) % Options.Length;
                        RenderMenu();
                    }
                    else if (Confirm(kb, pad))
                    {
                        Select();
                    }
                    break;
            }
        }

        /// Smooth held-scroll of the command-list body via W/S, up/down, stick, d-pad.
        private void Scroll(Keyboard kb, Gamepad pad)
        {
            if (scrollRect == null) return;
            float v = 0f;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v -= 1f;
            }
            if (pad != null)
            {
                v += pad.leftStick.ReadValue().y;
                if (pad.dpad.up.isPressed) v += 1f;
                if (pad.dpad.down.isPressed) v -= 1f;
            }
            if (Mathf.Abs(v) < 0.15f) return;

            // Speed scales with how much content overflows the viewport.
            float content = scrollRect.content != null ? scrollRect.content.rect.height : 0f;
            float view = scrollRect.viewport != null ? scrollRect.viewport.rect.height : 1f;
            float span = Mathf.Max(1f, content - view);
            float delta = v * 700f * Time.unscaledDeltaTime / span;
            scrollRect.verticalNormalizedPosition =
                Mathf.Clamp01(scrollRect.verticalNormalizedPosition + delta);
        }

        private static bool Down(UnityEngine.InputSystem.Controls.ButtonControl b) => b != null && b.wasPressedThisFrame;
        private static bool Confirm(Keyboard kb, Gamepad pad) =>
            Down(kb?.jKey) || Down(kb?.enterKey) || Down(kb?.spaceKey) || Down(pad?.buttonSouth);
        private static bool Back(Keyboard kb, Gamepad pad) => Down(pad?.buttonEast);

        private void OpenMenu()
        {
            cursor = 0;
            ShowMenu();
        }

        private void ShowMenu()
        {
            mode = Mode.Menu;
            if (panel != null) panel.SetActive(false);
            if (menuPanel != null) menuPanel.SetActive(true);
            RenderMenu();
        }

        private void RenderMenu()
        {
            if (menuText == null) return;
            bool hilo = CharacterRoster.Selected == PlayableCharacter.Hilo;
            var sb = new StringBuilder();
            sb.Append(hilo ? "== PAUSED - HILO ==\n\n" : "== PAUSED - SILVER ==\n\n");
            for (int i = 0; i < Options.Length; i++)
                sb.AppendLine(i == cursor ? $"> {Options[i]}" : $"   {Options[i]}");
            sb.Append('\n').Append($"{Currency.ScematicaWallet.Balance:N0} {Currency.ScematicaToken.Symbol}");
            sb.Append("\n\nW/S move   J select   Esc resume");
            menuText.text = sb.ToString();
        }

        private void Select()
        {
            switch (cursor)
            {
                case 0: Resume(); break;
                case 1: ShowCommandList(); break;
                case 2: ReloadScene(quickRestart: true); break;   // RESTART
                case 3: ReloadScene(quickRestart: false); break;  // QUIT TO INTRO
            }
        }

        private void ShowCommandList()
        {
            mode = Mode.CommandList;
            page = 0;
            BuildText(); // rebuilt per open: shows the selected hero's lists
            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
            if (balanceText != null)
                balanceText.text = $"{Currency.ScematicaWallet.Balance:N0} {Currency.ScematicaToken.Symbol}";
            if (menuPanel != null) menuPanel.SetActive(false);
            if (panel != null) panel.SetActive(true);
        }

        private void Resume()
        {
            mode = Mode.Closed;
            if (panel != null) panel.SetActive(false);
            if (menuPanel != null) menuPanel.SetActive(false);
            Core.GamePause.Release(this);
        }

        private void ReloadScene(bool quickRestart)
        {
            // Restart keeps the chosen hero and skips the intro; Quit to Intro
            // resets the selection so the crawl and select screen play again.
            CharacterRoster.QuickRestart = quickRestart;
            if (!quickRestart) CharacterRoster.SelectionMade = false;
            Core.GamePause.Release(this);
            Time.timeScale = 1f; // GamePause froze it; the new scene starts live
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void BuildText()
        {
            bool hilo = Core.CharacterRoster.Selected == Core.PlayableCharacter.Hilo;

            var mainSet = hilo && hiloComboMoveSet != null ? hiloComboMoveSet : swordMoveSet;
            var rangedSet = hilo && hiloEnergyMoveSet != null ? hiloEnergyMoveSet : gunMoveSet;
            var awkSet = hilo && hiloAwakenedMoveSet != null ? hiloAwakenedMoveSet : awakenedMoveSet;
            var dashSet = hilo && hiloDashMoveSet != null ? hiloDashMoveSet : dashMoveSet;
            var sprintSet = hilo && hiloSprintMoveSet != null ? hiloSprintMoveSet : sprintMoveSet;
            var tpSet = hilo && hiloTeleportMoveSet != null ? hiloTeleportMoveSet : teleportMoveSet;
            var airSet = hilo && hiloAirMoveSet != null ? hiloAirMoveSet : airMoveSet;
            var awkAirSet = hilo && hiloAwakenedAirMoveSet != null ? hiloAwakenedAirMoveSet : awakenedAirMoveSet;

            if (titleText != null) titleText.text = hilo ? "PAUSED - HILO" : "PAUSED - SILVER";
            if (tabText != null)
            {
                var t = new StringBuilder();
                for (int i = 0; i < Pages.Length; i++)
                {
                    if (i > 0) t.Append("      ");
                    t.Append(i == page ? $"[ {Pages[i]} ]" : Pages[i]);
                }
                t.Append("        (L1/R1 page  -  W/S scroll)");
                tabText.text = t.ToString();
            }

            var sb = new StringBuilder();
            if (page == 0) BuildBasicPage(sb, hilo, mainSet, rangedSet, airSet);
            else BuildSpecialPage(sb, hilo, dashSet, sprintSet, tpSet, awkSet, awkAirSet);

            if (bodyText != null)
            {
                bodyText.text = sb.ToString();
                // Resize the scroll content to the text so the viewport clips/
                // scrolls instead of letting the text spill past its box.
                LayoutRebuilder.ForceRebuildLayoutImmediate(bodyText.rectTransform);
            }
        }

        // ----- Page 1: plain button-press strings (no direction needed) --------
        private void BuildBasicPage(StringBuilder sb, bool hilo, MoveSet mainSet, MoveSet rangedSet, MoveSet airSet)
        {
            string mainHeader = hilo ? "CLAW STANCE COMBOS" : "SWORD STANCE COMBOS";
            string rangedHeader = hilo ? "ENERGY STANCE" : "GUN STANCE";

            Section(sb, mainHeader, "tap Light/Heavy/Gun in sequence");
            if (mainSet != null) sb.Append(Format(mainSet.moves));

            Section(sb, rangedHeader, "gun-stance fire strings");
            if (rangedSet != null) sb.Append(Format(rangedSet.moves));

            Section(sb, "AIR ATTACKS", "jump, then press an attack");
            if (airSet != null) sb.Append(Format(airSet.moves));

            Section(sb, "CONTROLS - KEYBOARD", null);
            sb.Append("L=Light(J)  H=Heavy(K)  G=Gun(L)\nJump=Space  Q=Stance  E=Ammo\nF=Awakened  LShift=Lock-On\nC=Character  V=Codex  Esc/Tab=Pause\nDouble-tap A/D=Dash (hold=Run)\n");
            Section(sb, "CONTROLS - GAMEPAD (PS5)", null);
            sb.Append("L=Square  H=Triangle  G=Circle\nJump=Cross  L1=Stance  R1=Ammo\nL2=Lock-On  R2=Awakened\nOptions=Pause  Share=Character\nR3=Codex  Move=Stick/D-pad\nDouble-tap d-pad=Dash\n");
        }

        // ----- Page 2: moves needing a direction / motion / special trigger ----
        private void BuildSpecialPage(StringBuilder sb, bool hilo, MoveSet dashSet, MoveSet sprintSet,
            MoveSet tpSet, MoveSet awkSet, MoveSet awkAirSet)
        {
            string awkHeader = hilo ? "SHADOW FORM" : "AWAKENED FORM";
            string awkAirHeader = hilo ? "SHADOW AIR" : "AWAKENED AIR";

            Section(sb, "DASH ATTACKS", "dash (double-tap), then attack");
            if (dashSet != null) sb.Append(Format(dashSet.moves));

            Section(sb, "SPRINT ATTACKS", "hold a run, then attack");
            if (sprintSet != null) sb.Append(Format(sprintSet.moves));

            Section(sb, "MOTION SPECIALS", "fighting-game inputs (facing-relative)");
            sb.Append("[->][\\/][->]+L/H/G   Teleport Strike / Heavy / Shoot\n");
            sb.Append("[<-][\\/][<-]+Heavy   Slash Mirage (invincible barrage)\n");
            sb.Append("Dash+L  Phantom Slash (light)   Sprint+H  Phantom Slash (heavy)\n");

            Section(sb, "TELEPORT ATTACKS", "while locked on / from a motion input");
            if (tpSet != null) sb.Append(Format(tpSet.moves));

            Section(sb, awkHeader, "during awakened form (R2/F)");
            if (awkSet != null) sb.Append(Format(awkSet.moves));

            Section(sb, awkAirHeader, "awakened jump attacks");
            if (awkAirSet != null) sb.Append(Format(awkAirSet.moves));
        }

        private static void Section(StringBuilder sb, string header, string note)
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append("=== ").Append(header).Append(" ===\n");
            if (!string.IsNullOrEmpty(note)) sb.Append('(').Append(note).Append(")\n");
        }

        private static string Format(System.Collections.Generic.List<MoveDefinition> moves)
        {
            if (moves == null) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < moves.Count; i++)
            {
                var move = moves[i];
                var seq = new StringBuilder();
                foreach (var token in move.sequence)
                {
                    if (seq.Length > 0) seq.Append(',');
                    seq.Append(token switch
                    {
                        InputToken.Light => 'L',
                        InputToken.Heavy => 'H',
                        InputToken.Gun => 'G',
                        _ => '?'
                    });
                }

                string tags = "";
                if (move.teleport == TeleportKind.BehindTarget) tags += " [TP]";
                else if (move.teleport == TeleportKind.ForwardDash) tags += " [DASH]";
                if (move.attack != null && move.attack.launch > 0f) tags += " [LAUNCH]";
                else if (move.attack != null && move.attack.knocksDown) tags += " [KD]";
                if (move.firesProjectile) tags += " [SHOT]";

                sb.AppendLine($"{seq,-9} {SplitPascal(move.id)}{tags}");
            }
            return sb.ToString();
        }

        private static string SplitPascal(string s)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i]) && !char.IsUpper(s[i - 1])) sb.Append(' ');
                sb.Append(s[i]);
            }
            return sb.ToString();
        }
    }
}
