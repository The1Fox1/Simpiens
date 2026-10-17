using System;
using System.Collections.Generic;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;
using UnityEngine;
using VContainer;

namespace Simpiens.Entities
{
    /// <summary>A crawler the player could click: its id and where it is drawn.</summary>
    public readonly struct PickCandidate
    {
        public readonly EntityId Id;
        public readonly Vector2 Position;

        public PickCandidate(EntityId id, Vector2 position)
        {
            Id = id;
            Position = position;
        }
    }

    /// <summary>Hit-testing for clicks on the map (plain C#, headless-testable).</summary>
    public static class WorldPicker
    {
        /// <summary>
        /// The nearest candidate within <paramref name="radius"/> of <paramref name="point"/>; equal distances go to the
        /// lower id, so a click on a crowd always picks the same crawler.
        /// </summary>
        public static bool TryPickNearest(Vector2 point, IReadOnlyList<PickCandidate> candidates, float radius, out EntityId id)
        {
            id = default;
            if (candidates == null) return false;
            float best = radius * radius;
            bool found = false;
            for (int i = 0; i < candidates.Count; i++)
            {
                float d = (candidates[i].Position - point).sqrMagnitude;
                if (d > best) continue;
                if (found && Mathf.Approximately(d, best) && candidates[i].Id.CompareTo(id) > 0) continue;
                best = d;
                id = candidates[i].Id;
                found = true;
            }
            return found;
        }
    }

    /// <summary>
    /// What the player has selected on the map (Epic MAP Phase 4): a crawler (optionally followed by the camera) and a
    /// sector. View state shared by the camera and the dashboard; nothing here enters the simulation.
    /// </summary>
    public sealed class WorldSelection
    {
        /// <summary>A click this close to a crawler (world units) selects it.</summary>
        public const float PickRadius = 0.6f;

        private readonly IWorldRegistry _registry;
        private readonly ISectorRegistry _sectors;
        private readonly IAgentIdentityService _identity;
        private readonly List<PickCandidate> _candidates = new List<PickCandidate>(64);

        public EntityId SelectedCrawler { get; private set; }

        /// <summary>The selected sector id, or -1.</summary>
        public int SelectedSector { get; private set; } = -1;

        /// <summary>True while the camera follows the selected crawler.</summary>
        public bool Following { get; private set; }

        /// <summary>Raised after any change of crawler, sector or following.</summary>
        public event Action Changed;

        [Inject]
        public WorldSelection(IWorldRegistry registry = null, ISectorRegistry sectors = null, IAgentIdentityService identity = null)
        {
            _registry = registry;
            _sectors = sectors;
            _identity = identity;
        }

        public void SelectCrawler(EntityId id, bool follow)
        {
            if (SelectedCrawler == id && Following == follow) return;
            SelectedCrawler = id;
            Following = follow && id != default;
            Changed?.Invoke();
        }

        public void SelectSector(int sectorId)
        {
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
        /// A click on the map: the nearest crawler within <see cref="PickRadius"/> (where it is drawn) is selected and
        /// followed, along with its sector; otherwise the crawler is deselected and the sector under the point selected.
        /// </summary>
        public void ClickAt(Vector2 world)
        {
            _candidates.Clear();
            var nodes = _registry?.ActiveNodes;
            if (nodes != null)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    var node = nodes[i];
                    if (node != null && node.Type == EntityType.Pawn) _candidates.Add(new PickCandidate(node.Id, node.transform.position));
                }
            }

            if (WorldPicker.TryPickNearest(world, _candidates, PickRadius, out var id))
            {
                SelectCrawler(id, follow: true);
                if (TryGetCrawlerPosition(out var at)) SelectSectorAt(at);
                return;
            }
            ClearCrawler();
            SelectSectorAt(world);
        }

        private void SelectSectorAt(Vector2 world)
        {
            if (_sectors == null) return;
            int sectorId = _sectors.GetSectorAt(world).Id;
            if (sectorId != 0) SelectSector(sectorId);
        }

        /// <summary>Where the selected crawler is drawn. False (and the selection dropped) once it no longer exists.</summary>
        public bool TryGetCrawlerPosition(out Vector2 position)
        {
            position = default;
            if (SelectedCrawler == default) return false;
            var node = _registry?.GetNode(SelectedCrawler);
            if (node == null)
            {
                ClearCrawler();
                return false;
            }
            position = node.transform.position;
            return true;
        }

        public string CrawlerName()
        {
            if (SelectedCrawler == default) return string.Empty;
            if (_identity != null) return _identity.GetFriendlyName(SelectedCrawler);
            var node = _registry?.GetNode(SelectedCrawler);
            return node != null ? node.name : string.Empty;
        }
    }
}
