using System.Collections.Generic;
using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Items;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// How often a creature downs or kills a crawler (Epic 11, S7.0), the trigger of a named creature (D31): every gate seed over
    /// 14 game days, in four kinds of play: no player; the soak's hunting players; the guild's clears (the forge's player and the
    /// guild's four armed from the stall under the Steady rule, as <c>TheOutpostRemembers</c>); and <c>DeathHasItsCost</c>'s
    /// pairs, two unarmed against the beetles under the Bold rule. Each downing is credited to the creature whose blow took the
    /// crawler to nothing (its <c>StruckBy</c>), so the run counts the creatures that would earn a name, how many each downs (its
    /// rank, to 3), whether it is slain after and by whom (the avenger, D35). Explicit, several minutes. The results go to
    /// docs/skills-model.md §5.11.
    /// </summary>
    public class CreatureDowningsMeasurementTests
    {
        private sealed class Downings
        {
            public int ByCreature, ByHazard, Deaths, ByBoss;
            public long First = -1, FirstWithinTen = -1;
            public readonly Dictionary<EntityId, int> PerCreature = new Dictionary<EntityId, int>();
            public readonly Dictionary<EntityId, string> Lair = new Dictionary<EntityId, string>();
            public readonly HashSet<EntityId> SlainAfter = new HashSet<EntityId>();
            public readonly Dictionary<string, int> ByLair = new Dictionary<string, int>();
            public int Named => PerCreature.Count;
            public int RankTwo => PerCreature.Values.Count(n => n >= 2);
            public int RankThree => PerCreature.Values.Count(n => n >= 3);

            public override string ToString() =>
                $"downed by creatures {ByCreature} (bosses {ByBoss}), by hazards {ByHazard}, deaths {Deaths}; first {(First < 0 ? "never" : $"t{First} (day {First / DayClock.TicksPerDay + 1})")}; " +
                $"would be named {Named} (rank 2: {RankTwo}, rank 3: {RankThree}), slain after {SlainAfter.Count}; by lair [{string.Join(", ", ByLair.Select(kv => kv.Key + " " + kv.Value))}]";
        }

        private static Downings Play(ulong seed, string scenario, long horizon)
        {
            using var sim = LocalSimulation.Create(seed);
            sim.Respawn(OutpostSession.SessionId);
            switch (scenario)
            {
                case "hunting":
                    EconomySoak.AttachPlayers(sim, "hunting");
                    break;
                case "clears":
                    new WorkshopPlayer(sim, StationKind.Forge, ItemType.IronBlade, batch: 4);
                    new GuildPlayer(sim, targetSize: 4).SendClear();
                    break;
                case "bold-pairs":
                    new WorkshopPlayer(sim, StationKind.Forge, ItemType.IronBlade, batch: 4);
                    new GuildPlayer(sim, targetSize: 2).SendClear(CreatureKind.StoneBeetle, RetreatRule.Bold, arm: false);
                    break;
            }
            var watch = new OutpostWatch(sim);
            var run = new Downings();
            var told = new List<ChronicleEvent>(64);
            long seen = -1;
            var creatures = sim.Simulation.Creatures;
            while (sim.Clock.CurrentTick < horizon)
            {
                watch.Run(1);
                told.Clear();
                sim.Chronicle.GetAllRecentEvents(told, 64);
                long newest = seen;
                foreach (var e in told)
                {
                    if (e.SimulationTick <= seen) continue; // each tick's events once: they all arrive with their tick
                    if (e.SimulationTick > newest) newest = e.SimulationTick;
                    if (e.EventType == ChronicleEventType.CrawlerDowned)
                    {
                        if (e.ActorBId == default) { run.ByHazard++; continue; }
                        run.ByCreature++;
                        if (run.First < 0) run.First = e.SimulationTick;
                        if (creatures.TryGet(e.ActorBId, out var downer))
                        {
                            if (downer.Definition != null && downer.Definition.IsBoss) { run.ByBoss++; continue; } // a boss has a name (D31)
                            string lair = creatures.Lairs[downer.LairIndex].Definition.Key;
                            run.Lair[downer.Id] = lair;
                            run.ByLair[lair] = (run.ByLair.TryGetValue(lair, out int l) ? l : 0) + 1;
                        }
                        run.PerCreature[e.ActorBId] = (run.PerCreature.TryGetValue(e.ActorBId, out int n) ? n : 0) + 1;
                    }
                    else if (e.EventType == ChronicleEventType.CrawlerDied && e.ActorBId != default) run.Deaths++;
                    else if (e.EventType == ChronicleEventType.CreatureSlain && run.PerCreature.ContainsKey(e.ActorBId)) run.SlainAfter.Add(e.ActorBId);
                }
                seen = newest;
            }
            watch.Finish();
            return run;
        }

        [Fact(Explicit = true)]
        public void MeasureDowningsOnTheGateSeeds()
        {
            long horizon = (System.Environment.GetEnvironmentVariable("SIMPIENS_DAYS") is string d && d.Length > 0 ? long.Parse(d) : 14) * DayClock.TicksPerDay;
            var scenarios = System.Environment.GetEnvironmentVariable("SIMPIENS_SCENARIOS") is string s && s.Length > 0
                ? s.Split(',') : new[] { "none", "hunting", "clears", "bold-pairs" };
            var seeds = OutpostWatch.GateSeeds;
            foreach (var scenario in scenarios)
            {
                var runs = new Downings[seeds.Length];
                System.Threading.Tasks.Parallel.For(0, seeds.Length, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 8 },
                    i => runs[i] = Play(seeds[i], scenario, horizon));
                var output = TestContext.Current.TestOutputHelper;
                output?.WriteLine($"== {scenario}, {horizon / DayClock.TicksPerDay} days ==");
                for (int i = 0; i < seeds.Length; i++) output?.WriteLine($"seed {seeds[i]}: {runs[i]}");
                int downed = runs.Count(r => r.ByCreature > 0), withinTen = runs.Count(r => r.First >= 0 && r.First < 10 * DayClock.TicksPerDay);
                int named = runs.Count(r => r.Named > 0), slain = runs.Count(r => r.SlainAfter.Count > 0);
                output?.WriteLine($"{scenario}: a creature downed a crawler on {downed} of {seeds.Length} seeds ({withinTen} within ten days); " +
                    $"downings {runs.Sum(r => r.ByCreature)} (bosses {runs.Sum(r => r.ByBoss)}), deaths {runs.Sum(r => r.Deaths)}, by hazards {runs.Sum(r => r.ByHazard)}; " +
                    $"would be named on {named} seeds ({runs.Sum(r => r.Named)} creatures, rank 2: {runs.Sum(r => r.RankTwo)}, rank 3: {runs.Sum(r => r.RankThree)}); " +
                    $"a named creature slain after on {slain} seeds ({runs.Sum(r => r.SlainAfter.Count)})");
            }
        }
    }
}
