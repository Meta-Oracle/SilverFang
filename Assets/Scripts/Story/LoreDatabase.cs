using System.Collections.Generic;
using System.Linq;

namespace SilverFang.Story
{
    public struct DialogueLine
    {
        public string speaker;
        public string text;

        public DialogueLine(string speaker, string text)
        {
            this.speaker = speaker;
            this.text = text;
        }
    }

    public class LoreEntry
    {
        public string id;
        public string title;
        public string excerpt; // one-liner shown on pickup
        public string body;    // full codex document
    }

    /// All narrative content: dialogue beats and datashard codex entries.
    /// See Docs/STORY.md for the bible. Rule: Vesper schedules, Senn
    /// confesses, Voss explains, Silver asks. The company never lies.
    public static class LoreDatabase
    {
        private const string Silver = "SILVER";
        private const string Vesper = "VESPER";

        public static readonly Dictionary<string, DialogueLine[]> Beats = new Dictionary<string, DialogueLine[]>
        {
            ["intro"] = new[]
            {
                new DialogueLine(Vesper, "Good evening, S-1L. Confirmed contract activity in Sector Nine. One K-9 class, two escorts. The bounty is live, and it is generous."),
                new DialogueLine(Silver, "It's three in the morning, Vesper."),
                new DialogueLine(Vesper, "Yes. The company appreciates your flexibility. Your augment lease statement is also attached — shall I read you the balance?"),
                new DialogueLine(Silver, "Cute. Don't."),
                new DialogueLine(Vesper, "Then earn, hunter. The city is watching. And the city pays in fear.")
            },

            ["enc1_start"] = new[]
            {
                new DialogueLine(Vesper, "Contact ahead. K-9 class — the street will call it a werewolf. Let them. Myths buy walls."),
                new DialogueLine(Silver, "And walls buy SCEMA."),
                new DialogueLine(Vesper, "Everything buys SCEMA, hunter. That is what money is for.")
            },

            ["enc1_clear"] = new[]
            {
                new DialogueLine(Vesper, "Confirmed burn. Bounty released to your wallet. The patent string is closed — lovely work."),
                new DialogueLine(Silver, "Vesper. The bounty posting. It's timestamped 02:11."),
                new DialogueLine(Vesper, "Correct."),
                new DialogueLine(Silver, "The first outbreak report came in at 06:40."),
                new DialogueLine(Vesper, "...Dispatch timestamps are frequently imprecise. I will file a correction. Move east, S-1L. You are still on the clock.")
            },

            ["enc2_start"] = new[]
            {
                new DialogueLine(Vesper, "Two M-09 units ahead, escorting a K-7. The K-7 is a big one. The Myth Department is calling it a chimera."),
                new DialogueLine(Silver, "The *what* department?"),
                new DialogueLine(Vesper, "Mind the chimera, hunter.")
            },

            ["enc2_clear"] = new[]
            {
                new DialogueLine(Vesper, "Burn confirmed. That was beautiful, S-1L. Almost as clean as Meridian Yard."),
                new DialogueLine(Silver, "Meridian Yard was nine years ago. Before your assignment."),
                new DialogueLine(Vesper, "...Yes. I must have read the file."),
                new DialogueLine(Silver, "You said 'we held the east stairwell.'"),
                new DialogueLine(Vesper, "Your bounty has cleared, hunter. Go home. Please."),
                new DialogueLine(Silver, "Vesper—"),
                new DialogueLine(Vesper, "*Please.*")
            }
        };

