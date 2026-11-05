using System;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;
namespace Simpiens.Intervention
{
    public enum InfluenceCommandType : byte
    {
        None = 0,
        PostBounty = 1,
        CancelBounty = 2,
        UpdateStallPrices = 3,
        /// <summary>The player calms a crawler: its frustration drops to zero and a tantrum or collapse ends (Epic CLIENT M2.2).</summary>
        CalmCrawler = 4,
        /// <summary>The guild (Epic 10 G4): sign a crawler on at a wage, let a member go, set a member's wage.</summary>
        Recruit = 5,
        Dismiss = 6,
        SetWage = 7,
        /// <summary>Expeditions (Epic 10 G5): send a party, call it home, give a member gear from the stall's stock.</summary>
        DispatchExpedition = 8,
        RecallExpedition = 9,
        GiveGear = 10,
        /// <summary>The workshops (Epic 10 Part 2): build a station, hire an artisan to work one (Dismiss and SetWage serve artisans too).</summary>
        BuildStation = 11,
        HireArtisan = 12,
        /// <summary>Shifts (Epic 10 Part 2, W1.3): order makes at a station, set which blocks of the day an artisan works.</summary>
        OrderStation = 13,
        SetShifts = 14
    }

    /// <summary>The player signs a crawler on with the guild at a wage per caravan visit (Epic 10 G4, D2).</summary>
    public readonly struct RecruitCommand
    {
        public readonly EntityId CrawlerId;
        public readonly ushort Wage;

        public RecruitCommand(EntityId crawlerId, ushort wage)
        {
            CrawlerId = crawlerId;
            Wage = wage;
        }
    }

    /// <summary>The player lets a guild member go (D4).</summary>
    public readonly struct DismissCommand
    {
        public readonly EntityId CrawlerId;

        public DismissCommand(EntityId crawlerId)
        {
            CrawlerId = crawlerId;
        }
    }

    /// <summary>The player sets a guild member's wage per caravan visit.</summary>
    public readonly struct SetWageCommand
    {
        public readonly EntityId CrawlerId;
        public readonly ushort Wage;

        public SetWageCommand(EntityId crawlerId, ushort wage)
        {
            CrawlerId = crawlerId;
            Wage = wage;
        }
    }

    /// <summary>
    /// The player sends a party of guild members out (Epic 10 G5, D7): one to <see cref="MaxMembers"/> members, the
    /// sector, how many of what its nodes yield to bring back, and when to turn back. Zero-allocation: four fields.
    /// </summary>
    public readonly struct DispatchExpeditionCommand
    {
        public const int MaxMembers = 4;

        public readonly byte TargetSectorId;
        public readonly byte Goal;
        public readonly RetreatRule Rule;
        public readonly int Count;
        private readonly EntityId _member0, _member1, _member2, _member3;

        public DispatchExpeditionCommand(byte targetSectorId, byte goal, RetreatRule rule, ReadOnlySpan<EntityId> members)
        {
            if (members.Length > MaxMembers) throw new ArgumentException($"A party has at most {MaxMembers} members.", nameof(members));
            TargetSectorId = targetSectorId;
            Goal = goal;
            Rule = rule;
            Count = members.Length;
            _member0 = Count > 0 ? members[0] : default;
            _member1 = Count > 1 ? members[1] : default;
            _member2 = Count > 2 ? members[2] : default;
            _member3 = Count > 3 ? members[3] : default;
        }

        public EntityId this[int index] => index switch
        {
            0 => _member0,
            1 => _member1,
            2 => _member2,
            3 => _member3,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };
    }

    /// <summary>The player calls the party home (D7). One expedition at a time, so it names none.</summary>
    public readonly struct RecallExpeditionCommand
    {
    }

    /// <summary>The player builds a station (Epic 10 Part 2, D10): the one at this index of the map's stations.</summary>
    public readonly struct BuildStationCommand
    {
        public readonly byte StationIndex;

        public BuildStationCommand(byte stationIndex)
        {
            StationIndex = stationIndex;
        }
    }

    /// <summary>The player hires a crawler to work a station at a wage per caravan visit (Epic 10 Part 2, D8, D9).</summary>
    public readonly struct HireArtisanCommand
    {
        public readonly EntityId CrawlerId;
        public readonly ushort Wage;
        public readonly byte StationIndex;

        public HireArtisanCommand(EntityId crawlerId, byte stationIndex, ushort wage)
        {
            CrawlerId = crawlerId;
            StationIndex = stationIndex;
            Wage = wage;
        }
    }

