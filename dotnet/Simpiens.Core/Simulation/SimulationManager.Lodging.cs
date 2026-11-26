using System.Collections.Generic;
using Simpiens.Cognition.Contracts;
using Simpiens.Diagnostics;
using Simpiens.Narrative;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.World;
using ZLogger;

namespace Simpiens.Simulation
{
    public partial class SimulationManager
    {
        // --- Rest anchor: bunks (Epic MAP Phase 3), kept by Lodging ---
        /// <summary>Slack on top of BunkTargeting.ReachDistance for a sleeper's final step onto the bunk.</summary>
        public const float BunkClaimTolerance = Lodging.BunkClaimTolerance;

        /// <summary>A sleeper this close to the bunk's position is lying in it.</summary>
        public const float BunkSettleDistance = Lodging.BunkSettleDistance;

        public void RegisterBunk(EntityId id, Vec2 position) => _lodging.RegisterBunk(id, position);

        /// <summary>A bunk in the guild's barracks (Epic 10 Part 2, W5.2, D62): a bunk like any, which the guild keeps for its members.</summary>
        public void RegisterBarracksBunk(EntityId id, Vec2 position)
        {
            _lodging.RegisterBunk(id, position);
            Guild.AddBarracksBunk(id, position);
        }

        /// <summary>Bunk id to sleeper id, for the bunks taken right now.</summary>
        public IReadOnlyDictionary<EntityId, EntityId> BunkOccupants => _lodging.BunkOccupants;

        /// <summary>Completed bunk sleeps per agent this session.</summary>
        public IReadOnlyDictionary<EntityId, int> BunkSleeps => _lodging.BunkSleeps;

        public bool TryClaimBunk(EntityId bunkId, EntityId agentId, Vec2 agentPosition) => _lodging.TryClaimBunk(bunkId, agentId, agentPosition);

        public IntentResult StepOntoBunk(WorldEntity node, EntityId bunkId) => _lodging.StepOntoBunk(node, bunkId);

        /// <summary>Claims a venue's seat for a crawler arriving at it: one crawler a seat (Epic PLACES plan 1, F.6). Main thread.</summary>
        public bool TryClaimSeat(int venueIndex, int seat, EntityId agentId) => _seating.TryClaim(venueIndex, seat, agentId);

        /// <summary>Who holds a venue's seat, if anyone (F.6).</summary>
        public bool TryGetSeatHolder(int venueIndex, int seat, out EntityId holder) => _seating.TryGetHolder(venueIndex, seat, out holder);

        public void DepartOutpost(WorldEntity node)
        {
            var agent = node?.Agent;
            if (agent == null || agent.HasLeft) return;
            uint tick = _clock != null ? (uint)_clock.CurrentTick : 0;

            var inventory = agent.Inventory;
            int gold = inventory != null ? inventory.Gold : 0;
            if (gold > 0)
            {
                _economyLedger?.RecordSink((uint)gold, "DrifterDepartures");
                inventory.Gold = 0;
            }

            int releasedClaims = ReleaseFromOutpost(node);
            agent.HasLeft = true;
            SimLog.Economy.ZLogInformation($"{SimLog.Agent(node.Id)} left by the Road Gate with {gold}g ({releasedClaims} claim(s) released)");
            _chronicleService?.RecordEvent(new ChronicleEvent(ChronicleEventType.DrifterLeft, node.Id, default, GetSectorForPosition(node.Position), tick));
        }

