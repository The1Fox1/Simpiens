using System.Linq;
using Gum.Wireframe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Simpiens.Cognition.Social;
using Simpiens.Core.Session;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Skills;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// Techniques on screen (Epic 11, S4.9, D18): the dossier lists the techniques a crawler holds, what each does in the line's
    /// tip; for one of the patron's people the sway's second button is Sponsor, its words how many techniques are open to it or
    /// the rule's reason, and it opens the technique tree (S4.11, D25) on the other technique at its best skill's top band,
    /// whose offer leaves as a command; a resident's is still Recruit. The techniques are set by hand on the live outpost.
    /// </summary>
    public class TechniquesOnScreenTests
    {
        private const int Width = 1280, Height = 720;

        [Fact]
        public void TheDossierListsTechniquesAndSponsorsOneOfOurs()
        {
            var ui = Start();
            var game = ui.Game;
            var sim = (SimulationManager)game.Session.Simulation.Simulation;
            var residents = game.Session.Simulation.Host.CrawlerStates.Where(s => !s.IsDrifter).ToList();
            var member = residents[0];
            var skills = default(SkillSet);
            skills.Set(Skill.Melee, 4);
            skills.Set(Skill.Mining, 3);
            member.Skills = skills;
            var traits = default(CrawlerTraits);
            traits.SetTechnique(Skill.Melee, SkillBand.Apprentice, Technique.ShieldWall);
            traits.SetTechnique(Skill.Mining, SkillBand.Apprentice, Technique.SeamStudent);
            member.Traits = traits;
            member.Personality = new Simpiens.Cognition.Contracts.AgentPersonality(0f, 0.2f, 0f, 0f, 0f);

            // A resident: its techniques, and Recruit
            game.Session.DashboardModel.SelectCrawler(member.AgentId);
            ui.Frame();
            var panels = game.Play.Dashboard.Panels;
            Assert.Equal("Shield Wall · Seam Student", panels.TechniquesShown);
            Assert.Equal("Shield Wall (Melee): +3 defense, 15% less damage\nSeam Student (Mining): learns 25% faster, 10% slower\n" + Simpiens.Presentation.Dashboard.DossierModel.TreeHint,
                game.Session.DashboardModel.Dossier.TechniquesTip);
            Assert.Equal("Recruit", panels.SwayButtonShown);
            Assert.False(game.Session.DashboardModel.Dossier.IsOurs);

            // Signed on, the same button is Sponsor: the tree, on the band's other at its best skill's top band
            member.Memory.SetAffinity(WorldRegistry.PlayerEntityId, new AffinityRecord(60, 80, 0, 0, 1, 0));
            Assert.Equal(GuildRules.Check.Ok, sim.Guild.Recruit(sim, member.AgentId, 30, 1));
            ui.Frame(game.Options.DashboardRefreshSeconds > 0 ? 30 : 1);
            Assert.True(game.Session.DashboardModel.Dossier.IsOurs);
            Assert.Equal("Sponsor", panels.SwayButtonShown);
            Assert.Equal("2 techniques open to sponsor, from 20g.", panels.RecruitShown); // Reckless Fury, and Hard Driver at Mining 3
            Assert.True(panels.RecruitButton.IsEnabled);
            uint treasury = sim.ActiveStalls[0].TreasuryGold;
            ui.Click(Centre(panels.RecruitButton));
            Assert.Equal(Technique.RecklessFury, game.Session.DashboardModel.Techniques.Selected);
            ui.Click(Centre(game.Play.Dashboard.Techniques.OfferButton));
            ui.Ticks(1);
            Assert.Equal(Technique.RecklessFury, member.Traits.TechniqueIn(Skill.Melee, SkillBand.Apprentice));
            Assert.Equal(treasury - 20, sim.ActiveStalls[0].TreasuryGold);

            // Its trust fallen under the line, the line says why; the tree still opens, to read
            member.Memory.SetAffinity(WorldRegistry.PlayerEntityId, new AffinityRecord(10, 40, 0, 0, 1, 0));
            ui.Frame(30);
            Assert.Equal("Sponsor Shield Wall? Not yet: trust 40 of 50.", panels.RecruitShown);
            Assert.True(panels.RecruitButton.IsEnabled);
        }

        private static UiDriver Start()
            => UiDriver.Start(new ClientOptions
            {
                ConfigureServices = services =>
                {
                    services.RemoveAll<ISessionDefinition>();
                    services.AddSingleton<ISessionDefinition>(new OutpostSession(OutpostSession.DefaultCrawlerCount, EconomyTuning.Default.WithStallTreasury(1000)));
                },
            }, width: Width, height: Height);

        private static Vec2 Centre(GraphicalUiElement control)
            => new Vec2(control.AbsoluteLeft + control.AbsoluteWidth * 0.5f, control.AbsoluteTop + control.AbsoluteHeight * 0.5f);

        private static Vec2 Centre(Gum.Forms.Controls.FrameworkElement control) => Centre(control.Visual);
    }
}
