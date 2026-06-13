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
    /// Pause menu (Esc / Tab / gamepad Start-Options). Freezes the game and
    /// shows a small option list — Resume, Command List, Restart, Quit to
    /// Intro. The full command/control reference is shown only when the player
    /// picks "Command List".
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
        [SerializeField] private GameObject menuPanel;       // option menu
        [SerializeField] private Text menuText;
        [SerializeField] private Text titleText;
        [SerializeField] private Text swordColumnA;
        [SerializeField] private Text swordColumnB;
        [SerializeField] private Text gunColumn;
        [SerializeField] private Text awakenedColumn;
        [SerializeField] private Text balanceText;

        private static readonly string[] Options = { "RESUME", "COMMAND LIST", "RESTART", "QUIT TO INTRO" };
        private enum Mode { Closed, Menu, CommandList }
        private Mode mode = Mode.Closed;
        private int cursor;

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
                    // any pause/back press returns to the menu
                    if (pausePressed || Back(kb, pad)) ShowMenu();
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
            BuildText(); // rebuilt per open: shows the selected hero's lists
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

            string mainHeader = hilo ? "== CLAW STANCE ==" : "== SWORD STANCE ==";
            string rangedHeader = hilo ? "== ENERGY STANCE ==" : "== GUN STANCE ==";
            string awkHeader = hilo ? "== SHADOW FORM ==" : "== AWAKENED ==";
            string tpNote = hilo ? "(shadow dash/sprint)" : "(awakened dash/sprint)";
            string awkAirHeader = hilo ? "== SHADOW AIR ==" : "== AWAKENED AIR ==";

            if (titleText != null) titleText.text = hilo ? "== PAUSED - HILO ==" : "== PAUSED - SILVER ==";

            if (mainSet != null)
            {
                var lines = mainSet.moves;
                int half = (lines.Count + 1) / 2;
                if (swordColumnA != null)
                    swordColumnA.text = mainHeader + "\n" + Format(lines, 0, half);
                if (swordColumnB != null)
                    swordColumnB.text = "\n" + Format(lines, half, lines.Count);
            }

            if (gunColumn != null && rangedSet != null)
            {
                gunColumn.text = rangedHeader + "\n" + Format(rangedSet.moves, 0, rangedSet.moves.Count)
                    + "\n== KEYBOARD ==\nL=Light(J)  H=Heavy(K)\nG=Gun(L)  Jump=Space\nQ=Stance  E=Ammo\nF=Awakened  LShift=Lock-On\nC=Character  V=Codex\nEsc/Tab=Pause\nDouble-tap A/D=Dash\n(hold after dash = Run)"
                    + "\n\n== GAMEPAD (PS5) ==\nL=Square  H=Triangle\nG=Circle  Jump=Cross\nL1=Stance  R1=Ammo\nL2=Lock-On  R2=Awakened\nOptions=Pause  Share=Character\nR3=Codex  Move=Stick/D-pad\nDouble-tap d-pad=Dash";
            }

            if (awakenedColumn != null && awkSet != null)
            {
                var sb = new StringBuilder();
                sb.Append(awkHeader).Append('\n').Append(Format(awkSet.moves, 0, awkSet.moves.Count));
                if (dashSet != null)
                    sb.Append("\n== DASH ATTACKS ==\n(attack during dash)\n")
                      .Append(Format(dashSet.moves, 0, dashSet.moves.Count));
                if (sprintSet != null)
                    sb.Append("\n== SPRINT ATTACKS ==\n(attack while running)\n")
                      .Append(Format(sprintSet.moves, 0, sprintSet.moves.Count));
                if (tpSet != null)
                    sb.Append("\n== TELEPORT ATTACKS ==\n").Append(tpNote).Append('\n')
                      .Append(Format(tpSet.moves, 0, tpSet.moves.Count));
                if (airSet != null)
                    sb.Append("\n== AIR ATTACKS ==\n(jump, then attack)\n")
                      .Append(Format(airSet.moves, 0, airSet.moves.Count));
                if (awkAirSet != null)
                    sb.Append("\n").Append(awkAirHeader).Append("\n(awakened jump attacks)\n")
                      .Append(Format(awkAirSet.moves, 0, awkAirSet.moves.Count));
                awakenedColumn.text = sb.ToString();
            }
        }

        private static string Format(System.Collections.Generic.List<MoveDefinition> moves, int from, int to)
        {
            var sb = new StringBuilder();
            for (int i = from; i < to && i < moves.Count; i++)
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
