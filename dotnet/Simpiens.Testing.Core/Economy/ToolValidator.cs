using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Planning.Handlers;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Spatial;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Economy
{
    /// <summary>
    /// Tools (Epic 10 Part 2, W1.4a): a pickaxe in the tool slot mines faster (D36) and wears by the unit mined until it
    /// breaks (D37); herbs are picked no faster, and wear nothing. Mechanism tests: the pickaxe is given by hand. Runs as a
    /// test (ValidatorTests), never at boot.
    /// </summary>
    public class ToolValidator
    {
        private static uint _guidSeed = 0x7001;
        private static EntityId NextGuid() => new EntityId(_guidSeed++, 0x7001, 0x3001, 0xC0DE);

        public void RunAllValidationTests()
        {
            TestLog.Log("[ToolValidator] Beginning tool validation...");

            ValidateMiningSpeedIsThePickaxesForMineralsOnly();
            ValidateAPickaxeMinesOreFasterAndHerbsNoFaster();
            ValidateAPickaxeWearsByTheUnitAndBreaks();

            TestLog.Log("<color=green>[ToolValidator] ALL 3 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private static ItemInstance Pickaxe(ushort durability)
            => new ItemInstance(1, ItemType.ToolPickaxe, 1, 30, durability: durability);

        private void ValidateMiningSpeedIsThePickaxesForMineralsOnly()
        {
            var registry = new WorldRegistry();
            var sim = new SimulationManager(registry, null);
            var miner = HeadlessEntities.Crawler(registry, sim, null, Vec2.Zero, id: NextGuid(), name: "[Test_Miner]").Agent;
            Assert(HarvestResourceIntentHandler.MiningSpeed(miner, ItemType.IronOre) == 1f, "Bare-handed, ore is mined at the usual pace.");
            miner.Inventory.TryEquip(Pickaxe(100));
            Assert(SimMath.Approximately(HarvestResourceIntentHandler.MiningSpeed(miner, ItemType.IronOre), 1.5f), "With a pickaxe, ore is mined 1.5 times as fast (D36).");
            Assert(SimMath.Approximately(HarvestResourceIntentHandler.MiningSpeed(miner, ItemType.Glowstone), 1.5f), "Glowstone is a mineral too.");
            Assert(HarvestResourceIntentHandler.MiningSpeed(miner, ItemType.MedicinalHerb) == 1f, "A pickaxe doesn't pick herbs any faster.");
            Assert(HarvestResourceIntentHandler.MiningSpeed(null, ItemType.IronOre) == 1f, "Nobody: the usual pace.");
            TestLog.Log("[PASS] Test 1: mining speed is the pickaxe's, for minerals only.");
        }

        private void ValidateAPickaxeMinesOreFasterAndHerbsNoFaster()
        {
            int Ticks(ItemType item, bool withPickaxe)
            {
                var registry = new WorldRegistry();
                var sim = new SimulationManager(registry, null);
                var nodeId = CreateResourceNode(registry, sim, Vec2.Zero, item, yield: 5);
                var crawler = HeadlessEntities.Crawler(registry, sim, null, new Vec2(0.5f, 0f), id: NextGuid(), name: "[Test_Miner]");
                if (withPickaxe) crawler.Agent.Inventory.TryEquip(Pickaxe(100));
                var state = Active(crawler.Node, new HarvestResourceIntent(crawler.Node.Id, nodeId, null));
                int ticks = 0;
                var result = IntentResult.InProgress;
                while (result == IntentResult.InProgress && ticks < HeadlessIntents.MaxTicks)
                {
                    result = sim.ExecuteIntent(crawler.Node, state);
                    ticks++;
                }
                Assert(result == IntentResult.Success && crawler.Agent.Inventory.GetItemCount(item) == 1, $"{item}: one unit harvested ({result}).");
                return ticks;
            }

            int bare = Ticks(ItemType.IronOre, withPickaxe: false), tooled = Ticks(ItemType.IronOre, withPickaxe: true);
            Assert(tooled < bare && System.Math.Abs(tooled * 1.5f - bare) <= 2f, $"A unit of ore takes {bare} ticks bare-handed and {tooled} with a pickaxe: 1.5 times as fast.");
            int herbs = Ticks(ItemType.MedicinalHerb, withPickaxe: false), herbsTooled = Ticks(ItemType.MedicinalHerb, withPickaxe: true);
            Assert(herbs == herbsTooled, $"Herbs take {herbs} ticks either way ({herbsTooled} with a pickaxe).");
            TestLog.Log($"[PASS] Test 2: a unit of ore takes {bare} ticks bare-handed, {tooled} with a pickaxe; herbs {herbs} either way.");
        }

        private void ValidateAPickaxeWearsByTheUnitAndBreaks()
        {
            var registry = new WorldRegistry();
            var chronicle = new NarrativeChronicleService();
            var sim = new SimulationManager(registry, null, chronicleService: chronicle);
            var oreId = CreateResourceNode(registry, sim, Vec2.Zero, ItemType.IronOre, yield: 10);
            var herbId = CreateResourceNode(registry, sim, new Vec2(0f, 0.6f), ItemType.MedicinalHerb, yield: 10);
            var crawler = HeadlessEntities.Crawler(registry, sim, null, new Vec2(0.5f, 0f), id: NextGuid(), name: "[Test_Miner]");
            var miner = crawler.Agent;
            miner.Inventory.TryEquip(Pickaxe(2));

            var herb = Active(crawler.Node, new HarvestResourceIntent(crawler.Node.Id, herbId, null));
            Assert(sim.RunToCompletion(crawler.Node, herb) == IntentResult.Success, "A herb is picked.");
            Assert(miner.Inventory.GetEquipped(EquipmentSlot.Tool).Durability == 2, "Picking a herb doesn't wear the pickaxe.");

            var ore = Active(crawler.Node, new HarvestResourceIntent(crawler.Node.Id, oreId, null));
            Assert(sim.RunToCompletion(crawler.Node, ore) == IntentResult.Success, "A unit of ore is mined.");
            Assert(miner.Inventory.GetEquipped(EquipmentSlot.Tool).Durability == 1, "A unit mined wears the pickaxe by one (D37).");
            Assert(miner.Activity.ToolMinedUnits == 1 && miner.Activity.ToolsWornOut == 0, "Counted: a unit mined with it.");

            ore = Active(crawler.Node, new HarvestResourceIntent(crawler.Node.Id, oreId, null));
            Assert(sim.RunToCompletion(crawler.Node, ore) == IntentResult.Success, "Another unit of ore is mined.");
            Assert(miner.Inventory.GetEquipped(EquipmentSlot.Tool).IsEmpty, "Worn to its end, the pickaxe breaks and is gone.");
            Assert(miner.Activity.ToolsWornOut == 1, "Counted: one tool broken.");
            var events = new List<ChronicleEvent>(8);
            chronicle.GetAllRecentEvents(events, 8);
            var told = events.Find(e => e.EventType == ChronicleEventType.ToolWornOut && e.ActorAId == miner.AgentId);
            Assert(told.EventType == ChronicleEventType.ToolWornOut && (ItemType)(byte)told.ExtraData == ItemType.ToolPickaxe, "The chronicle tells of the broken pickaxe.");
            Assert(new TemplateNarrativeFormatter().FormatEvent(told).Contains("pickaxe broke"), "In words: its pickaxe broke.");
            TestLog.Log("[PASS] Test 3: a pickaxe wears by the unit of ore mined, not by herbs, and breaks at its end, told in the chronicle.");
        }

        private static ActiveIntentState Active(WorldEntity node, AgentIntent intent)
            => new ActiveIntentState { Intent = intent, CurrentWaypointIndex = 0, LastSampledPosition = node.Position };

        private static EntityId CreateResourceNode(WorldRegistry registry, SimulationManager sim, Vec2 pos, ItemType kind, int yield)
        {
            var id = NextGuid();
            HeadlessEntities.Prop(registry, EntityType.Resource, pos, id: id, resourceKind: kind, name: $"[Test_{kind}Node]");
            sim.RegisterResource(new ResourceData { EntityId = id, RemainingYield = yield, MaxYield = yield, YieldItem = kind, RegrowTicks = 0 });
            return id;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new System.Exception($"[ToolValidator ASSERTION FAILED] {message}");
        }
    }
}
