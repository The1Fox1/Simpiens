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
        bool AbortIntent(EntityId agentId);
        void RegisterResource(ResourceData data);
        void RegisterAnchor(EntityId id, Vec2 position, float radius = 3.0f);
        void RegisterBunk(EntityId id, Vec2 position);
        void RegisterBarracksBunk(EntityId id, Vec2 position);
        void RegisterTradeStall(Simpiens.Simulation.Economy.TradeStallData stallData);
        bool TryGetTradeStall(EntityId stallId, out Simpiens.Simulation.Economy.TradeStallData stallData);
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

        /// <summary>The player's guild (Epic 10 G4).</summary>
        Simpiens.Simulation.Guild.GuildService Guild { get; }

        /// <summary>The loaded map's stations (Epic 10 Part 2): built or not, their orders and makes.</summary>
        Simpiens.Simulation.Workshops.WorkshopService Workshops { get; }

        /// <summary>The lairs and their creatures (Epic 10 Part 3, K2).</summary>
        Simpiens.Simulation.Creatures.CreatureService Creatures { get; }

        /// <summary>What has happened to the world that stays happened (Epic 10 Part 3, K10.4): the gate's breach.</summary>
        Simpiens.Simulation.World.WorldFlags WorldFlags { get; }

        /// <summary>The items on the ground (Epic 10 Part 3, K4.1).</summary>
        Simpiens.Simulation.Loot.PileService Piles { get; }

        /// <summary>A crawler dies (Epic 10 Part 3, K5.4a): its body a pile where it fell, gone from the world at the tick's end.</summary>
        void Die(WorldEntity node, Simpiens.Simulation.DeathCause cause, byte detail = 0, EntityId by = default);

        /// <summary>Danger for the fallen (K5.1): a hazard sector, or the reach of a lair with a living creature.</summary>
        bool InDanger(Vec2 position);

        /// <summary>The open hunts' parties (Epic 10 Part 3, K6.1), by bounty id.</summary>
        System.Collections.Generic.IReadOnlyList<Simpiens.Simulation.Economy.HuntRecord> Hunts { get; }

        /// <summary>A claim on a hunt joins its party while it has room (K6.1); true when the crawler is in it.</summary>
        bool TryJoinHunt(ushort bountyId, EntityId crawler);

        /// <summary>Fills the map's lairs on the session's seed (K2.1).</summary>
        void PopulateLairs(ulong seed);

        /// <summary>The creatures' phase, after the crawlers' runtime (seam A4).</summary>
        void TickCreatures(uint tick);

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

        /// <summary>
        /// Intent handlers that threw since this simulation was built (ROADMAP §1A.2 #17), respawns included: each was
        /// aborted, logged at Error and counted here, so a live scenario fails on any. <see cref="FirstIntentFault"/>
        /// describes the first (tick, agent, intent, exception), or is null.
        /// </summary>
        int IntentFaults { get; }

        /// <summary>
        /// Practice granted (Epic 11, S0.2, D12): the crawler, the skill, the experience it gained after every ceiling, and
        /// its source (its kind, ceiling and challenge: S1.7 compares a blow on a beetle with one on a rat). Raised on the main
        /// thread from practice's one entry, for observers (measurements, the live scenarios, <c>OutpostWatch</c>); no rule
        /// reads it.
        /// </summary>
        event System.Action<Simpiens.Cognition.AgentState, Simpiens.Simulation.Skills.Skill, int, Simpiens.Simulation.Skills.PracticeSource> Practised;

        /// <summary>An item served at a venue, with its price (Epic LIFE-1 plan 2, T.5): the tavern's takings, observed.</summary>
        event System.Action<EntityId, Simpiens.Simulation.Economy.ItemType, uint> Served;

        /// <summary>The simulation's seed (Epic LIFE-1 plan 2, W.2: a want's roll on waking is on it).</summary>
        ulong Seed { get; }
        string FirstIntentFault { get; }
    }
}
