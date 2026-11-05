using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Simpiens.Core.Session.Layouts;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Workshops;

namespace Simpiens.Testing.Economy
{
    /// <summary>
    /// The crafting rule and the stations' owner (Epic 10 Part 2, W0.5), engine-free: a make takes its inputs from the
    /// stall once, runs its work time, and stocks the stall, or holds its output until there is room; orders are only for
    /// a built station's own recipes; the service lays out the map's stations unbuilt and forgets them on a reset; the
    /// rule allocates nothing. Runs as a test (ValidatorTests), never at boot.
    /// </summary>
    public class CraftingValidator
    {
        private static readonly EntityId StallId = new EntityId(1u, 0u, 0u, 1u);
        private static readonly Recipe Pickaxe = RecipeBook.For(ItemType.ToolPickaxe);

        public void RunAllValidationTests()
        {
            TestLog.Log("[CraftingValidator] Beginning crafting validation...");

            ValidateIdleAndWaiting();
            ValidateAMakeFromInputsToStock();
            ValidateHoldingUntilThereIsRoom();
            ValidateOrders();
            ValidateTheServiceFollowsTheMap();
            ValidateTheRuleAllocatesNothing();

            TestLog.Log("<color=green>[CraftingValidator] ALL 6 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private static TradeStallData Stall(ushort ore, bool sellsPickaxes = true)
        {
            var stall = new TradeStallData(StallId, Vec2.Zero, 1000u).WithGood(new StallGood(ItemType.IronOre, StallRole.Buys, 20, ore));
            return sellsPickaxes ? stall.WithGood(new StallGood(ItemType.ToolPickaxe, StallRole.Sells, 30, 0)) : stall;
        }

        private static StationState Forge(ushort makes) => new StationState { Kind = StationKind.Forge, Built = true, Order = ItemType.ToolPickaxe, Remaining = makes };

        private void ValidateIdleAndWaiting()
        {
            var stall = Stall(ore: 10);
            var unbuilt = StationState.Unbuilt(StationKind.Forge);
            Assert(Crafting.Work(ref unbuilt, ref stall, out bool changed) == CraftStep.Idle && !changed, "An unbuilt station is idle.");
            var noOrder = new StationState { Kind = StationKind.Forge, Built = true };
            Assert(Crafting.Work(ref noOrder, ref stall, out changed) == CraftStep.Idle && !changed, "A station without an order is idle.");
            var wrongStation = new StationState { Kind = StationKind.Still, Built = true, Order = ItemType.ToolPickaxe, Remaining = 1 };
            Assert(Crafting.Work(ref wrongStation, ref stall, out changed) == CraftStep.Idle && !changed, "A station never makes another station's recipe.");

            var poor = Stall(ore: 2);
            var forge = Forge(1);
            Assert(Crafting.Work(ref forge, ref poor, out changed) == CraftStep.WaitingForInputs && !changed, "Two ore don't make a three-ore pickaxe: it waits.");
            Assert(poor.StockOf(ItemType.IronOre) == 2 && !forge.InputsTaken && forge.Progress == 0, "Waiting takes nothing.");
            TestLog.Log("  [Passed] An unbuilt or unordered station is idle; one without its inputs waits and takes nothing.");
        }

        private void ValidateAMakeFromInputsToStock()
        {
            var stall = Stall(ore: 7);
            var forge = Forge(2);
            Assert(Crafting.Work(ref forge, ref stall, out bool changed) == CraftStep.Working && changed, "The first tick of work starts a make.");
            Assert(stall.StockOf(ItemType.IronOre) == 4 && forge.InputsTaken && forge.Progress == 1, "Its inputs leave the stall's stock once, as it begins.");

            for (int t = 2; t < Pickaxe.WorkTicks; t++)
            {
                Assert(Crafting.Work(ref forge, ref stall, out changed) == CraftStep.Working && !changed, $"Tick {t}: working, the stall untouched.");
            }
            Assert(stall.StockOf(ItemType.IronOre) == 4, "Working takes nothing more.");
            Assert(Crafting.Work(ref forge, ref stall, out changed) == CraftStep.Made && changed, $"After {Pickaxe.WorkTicks} ticks of work the make is done.");
            Assert(stall.StockOf(ItemType.ToolPickaxe) == 1 && forge.Remaining == 1 && forge.Made == 1 && !forge.InputsTaken && forge.Progress == 0, "The pickaxe is in the stall's stock; one make left.");
            Assert(forge.Order == ItemType.ToolPickaxe, "The order stands while makes are left.");

            for (int t = 0; t < Pickaxe.WorkTicks; t++) Crafting.Work(ref forge, ref stall, out _);
            Assert(stall.StockOf(ItemType.ToolPickaxe) == 2 && stall.StockOf(ItemType.IronOre) == 1 && forge.Made == 2, "The second make took three more ore and stocked a second pickaxe.");
            Assert(forge.Remaining == 0 && forge.Order == ItemType.None && !forge.HasOrder, "A finished order closes.");
            Assert(Crafting.Work(ref forge, ref stall, out changed) == CraftStep.Idle && !changed, "Then the station is idle.");
            TestLog.Log($"  [Passed] A make takes its inputs as it begins, runs {Pickaxe.WorkTicks} ticks and stocks the stall; a finished order closes.");
        }

        private void ValidateHoldingUntilThereIsRoom()
        {
            var stall = Stall(ore: 3, sellsPickaxes: false);
            var forge = Forge(1);
            for (int t = 1; t < Pickaxe.WorkTicks; t++) Crafting.Work(ref forge, ref stall, out _);
            Assert(Crafting.Work(ref forge, ref stall, out bool changed) == CraftStep.Holding && !changed, "A stall that doesn't trade pickaxes has no room: the forge holds the make.");
            Assert(forge.HeldOutput == 1 && forge.Remaining == 0 && forge.Made == 1 && forge.HasOrder, "Held, made, and the order still open.");
            Assert(Crafting.Work(ref forge, ref stall, out changed) == CraftStep.Holding && !changed && forge.HeldOutput == 1, "It keeps holding while there is no room.");

            stall = stall.WithGood(new StallGood(ItemType.ToolPickaxe, StallRole.Sells, 30, 0));
            Assert(Crafting.Work(ref forge, ref stall, out changed) == CraftStep.Made && changed, "With room, the held make goes into the stall.");
            Assert(stall.StockOf(ItemType.ToolPickaxe) == 1 && forge.HeldOutput == 0 && !forge.HasOrder, "Stocked, and the order closes.");
            TestLog.Log("  [Passed] A finished make waits in the station until the stall has room (D3).");
        }

        private void ValidateOrders()
        {
            var host = new Host(Stall(ore: 9));
            var service = new WorkshopService().Follow(Floor1Layout.Definition);
            int forge = service.IndexOf(StationKind.Forge), still = service.IndexOf(StationKind.Still);
            Assert(forge >= 0 && still >= 0 && service.IndexOf(Floor1Layout.Forge) == forge, "Floor 1 has a forge and a still, found by kind and by key.");

            Assert(!service.SetOrder(host, forge, ItemType.ToolPickaxe, 2), "An unbuilt station takes no order.");
            Assert(service.Build(host, forge, 0) == WorkshopRules.Check.Ok && service.Build(host, forge, 1) == WorkshopRules.Check.AlreadyBuilt && service[forge].Built, "Built once.");
            Assert(!service.SetOrder(host, forge, ItemType.IronOre, 1), "No order for an item with no recipe.");
            service.Build(host, still, 0);
            Assert(!service.SetOrder(host, still, ItemType.ToolPickaxe, 1), "No order for another station's recipe.");

            Assert(service.SetOrder(host, forge, ItemType.ToolPickaxe, 2) && service[forge].Remaining == 2, "The forge takes an order for two pickaxes.");
            service.Work(host, forge);
            Assert(host.Stalls[0].StockOf(ItemType.IronOre) == 6 && service[forge].InputsTaken, "Work on the host's stall takes the first make's ore.");
            Assert(service.SetOrder(host, forge, ItemType.ToolPickaxe, 5) && service[forge].Remaining == 5 && service[forge].InputsTaken && service[forge].Progress == 1, "The same item again changes how many, keeping the make in progress.");
            Assert(service.SetOrder(host, forge, ItemType.None, 0) && !service[forge].HasOrder && !service[forge].InputsTaken, "A cancel clears the order.");
            Assert(host.Stalls[0].StockOf(ItemType.IronOre) == 9, "A cancelled make's ore goes back to the stall.");
            TestLog.Log("  [Passed] Orders: built stations only, their own recipes; changing the count keeps the make; a cancel gives its inputs back.");
        }

        private void ValidateTheServiceFollowsTheMap()
        {
            var map = Floor1Layout.Definition;
            var service = new WorkshopService().Follow(map);
            Assert(service.Count == map.Stations.Count, "One state per station of the map.");
            for (int i = 0; i < service.Count; i++)
            {
                Assert(service[i].Kind == map.Stations[i].Kind && !service[i].Built && !service[i].HasOrder, $"Station {i} ({map.Stations[i].Key}): the map's kind, unbuilt, no order.");
                Assert(service.DefinitionAt(i).Key == map.Stations[i].Key, $"Station {i}: its definition, in the map's order.");
            }
            service.Build(new Host(Stall(ore: 0)), 0, 0);
            Assert(service.Follow(map)[0].Built, "Following the same map keeps the stations.");
            service.Reset();
            Assert(service.Count == 0, "A reset forgets every station.");
            Assert(!service.Follow(map)[0].Built && service.Count == map.Stations.Count, "Following the map again after a reset lays its stations out unbuilt (a respawn).");
            Assert(new WorkshopService().Follow(null).Count == 0, "No map, no stations.");
            TestLog.Log("  [Passed] The service follows the loaded map's stations, unbuilt, and forgets them on a reset.");
        }

        private void ValidateTheRuleAllocatesNothing()
        {
            Assert(Unsafe.SizeOf<StationState>() == 16, $"A station's saved record is 16 bytes ({Unsafe.SizeOf<StationState>()}): a change of layout changes the save format.");
            var stall = Stall(ore: 60000);
            var forge = Forge(ushort.MaxValue);
            Crafting.Work(ref forge, ref stall, out _); // warm
            long before = GC.GetAllocatedBytesForCurrentThread();
            int ticks = 8 * (Pickaxe.WorkTicks + 1);
            for (int t = 0; t < ticks; t++) Crafting.Work(ref forge, ref stall, out _);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert(allocated == 0 && forge.Made >= 8, $"{ticks:N0} ticks of work, {forge.Made} makes, allocate nothing ({allocated} bytes).");
            TestLog.Log("  [Passed] The rule allocates nothing; a station's record is 16 bytes.");
        }

        private sealed class Host : IWorkshopHost
        {
            public readonly List<TradeStallData> Stalls = new List<TradeStallData>(1);

            public Host(TradeStallData stall) => Stalls.Add(stall);

            public IReadOnlyList<TradeStallData> ActiveStalls => Stalls;
            public IEconomyLedger EconomyLedger => null;
            public Simpiens.Narrative.INarrativeChronicleService ChronicleService => null;
            public EconomyTuning Economy => EconomyTuning.Default;
            public int GetSectorForPosition(Vec2 position) => 0;
            public IGroupRegistry Groups => null;
            public Simpiens.Simulation.Guild.GuildService Guild => null;

            public bool TryGetCrawler(EntityId crawlerId, out Simpiens.Cognition.AgentState crawler)
            {
                crawler = null;
                return false;
            }

            public void UpdateStall(TradeStallData stall)
            {
                for (int i = 0; i < Stalls.Count; i++)
                {
                    if (Stalls[i].StallEntityId == stall.StallEntityId) Stalls[i] = stall;
                }
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception($"[CraftingValidator ASSERTION FAILED] {message}");
        }
    }
}
