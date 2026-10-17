namespace Simpiens.Simulation
{
    public interface ISimulationManager
    {
        void Pause();
        void Resume();
        bool IsPaused { get; }
        void Tick();
        void Tick(long currentTick);
        void EnqueueIntent(Simpiens.Cognition.Contracts.AgentIntent intent);
        bool AbortIntent(UnityEngine.GUID agentId);
        void RegisterResource(ResourceData data);
        void RegisterAnchor(UnityEngine.GUID id, UnityEngine.Vector2 position, float radius = 3.0f);
        void RegisterBunk(UnityEngine.GUID id, UnityEngine.Vector2 position);
        void RegisterTradeStall(Simpiens.Simulation.Economy.TradeStallData stallData);
        bool TryGetTradeStall(UnityEngine.GUID stallId, out Simpiens.Simulation.Economy.TradeStallData stallData);
        System.Collections.Generic.IReadOnlyList<Simpiens.Simulation.Economy.TradeStallData> ActiveStalls { get; }
        void RegisterBounty(Simpiens.Simulation.Economy.BountyContract bounty);
        bool TryGetBounty(ushort bountyId, out Simpiens.Simulation.Economy.BountyContract bounty);
        System.Collections.Generic.IReadOnlyList<Simpiens.Simulation.Economy.BountyContract> ActiveBounties { get; }
        IGroupRegistry Groups { get; }
        IAgentRegistry Agents { get; }
        Simpiens.Narrative.INarrativeChronicleService Chronicle { get; }
        Simpiens.Simulation.Economy.IEconomyLedger EconomyLedger { get; }
        Simpiens.Simulation.Economy.ISupplyService Supply { get; }

        /// <summary>The session's economy tuning (never null; EconomyTuning.Default until a session applies one).</summary>
        Simpiens.Simulation.Economy.EconomyTuning Economy { get; }

        /// <summary>Applies a session's economy tuning: the supply caravan, trade rules and the snapshot cognition reads.</summary>
        void SetEconomy(Simpiens.Simulation.Economy.EconomyTuning economy);
        void RecordStoryEvent(in Simpiens.Narrative.ChronicleEvent evt);
        void ResetState();

        /// <summary>
        /// Writes the simulation's state into a save's economy, world, group and chronicle blocks (Milestone 1
        /// save/load). Main thread, between ticks.
        /// </summary>
        void CaptureTo(Simpiens.Simulation.Persistence.SessionSave save);

        /// <summary>
        /// Puts a save's economy, world, group and chronicle blocks back (a load), after the spawner has rebuilt the map
        /// and the crawlers. Main thread.
        /// </summary>
        void RestoreFrom(Simpiens.Simulation.Persistence.SessionSave save);

        /// <summary>The current session's number: starts at 1, and every ResetState (respawn) starts a new one.</summary>
        uint SessionEpoch { get; }

        /// <summary>
        /// Cognition on fixed ticks (ROADMAP §1A.2 #6): evaluations in flight, applied at the start of their due tick in
        /// agent-id order. Never null on <c>SimulationManager</c>.
        /// </summary>
        Simpiens.Cognition.CognitionScheduler Cognition { get; }
    }
}
