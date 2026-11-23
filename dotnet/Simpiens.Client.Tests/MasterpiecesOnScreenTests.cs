using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Narrative;
using Simpiens.Presentation.Cutscenes;
using Simpiens.Presentation.Dashboard;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Skills;
using Simpiens.Simulation.Workshops;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// A masterpiece on screen (Epic 11, S8.5, D45): worn, the dossier's gear line names it in Legendary's colour, its story in
    /// the line's tip (its maker, the forge and the day, its wearer and owners, its kills); its maker's legend line says what
    /// it made; on the stall's shelf, its row's stock names it; and the moment of its making for the patron's people, in
    /// grief with the friend's name. The masterpieces are placed by hand: inspiration is the forge's.
    /// </summary>
    public class MasterpiecesOnScreenTests
    {
        [Fact]
        public void TheDossierAndTheStallNameIt()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            sim.Step(20);
            var sm = (SimulationManager)sim.Simulation;
            var residents = sim.Host.CrawlerStates.Where(s => !s.IsDrifter).ToList();
            var (maker, wearer) = (residents[0], residents[1]);
            ushort blade = sm.Masterpieces.Add(new MasterpieceRecord { Item = ItemType.IronBlade, Station = StationKind.Forge, Place = MasterpiecePlace.Shelf, Adjective = 0, Noun = 0, Maker = maker.AgentId, MadeAt = (uint)DayClock.TicksPerDay * 5 });
            var piece = sm.Masterpieces.PieceFor(ItemType.IronBlade, Grades.Masterwork, 40, out _);
            Assert.True(wearer.Inventory.TryEquip(piece));
            sm.Masterpieces.HandOut(blade, wearer.AgentId);
            sm.Masterpieces.CountKill(wearer.Inventory, named: false, default, 0);
            var traits = maker.Traits;
            traits.Add(Deed.MasterpieceMade);
            maker.Traits = traits;

            var model = new DashboardModel(DashboardContext.From(sim.Services));
            model.SelectCrawler(wearer.AgentId);
            model.Refresh();
            var row = model.Dossier.Equipment.Slots.Single(s => s.Masterpiece == blade);
            Assert.StartsWith("The Quiet Fang ", row.Short);
            Assert.Equal(ItemRarity.Legendary, row.Rarity);
            string story = model.Dossier.GearTip;
            Assert.Contains("The Quiet Fang, a masterpiece.", story);
            Assert.Contains($"Made by {maker.GivenName} at the forge on day 6.", story);
            Assert.Contains($"Worn by {wearer.GivenName}, its first owner.", story);
            Assert.Contains("A kill.", story);

            model.SelectCrawler(maker.AgentId);
            model.Refresh();
            Assert.Contains("made the Quiet Fang", model.Dossier.Legend);
            Assert.Contains("Made the Quiet Fang, inspired.", model.Dossier.LegendTip);
            Assert.Equal(string.Empty, model.Dossier.GearTip);

            // A second on the shelf: the stall's row names it
            sm.Masterpieces.Add(new MasterpieceRecord { Item = ItemType.IronBlade, Station = StationKind.Forge, Place = MasterpiecePlace.Shelf, Adjective = 1, Noun = 1, Maker = maker.AgentId, MadeAt = 9u });
            sm.UpdateStall(sm.ActiveStalls[0].WithStocked(ItemType.IronBlade, 1, Grades.Masterwork));
            var stall = model.Merchant;
            stall.Open();
            stall.Refresh();
            var blades = stall.Rows.Single(r => r.Item == ItemType.IronBlade);
            Assert.Contains($"masterpiece the Patient Edge by {maker.GivenName}", blades.Stock);
        }

        [Fact]
        public void TheMomentOfItsMaking()
        {
            var plain = new MasterpieceRecord { Item = ItemType.IronBlade };
            var inspired = SkillScenes.InspiredFor(new ChronicleEvent(ChronicleEventType.MasterpieceMade, EntityId.Generate(), default, 0, 1u, ChronicleIntensity.Dramatic, MasterpieceNames.Pack(plain)));
            Assert.NotNull(inspired);
            Assert.Equal(CutsceneKind.Dynamic, inspired.Kind);
            var grief = new MasterpieceRecord { Item = ItemType.IronBlade, Form = MasterpieceForm.Grief };
            var grieving = SkillScenes.InspiredFor(new ChronicleEvent(ChronicleEventType.MasterpieceMade, EntityId.Generate(), EntityId.Generate(), 0, 1u, ChronicleIntensity.Dramatic, MasterpieceNames.Pack(grief)));
            Assert.NotNull(grieving);
            Assert.Single(grieving.Actors);
        }
    }
}
