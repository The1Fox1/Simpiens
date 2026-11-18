using System.Linq;
using Gum.Wireframe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Social;
using Simpiens.Core.Session;
using Simpiens.Presentation.Dashboard;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Skills;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The cost of mastery on screen (Epic 11, S2.5, D7, D8): a skill the budget holds reads "held at 5: two crafts past
    /// Apprentice" in the dossier; an asking wage above the wage shows in amber beside it, "asks 6g", in the dossier, on the
    /// guild's card and on the station window's artisan line. The band and the trust are set by hand on the live outpost;
    /// every action leaves as a typed command.
    /// </summary>
    public class UpkeepOnScreenTests
    {
        private const int Width = 1280, Height = 720;

        [Fact]
        public void TheTextsSayWhatIsHeldAndWhatIsAsked()
        {
            var skills = default(SkillSet);
            skills.Set(Skill.Mining, 6);
            skills.Set(Skill.Smithing, 7);
            skills.Set(Skill.Foraging, 5);
            Assert.Equal("Foraging held at 5: two crafts past Apprentice", SkillTexts.Held(skills));
            skills.Set(Skill.Foraging, 4);
            Assert.Equal(string.Empty, SkillTexts.Held(skills)); // below its ceiling it still climbs
            var master = default(SkillSet);
            master.Set(Skill.Melee, 9);
            master.Set(Skill.Mining, 8);
            Assert.Equal("Mining held at 8: a Master already", SkillTexts.Held(master));
            Assert.Equal("asks 7g", SkillTexts.Asks(5, 7));
            Assert.Equal(string.Empty, SkillTexts.Asks(7, 7));
        }

        [Fact]
        public void TheDossierAndTheGuildsCardShowTheAskAndTheHold()
        {
            var ui = Start();
            var game = ui.Game;
            var model = game.Session.DashboardModel;
            var panels = game.Play.Dashboard.Panels;
            var member = Trusted(game).First();
            model.SelectCrawler(member.AgentId);
            ui.Frame();
            ui.Click(Centre(panels.RecruitButton));
            ui.Ticks(1);
            var guild = game.Session.Simulation.Simulation.Guild;
            Assert.True(guild.IsMember(member.AgentId), "Recruit signed it on.");
            ui.Frame();
            Assert.Equal(string.Empty, panels.AsksShown);
            Assert.Equal(string.Empty, panels.HeldShown);

            // A Journeyman now (by hand): it asks 6g over its 5g
            Reach(member, Skill.Mining, 6);
            ui.Frame();
            Assert.Equal("asks 6g", panels.AsksShown);
            ui.Click(Centre(game.Play.Dashboard.Header.GuildButton));
            ui.Frame();
            Assert.Equal("asks 6g", game.Play.Dashboard.Guild.AsksShown(0));
            Assert.True(game.Play.Dashboard.CloseWindow());

            // Two crafts past Apprentice: the third is held at 5
            Reach(member, Skill.Smithing, 6);
            Reach(member, Skill.Foraging, 5);
            ui.Frame();
            Assert.Equal("Foraging held at 5: two crafts past Apprentice", panels.HeldShown);
        }

        [Fact]
        public void TheStationWindowShowsItsArtisansAsk()
        {
            var ui = Start();
            var game = ui.Game;
            var dashboard = game.Play.Dashboard;
            var workshops = game.Session.Simulation.Simulation.Workshops;
            int forge = workshops.IndexOf(StationKind.Forge);
            ui.Click(Centre(dashboard.Header.WorkshopButton));
            var window = dashboard.Stations;
            ui.Click(Centre(window.BuildButton));
            ui.Ticks(1);
            ui.Frame();
            var smith = game.Session.Simulation.Host.CrawlerStates.First(s => !s.IsDrifter && s.Personality.RiskTolerancePercent < WorkshopTuning.Default.MaxRiskTolerancePercent);
            smith.Memory.SetAffinity(WorldRegistry.PlayerEntityId, new AffinityRecord(warmth: 60, trust: 80, fear: 0, respect: 40, interactionCount: 1, lastInteractionTick: 0));
            ui.Frame();
            ui.Click(Centre(window.HireButton(0)));
            ui.Ticks(1);
            ui.Frame();
            var artisanId = workshops.ArtisanAt(forge);
            var artisan = game.Session.Simulation.Host.CrawlerStates.First(s => s.AgentId == artisanId);
            Assert.Equal(string.Empty, window.AsksShown);
            artisan.Personality = new AgentPersonality(0f, artisan.Personality.RiskTolerance, 0f, 0f, 0f); // the neutral 5g reservation
            Reach(artisan, Skill.Smithing, 9);
            Assert.True(workshops.TryGetContract(artisanId, out var contract));
            ui.Frame();
            Assert.Equal(SkillTexts.Asks(contract.Wage, 8), window.AsksShown);
            Assert.NotEqual(string.Empty, window.AsksShown);
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

        // Residents who would sign on (by hand), neutral in greed, so each asks the neutral 5g
        private static System.Collections.Generic.List<AgentState> Trusted(SimpiensGame game)
        {
            var residents = game.Session.Simulation.Host.CrawlerStates.Where(s => !s.IsDrifter).ToList();
            foreach (var resident in residents)
            {
                resident.Memory.SetAffinity(WorldRegistry.PlayerEntityId, new AffinityRecord(warmth: 60, trust: 80, fear: 0, respect: 40, interactionCount: 1, lastInteractionTick: 0));
                resident.Personality = new AgentPersonality(0f, resident.Personality.RiskTolerance, resident.Personality.Sociability, resident.Personality.Loyalty, resident.Personality.Diligence);
            }
            return residents;
        }

        private static void Reach(AgentState crawler, Skill skill, int level)
        {
            var skills = crawler.Skills;
            skills.Set(skill, level);
            crawler.Skills = skills;
        }

        private static Vec2 Centre(GraphicalUiElement control)
            => new Vec2(control.AbsoluteLeft + control.AbsoluteWidth * 0.5f, control.AbsoluteTop + control.AbsoluteHeight * 0.5f);

        private static Vec2 Centre(Gum.Forms.Controls.FrameworkElement control) => Centre(control.Visual);
    }
}
