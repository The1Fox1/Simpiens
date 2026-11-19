using System.Collections.Generic;
using System.Linq;
using Simpiens.Presentation.Dashboard;
using Simpiens.Simulation.Skills;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The skills the dashboard lists, with their passions (Epic 11, S1.6): the dossier keeps every skill a crawler loves,
    /// even at 0, in place of the lowest it doesn't; the roster shows its two best, a flame only on those; each passion
    /// names its flame from the UI kit.
    /// </summary>
    public class PassionTextsTests
    {
        private static (SkillSet Skills, CrawlerTraits Traits) Crawler()
        {
            var skills = default(SkillSet);
            skills.Set(Skill.Smithing, 5);
            skills.Set(Skill.Mining, 2);
            skills.Set(Skill.Melee, 1);
            skills.Set(Skill.Foraging, 1);
            skills.Set(Skill.Alchemy, 1);
            var traits = default(CrawlerTraits);
            traits.SetPassion(Skill.Ranged, Passion.Burning); // loved, and never practised
            traits.SetPassion(Skill.Melee, Passion.Keen);
            return (skills, traits);
        }

        [Fact]
        public void TheDossierKeepsEverySkillItLoves()
        {
            var (skills, traits) = Crawler();
            var shown = new List<SkillTexts.Shown>();
            SkillTexts.BestShown(skills, traits, 4, keepPassions: true, shown);
            Assert.Equal(new[] { "Smithing 5", "Mining 2", "Melee 1", "Ranged 0" }, shown.Select(s => s.Text));
            Assert.Equal(new[] { Passion.None, Passion.None, Passion.Keen, Passion.Burning }, shown.Select(s => s.Passion));
            Assert.Equal(Simpiens.Presentation.Art.UiKit.PassionKeen, shown[2].Glyph);
            Assert.Equal(Simpiens.Presentation.Art.UiKit.PassionBurning, shown[3].Glyph);
            Assert.Null(shown[0].Glyph);
        }

        [Fact]
        public void TheRosterShowsItsTwoBestWithTheirFlames()
        {
            var (skills, traits) = Crawler();
            var shown = new List<SkillTexts.Shown>();
            SkillTexts.BestShown(skills, traits, 2, keepPassions: false, shown);
            Assert.Equal(new[] { "Smithing 5", "Mining 2" }, shown.Select(s => s.Text));
            traits.SetPassion(Skill.Mining, Passion.Keen);
            SkillTexts.BestShown(skills, traits, 2, keepPassions: false, shown);
            Assert.Equal(Passion.Keen, shown[1].Passion);
            SkillTexts.BestShown(default, default, 4, keepPassions: true, shown);
            Assert.Empty(shown);
        }
    }
}
