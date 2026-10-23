using System;
using Simpiens.Simulation.World;
namespace Simpiens.Narrative
{
    /// <summary>
    /// Stage 1 deterministic natural language narrative synthesizer.
    /// Turns structured chronicle events into humanized prose using contextual sentence templates.
    /// Null-safe and test-isolated.
    /// </summary>
    public class TemplateNarrativeFormatter : INarrativeFormatter
    {
        private readonly IAgentIdentityService _identityService;
        private readonly ISectorRegistry _sectors;

        public TemplateNarrativeFormatter() : this(null, null)
        {
        }

        public TemplateNarrativeFormatter(
            IAgentIdentityService identityService = null,
            ISectorRegistry sectors = null)
        {
            _identityService = identityService ?? new AgentIdentityService();
            _sectors = sectors;
        }

        public string FormatEvent(in ChronicleEvent evt)
        {
            string actorA = ResolveActorName(evt.ActorAId, "Someone");
            string actorB = ResolveActorName(evt.ActorBId, "a peer");
            string sector = ResolveSectorName(evt.SectorId);

            switch (evt.EventType)
            {
                case ChronicleEventType.CampfireGossip:
                    return $"{actorA} shared campfire rumors with {actorB} in {sector}.";

                case ChronicleEventType.CampfireCompanionship:
                    return $"{actorA} enjoyed quiet companionship with {actorB} near the hearth in {sector}.";

                case ChronicleEventType.AltruisticFoodShare:
                    float amount = evt.ExtraData > 0f ? evt.ExtraData : 40f;
                    return $"{actorA} shared {amount:F0} rations with starving peer {actorB} in {sector}.";

                case ChronicleEventType.ResourceYielded:
                    return $"{actorA} stepped aside and conceded the resource to {actorB} in {sector}.";

                case ChronicleEventType.ResourceClaimed:
                    return $"{actorA} claimed the foraging node after {actorB} yielded in {sector}.";

                case ChronicleEventType.DirectiveIssued:
                    return $"{actorA} issued a collective directive to all agents in {sector}.";

                case ChronicleEventType.DirectiveComplied:
                    return $"{actorA} completed their assigned directive in {sector}.";

                case ChronicleEventType.TantrumStarted:
                    return $"{actorA} flew into an angry tantrum from high frustration in {sector}!";

                case ChronicleEventType.ExhaustionCollapsed:
                    return $"{actorA} collapsed onto the floor from pure exhaustion in {sector}.";

                case ChronicleEventType.StarvationPanic:
                    return $"{actorA} panicked frantically searching for food in {sector}!";

                case ChronicleEventType.GroupFormed:
                    return $"{actorA} and {actorB} banded together to establish a new collective in {sector}.";

                case ChronicleEventType.LeadershipAssumed:
                    return $"{actorA} assumed leadership of the group in {sector}.";

                case ChronicleEventType.LeadershipChallenged:
                    return $"{actorA} openly challenged {actorB}'s authority in {sector}!";

                case ChronicleEventType.GroupSchism:
                    return $"A bitter factional grievance fractured the group in {sector}!";

                case ChronicleEventType.TradeTransaction:
                    return $"{actorA} completed a trade at the outpost stall in {sector}.";

                case ChronicleEventType.BountyPosted:
                    return $"A public bounty was posted at the outpost board in {sector}.";

                case ChronicleEventType.BountyClaimed:
                    return $"{actorA} claimed a public bounty contract in {sector}.";

                case ChronicleEventType.BountyFulfilled:
                    return $"{actorA} delivered their bounty contract and collected their gold reward in {sector}.";

                case ChronicleEventType.BountyExpired:
                    return $"An unfulfilled bounty contract expired in {sector}.";

                case ChronicleEventType.UpkeepUnpaid:
                    return "The outpost couldn't pay its upkeep; the stall stops buying salvage until it can.";

                case ChronicleEventType.SupplyCaravanArrived:
                    return "A supply caravan restocked the outpost stall with rations.";

                case ChronicleEventType.ResourceRegrown:
                    return $"A depleted salvage or forage node regrew in {sector}.";

                case ChronicleEventType.DrifterArrived:
                    return $"{actorA}, a drifter, came in by the Road Gate.";

                case ChronicleEventType.DrifterLeft:
                    return $"{actorA} moved on by the Road Gate.";

                default:
                    return $"{actorA} interacted with {actorB} in {sector}.";
            }
        }

        private string ResolveActorName(EntityId actorId, string defaultName)
        {
            if (actorId == default) return defaultName;
            return _identityService != null ? _identityService.GetFriendlyName(actorId) : defaultName;
        }

        private string ResolveSectorName(int sectorId)
        {
            // The sector's name, or the fallback sector's for an unknown id (as the dashboard's zones resolve them)
            if (_sectors != null)
            {
                string name = _sectors.TryGetSector(sectorId, out var sector) ? sector.Name : _sectors.Fallback.Name;
                if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }
            }

            if (sectorId == 0) return "Transit Corridor";
            return $"Sector {sectorId}";
        }
    }
}
