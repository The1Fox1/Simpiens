using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Simpiens.Core.Session;
using Simpiens.Simulation;
using Simpiens.Simulation.World;
using Simpiens.Testing.Diagnostics;
using Simpiens.Testing.Sessions;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Simpiens.Tests.PlayMode.Scenarios
{
    /// <summary>
    /// The Dev Map toggle (Epic MAP Phase 2): switching sessions is a full reset with the same seed. Runs the outpost,
    /// presses the dashboard's session switch button (game paused, as a producer might) to go to the Dev Map, runs it,
    /// presses it again to come back, and asserts that the outpost comes back structurally identical
    /// to a fresh boot (entity ids, positions, personalities, stall, bounties, exploration, ledger baseline) with
    /// nothing from the other session left behind. Behavioural same-seed equality is limited by the known
    /// cognition-arrival deviation (ROADMAP §1A.2 #6) and is not asserted here.
    /// </summary>
    public class SessionSwitchScenario : ISimulationScenario
    {
        private const long RunTicks = 200;
        private const float WallClockTimeoutSeconds = 60f;

        public string Name => "SessionSwitchScenario";
        public string SessionId => OutpostSession.SessionId;
        public float DurationSeconds => 0f; // SetupAsync drives both switches
        public float TimeMultiplier => 10f;

        private readonly List<string> _failures = new List<string>();

        public IEnumerator SetupAsync(IObjectResolver resolver)
        {
            // Loading the test scene destroyed the boot-time dashboard: build a real one exactly as boot does
            GameObject createdDashboard = null;
            if (GameObject.Find("[FacilityDashboard]") == null)
            {
                createdDashboard = Simpiens.Core.Bootstrapping.SimulationStarter.CreateDashboard(resolver);
                yield return null; // presenter Start
            }

            try
            {
                yield return RunSwitches(resolver);
            }
            finally
            {
                if (createdDashboard != null) Object.Destroy(createdDashboard);
            }
        }

        private IEnumerator RunSwitches(IObjectResolver resolver)
        {
            var spawner = resolver.Resolve<SwarmSpawner>();
            var switcher = resolver.Resolve<ISessionSwitcher>();
            var clock = resolver.Resolve<ISimulationClock>();
            var sim = resolver.Resolve<ISimulationManager>();
            var sectors = resolver.Resolve<ISectorRegistry>();
            Assert.IsNotNull(spawner, "SwarmSpawner not registered.");
            Assert.AreSame(spawner, switcher, "The spawner is the session switcher.");

            var outpost = spawner.Find(OutpostSession.SessionId);
            var devMap = spawner.Find(CanaryPenSession.SessionId);
            Assert.IsNotNull(outpost, "Outpost session not registered.");
            Assert.IsNotNull(devMap, "Dev Map session not registered by the test harness.");
            Assert.AreSame(outpost, switcher.Default, "The outpost is the default (boot) session.");
            Assert.AreEqual(2, switcher.Sessions.Count, "Exactly the outpost and the Dev Map are registered in test builds.");

            if (clock is SimulationClock simClock) simClock.SetTimeMultiplier(TimeMultiplier);

            // 1. A fresh outpost, captured before any tick runs
            switcher.SwitchTo(outpost);
            var fresh = Capture(spawner, clock, sim, sectors);
            Check(fresh.Tick == 0, $"Respawn rewinds the clock to 0 (got {fresh.Tick}).");
            Check(fresh.MapName == "OutpostCommons", $"Outpost map loaded (got {fresh.MapName}).");
            Check(fresh.AgentCount == OutpostSession.CrawlerCount, $"Outpost spawns {OutpostSession.CrawlerCount} crawlers (got {fresh.AgentCount}).");
            yield return RunFor(clock, RunTicks);

            // 2. Press the switch: Dev Map, pens only, nothing from the outpost survives
            var ui = DashboardRoot();
            Check(IsShown(ui?.Q<VisualElement>("session-switch-overlay")), "Session switch is shown when a test session is registered.");
            Check(!IsShown(ui?.Q<Label>("session-banner")), "No test-session banner on the main map.");
            Check(ui?.Q<Button>("btn-session-switch")?.text == "SWITCH TO DEV MAP", $"Switch button offers the Dev Map (got '{ui?.Q<Button>("btn-session-switch")?.text}').");
            yield return PressSessionSwitch(ui, switcher, clock, devMap);
            Check(switcher.Current == devMap, "Current session is the Dev Map after the switch.");
            Check(IsShown(ui?.Q<Label>("session-banner")), "Test-session banner is shown on the Dev Map.");
            Check(ui?.Q<Button>("btn-session-switch")?.text == "SWITCH TO MAIN MAP", $"Switch button offers the main map (got '{ui?.Q<Button>("btn-session-switch")?.text}').");
            Check(clock.CurrentTick == 0, $"Switch rewinds the clock (got {clock.CurrentTick}).");
            Check(spawner.World.Map.Name == "DevMap", $"Dev Map loaded (got {spawner.World.Map.Name}).");
            Check(spawner.Agents.Count == CanaryPenSession.ScriptedPenAgentCount, $"Dev Map spawns the 24 pen agents (got {spawner.Agents.Count}).");
            Check(sim.ActiveStalls.Count == 0 && sim.ActiveBounties.Count == 0, "No stall or bounty survives the switch.");
            Check(OnlySetupEvents(sim), "Chronicle cleared by the switch: only the new session's tick-0 setup events remain.");
            Check(ActiveIntentCount(sim) == 0, "No intent from the outpost survives the switch.");
            Check(RootChildCount("[SwarmAgents]") == CanaryPenSession.ScriptedPenAgentCount, $"Scene holds only the Dev Map's agents (got {RootChildCount("[SwarmAgents]")}).");
            int devMapProps = DevMapLayout.Definition.ResourceNodes.Count + CountHearths(DevMapLayout.Definition);
            Check(RootChildCount("[MapEntities]") == devMapProps, $"Scene holds only the Dev Map's props ({devMapProps}, got {RootChildCount("[MapEntities]")}).");
            Check(sim.EconomyLedger == null || sim.EconomyLedger.TotalFaucetedGold == AgentGold(spawner), "Dev Map ledger baseline is the pen agents' gold.");
            if (clock is SimulationClock running) running.SetTimeMultiplier(TimeMultiplier);
            yield return RunFor(clock, RunTicks);

            // 3. Press it again: back to the outpost, structurally identical to the fresh boot
            yield return PressSessionSwitch(ui, switcher, clock, outpost);
            var again = Capture(spawner, clock, sim, sectors);
            Check(!IsShown(ui?.Q<Label>("session-banner")), "Banner hidden again on the main map.");
            Check(ActiveIntentCount(sim) == 0, "No intent from the Dev Map survives the switch back.");
            Check(RootChildCount("[SwarmAgents]") == OutpostSession.CrawlerCount, $"Scene holds only the outpost's agents (got {RootChildCount("[SwarmAgents]")}).");
            fresh.CompareTo(again, _failures);
            if (clock is SimulationClock resume) resume.SetTimeMultiplier(TimeMultiplier);
            yield return RunFor(clock, RunTicks);

            var ledgerOk = sim.EconomyLedger == null || sim.EconomyLedger.ValidateConservation(TotalStallGold(sim), (uint)AgentGold(spawner), out _);
            Check(ledgerOk, "Ledger conserved after running the restored outpost.");
        }

        public void AssertResults(IVisualAnomalyDetector detector)
        {
            Assert.AreEqual(0, detector.PenetrationCount, $"Wall penetration across the session switches: {detector.PenetrationCount}");
            Assert.AreEqual(0, detector.BoundaryBreachCount, $"Boundary breach across the session switches: {detector.BoundaryBreachCount}");
            Assert.IsTrue(_failures.Count == 0, "Session switch failures:\n  " + string.Join("\n  ", _failures));
        }

        private void Check(bool condition, string message)
        {
            if (!condition) _failures.Add(message);
        }

        private IEnumerator RunFor(ISimulationClock clock, long ticks)
        {
            long start = clock.CurrentTick;
            float elapsed = 0f;
            while (clock.CurrentTick - start < ticks && elapsed < WallClockTimeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Check(clock.CurrentTick - start >= ticks, $"Wall-clock timeout: ran {clock.CurrentTick - start}/{ticks} ticks.");
        }

        /// <summary>After a switch the chronicle holds only events the new session recorded while spawning (tick 0).</summary>
        private static bool OnlySetupEvents(ISimulationManager sim)
        {
            if (sim.Chronicle == null) return true;
            var events = new List<Simpiens.Narrative.ChronicleEvent>(64);
            sim.Chronicle.GetAllRecentEvents(events, Simpiens.Narrative.NarrativeChronicleService.DefaultGlobalCapacity);
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].SimulationTick != 0) return false;
            }
            return true;
        }

        private static VisualElement DashboardRoot()
        {
            var go = GameObject.Find("[FacilityDashboard]");
            var doc = go != null ? go.GetComponent<UIDocument>() : null;
            return doc != null ? doc.rootVisualElement : null;
        }

        private static bool IsShown(VisualElement element)
        {
            return element != null && element.style.display.value != DisplayStyle.None;
        }

        /// <summary>
        /// Pauses the game, presses the dashboard's session switch through UI Toolkit (the button's submit action) and
        /// waits for the respawn. The clock stays paused, so the new session is captured at tick 0.
        /// </summary>
        private IEnumerator PressSessionSwitch(VisualElement ui, ISessionSwitcher switcher, ISimulationClock clock, ISessionDefinition expected)
        {
            if (clock is SimulationClock simClock) simClock.SetTimeMultiplier(0f);
            var button = ui?.Q<Button>("btn-session-switch");
            Check(button != null, "Dashboard has the session switch button.");
            if (button == null)
            {
                switcher.SwitchTo(expected);
                yield break;
            }

            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }

            float elapsed = 0f;
            while (switcher.Current != expected && elapsed < 5f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Check(switcher.Current == expected, $"Pressing the switch leads to {expected.DisplayName} (current: {switcher.Current.DisplayName}).");
            Check(clock.CurrentTick == 0, $"A paused switch starts the new session at tick 0 (got {clock.CurrentTick}).");
        }

        private static int ActiveIntentCount(ISimulationManager sim)
        {
            return sim is SimulationManager manager ? manager.ActiveIntents.Count : 0;
        }

        private static int RootChildCount(string rootName)
        {
            var root = GameObject.Find(rootName);
            return root != null ? root.transform.childCount : 0;
        }

        private static int CountHearths(MapDefinition map)
        {
            int count = 0;
            for (int i = 0; i < map.Pois.Count; i++)
            {
                if (map.Pois[i].Kind == PoiKind.Hearth) count++;
            }
            return count;
        }

        private static int AgentGold(SwarmSpawner spawner)
        {
            int gold = 0;
            for (int i = 0; i < spawner.Agents.Count; i++)
            {
                var inv = spawner.Agents[i].State?.Inventory;
                if (inv != null) gold += inv.Gold;
            }
            return gold;
        }

        private static uint TotalStallGold(ISimulationManager sim)
        {
            uint gold = 0;
            for (int i = 0; i < sim.ActiveStalls.Count; i++) gold += sim.ActiveStalls[i].TreasuryGold;
            return gold;
        }

        private static WorldCapture Capture(SwarmSpawner spawner, ISimulationClock clock, ISimulationManager sim, ISectorRegistry sectors)
        {
            var c = new WorldCapture
            {
                Tick = clock.CurrentTick,
                MapName = spawner.World.Map.Name,
                AgentCount = spawner.Agents.Count,
                Fauceted = sim.EconomyLedger != null ? sim.EconomyLedger.TotalFaucetedGold : 0u,
                Escrow = sim.EconomyLedger != null ? sim.EconomyLedger.TotalEscrowGold : 0u,
            };
            var sb = new StringBuilder();
            for (int i = 0; i < spawner.Agents.Count; i++)
            {
                var a = spawner.Agents[i];
                sb.Append(a.AgentId).Append('@').Append(((Vector2)a.transform.position).ToString("F3"))
                  .Append(' ').Append(a.State?.Personality.ToString()).Append(';');
            }
            c.Agents = sb.ToString();
            for (int i = 0; i < sim.ActiveStalls.Count; i++) c.Stalls.Add(sim.ActiveStalls[i]);
            for (int i = 0; i < sim.ActiveBounties.Count; i++) c.Bounties.Add(sim.ActiveBounties[i]);
            var all = sectors.All;
            for (int i = 0; i < all.Count; i++) c.Explored.Append(all[i].Id).Append(sectors.IsExplored(all[i].Id) ? '+' : '-');
            return c;
        }

        private sealed class WorldCapture
        {
            public long Tick;
            public string MapName;
            public int AgentCount;
            public string Agents;
            public uint Fauceted;
            public uint Escrow;
            public readonly List<Simpiens.Simulation.Economy.TradeStallData> Stalls = new List<Simpiens.Simulation.Economy.TradeStallData>(2);
            public readonly List<Simpiens.Simulation.Economy.BountyContract> Bounties = new List<Simpiens.Simulation.Economy.BountyContract>(4);
            public readonly StringBuilder Explored = new StringBuilder();

            public void CompareTo(WorldCapture other, List<string> failures)
            {
                if (other.Tick != Tick) failures.Add($"Restored outpost starts at tick {other.Tick}, fresh at {Tick}.");
                if (other.MapName != MapName) failures.Add($"Restored map '{other.MapName}' differs from '{MapName}'.");
                if (other.Agents != Agents) failures.Add("Restored agents (ids, positions or personalities) differ from the fresh boot.");
                if (other.Fauceted != Fauceted || other.Escrow != Escrow) failures.Add($"Restored ledger (faucets {other.Fauceted}g, escrow {other.Escrow}g) differs from fresh ({Fauceted}g, {Escrow}g).");
                if (!SameList(Stalls, other.Stalls)) failures.Add("Restored stall terms differ from the fresh boot.");
                if (!SameList(Bounties, other.Bounties)) failures.Add("Restored seeded bounties differ from the fresh boot (status, expiry or escrow).");
                if (other.Explored.ToString() != Explored.ToString()) failures.Add($"Restored exploration '{other.Explored}' differs from fresh '{Explored}'.");
            }

            private static bool SameList<T>(List<T> a, List<T> b) where T : System.IEquatable<T>
            {
                if (a.Count != b.Count) return false;
                for (int i = 0; i < a.Count; i++)
                {
                    if (!a[i].Equals(b[i])) return false;
                }
                return true;
            }
        }
    }
}
