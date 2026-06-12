using System.Text;
using SilverFang.Combat;
using SilverFang.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SilverFang.UI
{
    /// Pause menu (Esc / Tab / gamepad Start-Options). Freezes the game and
    /// shows the full command list, controls, and Scematica balance.
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
        [SerializeField] private GameObject panel;
        [SerializeField] private Text titleText;
        [SerializeField] private Text swordColumnA;
        [SerializeField] private Text swordColumnB;
        [SerializeField] private Text gunColumn;
        [SerializeField] private Text awakenedColumn;
        [SerializeField] private Text balanceText;

        private bool open;

        private void Start()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            bool pressed = (kb != null && (kb.tabKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
                        || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
            if (!pressed) return;

            if (!open)
            {
                if (!Core.GamePause.TryAcquire(this)) return;
                open = true;
                BuildText(); // rebuilt per open: shows the selected hero's lists
                if (balanceText != null)
                    balanceText.text = $"{Currency.ScematicaWallet.Balance:N0} {Currency.ScematicaToken.Symbol}";
            }
            else
            {
                open = false;
                Core.GamePause.Release(this);
            }
            if (panel != null) panel.SetActive(open);
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