        public static readonly LoreEntry[] Entries =
        {
            new LoreEntry
            {
                id = "shard_01",
                title = "Payroll Anomaly",
                excerpt = "41 of 41 bounties were posted before their outbreaks were reported.",
                body = "INTERNAL LEDGER EXTRACT — ASSET RECOVERY / Q2\n\n" +
                       "Bounty 2347-0114: posted 02:11. First civilian report: 06:40.\n" +
                       "Bounty 2347-0109: posted 23:58. First civilian report: 05:15.\n" +
                       "Bounty 2347-0093: posted 11:02. First civilian report: 19:27.\n" +
                       "[ 38 further rows, all alike ]\n\n" +
                       "Annotation (L.S.): Dispatch doesn't predict outbreaks. Accounting does. " +
                       "Ask yourself what kind of company knows the monster's schedule.\n" +
                       "Then ask what kind of company SETS it."
            },
            new LoreEntry
            {
                id = "shard_02",
                title = "A Memo on Naming",
                excerpt = "Internally, the creatures are not called monsters. They are called yields.",
                body = "FROM: Myth Department\nTO: Release Coordination\nRE: Q3 yield branding\n\n" +
                       "'Werewolf' continues to outperform. Recommend retiring 'lycan unit' from public comms entirely. " +
                       "Districts briefed with mythological branding show 23% higher conversion to bounty insurance " +
                       "and 31% faster SCEMA velocity.\n\n" +
                       "Remember the founder's note: a monster is a product, and a myth is its packaging."
            },
            new LoreEntry
            {
                id = "shard_03",
                title = "Patent String",
                excerpt = "The money you are holding is the animal you killed.",
                body = "HEMOTYPE R&D — SEQUENCE AUDIT\n\n" +
                       "Gene-tag recovered from K-7 field tissue resolves to patent SD-77-031c.\n" +
                       "Patent SD-77-031c is reserve collateral for SCEMA issuance block 9,114.\n\n" +
                       "Annotation (L.S.): You were paid sixty SCEMA to burn the thing that backs the sixty SCEMA. " +
                       "The money you are holding is the animal you killed. There is no metaphor here. " +
                       "That is the design.\n\nI know, because I designed it."
            },
            new LoreEntry
            {
                id = "shard_04",
                title = "Standard Hunter Lease, Clause 9",
                excerpt = "They never sold you the gun. They leased you the hand.",
                body = "STANDARD HUNTER SERVICE AGREEMENT — EXTRACT\n\n" +
                       "9. The Issuer retains title to all augmentations, implants, and derived tissues (the 'Equipment'). " +
                       "Upon default, abandonment, or termination of service, all Equipment — and any organic substrate " +
                       "into which Equipment has integrated — reverts to the Issuer.\n\n" +
                       "Annotation (L.S.): Read clause 9 twice. 'Organic substrate' is you. " +
                       "They never sold you the gun, hunter. They leased you the hand."
            },
            new LoreEntry
            {
                id = "shard_05",
                title = "K-9 Protocol, Closing Report",
                excerpt = "Latent subject S-1L returned to Kennel rotation. Recommend continued field observation.",
                body = "K-9 PROTOCOL — TERMINATION SUMMARY (A.X. 2338)\n\n" +
                       "Volunteer cohort: 14 hunters, augment-compatible.\n" +
                       "Outcome: 11 full expression (reclassified as yields). 2 deceased. 1 latent.\n\n" +
                       "Latent subject S-1L returned to Kennel rotation. Expression projected under " +
                       "sustained adrenal load. Recommend continued field observation.\n\n" +
                       "Annotation (L.S.): They watched you for nine years, Silver. Every cull was a clinical trial. " +
                       "Your 'Awakened' state has a patent number."
            },
            new LoreEntry
            {
                id = "shard_06",
                title = "Dispatch Fragment",
                excerpt = "Query: date of death, Hunter E-V3. Query denied.",
                body = "RECOVERED BUFFER — DISPATCH NODE 'VESPER'\n\n" +
                       "...held the east stairwell until the charges went. We did. I did. I keep saying we. " +
                       "There were fourteen of us in the program and I am the only one still on the payroll " +
                       "and I do not remember signing anything after Meridian Yard.\n\n" +
                       "Query: date of death, Hunter E-V3. — Query denied.\n" +
                       "Query: date of death. — Query denied.\n" +
                       "Query: am I\n\n[ BUFFER ENDS ]"
            },
            new LoreEntry
            {
                id = "shard_07",
                title = "Depletion Audit (Suppressed)",
                excerpt = "Nine vein collapses show shear patterns consistent with staged demolition.",
                body = "EXTERNAL AUDIT — VEIN COLLAPSE EVENTS, A.X. 2331–2333\nSTATUS: SUPPRESSED\n\n" +
                       "Of 60 terminal collapses examined, 9 show shear patterns consistent with staged demolition. " +
                       "All 9 sites were optioned by Scematica Dynamics subsidiaries 6–14 months before collapse.\n\n" +
                       "Annotation (L.S.): The world was ending anyway. Voss just made sure it ended on time, " +
                       "with his money waiting at the door. He calls this 'stewardship.'"
            },
            new LoreEntry
            {
                id = "shard_08",
                title = "Release Schedule, Q3",
                excerpt = "Somewhere in the Mint, a careful person rounds your neighbors to the nearest ten.",
                body = "RELEASE COORDINATION — Q3 CALENDAR (EXTRACT)\n\n" +
                       "Wk 2: K-9 pair, Harrow district. Census attached. Casualty budget: 40–60.\n" +
                       "Wk 6: M-09 trio, dockside. Casualty budget: 25.\n" +
                       "Wk 9: K-7 single, Argent Row. Casualty budget: 'flexible.'\n\n" +
                       "Annotation (L.S.): Budget. They BUDGET it. Somewhere in the Mint a careful person " +
                       "rounds your neighbors to the nearest ten."
            },
            new LoreEntry
            {
                id = "shard_09",
                title = "Senn's Confession",
                excerpt = "They didn't build you to kill monsters. They built monsters to keep you killing.",
                body = "I built the binding. Genome to mint, mint to genome. I told myself information wanted " +
                       "to be currency — that we were backing money with creation itself. I did not ask why " +
                       "every genome we vaulted had teeth.\n\n" +
                       "You've read the others by now. So: follow the silver thread. Pull the tissue archive " +
                       "for SD-77-031c and read what the K-7's gene-tag is spliced FROM. You will find a serial " +
                       "you recognize. You wear it.\n\n" +
                       "They didn't build you to kill monsters, S-1L. They built monsters to keep you killing.\n\n— L.S."
            },
            new LoreEntry
            {
                id = "shard_10",
                title = "Directive 9",
                excerpt = "Your name is first on the recall list, hunter. End of quarter.",
                body = "OFFICE OF THE CHAIRMAN — DIRECTIVE 9\n\n" +
                       "The Awakened field assets have exceeded observation value. Begin recall per Clause 9. " +
                       "Schedule: end of quarter. List attached.\n\n" +
                       "Note from the Chairman: Do not mourn the program. Every currency retires its first coins; " +
                       "they become collectors' items. — A.V.\n\n" +
                       "Annotation (L.S.): Your name is first on the list, hunter. End of quarter. Spend wisely."
            }
        };

        public static LoreEntry FindEntry(string id) => Entries.FirstOrDefault(e => e.id == id);
    }
}
