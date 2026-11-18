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
    /// map; with <see cref="SendSecondParty"/> (W5.3b, D66), party 2 of the next four, to its own target, on its own. It
    /// pays each member's raise when its asking wage rises (Epic 11, S2.3), unless told to leave raises unpaid.
    /// Shared by the Definition-of-Done scenarios, the soak's guild profile (G6) and the console.
    /// </summary>
    public sealed class GuildPlayer
    {
        public const string SourceId = "GuildPlayer";

        private readonly LocalSimulation _sim;
        private readonly IPlayerInterventionService _commands;
        private readonly int _targetSize;
        private readonly ushort _wage;
        private readonly uint _reserve;
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
            /// <summary>A clear (Epic 10 Part 3, K7.4): of the first charted lair of this kind (None: any) with a creature in it, the party armed from stock first.</summary>
            public bool Clear;
            public Simpiens.Simulation.Creatures.CreatureKind LairKind;
            public bool Arm;
            public int Shooters;
            public ItemType Bow;
            public Simpiens.Simulation.Creatures.CreatureKind MaterialsFirst;
            /// <summary>Clears in order (Epic 10 Part 3, K10.6): the first of these kinds whose lair is charted and holds a creature; null for <see cref="LairKind"/> alone.</summary>
            public Simpiens.Simulation.Creatures.CreatureKind[] ClearOrder;
            public readonly List<EntityId> Members = new List<EntityId>(DispatchExpeditionCommand.MaxMembers);
        }

        private readonly PartyPlan[] _plans = { new PartyPlan { Slot = 1 }, new PartyPlan { Slot = 2 } };
        private bool _together;

        // The stall's stock less the gear promised this tick: the commands apply on the next, so the second party sees what
        // the first was given (Epic 10 Part 3, K1.4: any piece, slot by slot)
        private TradeStallData _stockLeft;
        private bool _stockTaken;
        private readonly GearGift[] _gifts = new GearGift[2 * DispatchExpeditionCommand.MaxMembers * Simpiens.Simulation.Items.EquipmentSlots.Capacity];
        private int _giftCount;
        private readonly bool[] _ready = new bool[2];
        private readonly DispatchExpeditionCommand[] _orders = new DispatchExpeditionCommand[2];
        private readonly bool[] _outfitting = new bool[2];

        /// <param name="wage">The wage it offers; the session's default wage when 0.</param>
        /// <param name="reserve">Gold it keeps in the treasury (Epic 10 Part 2, W6): it signs a member on only when the stall holds the fee and this besides.</param>
        public GuildPlayer(LocalSimulation sim, int targetSize, ushort wage = 0, uint reserve = 0)
        {
            _reserve = reserve;
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

        /// <summary>
        /// Sends party 1 to clear a lair (Epic 10 Part 3, K7.4, Q8): once the guild reaches its target size, each member is given
        /// the best weapon and armour for a fight the stall holds (if <paramref name="arm"/>), and when every member is armed and
        /// well, the first charted lair of <paramref name="kind"/> (any, for None) with a creature in it is ordered cleared,
        /// under <paramref name="rule"/>. Again each time the party is home and a lair shows. The last <paramref name="shooters"/>
        /// members are armed to shoot (K8.2c), the rest to fight in reach; the arming waits until each has its weapon. With a
        /// <paramref name="bow"/>, while the stall holds fewer of it than there are shooters, the party first clears the lairs of
        /// <paramref name="materialsFirst"/> with no shooters, for the makings (a crossbow's hide; K8.5).
        /// </summary>
        public void SendClear(Simpiens.Simulation.Creatures.CreatureKind kind = Simpiens.Simulation.Creatures.CreatureKind.None, RetreatRule rule = RetreatRule.Steady, bool arm = true, int shooters = 0,
            ItemType bow = ItemType.None, Simpiens.Simulation.Creatures.CreatureKind materialsFirst = Simpiens.Simulation.Creatures.CreatureKind.None)
        {
            var plan = _plans[0];
            Plan(plan, 0, 1, rule, outfit: false, draughts: 0, stockpile: 0);
            plan.Clear = true;
            plan.LairKind = kind;
            plan.Arm = arm;
            plan.Shooters = shooters;
            plan.Bow = bow;
            plan.MaterialsFirst = materialsFirst;
            plan.Target = byte.MaxValue; // planned: the lair is found when it is ready
        }

        /// <summary>
        /// Sends party 1 to clear lairs in <paramref name="order"/> (Epic 10 Part 3, K10.6: the Old Stair's web, then its gate):
        /// each time it is home and ready, the first kind in the order whose lair is charted and holds a creature, armed as
        /// <see cref="SendClear"/> arms it, with <paramref name="draughts"/> each from the stall's stock once it holds
        /// <paramref name="stockpile"/> of them.
        /// </summary>
        public void SendClears(Simpiens.Simulation.Creatures.CreatureKind[] order, RetreatRule rule = RetreatRule.Steady, int shooters = 0, ItemType bow = ItemType.None,
            Simpiens.Simulation.Creatures.CreatureKind materialsFirst = Simpiens.Simulation.Creatures.CreatureKind.None, byte draughts = 0, int stockpile = 0)
        {
            SendClear(order.Length > 0 ? order[0] : Simpiens.Simulation.Creatures.CreatureKind.None, rule, arm: true, shooters, bow, materialsFirst);
            var plan = _plans[0];
            plan.ClearOrder = (Simpiens.Simulation.Creatures.CreatureKind[])order.Clone();
            plan.Draughts = draughts;
            plan.Stockpile = stockpile;
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

        private bool _payRaises = true;

        /// <summary>
        /// Leaves raises unpaid (Epic 11, S2.3): members keep the wage they signed for when they come to ask more, for the
        /// trust test. Returns this player.
        /// </summary>
        public GuildPlayer LeaveRaisesUnpaid()
        {
            _payRaises = false;
            return this;
        }

        /// <summary>Raises it gave (Epic 11, S2.3: a member's wage set to what it asks), and when it first did; -1 until it has.</summary>
        public int RaisesPaid { get; private set; }
        public long FirstRaiseTick { get; private set; } = -1;

        /// <summary>
        /// Pays raises (Epic 11, S2.3, D8): each member whose asking wage stands above its wage, as the dossier shows it, is
        /// set to what it asks, while the treasury holds the reserve and a visit's wages at the raise besides.
        /// </summary>
        private void PayRaises(GuildService guild, long tick)
        {
            if (!_payRaises || guild.Members.Count == 0 || _sim.Simulation.ActiveStalls.Count == 0) return;
            var host = (IGuildHost)_sim.Simulation;
            var tuning = _sim.Simulation.Economy.Guild;
            uint wages = 0;
            for (int i = 0; i < guild.Members.Count; i++) wages += guild.Members[i].Wage;
            for (int i = 0; i < guild.Members.Count; i++)
            {
                var contract = guild.Members[i];
                if (!host.TryGetCrawler(contract.CrawlerId, out var member)) continue;
                ushort asking = GuildRules.AskingWage(member, tuning);
                if (asking <= contract.Wage) continue;
                uint raise = (uint)(asking - contract.Wage);
                if (_sim.Simulation.ActiveStalls[0].TreasuryGold < _reserve + wages + raise) continue;
                if (GuildRules.CheckSetWage(guild, contract.CrawlerId, asking, tuning) != GuildRules.Check.Ok) continue;
                _commands.QueueInfluence(InfluenceEvent.CreateSetWage(SourceId, contract.CrawlerId, asking));
                wages += raise;
                RaisesPaid++;
                if (FirstRaiseTick < 0) FirstRaiseTick = tick;
            }
        }

        /// <summary>Sends a price command, as the player's merchant terminal does.</summary>
        public void SetPrices(in UpdateStallPricesCommand prices) => _commands.QueueInfluence(InfluenceEvent.CreateUpdateStallPrices(SourceId, prices));

        private void Act(long tick)
        {
            var guild = _sim.Simulation.Guild;
            if (_commands == null) return;
            PayRaises(guild, tick);
            if (guild.Members.Count >= _targetSize)
            {
                // Readying gives the unprotected their gear first, in both parties, then every slot its piece, then the parties go
                // (the gifts apply before the dispatch, on the next tick)
                _stockTaken = false;
                _giftCount = 0;
                if (_together && _plans[0].Target != 0 && _plans[1].Target != 0)
                {
                    // Both at once: only when neither is out and both are ready
                    if (guild.HasExpedition) return;
                    bool first = Ready(guild, _plans[0], out var one), second = Ready(guild, _plans[1], out var two);
                    OutfitEverySlot(guild);
                    if (first && second)
                    {
                        Send(_plans[0], one, tick);
                        Send(_plans[1], two, tick);
                    }
                    return;
                }
                for (int i = 0; i < _plans.Length; i++)
                {
                    _ready[i] = _plans[i].Target != 0 && Ready(guild, _plans[i], out _orders[i]);
                }
                OutfitEverySlot(guild);
                for (int i = 0; i < _plans.Length; i++)
                {
                    if (_ready[i]) Send(_plans[i], _orders[i], tick);
                }
                return;
            }
            var host = (IGuildHost)_sim.Simulation;
            if (_reserve > 0 && _sim.Simulation.ActiveStalls[0].TreasuryGold < _sim.Simulation.Economy.Guild.SigningFee + _reserve) return;
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

            if (plan.Clear)
            {
                // A clear (K7.4): its lair charted and holding a creature, every member armed (from stock first) and well
                // The bows' makings first (K8.5): while the stall holds too few of the bow, the materials' lair, with no shooters
                var kind = plan.LairKind;
                if (plan.ClearOrder != null)
                    for (int k = plan.ClearOrder.Length - 1; k >= 0; k--) if (ClearTarget(plan.ClearOrder[k]) >= 0) kind = plan.ClearOrder[k]; // the first ready, in order
                int shooters = plan.Shooters;
                if (plan.Bow != ItemType.None && BowsInStock(plan.Bow) + BowsWorn(host, members, plan.Bow) < shooters)
                {
                    kind = plan.MaterialsFirst;
                    shooters = 0;
                }
                int lair = ClearTarget(kind);
                if (lair < 0) return false;
                if (plan.Arm && !Armed(guild, host, members, shooters)) return false;
                if (plan.Stockpile > 0 && DraughtsInStock() < plan.Stockpile) return false; // kitted (K10.6)
                if (!AllWell(host, members)) return false;
                order = new DispatchExpeditionCommand(plan.Slot, 0, plan.Goal, plan.Rule, plan.Draughts, ExpeditionKind.Clear, (byte)lair);
                return guild.CanDispatch(host, order) == GuildRules.Check.Ok;
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
        /// Outfits the party for its target's hazard from the stall's stock, as the party builder does (Epic 10 Part 2, D50;
        /// slot by slot, Epic 10 Part 3, K1.4: <see cref="GuildRules.PlanOutfit"/>): this round, members with no sound
        /// protection get their best piece; <see cref="OutfitEverySlot"/> gives every slot its piece after every party has had
        /// this round. True when every member wears some good for a trip.
        /// </summary>
        private bool Outfitted(GuildService guild, IGuildHost host, PartyPlan plan)
        {
            _outfitting[plan.Slot - 1] = true;
            int from = _giftCount;
            GuildRules.PlanOutfit(guild, host, plan.Members, GuildRules.HazardOf(host, plan.Target), OutfitRound.Unprotected, ref StockLeft(), _gifts, ref _giftCount);
            Give(from, GuildRules.HazardOf(host, plan.Target));
            bool all = true;
            for (int i = 0; i < plan.Members.Count; i++)
            {
                if (host.TryGetCrawler(plan.Members[i], out var member) && member.Inventory != null && !GuildRules.WearsSoundProtection(member.Inventory)) all = false; // a member with sound gear can go; better gear is given when there is some
            }
            return all;
        }

        /// <summary>The second round, after every party's first: each party being readied gets the best piece in stock for every slot of each member, as stock allows.</summary>
        private void OutfitEverySlot(GuildService guild)
        {
            var host = (IGuildHost)_sim.Simulation;
            for (int p = 0; p < _plans.Length; p++)
            {
                if (!_outfitting[p]) continue;
                _outfitting[p] = false;
                var hazard = GuildRules.HazardOf(host, _plans[p].Target);
                int from = _giftCount;
                GuildRules.PlanOutfit(guild, host, _plans[p].Members, hazard, OutfitRound.EverySlot, ref StockLeft(), _gifts, ref _giftCount);
                Give(from, hazard);
            }
        }

        private ref TradeStallData StockLeft()
        {
            if (!_stockTaken)
            {
                _stockLeft = _sim.Simulation.ActiveStalls.Count > 0 ? _sim.Simulation.ActiveStalls[0] : default;
                _stockTaken = true;
            }
            return ref _stockLeft;
        }

        private void Give(int from, Simpiens.Simulation.World.HazardKind hazard)
        {
            for (int i = from; i < _giftCount; i++)
            {
                _commands.QueueInfluence(InfluenceEvent.CreateGiveGear(SourceId, _gifts[i].Crawler, _gifts[i].Item, hazard));
                GearGiven++;
            }
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

        /// <summary>The first charted lair of <paramref name="kind"/> (any, for None) with a creature in it, as the board shows them; -1 for none.</summary>
        private int BowsInStock(ItemType bow) => _sim.Simulation.ActiveStalls.Count > 0 ? _sim.Simulation.ActiveStalls[0].SellStockOf(bow) : 0;

        private static int BowsWorn(IGuildHost host, List<EntityId> members, ItemType bow)
        {
            int worn = 0;
            for (int i = 0; i < members.Count; i++)
            {
                if (!host.TryGetCrawler(members[i], out var m) || m.Inventory == null) continue;
                var slots = Simpiens.Simulation.Items.EquipmentSlots.All;
                for (int s = 0; s < slots.Count; s++) if (m.Inventory.GetEquipped(slots[s].Id).Type == bow) worn++;
            }
            return worn;
        }

        private int ClearTarget(Simpiens.Simulation.Creatures.CreatureKind kind)
        {
            var creatures = _sim.Simulation.Creatures;
            for (int i = 0; i < creatures.Lairs.Count; i++)
            {
                if ((kind == Simpiens.Simulation.Creatures.CreatureKind.None || creatures.Lairs[i].Definition.Kind == kind) && creatures.IsCharted(i) && creatures.Lairs[i].Alive > 0) return i;
            }
            return -1;
        }

        /// <summary>
        /// Arms the party from the stall's stock for a fight (K7.4): each member, slot by slot (the weapon, the body, the head),
        /// is given the piece that most outfits it for fighting, while stock lasts (promised pieces counted); true when every
        /// member is armed.
        /// </summary>
        private bool Armed(GuildService guild, IGuildHost host, List<EntityId> members, int shooters)
        {
            ref var stock = ref StockLeft();
            int count = 0;
            GuildRules.PlanArms(guild, host, members, ref stock, _gifts, ref count, shooters); // the builder's rule (K7.1, K8.2c)
            for (int i = 0; i < count; i++)
            {
                _commands.QueueInfluence(InfluenceEvent.CreateGiveGear(SourceId, _gifts[i].Crawler, _gifts[i].Item, Simpiens.Simulation.World.HazardKind.None, forFighting: true));
                GearGiven++;
            }
            bool all = count == 0; // what it was given is worn on the next tick
            for (int i = 0; i < members.Count && all; i++)
            {
                if (!host.TryGetCrawler(members[i], out var member) || !Simpiens.Cognition.Vitality.IsArmed(member.Inventory)) all = false;
                else if (i >= members.Count - shooters && !Simpiens.Cognition.Planning.Handlers.Ranged.IsShooter(member)) all = false; // a shooter waits for its bow
            }
            return all;
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
