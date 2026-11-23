using System;
using System.Collections.Generic;
using System.Linq;
using Simpiens.Cognition;
using Simpiens.Cognition.Memory;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Persistence;
using Simpiens.Simulation.Skills;
using Simpiens.Simulation.Workshops;

namespace Simpiens.Testing.Economy
{
    /// <summary>
    /// Inspiration (Epic 11, S8.2, D41–D42): the chance by grade, passion and grief; the roll on its own salt, the same every
    /// time and near its chance over many makes; grief from a friend's death in the maker's memory, two game days long; the
    /// names' table and its forms; and at a forge, a Journeyman's Masterwork make come out a masterpiece with its record, its
    /// maker's deed and the chronicle's line, one a game day at most, never an Apprentice's, and a held make's masterpiece
    /// shelved when the stall takes it. Runs as a test (ValidatorTests).
    /// </summary>
    public class InspirationValidator
    {
        private const ulong Seed = 424242;
        private static readonly EntityId Smith = new EntityId(11u, 0u, 0u, 1u);
        private static readonly EntityId Friend = new EntityId(12u, 0u, 0u, 2u);
        private static readonly EntityId StallId = new EntityId(90u, 0u, 0u, 9u);

        public void RunAllValidationTests()
        {
            TestLog.Log("[InspirationValidator] Beginning inspiration validation...");
            ValidateTheChance();
            ValidateTheRoll();
            ValidateGrief();
            ValidateTheNames();
            ValidateAtTheForge();
            TestLog.Log("<color=green>[InspirationValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private static void ValidateTheChance()
        {
            Assert(InspirationRules.ChanceFor(Grades.Fine, Passion.Burning, true) == 0f && InspirationRules.ChanceFor(Grades.Standard, Passion.None, false) == 0f, "Below Masterwork, never.");
            Assert(Near(InspirationRules.ChanceFor(Grades.Masterwork, Passion.None, false), 0.08f), "A Masterwork make: 8%.");
            Assert(Near(InspirationRules.ChanceFor(Grades.Masterwork, Passion.Keen, false), 0.12f) && Near(InspirationRules.ChanceFor(Grades.Masterwork, Passion.Burning, false), 0.16f),
                "A keen smith's 12%, a burning one's 16%.");
            Assert(Near(InspirationRules.ChanceFor(Grades.Masterwork, Passion.Burning, true), 0.32f), "Grief doubles it.");
            TestLog.Log("[PASS] The chance: 8% a Masterwork make, by passion and grief (D41).");
        }

        private static void ValidateTheRoll()
        {
            float chance = InspirationRules.ChanceFor(Grades.Masterwork, Passion.None, false);
            int inspired = 0;
            const int makes = 20000;
            for (uint t = 0; t < makes; t++)
            {
                bool first = InspirationRules.Roll(Seed, Smith, t, chance, ItemType.IronBlade, out byte adjective, out byte noun);
                bool again = InspirationRules.Roll(Seed, Smith, t, chance, ItemType.IronBlade, out byte adjective2, out byte noun2);
                Assert(first == again && adjective == adjective2 && noun == noun2, $"The roll at t{t} is the same every time.");
                if (!first) continue;
                inspired++;
                Assert(adjective < MasterpieceNames.Adjectives.Count && noun < MasterpieceNames.NounsFor(MasterpieceKind.Blade).Count, "Its words are the table's.");
            }
            float share = inspired / (float)makes;
            Assert(share > 0.07f && share < 0.09f, $"Over {makes} makes, near 8% inspired ({share:P1}).");
            Assert(!InspirationRules.Roll(Seed, Smith, 5u, 0f, ItemType.IronBlade, out _, out _), "At no chance, never.");
            TestLog.Log($"[PASS] The roll: the same every time, {share:P1} of {makes} makes inspired (D41).");
        }

        private static void ValidateGrief()
        {
            var smith = new AgentState(Smith);
            Assert(!InspirationRules.TryGrief(smith, 5000u, out _), "No death remembered, no grief.");
            smith.Memory.EventLedger.AddEvent(new MemoryEvent(MemoryEventType.Grieved, Friend, GridPoint.Zero, 1000u));
            uint day = (uint)DayClock.TicksPerDay;
            Assert(InspirationRules.TryGrief(smith, 1000u + day, out var fallen) && fallen == Friend, "A friend dead a day ago is grieved.");
            Assert(InspirationRules.TryGrief(smith, 1000u + 2 * day, out _), "Two game days is still grief.");
            Assert(!InspirationRules.TryGrief(smith, 1001u + 2 * day, out _), "Past two game days, it has passed.");
            var other = new EntityId(13u, 0u, 0u, 3u);
            smith.Memory.EventLedger.AddEvent(new MemoryEvent(MemoryEventType.AgentSpotted, other, GridPoint.Zero, 1500u));
            smith.Memory.EventLedger.AddEvent(new MemoryEvent(MemoryEventType.Grieved, other, GridPoint.Zero, 2000u));
            Assert(InspirationRules.TryGrief(smith, 2500u, out fallen) && fallen == other, "The newest death is the one grieved.");
            TestLog.Log("[PASS] Grief: a friend's death in the maker's memory, two game days long (D41).");
        }

        private static void ValidateTheNames()
        {
            void Words(IReadOnlyList<string> words, string what)
            {
                Assert(words.Count > 0, $"{what}: some words.");
                foreach (var word in words)
                {
                    Assert(word.Length > 0 && word.Length <= MasterpieceNames.MaxWordLength, $"{what}: '{word}' fits ({MasterpieceNames.MaxWordLength} at most).");
                    Assert(word.All(c => c < 256 && (char.IsLetter(c) || c == '\'')), $"{what}: '{word}' is Latin-1 letters.");
                }
                Assert(words.Distinct().Count() == words.Count, $"{what}: no word twice.");
            }
            Words(MasterpieceNames.Adjectives, "adjectives");
            Assert(MasterpieceNames.Adjectives.Count <= 32, "The adjectives fit the chronicle's packing (under 32).");
            foreach (MasterpieceKind kind in Enum.GetValues(typeof(MasterpieceKind)))
            {
                Words(MasterpieceNames.NounsFor(kind), $"{kind}'s nouns");
                Assert(MasterpieceNames.NounsFor(kind).Count <= 16, $"{kind}'s nouns fit the packing (under 16).");
            }
            Assert(MasterpieceNames.KindOf(ItemType.IronBlade) == MasterpieceKind.Blade && MasterpieceNames.KindOf(ItemType.Crossbow) == MasterpieceKind.Bow
                && MasterpieceNames.KindOf(ItemType.ToolPickaxe) == MasterpieceKind.Tool && MasterpieceNames.KindOf(ItemType.RingCoat) == MasterpieceKind.Coat
                && MasterpieceNames.KindOf(ItemType.IronHelm) == MasterpieceKind.Helm, "A blade, a bow, a tool, a coat and a helm, by their equipment.");
            // Every piece the stations make has a kind with nouns
            foreach (var recipe in RecipeBook.All)
            {
                if (!Grades.Graded(recipe.Output)) continue;
                Assert(MasterpieceNames.NounsFor(MasterpieceNames.KindOf(recipe.Output)).Count > 0, $"{recipe.Output} has nouns for its name.");
            }

            var plain = new MasterpieceRecord { Item = ItemType.IronBlade, Adjective = 0, Noun = 0 };
            Assert(MasterpieceNames.NameOf(plain, _ => "Ama") == "the Quiet Fang", "Plain: \"the {adjective} {noun}\".");
            var grief = new MasterpieceRecord { Item = ItemType.IronBlade, Form = MasterpieceForm.Grief, NamedFor = Friend, Adjective = 0, Noun = 3 };
            Assert(MasterpieceNames.NameOf(grief, id => id == Friend ? "Ama" : null) == "Ama's Oath", "Grief: the dead friend's name.");
            Assert(MasterpieceNames.NameOf(grief, _ => null) == "the Quiet Oath", "With the friend's name lost, it reads plain.");
            var packed = new MasterpieceRecord { Item = ItemType.RingCoat, Form = MasterpieceForm.Grief, Adjective = 17, Noun = 5 };
            MasterpieceNames.Unpack(MasterpieceNames.Pack(packed), out var item, out var form, out byte adjective, out byte noun);
            Assert(item == ItemType.RingCoat && form == MasterpieceForm.Grief && adjective == 17 && noun == 5 && MasterpieceNames.Pack(packed) < (1 << 24), "Packed for the chronicle and back, exact in its float.");
            TestLog.Log("[PASS] The names: the table, the forms, the packing (D42).");
        }

        private static void ValidateAtTheForge()
        {
            uint day = (uint)DayClock.TicksPerDay;
            float chance = InspirationRules.ChanceFor(Grades.Masterwork, Passion.None, false);
            uint Lucky(uint from) // the first tick from which the smith's roll comes up
            {
                for (uint t = from; ; t++) if (InspirationRules.Roll(Seed, Smith, t, chance, ItemType.IronBlade, out _, out _)) return t;
            }

            var (workshops, host, smith) = Forge(level: 8);
            Assert(WorkshopService.GradeOf(smith, StationKind.Forge) == Grades.Masterwork, "A Journeyman smith makes Masterwork.");
            uint first = Lucky(1000u);
            Finish(workshops, host, first);
            bool found = workshops.Masterpieces.TryGet(1, out var made);
            Assert(workshops.Masterpieces.Count == 1 && found, "The lucky make is a masterpiece.");
            Assert(made.Maker == Smith && made.Item == ItemType.IronBlade && made.Station == StationKind.Forge && made.StationIndex == 0 && made.Place == MasterpiecePlace.Shelf
                && made.MadeAt == first && made.Form == MasterpieceForm.Plain && made.OwnerCount == 0, "Its record: its maker, its forge and tick, on the shelf, nobody's yet.");
            Assert(host.Stalls[0].TryGetGood(ItemType.IronBlade, out var blades) && blades.Masterwork == 1 && blades.Stock == 1, "The piece stocked among the Masterwork pieces.");
            Assert(smith.Traits.Count(Deed.MasterpieceMade) == 1 && smith.Traits.Count(Deed.Make) == 1, "The maker's deed, beside the make.");
            var told = new List<ChronicleEvent>();
            host.Chronicle.GetAllRecentEvents(told);
            var line = told.Single(e => e.EventType == ChronicleEventType.MasterpieceMade);
            Assert(line.ActorAId == Smith && line.ActorBId == default, "Told: the smith, inspired.");
            var names = new AgentIdentityService();
            names.RegisterCustomName(Smith, "Tamsin");
            string words = new TemplateNarrativeFormatter(names).FormatEvent(line);
            Assert(words == $"Tamsin, inspired, made a masterpiece at the forge: {MasterpieceNames.NameOf(made, null)}.", $"The chronicle's line: {words}");

            Finish(workshops, host, Lucky(first + 1) < first + day ? Lucky(first + 1) : first + 1);
            Assert(workshops.Masterpieces.Count == 1, "Within the game day, no second one.");
            Finish(workshops, host, Lucky(first + day));
            Assert(workshops.Masterpieces.Count == 2, "A day on, inspired again.");

            // An Apprentice's makes are Fine: never inspired
            var (apprenticeShop, apprenticeHost, apprentice) = Forge(level: 4);
            for (uint t = 0; t < 40; t++) Finish(apprenticeShop, apprenticeHost, Lucky(1000u + t * day));
            Assert(apprenticeShop.Masterpieces.Count == 0 && apprentice.Traits.Count(Deed.Make) == 40, "An Apprentice's forty makes, at its lucky ticks: no masterpiece.");

            // Held while the stall has no room, shelved when it takes the make
            var (heldShop, heldHost, _) = Forge(level: 8);
            heldHost.Stalls[0] = heldHost.Stalls[0].WithStock(ItemType.IronBlade, ushort.MaxValue);
            uint lucky = Lucky(1000u);
            Finish(heldShop, heldHost, lucky);
            Assert(heldShop[0].HeldOutput == 1 && heldShop.Masterpieces.TryGet(1, out var held) && held.Place == MasterpiecePlace.Held, "A make the stall has no room for: its masterpiece held at the forge.");
            heldHost.Stalls[0] = heldHost.Stalls[0].WithStock(ItemType.IronBlade, 0);
            heldShop.Work(heldHost, 0, lucky + 1, Seed);
            Assert(heldShop[0].HeldOutput == 0 && heldShop.Masterpieces.TryGet(1, out var shelved) && shelved.Place == MasterpiecePlace.Shelf, "Stocked, it is on the shelf.");

            // Grief names the piece for the friend
            var (griefShop, griefHost, grieving) = Forge(level: 8);
            grieving.Memory.EventLedger.AddEvent(new MemoryEvent(MemoryEventType.Grieved, Friend, GridPoint.Zero, 900u));
            float griefChance = InspirationRules.ChanceFor(Grades.Masterwork, Passion.None, true);
            uint sad = 1000u;
            while (!InspirationRules.Roll(Seed, Smith, sad, griefChance, ItemType.IronBlade, out _, out _)) sad++;
            Finish(griefShop, griefHost, sad);
            Assert(griefShop.Masterpieces.TryGet(1, out var named) && named.Form == MasterpieceForm.Grief && named.NamedFor == Friend, "Made in grief: named for the friend.");
            TestLog.Log("[PASS] At the forge: a Journeyman's masterpiece, its record, deed and line; one a day; never an Apprentice's; held, then shelved; grief's name (D41).");
        }

        // One make finished at the forge, its last tick at the given one (a make's work ticks all pass at that tick)
        private static void Finish(WorkshopService workshops, Host host, uint tick)
        {
            uint made = workshops[0].Made;
            for (int i = 0; i < 10000 && workshops[0].Made == made; i++) workshops.Work(host, 0, tick, Seed);
            Assert(workshops[0].Made == made + 1, "A make finished.");
        }

        // A forge on Floor 1 staffed by a smith at the given level, ordered blades, on a stall with ore to spare
        private static (WorkshopService, Host, AgentState) Forge(int level)
        {
            var map = Simpiens.Core.Session.Layouts.Floor1Layout.Definition;
            var workshops = new WorkshopService().Follow(map);
            int forge = workshops.IndexOf(StationKind.Forge);
            Assert(forge == 0, "Floor 1's forge is its first station.");
            var stations = new StationState[workshops.Count];
            for (int i = 0; i < stations.Length; i++) stations[i] = StationState.Unbuilt(workshops[i].Kind);
            stations[forge] = new StationState { Kind = StationKind.Forge, Built = true, Order = ItemType.IronBlade, Remaining = 1000 };
            workshops.RestoreFrom(new WorkshopSave { Stations = stations, Artisans = new[] { new ArtisanContract(Smith, (byte)forge, 6, 0u) } });
            var smith = new AgentState(Smith);
            var skills = default(SkillSet);
            skills.Set(Skill.Smithing, level);
            smith.Skills = skills;
            var stall = new TradeStallData(StallId, Vec2.Zero, 1000u)
                .WithGood(new StallGood(ItemType.IronOre, StallRole.Buys, 4, 60000))
                .WithGood(new StallGood(ItemType.IronBlade, StallRole.Sells, 40, 0));
            return (workshops, new Host(stall, smith), smith);
        }

        private sealed class Host : IWorkshopHost
        {
            public readonly List<TradeStallData> Stalls = new List<TradeStallData>(1);
            public readonly NarrativeChronicleService Chronicle = new NarrativeChronicleService();
            private readonly AgentState _smith;

            public Host(TradeStallData stall, AgentState smith)
            {
                Stalls.Add(stall);
                _smith = smith;
            }

            public IReadOnlyList<TradeStallData> ActiveStalls => Stalls;
            public void Practise(AgentState agent, in SkillWork work, int experience, in PracticeSource source) { } // the level stays where the test set it
            public IEconomyLedger EconomyLedger => null;
            public INarrativeChronicleService ChronicleService => Chronicle;
            public EconomyTuning Economy => EconomyTuning.Default;
            public int GetSectorForPosition(Vec2 position) => 0;
            public IGroupRegistry Groups => null;
            public Simpiens.Simulation.Guild.GuildService Guild => null;

            public bool TryGetCrawler(EntityId crawlerId, out AgentState crawler)
            {
                crawler = crawlerId == _smith.AgentId ? _smith : null;
                return crawler != null;
            }

            public void UpdateStall(TradeStallData stall) => Stalls[0] = stall;
        }

        private static bool Near(float a, float b) => Math.Abs(a - b) < 0.0001f;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException($"[InspirationValidator] FAILED: {message}");
        }
    }
}