    /// <summary>The player orders makes at a station (Epic 10 Part 2, D32): this many of an item its recipe makes; none cancels.</summary>
    public readonly struct OrderStationCommand
    {
        public readonly ItemType Item;
        public readonly ushort Count;
        public readonly byte StationIndex;

        public OrderStationCommand(byte stationIndex, ItemType item, ushort count)
        {
            StationIndex = stationIndex;
            Item = item;
            Count = count;
        }
    }

    /// <summary>The player sets which of the day's six blocks an artisan works (Epic 10 Part 2, D12): a mask, block 0 from 06:00.</summary>
    public readonly struct SetShiftsCommand
    {
        public readonly EntityId CrawlerId;
        public readonly byte Shifts;

        public SetShiftsCommand(EntityId crawlerId, byte shifts)
        {
            CrawlerId = crawlerId;
            Shifts = shifts;
        }
    }

    /// <summary>The player gives a guild member gear from the stall's stock (D12): no gold changes hands.</summary>
    public readonly struct GiveGearCommand
    {
        public readonly EntityId CrawlerId;
        public readonly ItemType Item;

        public GiveGearCommand(EntityId crawlerId, ItemType item)
        {
            CrawlerId = crawlerId;
            Item = item;
        }
    }

    public readonly struct PostBountyCommand
    {
        public readonly ushort BountyId;
        public readonly BountyType Type;
        public readonly byte SectorId;
        public readonly ItemType ItemType;
        public readonly byte Quantity;
        public readonly ushort RewardGold;
        public readonly uint DurationTicks;
        public readonly Vec2 TargetLocation;
        public readonly EntityId CreatorStallId;

        public PostBountyCommand(
            ushort bountyId,
            BountyType type,
            byte sectorId,
            ItemType itemType,
            byte quantity,
            ushort rewardGold,
            uint durationTicks = 1000,
            Vec2 targetLocation = default,
            EntityId creatorStallId = default)
        {
            BountyId = bountyId;
            Type = type;
            SectorId = sectorId;
            ItemType = itemType;
            Quantity = quantity;
            RewardGold = rewardGold;
            DurationTicks = durationTicks > 0 ? durationTicks : 1000;
            TargetLocation = targetLocation;
            CreatorStallId = creatorStallId;
        }
    }

    public readonly struct CancelBountyCommand
    {
        public readonly ushort BountyId;

        public CancelBountyCommand(ushort bountyId)
        {
            BountyId = bountyId;
        }
    }

    /// <summary>One price in a price command: the item and its new price (0 keeps the current one).</summary>
    public readonly struct StallPrice
    {
        public readonly ItemType Item;
        public readonly ushort Price;

        public StallPrice(ItemType item, ushort price)
        {
            Item = item;
            Price = price;
        }
    }

    [System.Runtime.CompilerServices.InlineArray(UpdateStallPricesCommand.Capacity)]
    public struct StallPrices
    {
        private StallPrice _entry;
    }

    /// <summary>
    /// The player sets a stall's prices (item system I5): up to <see cref="Capacity"/> (item, price) pairs, applied
    /// together on the next tick in their order. A price of 0 keeps the current one, and an item the stall doesn't trade
    /// is ignored. Zero-allocation: the pairs are a fixed table.
    /// </summary>
    public readonly struct UpdateStallPricesCommand
    {
        public const int Capacity = 8;

        public readonly EntityId StallId;
        public readonly int Count;
        private readonly StallPrices _prices;

        public UpdateStallPricesCommand(EntityId stallId) : this(stallId, 0, default)
        {
        }

        private UpdateStallPricesCommand(EntityId stallId, int count, in StallPrices prices)
        {
            StallId = stallId;
            Count = count;
            _prices = prices;
        }

        public StallPrice this[int index] => _prices[index];

        /// <summary>The same command setting one more price (replacing an earlier price for the same item).</summary>
        public UpdateStallPricesCommand With(ItemType item, ushort price)
        {
            var prices = _prices;
            for (int i = 0; i < Count; i++)
            {
                if (prices[i].Item == item)
                {
                    prices[i] = new StallPrice(item, price);
                    return new UpdateStallPricesCommand(StallId, Count, prices);
                }
            }
            if (Count >= Capacity) throw new System.InvalidOperationException($"A price command sets at most {Capacity} prices.");
            prices[Count] = new StallPrice(item, price);
            return new UpdateStallPricesCommand(StallId, Count + 1, prices);
        }
    }

    /// <summary>The player calms one crawler (the dossier's Calm Frustration).</summary>
    public readonly struct CalmCrawlerCommand
    {
        public readonly EntityId CrawlerId;

        public CalmCrawlerCommand(EntityId crawlerId)
        {
            CrawlerId = crawlerId;
        }
    }

