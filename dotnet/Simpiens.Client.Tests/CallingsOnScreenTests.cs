using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Simpiens.Cognition.Social;
using Simpiens.Core.Session;
using Simpiens.Presentation.Dashboard;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Skills;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// Callings on screen (Epic 11, S5.7, D24): the calling's title in the dossier's header ("Bram the Warden", the Master title
    /// at Master), before what a resident does on its card in the sector, and on the guild's card with the part it plays in a
    /// party ("Warden (front)"); a crawler without one keeps the identity's title. The calling is set by hand on the live outpost.
    /// </summary>
    public class CallingsOnScreenTests
    {
        [Fact]
        public void TheCallingsTitleIsOnEveryCard()
        {
            var ui = UiDriver.Start(new ClientOptions
            {
                ConfigureServices = services =>
                {
                    services.RemoveAll<ISessionDefinition>();
                    services.AddSingleton<ISessionDefinition>(new OutpostSession(OutpostSession.DefaultCrawlerCount, EconomyTuning.Default.WithStallTreasury(1000)));
                },
            }, 1280, 720);
            var game = ui.Game;
            var sim = (SimulationManager)game.Session.Simulation.Simulation;
            var model = game.Session.DashboardModel;
            var warden = game.Session.Simulation.Host.CrawlerStates.First(s => !s.IsDrifter);
            var skills = default(SkillSet);
            skills.Set(Skill.Melee, 4);
            warden.Skills = skills;
            var traits = default(CrawlerTraits);
            traits.SetTechnique(Skill.Melee, SkillBand.Apprentice, Technique.ShieldWall);
            traits.SetCalling(Calling.Warden);
            warden.Traits = traits;
            string name = game.Session.Identity.GetFriendlyName(warden.AgentId);

            model.SelectCrawler(warden.AgentId);
            ui.Frame();
            Assert.StartsWith(name + " the Warden", model.Dossier.Name);
            model.SelectSector(game.Session.Simulation.Sectors.GetSectorAt(warden.Position).Id);
            ui.Frame();
            Assert.StartsWith("Warden · ", model.Sectors.Residents.First(r => r.CrawlerId == warden.AgentId).Activity);

            // A guild member: its title with its part in a party on the guild's card
            warden.Memory.SetAffinity(WorldRegistry.PlayerEntityId, new AffinityRecord(60, 80, 0, 0, 1, 0));
            Assert.Equal(GuildRules.Check.Ok, sim.Guild.Recruit(sim, warden.AgentId, 30, 1));
            model.OpenGuild();
            ui.Frame(30);
            Assert.StartsWith("Warden (front)", model.Guild.Rows.First(r => r.CrawlerId == warden.AgentId).Doing);

            // Its badge (Epic 11, S5.8): at the portrait's corner, and over its body on the map
            model.SelectCrawler(warden.AgentId);
            ui.Frame(30);
            Assert.True(game.Play.Dashboard.Panels.PortraitBadgeShown, "The dossier's portrait wears the front's badge.");
            var catalog = game.World.Scene.Catalog;
            int badge = catalog.Sprite(catalog.Id(Simpiens.Presentation.Art.ArtKeys.BadgeFront), 0);
            game.World.Rig.Pan(warden.Position - game.World.Rig.Position); // the camera over it
            ui.Ticks(1); // a tick, so the frames capture it as a member
            ui.Frame(2);
            var items = game.World.Scene.Items;
            bool drawn = false;
            for (int i = 0; i < items.Count; i++) drawn |= items[i].SpriteIndex == badge;
            var frame = game.Session.Frames.Current;
            string role = frame.TryGetIndex(warden.AgentId, out int at) ? frame[at].Role.ToString() : "not in the frame";
            Assert.True(drawn, $"The map draws the front's badge over it (frame role {role}, tick {frame.Tick}, items {items.Count}, badge sprite {badge}, catalogue {catalog.Name}).");

            // At Master, the Master title
            skills.Set(Skill.Melee, 9);
            warden.Skills = skills;
            model.SelectCrawler(warden.AgentId);
            ui.Frame(30);
            Assert.StartsWith(name + " the Master Warden", model.Dossier.Name);

            // Without a calling, the identity's title as before
            warden.Traits = default;
            ui.Frame(30);
            Assert.StartsWith(name + " the " + game.Session.Identity.GetFriendlyTitle(warden.AgentId), model.Dossier.Name);
        }

        [Fact]
        public void TheWordsOfACalling()
        {
            var skills = default(SkillSet);
            skills.Set(Skill.Medicine, 3);
            var traits = default(CrawlerTraits);
            traits.SetCalling(Calling.FieldSurgeon);
            Assert.Equal("Field Surgeon (carrier)", CallingTexts.TitleWithRole(skills, traits));
            traits.SetCalling(Calling.Smith);
            Assert.Equal("Smith", CallingTexts.TitleWithRole(skills, traits)); // a craft plays no part in a party
            Assert.Equal(string.Empty, CallingTexts.TitleWithRole(skills, default));
            Assert.Equal("Smith · Idle", CallingTexts.Before("Smith", "Idle"));
            Assert.Equal("Idle", CallingTexts.Before(string.Empty, "Idle"));
        }
    }
}
