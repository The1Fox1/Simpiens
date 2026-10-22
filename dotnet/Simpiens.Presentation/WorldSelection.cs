using System;
using System.Collections.Generic;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;

namespace Simpiens.Presentation
{
    /// <summary>
    /// Hit-testing clicks on the map against what was drawn (review A1): a frame's draw list through its view, so a click
    /// picks what the player sees, in any projection.
    /// </summary>
    public static class WorldPicker
    {
        /// <summary>A click this close to a pickable item's centre (world units at the view's scale) still picks it: Unity's pick radius.</summary>
        public const float Reach = 0.6f;

        /// <summary>
        /// The pickable item under the screen point: the topmost one whose quad holds it; otherwise the one whose centre
        /// is nearest within <see cref="Reach"/>, equal distances going to the one on top.
        /// </summary>
        public static bool TryPick(DrawList items, in View view, Vec2 screen, out EntityId id)
        {
            id = default;
            if (items == null || view.Projection == null) return false;
            float reach = Reach * view.PixelsPerUnit;
            float best = reach * reach;
            int nearest = -1;
            for (int i = items.Count - 1; i >= 0; i--) // topmost first
            {
                ref readonly var item = ref items[i];
                if (item.Pick < 0) continue;
                var quad = ScreenQuad.Of(item, view);
                if (screen.X >= quad.Left && screen.X < quad.Left + quad.Width && screen.Y >= quad.Top && screen.Y < quad.Top + quad.Height)
                {
                    id = items.PickedEntity(item.Pick);
                    return true;
                }
                float d = (quad.Centre - screen).SqrMagnitude;
                if (d < best || (nearest < 0 && d <= best))
                {
                    best = d;
                    nearest = i;
                }
            }
            if (nearest < 0) return false;
            id = items.PickedEntity(items[nearest].Pick);
            return true;
        }
    }

    /// <summary>
    /// What the player has selected on the map (Epic CLIENT M1.5), a port of Unity's <c>WorldSelection</c>: a crawler
    /// (optionally followed by the camera) and a sector. View state shared by the camera and, from M2, the dashboard;
    /// nothing here enters the simulation. It finds the crawler in the view's frames (review A3), where it is drawn.
    /// </summary>
    public sealed class WorldSelection
    {
        private readonly WorldViewFrames _frames;
        private readonly ISectorRegistry _sectors;
        private readonly IAgentIdentityService _identity;
        private uint _epoch; // the world the selection was made in

        /// <param name="frames">Where crawlers are drawn.</param>
        /// <param name="sectors">The map's sectors, for the sector under a click.</param>
        /// <param name="identity">Crawlers' names.</param>
        public WorldSelection(WorldViewFrames frames = null, ISectorRegistry sectors = null, IAgentIdentityService identity = null)
        {
            _frames = frames;
            _sectors = sectors;
            _identity = identity;
            _epoch = frames != null ? frames.Epoch : 0;
        }

        public EntityId SelectedCrawler { get; private set; }

        /// <summary>The selected sector id, or -1.</summary>
        public int SelectedSector { get; private set; } = -1;

        /// <summary>True while the camera follows the selected crawler.</summary>
        public bool Following { get; private set; }

        /// <summary>Raised after any change of crawler, sector or following.</summary>
        public event Action Changed;

        public void SelectCrawler(EntityId id, bool follow)
        {
            DropOtherWorld();
            if (SelectedCrawler == id && Following == follow) return;
            SelectedCrawler = id;
            Following = follow && id != default;
            Changed?.Invoke();
        }

        public void SelectSector(int sectorId)
        {
            DropOtherWorld();
            if (SelectedSector == sectorId) return;
            SelectedSector = sectorId;
            Changed?.Invoke();
        }

        public void StopFollowing()
        {
            if (!Following) return;
            Following = false;
            Changed?.Invoke();
        }

        public void ClearCrawler() => SelectCrawler(default, false);

        /// <summary>Drops everything (the world was rebuilt; the selected crawler no longer exists).</summary>
        public void Clear()
        {
            if (SelectedCrawler == default && SelectedSector < 0 && !Following) return;
            SelectedCrawler = default;
            SelectedSector = -1;
            Following = false;
            Changed?.Invoke();
        }

        /// <summary>
        /// A click on the map at the floor point <paramref name="floor"/>, on the crawler <paramref name="picked"/> drawn
        /// there (<see cref="WorldPicker"/>), if any. The crawler is selected and followed, along with its sector;
        /// without one the crawler is deselected and the sector under the point selected.
        /// </summary>
        public void ClickAt(Vec2 floor, EntityId picked = default)
        {
            if (picked != default)
            {
                SelectCrawler(picked, follow: true);
                if (TryGetCrawlerPosition(out var at)) SelectSectorAt(at);
                return;
            }
            ClearCrawler();
            SelectSectorAt(floor);
        }

        private void SelectSectorAt(Vec2 world)
        {
            if (_sectors == null) return;
            int sectorId = _sectors.GetSectorAt(world).Id;
            if (sectorId != 0) SelectSector(sectorId);
        }

        /// <summary>
        /// Where the selected crawler is drawn; false when none is selected, it isn't in the world, or the world was
        /// rebuilt since it was selected (its id may now name another crawler). See <see cref="Update"/>.
        /// </summary>
        public bool TryGetCrawlerPosition(out Vec2 position)
        {
            position = default;
            return SelectedCrawler != default && _frames != null && _frames.Epoch == _epoch && _frames.TryGetDrawnPosition(SelectedCrawler, out position);
        }

        /// <summary>
        /// Brings the selection up to date with the frames, once per update: a rebuilt world (a new epoch: entity ids are
        /// reissued) drops everything, and a crawler that left the world is dropped.
        /// </summary>
        public void Update()
        {
            DropOtherWorld();
            if (SelectedCrawler == default || _frames == null) return;
            if (!_frames.Current.TryGetIndex(SelectedCrawler, out _)) ClearCrawler();
        }

        /// <summary>The frames show another world than the one the selection was made in: drop it.</summary>
        private void DropOtherWorld()
        {
            if (_frames == null || _frames.Epoch == _epoch) return;
            _epoch = _frames.Epoch;
            Clear();
        }

        public string CrawlerName()
        {
            if (SelectedCrawler == default || _identity == null) return string.Empty;
            return _identity.GetFriendlyName(SelectedCrawler);
        }
    }
}
