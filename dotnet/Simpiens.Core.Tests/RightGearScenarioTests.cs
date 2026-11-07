using System.Collections.Generic;
using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Items;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// W3's DoD, <c>TheRightGearForTheRightHazard</c> (Epic 10 Part 2), live and headless with nothing injected, on the
    /// default seed. With the guild: the scripted players build and staff the forge, which makes respirators from the
    /// stall's glowstone and herbs, and send the guild to the Drowned Gallery, outfitted for its flooded air (D50), so its
    /// trips go from hazard gear to respirators as the forge makes them: in the Gallery a member in a respirator takes half
    /// the harm of one in hazard gear, and trips all in respirators bring home as much as trips with members in hazard gear. Without the guild, the forge's respirators are sold: a
    /// bold resident trades its gear for one (D53) and works the Gallery alone (D51). The ledger balances and the days'
    /// checks hold.
    /// </summary>
    public class RightGearScenarioTests
    {
        private const long Horizon = 6 * DayClock.TicksPerDay;

        [Fact]
        public void TheRightGearForTheRightHazard()
        {
            // With the guild: the harm by gear, tick by tick in the Gallery at work, and each trip's gear and haul
            using (var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed))
            {
                sim.Respawn(OutpostSession.SessionId);
                var forge = new WorkshopPlayer(sim, StationKind.Forge, ItemType.Respirator);
                var player = new GuildPlayer(sim, targetSize: 4);
                player.SendExpeditions(Floor1Layout.DrownedGallerySectorId, Floor1Layout.GallerySeamYield, RetreatRule.Steady);
                var watch = new OutpostWatch(sim);
                var guild = sim.Simulation.Guild;
                var host = (IGuildHost)sim.Simulation;
                var map = sim.Host.Current.Map;
                var harm = new Dictionary<ItemType, (double Damage, int Ticks)>();
                var lastDamage = new Dictionary<EntityId, float>();
                var trips = new List<(int Respirators, int Haul, bool Back)>();
                int respiratorsOnTrip = 0, haul = 0;
                bool active = false, back = false;
                while (sim.Clock.CurrentTick < Horizon && !(harm.ContainsKey(ItemType.Respirator) && harm.ContainsKey(ItemType.HazardGear) && trips.Any(t => t.Respirators < 4) && trips.Any(t => t.Respirators == 4)))
                {
                    watch.Run(1);
                    var expedition = guild.Parties[0].Expedition;
                    if (!expedition.IsActive)
                    {
                        if (active) trips.Add((respiratorsOnTrip, haul, back));
                        active = false;
                        continue;
                    }
                    if (!active) { active = true; respiratorsOnTrip = 0; haul = 0; }
                    foreach (var member in guild.Parties[0].Members)
                    {
                        if (!member.IsWithParty || !host.TryGetCrawler(member.CrawlerId, out var crawler)) continue;
                        float before = lastDamage.TryGetValue(member.CrawlerId, out var d) ? d : crawler.Activity.HazardDamage;
                        lastDamage[member.CrawlerId] = crawler.Activity.HazardDamage;
                        var worn = crawler.Inventory.GetEquipped(EquipmentSlot.Protection).Type;
                        if (expedition.Stage == ExpeditionStage.Work && map.FindSector(crawler.Position).Id == Floor1Layout.DrownedGallerySectorId && crawler.Activity.HazardDamage > before)
                        {
                            var (sum, ticks) = harm.TryGetValue(worn, out var h) ? h : (0d, 0);
                            harm[worn] = (sum + crawler.Activity.HazardDamage - before, ticks + 1);
                        }
                    }
                    if (expedition.Stage == ExpeditionStage.Work && respiratorsOnTrip == 0)
                        respiratorsOnTrip = guild.Parties[0].Members.Count(m => m.IsWithParty && host.TryGetCrawler(m.CrawlerId, out var c) && c.Inventory.GetEquipped(EquipmentSlot.Protection).Type == ItemType.Respirator);
                    if (expedition.Stage == ExpeditionStage.Work || expedition.Stage == ExpeditionStage.Return) haul = System.Math.Max(haul, guild.Parties[0].Gathered(host));
                    back = expedition.TurnedBack;
                }
                watch.Finish();
                double Rate(ItemType gear) => harm.TryGetValue(gear, out var h) && h.Ticks > 0 ? h.Damage / h.Ticks : 0d;
                TestContext.Current.TestOutputHelper?.WriteLine($"with the guild: respirators made {sim.Simulation.Workshops[sim.Simulation.Workshops.IndexOf(StationKind.Forge)].Made}; harm a tick in the Gallery: hazard gear {Rate(ItemType.HazardGear):F4} over {(harm.TryGetValue(ItemType.HazardGear, out var g) ? g.Ticks : 0)} ticks, respirator {Rate(ItemType.Respirator):F4} over {(harm.TryGetValue(ItemType.Respirator, out var r) ? r.Ticks : 0)}; trips {string.Join(", ", trips.Select(t => $"{t.Respirators} in respirators, haul {t.Haul}{(t.Back ? " back" : "")}"))}; ended t{sim.Clock.CurrentTick}\n{watch.Report()}");

                Assert.True(forge.BuiltTick >= 0 && player.Dispatches > 0, "The forge was built and the guild sent parties.");
                Assert.True(Rate(ItemType.HazardGear) > 0 && Rate(ItemType.Respirator) > 0, "Members worked the Gallery in hazard gear and in respirators.");
                Assert.InRange(Rate(ItemType.Respirator) / Rate(ItemType.HazardGear), 0.45, 0.55); // half the harm
                var inGear = trips.Where(t => t.Respirators < 4).ToList(); // members in hazard gear: the forge's respirators weren't enough yet
                var inRespirators = trips.Where(t => t.Respirators == 4).ToList();
                Assert.True(inGear.Count > 0 && inRespirators.Count > 0, "A trip with members in hazard gear, and one all in respirators.");
                Assert.True(inRespirators.Min(t => t.Haul) >= inGear.Max(t => t.Haul) && inRespirators.All(t => !t.Back), "In respirators the party brought home as much.");
                Assert.Equal(0, watch.ConservationViolations);
                var failures = watch.DayFailures();
                Assert.True(failures.Count == 0, "The days failed:\n  " + string.Join("\n  ", failures));
            }

            // Without the guild: the forge's respirators are sold, and a bold resident works the Gallery alone in one
            using (var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed))
            {
                sim.Respawn(OutpostSession.SessionId);
                var forge = new WorkshopPlayer(sim, StationKind.Forge, ItemType.Respirator);
                var map = sim.Host.Current.Map;
                long alone = -1;
                EntityId loner = default;
                while (sim.Clock.CurrentTick < Horizon && alone < 0)
                {
                    sim.Step(1);
                    foreach (var crawler in sim.Host.CrawlerStates)
                    {
                        if (crawler.Inventory.GetEquipped(EquipmentSlot.Protection).Type != ItemType.Respirator) continue;
                        if (map.FindSector(crawler.Position).Id != Floor1Layout.DrownedGallerySectorId) continue;
                        alone = sim.Clock.CurrentTick;
                        loner = crawler.AgentId;
                        break;
                    }
                }
                var bought = sim.Host.CrawlerStates.FirstOrDefault(s => s.AgentId == loner);
                TestContext.Current.TestOutputHelper?.WriteLine($"without the guild: alone in the Gallery in a respirator at t{alone} (day {DayClock.Day(alone)}); risk {bought?.Personality.RiskTolerancePercent}%, gear bought {bought?.Activity.GearPurchases}");
                Assert.True(alone >= 0, "A bold resident worked the Gallery alone in a respirator it bought.");
                Assert.True(bought.Activity.GearPurchases >= 1 && !bought.IsDrifter && bought.Personality.RiskTolerancePercent >= Simpiens.Cognition.Vitality.VentureRiskTolerancePercent, "It was a bold resident, and it bought its respirator.");
                Assert.False(sim.Simulation.Guild.HasExpedition, "Alone: no guild, no party.");
                Assert.Equal(0, sim.Simulation.IntentFaults);
            }
        }
    }
}
