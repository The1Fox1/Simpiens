using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Simpiens.Cognition;
using Simpiens.Cognition.Social;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Skills;
using Simpiens.Simulation.Workshops;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// The scripted players pay raises (Epic 11, S2.3, D8), through typed commands only: when a member's or its artisan's
    /// asking wage rises above its wage, the guild's and the workshop's players set the wage to what it asks, within their
    /// reserves; told to leave raises unpaid, they don't. Mechanism tests: the trust to sign and the band reached are set by
    /// hand on the real outpost.
    /// </summary>
    public class ScriptedRaisesTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TheGuildPlayerPaysARaise(bool pays)
        {
            using var sim = Outpost();
            var player = new GuildPlayer(sim, targetSize: 1);
            if (!pays) player.LeaveRaisesUnpaid();
            var member = Trusted(sim).First();
            var guild = sim.Simulation.Guild;
            for (int t = 0; t < 20 && !guild.IsMember(member.AgentId); t++) sim.Step(1);
            Assert.True(guild.IsMember(member.AgentId), "The player signs the trusted resident on.");
            Assert.True(guild.TryGetContract(member.AgentId, out var signed));
            Assert.Equal(0, player.RaisesPaid);

            Reach(member, Skill.Mining, 6);
            ushort asking = GuildRules.AskingWage(member, sim.Simulation.Economy.Guild);
            Assert.True(asking > signed.Wage, $"A Journeyman asks more ({asking}g) than it signed for ({signed.Wage}g).");
            sim.Step(3);
            Assert.True(guild.TryGetContract(member.AgentId, out var now));
            Assert.Equal(pays ? asking : signed.Wage, now.Wage);
            Assert.Equal(pays ? 1 : 0, player.RaisesPaid);
            sim.Step(3);
            Assert.Equal(pays ? 1 : 0, player.RaisesPaid); // once
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TheWorkshopPlayerPaysItsArtisansRaise(bool pays)
        {
            using var sim = Outpost();
            var player = new WorkshopPlayer(sim, StationKind.Forge, ItemType.ToolPickaxe);
            if (!pays) player.LeaveRaisesUnpaid();
            Trusted(sim);
            var workshops = sim.Simulation.Workshops;
            int forge = workshops.IndexOf(StationKind.Forge);
            for (int t = 0; t < 20 && workshops.ArtisanAt(forge) == default; t++) sim.Step(1);
            var artisanId = workshops.ArtisanAt(forge);
            Assert.NotEqual(default, artisanId);
            var artisan = sim.Host.CrawlerStates.First(s => s.AgentId == artisanId);
            Assert.True(workshops.TryGetContract(artisanId, out var hired));

            Reach(artisan, Skill.Smithing, 9);
            artisan.Personality = new Simpiens.Cognition.Contracts.AgentPersonality(0f, artisan.Personality.RiskTolerance, 0f, 0f, 0f); // its reservation the neutral 5g
            ushort asking = WorkshopService.AskingWage((IWorkshopHost)sim.Simulation, artisan);
            Assert.True(asking > hired.Wage, $"A Master asks more ({asking}g) than it was hired for ({hired.Wage}g).");
            sim.Step(6);
            Assert.True(workshops.TryGetContract(artisanId, out var now));
            Assert.Equal(pays ? asking : hired.Wage, now.Wage);
            Assert.Equal(pays ? 1 : 0, player.RaisesPaid);
        }

        private static LocalSimulation Outpost()
        {
            var economy = EconomyTuning.Default.WithStallTreasury(1000);
            var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed, services =>
            {
                services.RemoveAll<ISessionDefinition>();
                services.AddSingleton<ISessionDefinition>(new OutpostSession(OutpostSession.DefaultCrawlerCount, economy));
            });
            sim.Respawn(OutpostSession.SessionId);
            return sim;
        }

        // Every resident trusts the player enough to sign on or take station work (by hand)
        private static System.Collections.Generic.List<AgentState> Trusted(LocalSimulation sim)
        {
            var residents = sim.Host.CrawlerStates.Where(s => !s.IsDrifter).ToList();
            foreach (var resident in residents) resident.Memory.SetAffinity(WorldRegistry.PlayerEntityId, new AffinityRecord(warmth: 60, trust: 80, fear: 0, respect: 40, interactionCount: 1, lastInteractionTick: 0));
            return residents;
        }

        private static void Reach(AgentState crawler, Skill skill, int level)
        {
            var skills = crawler.Skills;
            skills.Set(skill, level);
            crawler.Skills = skills;
        }
    }
}
