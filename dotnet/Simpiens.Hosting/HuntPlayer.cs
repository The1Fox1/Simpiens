using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Intervention;
using Simpiens.Simulation;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Economy;

namespace Simpiens.Hosting
{
    /// <summary>
    /// A scripted player for hunts (Epic 10 Part 3, K6.4): it acts only through the typed command a real player sends, as the
    /// other scripted players do. After every tick it looks at what the bounty board shows: once a lair of the kind it wants
    /// (any kind, for <see cref="CreatureKind.None"/>: the first in the map's order) is charted with a creature in it, a bold resident is armed well enough to brave it (D21), no hunt of its is open and the stall holds the
    /// reward, it posts a hunt on that lair. Again each time its last hunt is done or expired.
    /// </summary>
    public sealed class HuntPlayer
    {
        public const string SourceId = "HuntPlayer";

        private readonly LocalSimulation _sim;
        private readonly IPlayerInterventionService _commands;
        private readonly CreatureKind _kind;
        private readonly ushort _reward;
        private readonly List<ushort> _posted = new List<ushort>(4);
        private readonly uint _reserve;

        /// <summary>The treasury it keeps besides the reward by default.</summary>
        public const uint DefaultReserve = 150;

        /// <param name="reserve">Gold it keeps in the treasury besides the reward (as the workshop player's): a careful player doesn't empty the stall that buys the salvage.</param>
        public HuntPlayer(LocalSimulation sim, CreatureKind kind, ushort reward = 0, uint reserve = DefaultReserve)
        {
            _reserve = reserve;
            _sim = sim;
            _commands = (IPlayerInterventionService)sim.Services.GetService(typeof(IPlayerInterventionService));
            _kind = kind;
            _reward = reward != 0 ? reward : sim.Simulation.Economy.Hunt.DefaultReward;
            sim.Host.TickCompleted += Act;
        }

        /// <summary>Hunts posted, and when the first was (-1 until it is).</summary>
        public int Posted { get; private set; }
        public long FirstPostTick { get; private set; } = -1;

        public void Stop() => _sim.Host.TickCompleted -= Act;

        private void Act(long tick)
        {
            if (_commands == null) return;
            var simulation = _sim.Simulation;
            var hunts = simulation.Hunts;
            for (int i = 0; i < hunts.Count; i++)
            {
                if (_posted.Contains(hunts[i].BountyId)) return; // one of its own is open
            }
            var lairs = simulation.Creatures.Lairs;
            int lair = -1;
            EntityId quarry = default;
            // A named creature in a charted lair first, the highest rank (Epic 11, S7.4, D34): a hunt naming it
            var creatures = simulation.Creatures.Creatures;
            int rank = 0;
            for (int i = 0; i < creatures.Count; i++)
            {
                var c = creatures[i];
                if (!c.IsNamed || !c.IsAlive || c.Rank <= rank || !simulation.Creatures.IsCharted(c.LairIndex)) continue;
                if (_kind != CreatureKind.None && lairs[c.LairIndex].Definition.Kind != _kind) continue;
                rank = c.Rank;
                lair = c.LairIndex;
                quarry = c.Id;
            }
            for (int i = 0; i < lairs.Count && lair < 0; i++)
            {
                if ((_kind == CreatureKind.None || lairs[i].Definition.Kind == _kind) && simulation.Creatures.IsCharted(i) && lairs[i].Alive > 0) lair = i;
            }
            if (lair < 0 || !SomeoneBraves(lairs[lair].Definition, simulation.Creatures.IsBossAlive(lair), simulation.Creatures.LeaderRank(lair))) return;
            if (simulation.ActiveStalls.Count == 0 || simulation.ActiveStalls[0].TreasuryGold < _reward + _reserve) return;
            _commands.QueueInfluence(InfluenceEvent.CreatePostBounty(SourceId, new PostBountyCommand(0, BountyType.Hunt, 0, ItemType.None, 1, _reward,
                DayClock.Hours(simulation.Economy.Hunt.LifeHours), default, simulation.ActiveStalls[0].StallEntityId, (byte)lair, quarry)));
            _pending = true;
            Posted++;
            if (FirstPostTick < 0) FirstPostTick = tick;
            _sim.Host.TickCompleted -= Act;
            _sim.Host.TickCompleted += Remember;
        }

        private bool _pending;

        // The tick after the command applies: note the hunt it posted, then look again
        private void Remember(long tick)
        {
            if (!_pending) return;
            _pending = false;
            var hunts = _sim.Simulation.Hunts;
            if (hunts.Count > 0) _posted.Add(hunts[hunts.Count - 1].BountyId);
            _sim.Host.TickCompleted -= Remember;
            _sim.Host.TickCompleted += Act;
        }

        // A resident who would take the hunt on: bold, armed well enough to brave the lair at full health (D21), as a player
        // reading the guild cards' gear lines would judge, a named creature leading the lair with it (Epic 11, S7.2)
        private bool SomeoneBraves(in Simpiens.Simulation.World.LairDefinition lair, bool bossAlive, int leaderRank)
        {
            var crawlers = _sim.Host.CrawlerStates;
            for (int i = 0; i < crawlers.Count; i++)
            {
                var c = crawlers[i];
                if (!c.IsDrifter && Vitality.BravesLair(lair, bossAlive, c.Skills, c.Inventory, c.Traits, Vitality.MaxHealth, c.Personality.RiskTolerancePercent, downed: false, leaderRank)) return true;
            }
            return false;
        }
    }
}
