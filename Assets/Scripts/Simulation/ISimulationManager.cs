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
        void RecordStoryEvent(in Simpiens.Narrative.ChronicleEvent evt);
        void ResetState();
    }
}
