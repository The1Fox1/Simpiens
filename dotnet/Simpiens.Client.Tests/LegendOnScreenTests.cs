using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Simpiens.Cognition.Social;
using Simpiens.Core.Session;
using Simpiens.Narrative;
using Simpiens.Presentation.Dashboard;
using Simpiens.Simulation;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Skills;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// A crawler's legend on screen (Epic 11, S6.6, D36): its epithet in the names the dashboard shows (the dossier with its
    /// title, the residents' cards, the inspector, the technique tree; on the guild's card before its title, the name line
    /// holding two skills); the dossier's legend line (the epithet and why, earned traits, scars) with each in its tip; and the
    /// longest name, epithet and title fitting. The legend is set by hand on the live outpost.
    /// </summary>
    public class LegendOnScreenTests
    {
        private const int Width = 1280, Height = 720;

        [Fact]
        public void TheLegendIsInTheNamesAndTheDossier()
        {
            var (ui, member, sim) = Start();
            var game = ui.Game;
            var model = game.Session.DashboardModel;
            var panels = game.Play.Dashboard.Panels;
            string name = model.Context.FriendlyName(member.AgentId, "?");

            Assert.Equal($"{name} Beetlebane the Warden", model.Dossier.Name);
            Assert.Equal("Beetlebane, 3 creatures slain · Battle-Hardened · a scar", panels.LegendShown);
            Assert.Equal("Beetlebane: for three creatures slain\nBattle-Hardened: +1 defense, after two downings survived\nA beetle's claw across the left cheek", model.Dossier.LegendTip);
            var row = model.Sectors.Residents.First(r => r.CrawlerId == member.AgentId);
            Assert.Equal($"{name} Beetlebane", row.Name);

            // The guild's card: the epithet before its title
            Assert.Equal(GuildRules.Check.Ok, sim.Guild.Recruit(sim, member.AgentId, 30, 1));
            ui.Frame(30);
            model.OpenGuild();
            ui.Frame(2);
            var card = model.Guild.Rows.First(m => m.CrawlerId == member.AgentId);
            Assert.Equal(name, card.Name);
            Assert.StartsWith("Beetlebane · Warden (front)", card.Doing);
            model.OpenTechniques(member.AgentId);
            ui.Frame(2);
            Assert.Equal($"{name} Beetlebane the Warden", model.Techniques.Name);
            model.CloseWindow();

            // The inspector on the map
            game.Play.Dashboard.TogglePanels();
            ui.Frame(2);
            Assert.Equal($"{name} Beetlebane", model.Inspector.Name);

            // A crawler with none: its name alone, and no legend line
            game.Play.Dashboard.TogglePanels();
            var plain = game.Session.Simulation.Host.CrawlerStates.First(c => !c.IsDrifter && c.AgentId != member.AgentId);
            model.SelectCrawler(plain.AgentId);
            ui.Frame(2);
            Assert.Equal(string.Empty, panels.LegendShown);
            Assert.DoesNotContain("Beetlebane", model.Dossier.Name);
        }

        /// <summary>The longest given name with the longest epithet fits a resident's card short of its badge, and the dossier's header holds it with the longest title.</summary>
        [Fact]
        public void TheLongestNameFits()
        {
            var (ui, member, _) = Start();
            var game = ui.Game;
            var model = game.Session.DashboardModel;
            var identity = (IAgentIdentityService)game.Session.Simulation.Services.GetService(typeof(IAgentIdentityService));
            string longestName = new string('W', Simpiens.Simulation.People.GivenNames.MaxLength);
            ((AgentIdentityService)identity).RegisterCustomName(member.AgentId, longestName);
            // The widest epithet the table can give: every plain row's words and every kind's word
            var traits = member.Traits;
            string widest = null;
            foreach (var row in EpithetTable.All.Where(r => r.Form == EpithetForm.Plain)) if (widest == null || row.Words.Length > widest.Length) widest = row.Words;
            var plainRow = EpithetTable.All.First(r => r.Words == widest);
            traits.SetEpithet(plainRow.Id, 0, default);
            member.Traits = traits;
            ui.Frame(30);
            var panels = game.Play.Dashboard.Panels;
            int index = model.Sectors.Residents.ToList().FindIndex(r => r.CrawlerId == member.AgentId);
            Assert.True(index >= 0, "The member is in the sector shown.");
            var card = panels.ResidentCards[index];
            Assert.Equal($"{longestName} {widest}", card.Name.Text);
            Assert.True(card.Name.AbsoluteLeft + card.Name.AbsoluteWidth <= card.Badge.AbsoluteLeft - 4f, $"\"{card.Name.Text}\" ends short of the badge.");
            Assert.True(panels.DossierShown && model.Dossier.Name.StartsWith(longestName + " " + widest), "The dossier's header holds it (it wraps).");
        }

        private static (UiDriver Ui, Simpiens.Cognition.AgentState Member, SimulationManager Sim) Start()
        {
            var ui = UiDriver.Start(new ClientOptions
            {
                ConfigureServices = services =>
                {
                    services.RemoveAll<ISessionDefinition>();
                    services.AddSingleton<ISessionDefinition>(new OutpostSession(OutpostSession.DefaultCrawlerCount, EconomyTuning.Default.WithStallTreasury(1000)));
                },
            }, width: Width, height: Height);
            var game = ui.Game;
            var sim = (SimulationManager)game.Session.Simulation.Simulation;
            var member = game.Session.Simulation.Host.CrawlerStates.First(s => !s.IsDrifter);
            var skills = default(SkillSet);
            skills.Set(Skill.Melee, 4);
            member.Skills = skills;
            var traits = default(CrawlerTraits);
            traits.SetTechnique(Skill.Melee, SkillBand.Apprentice, Technique.ShieldWall);
            traits.SetCalling(Calling.Warden);
            traits.SetEpithet(Epithet.KindSlayer, (byte)CreatureKind.StoneBeetle, default);
            traits.AddTrait(EarnedTrait.BattleHardened);
            traits.AddScar(Scar.BeetleClaw);
            traits.ClearNews(LegendNews.Epithet | LegendNews.Trait | LegendNews.Scar);
            member.Traits = traits;
            member.Memory.SetAffinity(WorldRegistry.PlayerEntityId, new AffinityRecord(60, 80, 0, 0, 1, 0));
            game.Session.DashboardModel.SelectCrawler(member.AgentId);
            var sector = game.Session.Simulation.Sectors.GetSectorAt(member.Position); // who is here: its sector's
            if (sector.Id != 0) game.Session.DashboardModel.SelectSector(sector.Id);
            ui.Frame(30);
            return (ui, member, sim);
        }
    }
}