        /// <summary>
        /// A crawler dies (Epic 10 Part 3, K5.4a, A2: it leaves the world the drifter's way). Its body is a pile where it fell,
        /// claimed for nobody: its pack, then what it wore, and its purse, which the ledger holds as gold on the ground
        /// (K5.4b). It lets go of everything a departure lets go of (claims, bunks, every group, the guild's and the
        /// workshops' contracts and its kept bunk, its party one short), and the host removes it at the end of the tick,
        /// noting the cause in the departed list. <paramref name="by"/> is what downed it (a creature), if anything.
        /// </summary>
        public void Die(WorldEntity node, Simpiens.Simulation.DeathCause cause, byte detail = 0, EntityId by = default)
        {
            var agent = node?.Agent;
            if (agent == null || agent.HasLeft) return;
            uint tick = _clock != null ? (uint)_clock.CurrentTick : 0;

            // The body: its pack's stacks, then each worn piece, and its purse
            var inventory = agent.Inventory;
            int gold = 0;
            System.Span<ItemInstance> body = stackalloc ItemInstance[Simpiens.Simulation.Loot.PileService.Capacity];
            int stacks = 0;
            if (inventory != null)
            {
                for (int i = 0; i < inventory.Capacity && stacks < body.Length; i++)
                {
                    var stack = inventory.GetSlot(i);
                    if (!stack.IsEmpty) body[stacks++] = stack;
                }
                var slots = Simpiens.Simulation.Items.EquipmentSlots.All;
                for (int i = 0; i < slots.Count && stacks < body.Length; i++)
                {
                    var worn = inventory.Unequip(slots[i].Id);
                    if (!worn.IsEmpty) body[stacks++] = worn;
                }
                for (int i = 0; i < inventory.Capacity; i++)
                {
                    var stack = inventory.GetSlot(i);
                    if (!stack.IsEmpty) inventory.TryRemoveItem(stack.Type, stack.Quantity);
                }
                gold = inventory.Gold;
                inventory.Gold = 0;
            }
            Piles.Drop(this, node.Position, body.Slice(0, stacks), gold, default, tick, bodyOf: node.Id);

            // Under the guild's orders (on an expedition), its death costs the patron the other members' trust (Q4, K5.5)
            bool underOrders = Guild.IsMember(node.Id) && agent.Expedition.IsActive;
            int releasedClaims = ReleaseFromOutpost(node);
            agent.HasLeft = true;
            agent.DiedOf = cause;
            agent.DiedDetail = detail;
            int grieving = Grieve(node, tick, underOrders);
            SimLog.Drives.ZLogInformation($"{SimLog.Agent(node.Id)} died ({cause}) at {node.Position}, leaving {stacks} stacks and {gold}g ({releasedClaims} claim(s) released; {grieving} grieve)");
            _chronicleService?.RecordEvent(new ChronicleEvent(ChronicleEventType.CrawlerDied, node.Id, by, GetSectorForPosition(node.Position), tick,
                ChronicleIntensity.Crisis, (int)cause * 256 + detail));
            // The creature that struck it last takes its name from it, if it hasn't one (Epic 11, S7.1, D31)
            if (cause == Simpiens.Simulation.DeathCause.Creature && by != default)
            {
                TellRenown(Creatures.Credit(by, node.Id, death: true), by, node.Id, killed: true, tick);
                Kindle(by, node.Id, tick); // its friends' grudges (S7.3)
            }
        }

        /// <summary>A friend's grief raises frustration by this much (Q4, K5.5): the portrait's frustrated face shows it.</summary>
        public const float GriefFrustration = 25f;

        /// <summary>What a death under the guild's orders costs the patron in each other member's trust (Q4, K5.5).</summary>
        public const sbyte TrustLostUnderOrders = -15;

        // The living remember (Q4, K5.5): each friend (warmth toward the dead at the friend line) grieves, a memory and
        // frustration; under the guild's orders, the other members trust the patron less. The friends who grieve.
        private int Grieve(WorldEntity dead, uint tick, bool underOrders)
        {
            var entities = _worldRegistry?.ActiveEntities;
            if (entities == null) return 0;
            int grieving = 0;
            var at = GridPoint.RoundToInt(dead.Position);
            for (int i = 0; i < entities.Count; i++)
            {
                var living = entities[i]?.Agent;
                if (living == null || living.HasLeft || living.AgentId == dead.Id || living.Memory == null) continue;
                if (living.Memory.GetAffinity(dead.Id, tick).Warmth >= Simpiens.Cognition.Planning.Rescue.FriendWarmth)
                {
                    living.Memory.EventLedger?.AddEvent(new Simpiens.Cognition.Memory.MemoryEvent(Simpiens.Cognition.Memory.MemoryEventType.Grieved, dead.Id, at, tick));
                    living.Frustration = SimMath.Min(100f, (float)(living.Frustration + GriefFrustration));
                    living.Think(Simpiens.Simulation.Life.Thought.FriendDied, tick, dead.Id); // a friend lost, for days (Epic LIFE-1, L1.2)
                    grieving++;
                }
                if (underOrders && Guild.IsMember(living.AgentId))
                {
                    living.Memory.RecordInteraction(PlayerEntityId, deltaWarmth: -5, deltaTrust: TrustLostUnderOrders, deltaFear: 0, deltaRespect: 0, tick);
                }
            }
            return grieving;
        }

        // What a crawler lets go of as it leaves the world, by the Road Gate or by dying: its bounty claims, its bunk, every
        // group (Epic 10 G4.4: no departed crawler stays on a roster, nor in the guild), and its contracts. The claims released.
        private int ReleaseFromOutpost(WorldEntity node)
        {
            var agent = node.Agent;
            int releasedClaims = _market.ReleaseClaimsForAgent(node.Id);
            _lodging.ReleaseAllForAgent(node.Id);
            _seating.ReleaseAllForAgent(node.Id);
            if (_groupRegistry != null)
            {
                _groupRegistry.LeaveAllGroups(node.Id);
                agent.Memberships = _groupRegistry.GetMemberships(node.Id);
            }
            Guild.Forget(this, node.Id);
            _workshops.Forget(node.Id);
            return releasedClaims;
        }

        public void ReleaseBunk(EntityId bunkId, EntityId agentId) => _lodging.ReleaseBunk(bunkId, agentId);

        public void RecordBunkSleep(EntityId agentId) => _lodging.RecordBunkSleep(agentId);

        /// <summary>
        /// Drops every claim whose sleeper is no longer sleeping in that bunk (preempted, cancelled or removed), so a
        /// bunk can never stay taken by someone who left it. Runs every tick before the snapshot.
        /// </summary>
        internal void ValidateBunkOccupancy() => _lodging.ValidateOccupancy(_activeIntents, _agentRegistry);
    }
}