    /// <summary>
    /// A strongly-typed data object representing a player input or external intervention.
    /// Operates as a zero-allocation command struct applied at fixed simulation tick boundaries.
    /// </summary>
    public readonly struct InfluenceEvent
    {
        public readonly string SourceId;
        public readonly InfluenceCommandType CommandType;
        public readonly PostBountyCommand PostBounty;
        public readonly CancelBountyCommand CancelBounty;
        public readonly UpdateStallPricesCommand UpdateStallPrices;
        public readonly CalmCrawlerCommand CalmCrawler;
        public readonly RecruitCommand Recruit;
        public readonly DismissCommand Dismiss;
        public readonly SetWageCommand SetWage;
        public readonly DispatchExpeditionCommand DispatchExpedition;
        public readonly GiveGearCommand GiveGear;
        public readonly BuildStationCommand BuildStation;
        public readonly HireArtisanCommand HireArtisan;
        public readonly OrderStationCommand OrderStation;
        public readonly SetShiftsCommand SetShifts;

        public string LegacyCommandName => CommandType switch
        {
            InfluenceCommandType.PostBounty => "PostBounty",
            InfluenceCommandType.CancelBounty => "CancelBounty",
            InfluenceCommandType.UpdateStallPrices => "UpdateStallPrices",
            InfluenceCommandType.CalmCrawler => "CalmCrawler",
            InfluenceCommandType.Recruit => "Recruit",
            InfluenceCommandType.Dismiss => "Dismiss",
            InfluenceCommandType.SetWage => "SetWage",
            InfluenceCommandType.DispatchExpedition => "DispatchExpedition",
            InfluenceCommandType.RecallExpedition => "RecallExpedition",
            InfluenceCommandType.GiveGear => "GiveGear",
            InfluenceCommandType.BuildStation => "BuildStation",
            InfluenceCommandType.HireArtisan => "HireArtisan",
            InfluenceCommandType.OrderStation => "OrderStation",
            InfluenceCommandType.SetShifts => "SetShifts",
            _ => string.Empty
        };

        public InfluenceEvent(
            string sourceId,
            InfluenceCommandType commandType,
            in PostBountyCommand postBounty = default,
            in CancelBountyCommand cancelBounty = default,
            in UpdateStallPricesCommand updateStallPrices = default,
            in CalmCrawlerCommand calmCrawler = default,
            in RecruitCommand recruit = default,
            in DismissCommand dismiss = default,
            in SetWageCommand setWage = default,
            in DispatchExpeditionCommand dispatchExpedition = default,
            in GiveGearCommand giveGear = default,
            in BuildStationCommand buildStation = default,
            in HireArtisanCommand hireArtisan = default,
            in OrderStationCommand orderStation = default,
            in SetShiftsCommand setShifts = default)
        {
            BuildStation = buildStation;
            HireArtisan = hireArtisan;
            OrderStation = orderStation;
            SetShifts = setShifts;
            DispatchExpedition = dispatchExpedition;
            GiveGear = giveGear;
            SourceId = sourceId;
            CommandType = commandType;
            PostBounty = postBounty;
            CancelBounty = cancelBounty;
            UpdateStallPrices = updateStallPrices;
            CalmCrawler = calmCrawler;
            Recruit = recruit;
            Dismiss = dismiss;
            SetWage = setWage;
        }

        public static InfluenceEvent CreateRecruit(string sourceId, EntityId crawlerId, ushort wage)
        {
            return new InfluenceEvent(sourceId, InfluenceCommandType.Recruit, recruit: new RecruitCommand(crawlerId, wage));
        }

        public static InfluenceEvent CreateDismiss(string sourceId, EntityId crawlerId)
        {
            return new InfluenceEvent(sourceId, InfluenceCommandType.Dismiss, dismiss: new DismissCommand(crawlerId));
        }

        public static InfluenceEvent CreateSetWage(string sourceId, EntityId crawlerId, ushort wage)
        {
            return new InfluenceEvent(sourceId, InfluenceCommandType.SetWage, setWage: new SetWageCommand(crawlerId, wage));
        }

        public static InfluenceEvent CreateDispatchExpedition(string sourceId, in DispatchExpeditionCommand cmd)
        {
            return new InfluenceEvent(sourceId, InfluenceCommandType.DispatchExpedition, dispatchExpedition: cmd);
        }

        public static InfluenceEvent CreateRecallExpedition(string sourceId)
        {
            return new InfluenceEvent(sourceId, InfluenceCommandType.RecallExpedition);
        }

        public static InfluenceEvent CreateGiveGear(string sourceId, EntityId crawlerId, ItemType item)
        {
            return new InfluenceEvent(sourceId, InfluenceCommandType.GiveGear, giveGear: new GiveGearCommand(crawlerId, item));
        }

        public static InfluenceEvent CreateBuildStation(string sourceId, byte stationIndex)
        {
            return new InfluenceEvent(sourceId, InfluenceCommandType.BuildStation, buildStation: new BuildStationCommand(stationIndex));
        }

        public static InfluenceEvent CreateHireArtisan(string sourceId, EntityId crawlerId, byte stationIndex, ushort wage)
        {
            return new InfluenceEvent(sourceId, InfluenceCommandType.HireArtisan, hireArtisan: new HireArtisanCommand(crawlerId, stationIndex, wage));
        }

        public static InfluenceEvent CreateOrderStation(string sourceId, byte stationIndex, ItemType item, ushort count)
        {
            return new InfluenceEvent(sourceId, InfluenceCommandType.OrderStation, orderStation: new OrderStationCommand(stationIndex, item, count));
        }

        public static InfluenceEvent CreateSetShifts(string sourceId, EntityId crawlerId, byte shifts)
        {
            return new InfluenceEvent(sourceId, InfluenceCommandType.SetShifts, setShifts: new SetShiftsCommand(crawlerId, shifts));
        }

        public static InfluenceEvent CreatePostBounty(string sourceId, in PostBountyCommand cmd)
        {
            return new InfluenceEvent(sourceId, InfluenceCommandType.PostBounty, postBounty: cmd);
        }

        public static InfluenceEvent CreateCancelBounty(string sourceId, ushort bountyId)
        {
            return new InfluenceEvent(sourceId, InfluenceCommandType.CancelBounty, cancelBounty: new CancelBountyCommand(bountyId));
        }

        public static InfluenceEvent CreateUpdateStallPrices(string sourceId, in UpdateStallPricesCommand cmd)
        {
            return new InfluenceEvent(sourceId, InfluenceCommandType.UpdateStallPrices, updateStallPrices: cmd);
        }

        public static InfluenceEvent CreateCalmCrawler(string sourceId, EntityId crawlerId)
        {
            return new InfluenceEvent(sourceId, InfluenceCommandType.CalmCrawler, calmCrawler: new CalmCrawlerCommand(crawlerId));
        }

        /// <summary>
        /// Legacy constructor for backward compatibility with string-based tests and tools.
        /// Parses CSV payloads at instantiation time rather than inside the simulation hot loop.
        /// </summary>
        public InfluenceEvent(string sourceId, string commandType, string payload)
        {
            SourceId = sourceId;
            PostBounty = default;
            CancelBounty = default;
            UpdateStallPrices = default;
            CalmCrawler = default;
            Recruit = default;
            Dismiss = default;
            SetWage = default;

            if (string.Equals(commandType, "PostBounty", StringComparison.OrdinalIgnoreCase))
            {
                CommandType = InfluenceCommandType.PostBounty;
                if (!string.IsNullOrEmpty(payload))
                {
                    string[] parts = payload.Split(new char[] { ',', ':', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 6)
                    {
                        ushort.TryParse(parts[0].Trim(), out ushort bId);
                        Enum.TryParse<BountyType>(parts[1].Trim(), true, out var bType);
                        byte.TryParse(parts[2].Trim(), out byte sId);
                        Enum.TryParse<ItemType>(parts[3].Trim(), true, out var iType);
                        byte.TryParse(parts[4].Trim(), out byte qty);
                        ushort.TryParse(parts[5].Trim(), out ushort reward);

                        uint duration = 1000;
                        if (parts.Length > 6) uint.TryParse(parts[6].Trim(), out duration);

                        Vec2 loc = Vec2.Zero;
                        if (parts.Length > 8)
                        {
                            float.TryParse(parts[7].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x);
                            float.TryParse(parts[8].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y);
                            loc = new Vec2(x, y);
                        }

                        PostBounty = new PostBountyCommand(bId, bType, sId, iType, qty, reward, duration, loc);
                    }
                }
            }
            else if (string.Equals(commandType, "CancelBounty", StringComparison.OrdinalIgnoreCase))
            {
                CommandType = InfluenceCommandType.CancelBounty;
                if (!string.IsNullOrEmpty(payload) && ushort.TryParse(payload.Trim(), out ushort bId))
                {
                    CancelBounty = new CancelBountyCommand(bId);
                }
            }
            else if (string.Equals(commandType, "CalmCrawler", StringComparison.OrdinalIgnoreCase))
            {
                CommandType = InfluenceCommandType.CalmCrawler;
                if (!string.IsNullOrEmpty(payload) && EntityId.TryParse(payload.Trim(), out var crawlerId))
                {
                    CalmCrawler = new CalmCrawlerCommand(crawlerId);
                }
            }
            else
            {
                CommandType = InfluenceCommandType.None;
            }
        }
    }
}
