using System.Linq;
using Simpiens.Cognition.Social;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Narrative;
using Simpiens.Presentation;
using Simpiens.Presentation.Cutscenes;
using Simpiens.Presentation.Dashboard;
using Simpiens.Simulation;
using Simpiens.Simulation.Combat;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Skills;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// A named creature on screen (Epic 11, S7.7, D37): its mark over it and its name in amber on the map, a frame carrying its
    /// rank; the inspector naming it, whom it downed and its foe; the bounty board offering it as a hunt's quarry beside its
    /// lair and its card naming it; the dossier saying whom a Vengeful crawler hates; and the moments for the patron's people,
    /// the vow and the avenger's word. The creature is named by its own blow, through the simulation's landing, on a resident
    /// placed by hand.
    /// </summary>
    public class NamedCreaturesOnScreenTests
    {
        [Fact]
        public void TheDashboardNamesItAndOffersItAsAQuarry()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            sim.Step(20);
            int warren = ChartTheWarren(sim);
            var rat = sim.Simulation.Creatures.Creatures.First(c => c.LairIndex == warren && !c.Definition.IsBoss);
            var residents = sim.Host.CrawlerStates.Where(s => !s.IsDrifter).ToList();
            var (ama, bram) = (residents[0], residents[1]);
            bram.Memory.SetAffinity(ama.AgentId, new AffinityRecord(70, 50, 0, 0, 5, 0)); // Ama's friend
            ((ICreatureWorld)sim.Simulation).Strike(sim.Registry.GetEntity(ama.AgentId), rat.Id, new SwingOutcome(true, 1f, 1000f), (uint)sim.Clock.CurrentTick);
            bram.Traits = Hated(bram.Traits); // as the simulation left it, the trait's news told
            sim.Step(1);
            string name = $"{ama.GivenName}'s Bane";
            Assert.Equal(name, sim.Identity.GetFriendlyName(rat.Id));
            rat.Foe = bram.AgentId;

            var model = new DashboardModel(DashboardContext.From(sim.Services));
            // The inspector: its name, its kind and title, whom it downed, its foe
            model.Inspector.Refresh(default, false, rat.Id);
            Assert.Equal(name, model.Inspector.Name);
            Assert.Equal($"A burrow rat, a bane of Overgrowth. It downed {ama.GivenName}. Its foe: {bram.GivenName}.", model.Inspector.MoodLine);

            // The dossier: whom Bram hates
            model.SelectCrawler(bram.AgentId);
            model.Refresh();
            Assert.Contains("Vengeful", model.Dossier.Legend);
            Assert.Contains($"Vengeful: against {name}, it braves its foe's lair with a quarter less to spare", model.Dossier.LegendTip);

            // The bounty board: the rat as a hunt's quarry beside its lair, posted, its card naming it
            var board = model.Bounties;
            board.Open();
            board.SetType(BountyType.Hunt);
            board.Refresh();
            string choice = $"{name}, of Overgrowth";
            Assert.Contains(choice, board.SectorChoiceLabels);
            Assert.Contains("The Matriarch's brood of Overgrowth", board.SectorChoiceLabels);
            board.SelectedSectorIndex = board.SectorChoiceLabels.ToList().IndexOf(choice);
            board.Reward = 60;
            board.Post();
            sim.Step(1);
            var hunt = Assert.Single(sim.Simulation.Hunts);
            Assert.Equal(rat.Id, hunt.Quarry);
            board.Refresh();
            Assert.Contains($"Hunt {name} of Overgrowth · 0 of 3 hunters", board.Contracts.Single(r => r.BountyId == hunt.BountyId).Details);
        }

        [Fact]
        public void TheMapMarksItAndNamesItInAmber()
        {
            var game = Offscreen.Start(new ClientOptions { TicksPerUpdate = 1 }, frames: 1);
            var session = game.Session.Simulation;
            var sim = (SimulationManager)session.Simulation;
            session.Sectors.SetExplored(Floor1Layout.OvergrowthSectorId, true);
            int warren = -1;
            for (int i = 0; i < sim.Creatures.Lairs.Count; i++) if (sim.Creatures.Lairs[i].Definition.Key == Floor1Layout.RatWarren) warren = i;
            var rat = sim.Creatures.Creatures.First(c => c.LairIndex == warren && !c.Definition.IsBoss);
            var ama = session.Host.CrawlerStates.First(s => !s.IsDrifter);
            ((ICreatureWorld)sim).Strike(session.Registry.GetEntity(ama.AgentId), rat.Id, new SwingOutcome(true, 1f, 1000f), (uint)session.Clock.CurrentTick);
            game.World.Rig.ZoomAt(rat.Position, 0.3f); // the warren in view: a draw keeps only what lands in it
            game.World.Rig.Pan(rat.Position - game.World.Rig.Position);
            for (int i = 0; i < 60; i++) game.RunOneFrame(); // ticks for the name and the frame, and the fog's fade
            string name = $"{ama.GivenName}'s Bane";

            Assert.True(game.Session.Frames.Current.TryGetIndex(rat.Id, out int index) && game.Session.Frames.Current[index].CreatureRank == 1, "The frame carries its rank.");
            var catalog = game.World.Mode.Catalog;
            var mark = catalog.Piece(catalog.Id(Simpiens.Presentation.Art.ArtKeys.BadgeNamed), 0f).Sprite;
            var items = Enumerable.Range(0, game.World.Scene.Items.Count).Select(i => game.World.Scene.Items[i]).ToList();
            Assert.Contains(items, i => i.SpriteIndex == mark);
            var label = Assert.Single(game.World.Scene.Labels, l => l.Text == name);
            Assert.Equal(LabelKind.Name, label.Kind);
            Assert.True(label.Color.R > label.Color.B + 100, "In amber.");

            // Selected, the selection names it, once
            game.Session.Selection.SelectCreature(rat.Id, follow: false);
            game.RunOneFrame();
            Assert.Single(game.World.Scene.Labels, l => l.Text == name);
        }

        [Fact]
        public void TheVowAndTheAvengersWord()
        {
            var vow = SkillScenes.VowFor(new ChronicleEvent(ChronicleEventType.TraitEarned, EntityId.Generate(), EntityId.Generate(), 0, 1u, ChronicleIntensity.Notable,
                (byte)EarnedTraitTable.All.First(t => t.Act == TraitAct.FriendFelled).Id));
            Assert.NotNull(vow);
            Assert.Equal(CutsceneKind.Dynamic, vow.Kind);
            Assert.Null(SkillScenes.VowFor(new ChronicleEvent(ChronicleEventType.TraitEarned, EntityId.Generate(), default, 0, 1u, ChronicleIntensity.Notable,
                (byte)EarnedTraitTable.All.First(t => t.Act == TraitAct.None).Id))); // a deed's trait: no vow
            var avenged = SkillScenes.AvengedFor(new ChronicleEvent(ChronicleEventType.NamedCreatureSlain, EntityId.Generate(), EntityId.Generate(), 0, 1u, ChronicleIntensity.Crisis,
                (byte)CreatureKind.StoneBeetle + 256));
            Assert.NotNull(avenged);
            Assert.Single(avenged.Actors);
        }

        // A grudge as the simulation's own act leaves it, its news already told (the trait's news would otherwise wait for a runtime)
        private static CrawlerTraits Hated(CrawlerTraits traits)
        {
            traits.ClearNews(LegendNews.Trait);
            return traits;
        }

        // By the crawlers' own rule: the Overgrowth charted, and a resident held within sight of a rat until the warren is charted
        private static int ChartTheWarren(LocalSimulation sim)
        {
            sim.Sectors.SetExplored(Floor1Layout.OvergrowthSectorId, true);
            int warren = -1;
            var lairs = sim.Simulation.Creatures.Lairs;
            for (int i = 0; i < lairs.Count; i++) if (lairs[i].Definition.Key == Floor1Layout.RatWarren) warren = i;
            var watcher = sim.Host.CrawlerStates.Last(s => !s.IsDrifter);
            for (int t = 0; t < 200 && !sim.Simulation.Creatures.IsCharted(warren); t++)
            {
                watcher.Position = new Vec2(20f, 14f);
                sim.Step(1);
            }
            Assert.True(sim.Simulation.Creatures.IsCharted(warren));
            return warren;
        }
    }
}
