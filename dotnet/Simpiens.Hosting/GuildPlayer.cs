using System;
using System.Collections.Generic;
using Simpiens.Cognition.Inventory;
using Simpiens.Intervention;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Spatial;

namespace Simpiens.Hosting
{
    /// <summary>
    /// A scripted player for the guild (Epic 10 G4): it acts only through the typed commands a real player sends, as the
    /// economy soak's profiles do. After every tick it looks at what the dossier shows (the guild's own rule, read-only)
    /// and recruits the first resident, in spawn order, who would sign on at its wage, until the guild reaches its target
    /// size. With <see cref="SendExpeditions"/> (Epic 10 G5) it then outfits the members from the stall's stock and sends
    /// them out whenever the target's seam shows on the map. Shared by the Definition-of-Done scenarios, the soak's guild
    /// profile (G6) and the console.
    /// </summary>
    public sealed class GuildPlayer
    {
        public const string SourceId = "GuildPlayer";

        private readonly LocalSimulation _sim;
        private readonly IPlayerInterventionService _commands;
        private readonly int _targetSize;
        private readonly ushort _wage;
        private readonly List<EntityId> _recruited = new List<EntityId>(4);
        private readonly List<EntityId> _party = new List<EntityId>(DispatchExpeditionCommand.MaxMembers);
        private byte _target, _goal;
        private RetreatRule _rule;
        private bool _outfit;
        private byte _draughts;

        /// <param name="wage">The wage it offers; the session's default wage when 0.</param>
        public GuildPlayer(LocalSimulation sim, int targetSize, ushort wage = 0)
        {
            _sim = sim;
            _commands = (IPlayerInterventionService)sim.Services.GetService(typeof(IPlayerInterventionService));
            _targetSize = targetSize;
            _wage = wage != 0 ? wage : sim.Simulation.Economy.Guild.DefaultWage;
            sim.Host.TickCompleted += Act;
        }

        /// <summary>Every crawler it has asked to sign on, in order.</summary>
        public IReadOnlyList<EntityId> Recruited => _recruited;

        /// <summary>The tick it first asked someone to sign on, or -1.</summary>
        public long FirstRecruitTick { get; private set; } = -1;

        public ushort Wage => _wage;

        /// <summary>Expeditions sent, and when the first was.</summary>
        public int Dispatches { get; private set; }
        public long FirstDispatchTick { get; private set; } = -1;

        /// <summary>Gear given from the stall's stock.</summary>
        public int GearGiven { get; private set; }

        /// <summary>
        /// Sends expeditions (Epic 10 G5): once the guild reaches its target size, every member not wearing protection is
        /// given some from the stall's stock (if <paramref name="outfit"/>), and when all wear it, no party is out and a node
        /// of <paramref name="sectorId"/> shows on the map (ready, not regrowing), the whole guild is sent for
        /// <paramref name="goal"/> under <paramref name="rule"/>, each member given <paramref name="draughts"/> draughts from
        /// the stall's stock at the muster as stock allows (Epic 10 Part 2, W2). Again each time the seam is back.
        /// </summary>
        public void SendExpeditions(byte sectorId, byte goal, RetreatRule rule = RetreatRule.Steady, bool outfit = true, byte draughts = 0)
        {
            _target = sectorId;
            _goal = goal;
            _rule = rule;
            _outfit = outfit;
            _draughts = draughts;
        }

        /// <summary>Stops acting (a scenario that takes over).</summary>
        public void Stop() => _sim.Host.TickCompleted -= Act;

        /// <summary>Sends a price command, as the player's merchant terminal does.</summary>
        public void SetPrices(in UpdateStallPricesCommand prices) => _commands.QueueInfluence(InfluenceEvent.CreateUpdateStallPrices(SourceId, prices));

        private void Act(long tick)
        {
            var guild = _sim.Simulation.Guild;
            if (_commands == null) return;
            if (guild.Members.Count >= _targetSize)
            {
                if (_target != 0) Expedition(guild, tick);
                return;
            }
            var host = (IGuildHost)_sim.Simulation;
            var crawlers = _sim.Host.CrawlerStates;
            for (int i = 0; i < crawlers.Count; i++)
            {
                var crawler = crawlers[i];
                if (guild.CanRecruit(host, crawler.AgentId, _wage, (uint)tick) != GuildRules.Check.Ok) continue;
                _commands.QueueInfluence(InfluenceEvent.CreateRecruit(SourceId, crawler.AgentId, _wage));
                _recruited.Add(crawler.AgentId);
                if (FirstRecruitTick < 0) FirstRecruitTick = tick;
                return; // one a tick: the command applies at the start of the next
            }
        }

        private void Expedition(GuildService guild, long tick)
        {
            if (guild.HasExpedition) return;
            var host = (IGuildHost)_sim.Simulation;
            if (_outfit && !Outfitted(guild, host)) return;
            if (!SeamShows() || !AllWell(guild, host)) return;

            _party.Clear();
            for (int i = 0; i < guild.Members.Count && _party.Count < DispatchExpeditionCommand.MaxMembers; i++) _party.Add(guild.Members[i].CrawlerId);
            var order = new DispatchExpeditionCommand(_target, _goal, _rule, _party.ToArray(), _draughts);
            if (guild.CanDispatch(host, order) != GuildRules.Check.Ok) return;
            _commands.QueueInfluence(InfluenceEvent.CreateDispatchExpedition(SourceId, order));
            Dispatches++;
            if (FirstDispatchTick < 0) FirstDispatchTick = tick;
        }

        /// <summary>Gives protection from the stall's stock to each member without, or with it worn to half its life (the roster shows it); true when every member wears some good for a trip.</summary>
        private bool Outfitted(GuildService guild, IGuildHost host)
        {
            var stall = _sim.Simulation.ActiveStalls.Count > 0 ? _sim.Simulation.ActiveStalls[0] : default;
            var gear = ItemType.None;
            var protection = ItemCatalog.InCategory(ItemCategory.Protection);
            for (int i = 0; i < protection.Count && gear == ItemType.None; i++)
            {
                if (stall.SellStockOf(protection[i]) > 0) gear = protection[i];
            }
            int stock = gear != ItemType.None ? stall.SellStockOf(gear) : 0;
            bool all = true;
            for (int i = 0; i < guild.Members.Count; i++)
            {
                var id = guild.Members[i].CrawlerId;
                if (!host.TryGetCrawler(id, out var member) || member.Inventory == null || !GuildRules.WornOut(member.Inventory.GetEquipped(EquipmentSlot.Protection))) continue;
                all = false;
                if (stock == 0 || guild.CanGiveGear(host, id, gear) != GuildRules.Check.Ok) continue;
                _commands.QueueInfluence(InfluenceEvent.CreateGiveGear(SourceId, id, gear));
                GearGiven++;
                stock--;
            }
            return all;
        }

        /// <summary>Every member well enough for the muster, as the roster's health bars show.</summary>
        private bool AllWell(GuildService guild, IGuildHost host)
        {
            float well = _sim.Simulation.Economy.Guild.Expedition.MusterHealth;
            for (int i = 0; i < guild.Members.Count; i++)
            {
                if (!host.TryGetCrawler(guild.Members[i].CrawlerId, out var member) || member.IsDowned || member.Health < well) return false;
            }
            return true;
        }

        /// <summary>A node of the target sector on the map: the registry holds a node only while it is ready.</summary>
        private bool SeamShows()
        {
            var entities = _sim.Registry.ActiveEntities;
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].Type == EntityType.Resource && _sim.Sectors.GetSectorAt(entities[i].Position).Id == _target) return true;
            }
            return false;
        }
    }
}
