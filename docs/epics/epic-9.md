# Epic 9: The Outpost Economy & Public Bounties

- **Status**: MECHANISM COMPLETE (Track A / Production Phase 1 Engine). Status is owned by [ROADMAP.md](../ROADMAP.md); the live-loop criterion of its Definition of Done (§1B) is not yet demonstrated. The review §3.1 supply side was implemented on 2026-10-09; its PlayMode scenario has not been run yet, and promotion is held until §3.2/§3.3 land.
- **Track**: Track A: Core Simulation & Cognition Engine
- **Preceding Epic**: Epic 8: Socio-Political Structures & The Generic Group Primitive
- **Following Epic**: Epic 10: Sector Hazards & Dungeon Descent
- **Downstream Consumers**: Epic UI-2 (Merchant Terminal & Bounty Board), Epic Test-2 (Outpost Economy Harness)
- **Architectural Layer**: Core Simulation (`Simpiens.Simulation.Economy`, `SimulationManager`), Cognition (`Simpiens.Cognition.Planning`, `AgentInventory`), Intervention (`PlayerInterventionService`, `BountyContract`)

---

> [!IMPORTANT]
> **Status Reconciliation (2026-10-08).** Slices 1–3 (inventory/currency primitives, trade stall, bounty board and intervention pipeline) are delivered and validator-tested. The items below were specified in this document but **not delivered**, and the live gameplay loop is **not yet demonstrated** (see [ROADMAP Milestone 1 Status](../ROADMAP.md#milestone-1-status-verified-against-the-code-2026-10-08-not-yet-closed)).
>
> **Deferred / not delivered:**
> - `MemoryPack` state serialization (package not installed; `ItemInstance`/`BountyContract` were *laid out* to be blittable, but nothing serializes them).
> - ~~Zero-allocation Grid A* upgrade of `AsyncPathfinder`~~ **DELIVERED (2026-10-09)**: Delivered via `StaticNavGrid`, zero-allocation `GridAStarSearcher`, and worker pool in `AsyncPathfinder`.
> - `EconomyZeroAllocValidator` and `EconomySerializationValidator` (§6 items 3–4).
>
> **Live-loop gaps (2026-10-08) → addressed in code by review §3.1 (2026-10-09):** typed resources and salvage harvesting, Scout bounty fulfilment, supply caravan restock and node regrowth, escrow-only payout, and a free-crawler population in the Outpost Commons. The root cause was that Tier-2 GOAP never ran in the live session (ROADMAP §1A.2 #11). **Still open:** the no-injection PlayMode scenario `OutpostSupplyLoopScenario` has not been run, so the live loop is not yet demonstrated.

---

## 1. Executive Summary & Problem Statement

In **Epic 8**, agents gained the ability to organize into collectives ([`GroupRegistry`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Simulation/GroupRegistry.cs)) and execute top-down directives ([`GroupDirective`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Groups/GroupDirective.cs)) based on hierarchical compliance ([`ComplianceUtility`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Groups/ComplianceUtility.cs)).

However, our simulation currently lacks an **economic feedback loop**:
1. **The Direct Intervention Deficit**: While [`PlayerInterventionService`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Intervention/PlayerInterventionService.cs) exists as a concurrent queue of `InfluenceEvent` structs, it is completely unhooked from `SimulationManager.Tick()`. The player has no mechanism to influence the world.
2. **The Binary Inventory Bottleneck**: Agents only possess a single boolean flag `bool HasFoodInInventory` ([`AutonomousAgent.cs:69`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/AutonomousAgent.cs#L69)). There is no concept of item types, stack quantities, equipment, or currencies.
3. **Absence of Public Contracts**: `GroupDirective` only works for formal leaders commanding subordinates within an established group. Unaligned drifters and independent crawlers have no mechanism to take on freelance work.
4. **Zero Trading Mechanics**: There is no merchant infrastructure. Agents cannot trade harvested salvage for provisions, and players cannot accumulate capital or interpersonal affinity through commerce.

### The Epic 9 Solution: The Outpost Economy
Epic 9 implements the foundational engine mechanics for **Production Phase 1 (The Outpost Loop)**:
* **Zero-Allocation Inventory & Currency**: Fixed-capacity unmanaged `ItemInstance` structs and integer `Gold` balances on agents and the player outpost.
* **Public Bounty Board**: Player posts public contracts (`ScoutSector`, `GatherResource`) via `PlayerInterventionService` which are broadcast through `SharedWorldSnapshot`.
* **Trade Stall Entity (`EntityType.Stall`)**: A spatial market anchor where agents buy provisions (Rations) and sell harvested materials (Iron Ore, Herbs).
* **GOAP Economic Expansion**: New goals (`ClaimBountyGoal`, `PurchaseProvisionsGoal`) and actions (`TravelToStallAction`, `DeliverBountyAction`, `TradeAtStallAction`).
* **Trade-to-Affinity Pipeline**: Commercial transactions directly mutate the agent's dyadic [`AffinityRecord`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Social/AffinityRecord.cs) toward the Player entity ($+\text{Trust}$ and $+\text{Warmth}$ for fair trades; $-\text{Trust}$ for extortion).

---

## 2. Conceptual Architecture & Data Flow

Player agency operates strictly through economic leverage and utility injection rather than direct pawn manipulation:

```mermaid
sequenceDiagram
    autonumber
    actor Player as Player (UI Terminal)
    participant PIS as PlayerInterventionService
    participant SM as SimulationManager
    participant SWS as SharedWorldSnapshot
    participant Agent as AutonomousAgent (GOAP)
    participant Stall as TradeStall (World Node)

    Player->>PIS: QueueInfluence(PostBounty: Gather Iron, 25 Gold)
    SM->>PIS: TryDequeueInfluence() during Tick()
    SM->>SM: Register Bounty in Public BountyBoard
    SM->>SWS: Bake active Bounties into lock-free snapshot
    
    Note over Agent: Background GOAP Planning
    Agent->>SWS: Query Available Bounties
    Agent->>Agent: Evaluate Utility (Reward vs Distance vs Needs)
    Agent->>SM: Enqueue ClaimBountyIntent
    
    Note over Agent: Execution Phase
    Agent->>Agent: Travel to Sector & Harvest Resource
    Agent->>Stall: Travel to Trade Stall
    Agent->>SM: Enqueue DeliverBountyIntent
    
    SM->>Agent: Transfer Gold (+25g) & Clear Harvested Item
    SM->>Stall: Increment Stall Stockpile (+1 Iron Ore)
    SM->>Agent: Mutate Affinity toward Player (+10 Trust, +5 Warmth)
    SM->>SM: RecordStoryEvent(ChronicleEventType.BountyFulfilled)
```

---

## 3. Data Architecture & Memory Layout (Zero-Allocation Hot Paths)

All economic data types are designed as **unmanaged, fixed-size value types** to guarantee zero heap allocations during high-frequency simulation ticks. Furthermore, their unmanaged, blittable layout makes them natively compatible with **`MemoryPack`** (`com.cysharp.memorypack`) for direct memory block copy (`Unsafe.CopyBlock`) serialization—enabling instant save/load, tick replay streams, and roguenote floor migration at memory bandwidth speed.

```
Memory Layout of Core Economic Structs:
┌────────────────────────────────────────────────────────┐
│ ItemInstance (16 bytes, unmanaged struct)              │
│ [ItemId:2B][Type:1B][Quality:1B]                       │
│ [Quantity:2B][BaseValue:2B]                            │
│ [Durability:2B][Pad:6B]                                │
├────────────────────────────────────────────────────────┤
│ BountyContract (32 bytes, unmanaged struct)            │
│ [BountyId:2B][Type:1B][TargetSectorId:1B]              │
│ [TargetItemType:1B][TargetQuantity:1B][RewardGold:2B]  │
│ [ClaimantId:16B (GUID)]                                │
│ [IssuedTick:4B][ExpirationTick:4B]                     │
├────────────────────────────────────────────────────────┤
│ TradeStallData (32 bytes, unmanaged snapshot struct)   │
│ [StallEntityId:16B (GUID)]                             │
│ [RationSellPrice:2B][RationStock:2B]                   │
│ [OreBuyPrice:2B][OreStock:2B]                          │
│ [HerbBuyPrice:2B][HerbStock:2B]                        │
│ [TreasuryGold:4B]                                      │
└────────────────────────────────────────────────────────┘
```

### 3.1 Type Definitions (`Simpiens.Simulation.Economy`)

```csharp
namespace Simpiens.Simulation.Economy
{
    public enum ItemType : byte
    {
        None        = 0,
        Ration      = 1, // Restores hunger; purchased by agents
        IronOre     = 2, // Raw metal; gathered via bounties, sold to player
        MedicinalHerb = 3, // Healing/comfort; gathered via bounties
        ToolPickaxe = 4, // Enhances harvest yield
        BasicWeapon = 5  // Equipped for hazard defense
    }

    public enum BountyType : byte
    {
        None           = 0,
        ScoutSector    = 1, // Explore shrouded sector and report back
        GatherResource = 2  // Harvest specific resource and deliver to stall
    }

    [MemoryPack.MemoryPackable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public readonly partial struct ItemInstance : System.IEquatable<ItemInstance>
    {
        public readonly ushort ItemId;
        public readonly ItemType Type;
        public readonly byte Quality; // 0 = Standard, 1 = Fine, 2 = Masterwork
        public readonly ushort Quantity;
        public readonly ushort BaseValue;
        public readonly ushort Durability;
        private readonly ushort _pad0;
        private readonly uint _pad1;

        public static readonly ItemInstance Empty = default;
        public bool IsEmpty => Type == ItemType.None || Quantity == 0;

        public ItemInstance(ushort itemId, ItemType type, ushort quantity, ushort baseValue, byte quality = 0, ushort durability = 100)
        {
            ItemId = itemId;
            Type = type;
            Quality = quality;
            Quantity = quantity;
            BaseValue = baseValue;
            Durability = durability;
            _pad0 = 0;
            _pad1 = 0;
        }

        public ItemInstance WithQuantity(ushort newQty)
        {
            return new ItemInstance(ItemId, Type, newQty, BaseValue, Quality, Durability);
        }

        public bool Equals(ItemInstance other) => ItemId == other.ItemId && Type == other.Type && Quantity == other.Quantity;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public readonly struct BountyContract
    {
        public readonly ushort BountyId;
        public readonly BountyType Type;
        public readonly byte TargetSectorId;
        public readonly ItemType TargetItemType;
        public readonly byte TargetQuantity;
        public readonly ushort RewardGold;
        public readonly UnityEngine.GUID ClaimantId;
        public readonly uint IssuedTick;
        public readonly uint ExpirationTick;

        public static readonly BountyContract None = default;

        public bool IsActive(uint currentTick) => Type != BountyType.None && currentTick <= ExpirationTick;
        public bool IsClaimed => ClaimantId != default;

        public BountyContract(ushort bountyId, BountyType type, byte targetSectorId, ItemType targetItemType,
                              byte targetQuantity, ushort rewardGold, UnityEngine.GUID claimantId,
                              uint issuedTick, uint expirationTick)
        {
            BountyId = bountyId;
            Type = type;
            TargetSectorId = targetSectorId;
            TargetItemType = targetItemType;
            TargetQuantity = targetQuantity;
            RewardGold = rewardGold;
            ClaimantId = claimantId;
            IssuedTick = issuedTick;
            ExpirationTick = expirationTick;
        }

        public BountyContract WithClaimant(UnityEngine.GUID claimantId)
        {
            return new BountyContract(BountyId, Type, TargetSectorId, TargetItemType, TargetQuantity, RewardGold, claimantId, IssuedTick, ExpirationTick);
        }
    }
}
```

### 3.2 Fixed-Buffer Agent Inventory

To avoid heap allocations and Garbage Collector pressure in `AutonomousAgent`, inventories use a fixed-size internal array (4 slots for drifters, expandable to 8 slots for contracted veterans):

```csharp
namespace Simpiens.Cognition
{
    public class AgentInventory
    {
        public const int DefaultCapacity = 4;
        private readonly ItemInstance[] _slots;
        private readonly int _capacity;

        public int Capacity => _capacity;
        public int Gold { get; set; }

        public AgentInventory(int capacity = DefaultCapacity, int startingGold = 10)
        {
            _capacity = capacity;
            _slots = new ItemInstance[capacity];
            Gold = startingGold;
        }

        public bool TryAddItem(in ItemInstance item, out ItemInstance remainder)
        {
            // 1. Stack into existing slots of same Type
            for (int i = 0; i < _capacity; i++)
            {
                if (_slots[i].Type == item.Type && _slots[i].Quality == item.Quality)
                {
                    _slots[i] = _slots[i].WithQuantity((ushort)(_slots[i].Quantity + item.Quantity));
                    remainder = ItemInstance.Empty;
                    return true;
                }
            }

            // 2. Insert into first empty slot
            for (int i = 0; i < _capacity; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    _slots[i] = item;
                    remainder = ItemInstance.Empty;
                    return true;
                }
            }

            remainder = item;
            return false; // Inventory full
        }

        public bool HasItem(ItemType type, int count = 1)
        {
            int total = 0;
            for (int i = 0; i < _capacity; i++)
            {
                if (_slots[i].Type == type)
                {
                    total += _slots[i].Quantity;
                    if (total >= count) return true;
                }
            }
            return false;
        }

        public bool TryRemoveItem(ItemType type, int count)
        {
            if (!HasItem(type, count)) return false;

            int remainingToRemove = count;
            for (int i = 0; i < _capacity; i++)
            {
                if (_slots[i].Type == type)
                {
                    if (_slots[i].Quantity <= remainingToRemove)
                    {
                        remainingToRemove -= _slots[i].Quantity;
                        _slots[i] = ItemInstance.Empty;
                    }
                    else
                    {
                        _slots[i] = _slots[i].WithQuantity((ushort)(_slots[i].Quantity - remainingToRemove));
                        remainingToRemove = 0;
                    }

                    if (remainingToRemove <= 0) break;
                }
            }
            return true;
        }
    }
}
```

---

## 4. GOAP Integration & Utility Decision Math

### 4.1 StateFact Additions
Expand [`StateFact.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Planning/StateFact.cs) with economic facts:
```csharp
HasActiveBounty       = 25,
AtTradeStall          = 26,
BountyDelivered       = 27,
HasProvisions         = 28,
HasSalvageInInventory = 29
```

### 4.2 Economic Goals
1. **`ClaimBountyGoal`**: Triggered when the agent's Energy and Hunger are stable ($\text{Hunger} < 60$, $\text{Energy} > 40$) and an unclaimed public bounty exists in memory or snapshot.
   * Priority formula:
     $$\text{Priority} = \text{BaseRewardWeight} \times \left(\frac{\text{RewardGold}}{50}\right) - \left(\frac{\text{DistanceToSector}}{20}\right) - (\text{Frustration} \times 0.2)$$
2. **`PurchaseProvisionsGoal`**: Triggered when an agent is hungry ($\text{Hunger} \ge 70$), possesses Gold ($\text{Gold} \ge \text{RationPrice}$), but has no food in inventory.

### 4.3 Economic Actions
* **`TravelToStallAction`**: Navigates agent to the trade stall anchor across sector bounds via [`AsyncPathfinder`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Pathfinding/AsyncPathfinder.cs).
* **`ClaimBountyAction`**: Claims an active bounty contract from the public board.
* **`DeliverBountyAction`**: Interacts with the stall node, handing over required salvage and receiving bounty gold.
* **`BuyProvisionsAction`**: Spends gold at the stall to acquire a `Ration` item.
* **`SellSalvageAction`**: Sells excess harvested ore/herbs for market price.

> [!NOTE]
> **Navigation Infrastructure Upgrade** *(DEFERRED — not delivered)*: Multi-sector travel between resource nodes and the player stall will utilize the upgraded zero-allocation Grid A* engine in [`AsyncPathfinder.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Pathfinding/AsyncPathfinder.cs), porting the array-backed min-heap and node pool from [`GoapPlanner.cs`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/Planning/GoapPlanner.cs) to eliminate watchdog stalls across complex sector layouts.

---

## 5. System Integration & Execution Flow

### 5.1 Connecting `PlayerInterventionService`
In [`SimulationManager.Tick()`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Core/SimulationManager.cs#L170), process queued influence commands before updating spatial snapshots:

```csharp
while (_playerIntervention.TryDequeueInfluence(out var influence))
{
    switch (influence.CommandType)
    {
        case "PostBounty":
            ParseAndRegisterBounty(influence.Payload);
            break;
        case "CancelBounty":
            CancelBounty(influence.Payload);
            break;
        case "UpdateStallPrices":
            UpdateStallPrices(influence.Payload);
            break;
    }
}
```

### 5.2 Trade-to-Affinity Pipeline
When an agent completes a transaction at the trade stall, dyadic affinity toward the Player entity (`WorldRegistry.PlayerEntityId`) is updated:
* **Fair Trade / Generous Reward**:
  * Payout $\ge \text{FairMarketValue}$: $+10\text{ Trust}$, $+5\text{ Warmth}$, $+5\text{ Respect}$.
* **Price Gouging / Low Reward**:
  * Ration price $> 150\%$ base cost: $-15\text{ Trust}$, $-10\text{ Warmth}$.
* **Threshold Conversion**:
  * Once dyadic $\text{Trust} \ge 60$ and $\text{Warmth} \ge 40$, the agent qualifies for recruitment into the player's permanent Guild in **Phase 2 / Epic 10**.

---

## 6. Testing & Headless Verification Suite (`Simpiens.Testing.Economy`)

Following the defensive null-safety and headless testing architecture of Simpiens, all economic mechanics are accompanied by batch-mode domain validators in `Assets/Scripts/Testing/`:

1. **`BountyFulfillmentValidator.cs`**:
   * Simulates posting a `GatherResource` bounty.
   * Asserts an unaligned agent claims the bounty, navigates to the node, gathers the resource, delivers to the stall, and receives the gold payout.
   * Asserts stall stockpile increments by the target quantity.
2. **`TradeTransactionValidator.cs`**:
   * Tests buy/sell boundaries, wallet underflow prevention, and inventory overflow handling.
   * Validates that dyadic `Trust` and `Warmth` increase after fair trade transactions.
3. **`EconomyZeroAllocValidator.cs`** *(DEFERRED — not implemented)*:
   * Runs 1,000 simulated trading and bounty evaluation ticks off-thread.
   * Asserts zero heap allocations during the steady-state evaluation loop.
4. **`EconomySerializationValidator.cs`** *(DEFERRED — not implemented; requires `MemoryPack`)*:
   * Validates that `ItemInstance`, `BountyContract`, and `AgentInventory` round-trip serialize with `MemoryPack` at 0 bytes GC.

---

## 7. Acceptance Criteria (Definition of Done)

- [x] `ItemInstance`, `BountyContract`, and `TradeStallData` implemented as unmanaged structs with zero heap allocations.
- [x] Memory layout compatibility verified for unmanaged economic data structures (fixed 16B, 44B, 64B sizes).
- [x] `AgentInventory` integrated into [`AutonomousAgent`](file:///home/bfox/Code/Repos/Unity/Simpiens/Assets/Scripts/Cognition/AutonomousAgent.cs) with fixed-size struct array storage.
- [x] `PlayerInterventionService` drained and executed within `SimulationManager.Tick()`.
- [x] Public `BountyBoard` baked into `SharedWorldSnapshot` for lock-free background worker queries.
- [x] GOAP actions (`ClaimBountyAction`, `DeliverBountyAction`, `BuyProvisionsAction`, `SellSalvageAction`) registered in `SwarmSpawner` action graph.
- [x] Trade transactions mutate agent dyadic affinity toward the player entity.
- [x] All headless validators (`InventoryValidator`, `TradeTransactionValidator`, `BountyFulfillmentValidator`) passing with 0 errors in headless batch mode.

### Review §3.1 — Milestone 1 Supply Side (implemented 2026-10-09)

Delivered in code (compiles; `SupplyLoopValidator` and `OutpostSupplyLoopScenario` **not yet run**):

- [x] Typed resources: `ResourceData.YieldItem/MaxYield/RegrowTicks`, `EntitySnapshot.ResourceKind`, `SpatialMemoryRecord.ResourceKind`, salvage node sprites.
- [x] Tier-1 `SurvivalUtilityEvaluator` reduced to a hunger reflex; ambient idle/wander moved to the Tier-3 `AmbientWanderEvaluator` fallback, so GOAP runs in the live session.
- [x] Real food chain intents (`SearchResource`, `TravelToResource`, `HarvestResource`).
- [x] Salvage GOAP domain: facts `KnowsSalvageLocation`, `HasGatherBounty`, `HasScoutBounty`, `HasBountyItem`, `HasScoutReport`, `HasSoldSalvage`; actions `SearchSalvage`, `GatherSalvage`, `ClaimGatherBounty`, `ClaimScoutBounty`, `ScoutSector`, `ReportScoutBounty`; `EarnIncomeGoal`.
- [x] Harvest handler yields salvage into inventory, fails without consuming yield when the inventory is full, supports multi-unit gathering, and sends depleted regrowable nodes dormant.
- [x] Scout fulfilment (`ScoutIntent`/`ScoutIntentHandler`, report redeemed at a stall, sector marked explored).
- [x] Escrow-only bounty payout; `RegisterBounty` escrows or rejects.
- [x] `SupplyService`: deterministic dormant regrowth and supply caravan (`SupplyCaravan` ledger sink).
- [x] Outpost Commons arena (stall at (0, 9), Ore Ridge, Herb Hollow, 6 free crawlers, zones 11–13).

**Deferred:** live-loop demonstration (first run of `OutpostSupplyLoopScenario`) — owned by Epic 9 / Milestone 1 closure; personality-driven bounty acceptance — §3.2; faucet/sink retuning — §3.3 / Epic Test-2; moving the production GOAP domain out of `Testing/` — §2.8.
