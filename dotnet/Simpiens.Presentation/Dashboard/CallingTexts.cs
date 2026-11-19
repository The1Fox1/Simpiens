using Simpiens.Simulation.Skills;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>
    /// A crawler's calling in the dashboard's words (Epic 11, S5.7, D24): its title ("Warden", "Master Warden" at Master) and
    /// the part it plays in a party ("front"), from the tables, so a calling added as a row is shown with no code here.
    /// </summary>
    public static class CallingTexts
    {
        /// <summary>Its title: the calling's word, its Master title at Master; empty for none.</summary>
        public static string Title(in SkillSet skills, in CrawlerTraits traits) => CallingRules.TitleOf(traits.Calling, skills);

        /// <summary>The role's word in a party: "front", "back", "carrier", "leader"; empty for none.</summary>
        public static string RoleWord(PartyRole role) => role switch
        {
            PartyRole.Front => "front",
            PartyRole.Back => "back",
            PartyRole.Carrier => "carrier",
            PartyRole.Leader => "leader",
            _ => string.Empty
        };

        /// <summary>"Warden (front)": its title with the part it plays in a party, the title alone for a calling with none.</summary>
        public static string TitleWithRole(in SkillSet skills, in CrawlerTraits traits)
        {
            string title = Title(skills, traits), role = RoleWord(CallingTable.RoleOf(traits.Calling));
            return role.Length == 0 || title.Length == 0 ? title : $"{title} ({role})";
        }

        /// <summary>"Warden · Going to bed": <paramref name="title"/> before <paramref name="line"/>, either alone when the other is empty.</summary>
        public static string Before(string title, string line) => title.Length == 0 ? line : line.Length == 0 ? title : $"{title} · {line}";
    }
}
