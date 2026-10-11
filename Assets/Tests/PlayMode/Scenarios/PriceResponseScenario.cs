using Simpiens.Core.Session;
using System.Collections;
using System.IO;
using System.Text;
using NUnit.Framework;
using Simpiens.Intervention;
using Simpiens.Simulation;
using Simpiens.Testing.Diagnostics;
using Simpiens.Testing.Environment;
using UnityEngine;
using VContainer;

namespace Simpiens.Tests.PlayMode.Scenarios
{
    /// <summary>
    /// Definition-of-Done scenario for review §3.2 ("if all agents accept a price below X and refuse above X, the price
    /// slider is an on/off switch"). Runs the real session and changes the ration price only through the player's own
    /// lever (UpdateStallPricesCommand via IPlayerInterventionService), injecting nothing else. It observes how many
    /// distinct crawlers buy at each price step. Personalities and hunger give each crawler its own reservation price,
    /// so demand must fall off in more than one step instead of switching from everyone to no one.
    /// </summary>
    public class PriceResponseScenario : ISimulationScenario
    {
        /// <summary>Ration prices in order. 60g is above any reservation price (max 45g), so nobody may buy there.</summary>
        public static readonly ushort[] PriceSteps = { 10, 15, 20, 25, 30, 60 };
        public const uint StepTicks = 1500; // 2.5 sim-minutes: every crawler gets hungry enough to shop at least once
        private const float WallClockTimeoutSeconds = 300f;
        public const string ResultsPath = "Logs/price-response.csv";

        public string Name => "PriceResponseScenario";
        public float DurationSeconds => 0f;
        public float TimeMultiplier => 10f;

        private readonly int[] _buyers = new int[PriceSteps.Length];
        private readonly uint[] _purchases = new uint[PriceSteps.Length];
        private int _completedSteps;
        private long _observedTicks;

        public IEnumerator SetupAsync(IObjectResolver resolver)
        {
            var clock = resolver.Resolve<ISimulationClock>();
            var sim = resolver.Resolve<ISimulationManager>();
            var spawner = resolver.Resolve<SwarmSpawner>();
            var intervention = resolver.Resolve<IPlayerInterventionService>();
            Assert.IsNotNull(clock, "ISimulationClock not registered.");
            Assert.IsNotNull(sim?.EconomyLedger, "IEconomyLedger not wired.");
            Assert.IsNotNull(spawner, "SwarmSpawner not registered.");
            Assert.IsNotNull(intervention, "IPlayerInterventionService not registered.");
            Assert.Greater(sim.ActiveStalls.Count, 0, "The session has no trade stall.");

            if (clock is SimulationClock simClock) simClock.SetTimeMultiplier(TimeMultiplier);

            var agents = spawner.Agents;
            int crawlerCount = agents.Count - SwarmSpawner.ScriptedPenAgentCount;
            Assert.Greater(crawlerCount, 1, "Need several crawlers to observe a demand curve.");
            var lastGold = new int[crawlerCount];
            var boughtThisStep = new bool[crawlerCount];

            long startTick = clock.CurrentTick;
            float elapsed = 0f;
            int step = -1;
            uint stepStartPurchases = 0;

            while (elapsed < WallClockTimeoutSeconds)
            {
                long rel = clock.CurrentTick - startTick;
                _observedTicks = rel;
                int wantedStep = (int)(rel / StepTicks);

                if (wantedStep != step)
                {
                    if (step >= 0) CloseStep(step, boughtThisStep, sim.EconomyLedger.Flows.Purchases - stepStartPurchases);
                    if (wantedStep >= PriceSteps.Length) break;

                    step = wantedStep;
                    var stall = sim.ActiveStalls[0];
                    intervention.QueueInfluence(InfluenceEvent.CreateUpdateStallPrices("Player",
                        new UpdateStallPricesCommand(stall.StallEntityId, PriceSteps[step], stall.OreBuyPrice, stall.HerbBuyPrice)));
                    stepStartPurchases = sim.EconomyLedger.Flows.Purchases;
                    for (int c = 0; c < crawlerCount; c++)
                    {
                        boughtThisStep[c] = false;
                        lastGold[c] = CrawlerGold(agents, c);
                    }
                }

                // Crawlers only spend gold on rations, so any drop in a crawler's gold is a purchase
                for (int c = 0; c < crawlerCount; c++)
                {
                    int gold = CrawlerGold(agents, c);
                    if (gold < lastGold[c]) boughtThisStep[c] = true;
                    lastGold[c] = gold;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            WriteResults();
        }

        private static int CrawlerGold(System.Collections.Generic.IReadOnlyList<Simpiens.Cognition.AutonomousAgent> agents, int crawler)
        {
            var agent = agents[SwarmSpawner.ScriptedPenAgentCount + crawler];
            return agent != null && agent.State?.Inventory != null ? agent.State.Inventory.Gold : 0;
        }

        private void CloseStep(int step, bool[] bought, uint purchases)
        {
            int buyers = 0;
            for (int c = 0; c < bought.Length; c++) if (bought[c]) buyers++;
            _buyers[step] = buyers;
            _purchases[step] = purchases;
            _completedSteps = step + 1;
            Debug.Log($"[PriceResponseScenario] ration {PriceSteps[step]}g: {buyers} distinct buyers, {purchases} purchases");
        }

        private void WriteResults()
        {
            var sb = new StringBuilder("ration_price,distinct_buyers,purchases\n");
            for (int i = 0; i < _completedSteps; i++)
            {
                sb.Append(PriceSteps[i]).Append(',').Append(_buyers[i]).Append(',').Append(_purchases[i]).Append('\n');
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ResultsPath));
            File.WriteAllText(ResultsPath, sb.ToString());
        }

        public void AssertResults(IVisualAnomalyDetector detector)
        {
            Assert.AreEqual(PriceSteps.Length, _completedSteps,
                $"Only {_completedSteps}/{PriceSteps.Length} price steps completed by tick {_observedTicks} (wall-clock timeout).");

            int last = PriceSteps.Length - 1;
            Assert.Greater(_buyers[0], 0, $"Nobody bought at the lowest price ({PriceSteps[0]}g).");
            Assert.AreEqual(0u, _purchases[last], $"Purchases at {PriceSteps[last]}g, above every reservation price.");

            // Not an on/off switch: demand must take at least three distinct levels across the price steps
            // (e.g. 5 buyers -> 3 -> 0), i.e. at least one intermediate price where some, but not all, keep buying
            int distinctLevels = 0;
            for (int i = 0; i < PriceSteps.Length; i++)
            {
                bool seen = false;
                for (int j = 0; j < i; j++) if (_buyers[j] == _buyers[i]) seen = true;
                if (!seen) distinctLevels++;
            }
            Assert.GreaterOrEqual(distinctLevels, 3,
                $"Demand is binary: distinct buyer counts per step were {string.Join("/", _buyers)} at prices {string.Join("/", PriceSteps)}g.");
        }
    }
}
