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
    /// size. With <see cref="SendExpeditions"/> (Epic 10 G5) it then forms party 1 of its first four members (Epic 10
    /// Part 2, W5, D65), outfits them from the stall's stock and sends them out whenever the target's seam shows on the
    /// map; with <see cref="SendSecondParty"/> (W5.3b, D66), party 2 of the next four, to its own target, on its own.
    /// Shared by the Definition-of-Done scenarios, the soak's guild profile (G6) and the console.
    /// </summary>
    public sealed class GuildPlayer
    {
        public const string SourceId = "GuildPlayer";

        private readonly LocalSimulation _sim;
        private readonly IPlayerInterventionService _commands;
        private readonly int _targetSize;
        private readonly ushort _wage;
        private readonly List<EntityId> _recruited = new List<EntityId>(4);

        /// <summary>What it does with each party: party 1's members are its first four in join order, party 2's the next four.</summary>
        private sealed class PartyPlan
        {
            public byte Slot;
            public byte Target, Goal;
            public RetreatRule Rule;
            public bool Outfit;
            public byte Draughts;
            public int Stockpile;
            public int Dispatches;
            public readonly List<EntityId> Members = new List<EntityId>(DispatchExpeditionCommand.MaxMembers);
        }

        private readonly PartyPlan[] _plans = { new PartyPlan { Slot = 1 }, new PartyPlan { Slot = 2 } };
        private bool _together;

        // Gear promised from stock this tick: the commands apply on the next, so the second party counts what the first was given
        private ItemType _promisedGear;
        private int _promised;

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

        /// <summary>Expeditions sent, by both parties, and when the first was.</summary>
        public int Dispatches => _plans[0].Dispatches + _plans[1].Dispatches;
        public long FirstDispatchTick { get; private set; } = -1;

        /// <summary>Expeditions party <paramref name="slot"/> was sent on.</summary>
        public int DispatchesOf(byte slot) => _plans[slot - 1].Dispatches;

        /// <summary>Gear given from the stall's stock.</summary>
        public int GearGiven { get; private set; }

        /// <summary>
        /// Sends expeditions (Epic 10 G5): once the guild reaches its target size, every member not wearing protection is
        /// given some from the stall's stock (if <paramref name="outfit"/>), and when all wear it, no party is out and a node
        /// of <paramref name="sectorId"/> shows on the map (ready, not regrowing), the whole guild is sent for
        /// <paramref name="goal"/> under <paramref name="rule"/>, each member given <paramref name="draughts"/> draughts from
        /// the stall's stock at the muster as stock allows (Epic 10 Part 2, W2). With <paramref name="stockpile"/> it waits until
        /// the stall holds that many draughts before sending, as it waits for gear. Again each time the seam is back.
        /// </summary>
        public void SendExpeditions(byte sectorId, byte goal, RetreatRule rule = RetreatRule.Steady, bool outfit = true, byte draughts = 0, int stockpile = 0)
            => Plan(_plans[0], sectorId, goal, rule, outfit, draughts, stockpile);

        /// <summary>
        /// Party 2 (Epic 10 Part 2, W5.3b, D66), of the guild's next four members, sent to <paramref name="sectorId"/> as
        /// <see cref="SendExpeditions"/> sends party 1: both may be out at once, never to the same sector. Each goes when it
        /// is ready; with <paramref name="together"/>, each waits until the other is ready too and both set out on the same
        /// tick (the caravan's gear outfits one party a visit, so otherwise they tend to take turns).
        /// </summary>
        public void SendSecondParty(byte sectorId, byte goal, RetreatRule rule = RetreatRule.Steady, bool outfit = true, byte draughts = 0, int stockpile = 0, bool together = false)
        {
            Plan(_plans[1], sectorId, goal, rule, outfit, draughts, stockpile);
            _together = together;
        }

        private static void Plan(PartyPlan plan, byte sectorId, byte goal, RetreatRule rule, bool outfit, byte draughts, int stockpile)
        {
            plan.Target = sectorId;
            plan.Goal = goal;
            plan.Rule = rule;
            plan.Outfit = outfit;
            plan.Draughts = draughts;
            plan.Stockpile = stockpile;
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
                _promised = 0;
                if (_together && _plans[0].Target != 0 && _plans[1].Target != 0)
                {
                    // Both at once: only when neither is out and both are ready
                    if (guild.HasExpedition) return;
                    bool first = Ready(guild, _plans[0], out var one), second = Ready(guild, _plans[1], out var two);
                    if (first && second)
                    {
                        Send(_plans[0], one, tick);
                        Send(_plans[1], two, tick);
                    }
                    return;
                }
                for (int i = 0; i < _plans.Length; i++)
                {
                    if (_plans[i].Target != 0 && Ready(guild, _plans[i], out var order)) Send(_plans[i], order, tick);
                }
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

        /// <summary>The medicine the stall sells and holds (its draughts).</summary>
        private int DraughtsInStock()
        {
            var stall = _sim.Simulation.ActiveStalls[0];
            var medicines = Simpiens.Simulation.Items.ItemCatalog.InCategory(Simpiens.Simulation.Items.ItemCategory.Medicine);
            int stock = 0;
            for (int i = 0; i < medicines.Count; i++) stock += stall.SellStockOf(medicines[i]);
            return stock;
        }

        /// <summary>
        /// Whether <paramref name="plan"/>'s party can go now, readying it on the way: formed of its members (or the command
        /// to form it sent), outfitted from stock (gear sent as stock allows), the stockpile there, its seam showing, every
        /// member well, and the rule's yes.
        /// </summary>
        private bool Ready(GuildService guild, PartyPlan plan, out DispatchExpeditionCommand order)
        {
            order = default;
            var party = guild.Parties[plan.Slot - 1];
            if (party.IsOut) return false;
            var host = (IGuildHost)_sim.Simulation;

            // Its members: four in join order, from the party's place (party 1 the first four, party 2 the next), formed once
            // (and again if one left)
            var members = plan.Members;
            members.Clear();
            int from = (plan.Slot - 1) * DispatchExpeditionCommand.MaxMembers;
            for (int i = from; i < guild.Members.Count && members.Count < DispatchExpeditionCommand.MaxMembers; i++) members.Add(guild.Members[i].CrawlerId);
            if (members.Count == 0) return false;
            if (!IsFormed(party, members))
            {
                var form = new FormPartyCommand(plan.Slot, members.ToArray());
                if (guild.CanFormParty(form) == GuildRules.Check.Ok) _commands.QueueInfluence(InfluenceEvent.CreateFormParty(SourceId, form));
                return false; // the command applies at the start of the next tick
            }

            if (plan.Outfit && !Outfitted(guild, host, plan)) return false;
            if (plan.Stockpile > 0 && DraughtsInStock() < plan.Stockpile) return false;
            if (!SeamShows(plan.Target) || !AllWell(host, members)) return false;
            order = new DispatchExpeditionCommand(plan.Slot, plan.Target, plan.Goal, plan.Rule, plan.Draughts);
            return guild.CanDispatch(host, order) == GuildRules.Check.Ok;
        }

        private void Send(PartyPlan plan, in DispatchExpeditionCommand order, long tick)
        {
            _commands.QueueInfluence(InfluenceEvent.CreateDispatchExpedition(SourceId, order));
            plan.Dispatches++;
            if (FirstDispatchTick < 0) FirstDispatchTick = tick;
        }

        /// <summary>The party's members are <paramref name="members"/>, in that order.</summary>
        private static bool IsFormed(GuildParty party, List<EntityId> members)
        {
            if (party.Members.Count != members.Count) return false;
            for (int i = 0; i < members.Count; i++)
            {
                if (party.Members[i].CrawlerId != members[i]) return false;
            }
            return true;
        }

        /// <summary>
        /// Gives protection from the stall's stock to each member without, or with it worn to half its life (the roster shows
        /// it), or wearing gear that cuts the target's hazard less than the best in stock (Epic 10 Part 2, D50); true when
        /// every member wears some good for a trip.
        /// </summary>
        private bool Outfitted(GuildService guild, IGuildHost host, PartyPlan plan)
        {
            var stall = _sim.Simulation.ActiveStalls.Count > 0 ? _sim.Simulation.ActiveStalls[0] : default;
            // The gear in stock that cuts the target's hazard most (Epic 10 Part 2, D50), less what this tick promised already
            var hazard = GuildRules.HazardOf(host, plan.Target);
            var gear = GuildRules.BestProtectionInStock(host, hazard);
            int stock = gear != ItemType.None ? stall.SellStockOf(gear) - (gear == _promisedGear ? _promised : 0) : 0;
            bool all = true;
            for (int i = 0; i < plan.Members.Count; i++)
            {
                var id = plan.Members[i];
                if (!host.TryGetCrawler(id, out var member) || member.Inventory == null) continue;
                var worn = member.Inventory.GetEquipped(EquipmentSlot.Protection);
                bool better = gear != ItemType.None && GuildRules.ProtectionAgainst(gear, hazard) > GuildRules.ProtectionAgainst(worn.Type, hazard);
                if (!GuildRules.WornOut(worn) && !better) continue;
                if (GuildRules.WornOut(worn)) all = false; // a member with sound gear can go; better gear is given when there is some
                if (stock <= 0 || guild.CanGiveGear(host, id, gear, hazard) != GuildRules.Check.Ok) continue;
                _commands.QueueInfluence(InfluenceEvent.CreateGiveGear(SourceId, id, gear, hazard));
                GearGiven++;
                stock--;
                if (_promisedGear != gear) { _promisedGear = gear; _promised = 0; }
                _promised++;
            }
            return all;
        }

        /// <summary>Every member of a party well enough for the muster, as the roster's health bars show.</summary>
        private bool AllWell(IGuildHost host, List<EntityId> members)
        {
            float well = _sim.Simulation.Economy.Guild.Expedition.MusterHealth;
            for (int i = 0; i < members.Count; i++)
            {
                if (!host.TryGetCrawler(members[i], out var member) || member.IsDowned || member.Health < well) return false;
            }
            return true;
        }

        /// <summary>A node of <paramref name="target"/> on the map: the registry holds a node only while it is ready.</summary>
        private bool SeamShows(byte target)
        {
            var entities = _sim.Registry.ActiveEntities;
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].Type == EntityType.Resource && _sim.Sectors.GetSectorAt(entities[i].Position).Id == target) return true;
            }
            return false;
        }
    }
}
