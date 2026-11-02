namespace Simpiens.Simulation
{
    public partial class SimulationManager
    {
        /// <summary>
        /// Writes the simulation's state into a save (Milestone 1 save/load): the stalls, every bounty and the ledger;
        /// resource nodes, the regrowth queue, the fog and the session's tallies; groups; the chronicle. Records are
        /// sorted by id. Active intents, the bunk claims and contention that depend on them, and the snapshot are not
        /// saved. Main thread, between ticks.
        /// </summary>
        public void CaptureTo(Simpiens.Simulation.Persistence.SessionSave save)
        {
            if (save == null) throw new System.ArgumentNullException(nameof(save));

            save.Economy = _market.Capture();

            var resources = new Simpiens.Simulation.Persistence.ResourceRecord[_resources.Count];
            int n = 0;
            foreach (var kvp in _resources) resources[n++] = new Simpiens.Simulation.Persistence.ResourceRecord(kvp.Value);
            System.Array.Sort(resources, (a, b) => a.EntityId.CompareTo(b.EntityId));

            var supply = new Simpiens.Simulation.Persistence.SupplySave();
            _supplyService?.CaptureTo(supply);

            var harvested = new Simpiens.Simulation.Persistence.ItemCountRecord[_harvested.Count];
            n = 0;
            foreach (var kvp in _harvested) harvested[n++] = new Simpiens.Simulation.Persistence.ItemCountRecord(kvp.Key, kvp.Value);
            System.Array.Sort(harvested, (a, b) => ((byte)a.Item).CompareTo((byte)b.Item));

            save.World ??= new Simpiens.Simulation.Persistence.WorldSave();
            save.World.Resources = resources;
            save.World.Supply = supply;
            save.World.ExploredSectors = _sectors?.CaptureExplored();
            save.World.BunkSleeps = _lodging.CaptureSleeps();
            save.World.Harvested = harvested;

            var groups = new Simpiens.Simulation.Persistence.GroupSave();
            _groupRegistry?.CaptureTo(groups);
            save.Groups = groups;

            var chronicle = new Simpiens.Simulation.Persistence.ChronicleSave();
            _chronicleService?.CaptureTo(chronicle);
            save.Chronicle = chronicle;

            var guild = new Simpiens.Simulation.Persistence.GuildSave();
            Guild.CaptureTo(guild);
            save.Guild = guild;
        }

        /// <summary>
        /// Puts a save's simulation state back (a load), after the spawner has rebuilt the map and the crawlers: the
        /// stalls, every bounty and the ledger as saved (no escrow is locked again); resource nodes, hiding the views of
        /// dormant nodes and of nodes used up for good; the regrowth queue, the fog, the tallies, groups and the
        /// chronicle. Main thread.
        /// </summary>
        public void RestoreFrom(Simpiens.Simulation.Persistence.SessionSave save)
        {
            if (save == null) throw new System.ArgumentNullException(nameof(save));

            _market.RestoreFrom(save.Economy);

            var world = save.World;
            RestoreResources(world?.Resources);
            if (world?.Supply != null) _supplyService?.RestoreFrom(world.Supply);
            if (world?.ExploredSectors != null) _sectors?.RestoreExplored(world.ExploredSectors);

            _lodging.RestoreSleeps(world?.BunkSleeps);
            _harvested.Clear();
            if (world?.Harvested != null)
            {
                for (int i = 0; i < world.Harvested.Length; i++) _harvested[world.Harvested[i].Item] = world.Harvested[i].Count;
            }

            if (save.Groups != null) _groupRegistry?.RestoreFrom(save.Groups);
            Guild.RestoreFrom(save.Guild);
            if (save.Chronicle != null) _chronicleService?.RestoreFrom(save.Chronicle);
        }

        /// <summary>
        /// The map's spawn registered every node full. Put back each saved node's yield and regrowth, hide a dormant node's
        /// view until it regrows, and remove a node missing from the save: it was used up for good (as DepleteResource does).
        /// </summary>
        private void RestoreResources(Simpiens.Simulation.Persistence.ResourceRecord[] records)
        {
            _keysToRemove.Clear();
            foreach (var kvp in _resources) _keysToRemove.Add(kvp.Key);

            if (records != null)
            {
                for (int i = 0; i < records.Length; i++)
                {
                    var r = records[i];
                    _keysToRemove.Remove(r.EntityId);
                    if (!_resources.TryGetValue(r.EntityId, out var data))
                    {
                        data = new ResourceData { EntityId = r.EntityId };
                        _resources[r.EntityId] = data;
                    }
                    data.RemainingYield = r.RemainingYield;
                    data.MaxYield = r.MaxYield;
                    data.YieldItem = r.YieldItem;
                    data.RegrowTicks = r.RegrowTicks;
                    data.DepletedAtTick = r.DepletedAtTick;
                    data.IsDormant = r.IsDormant;
                    if (r.IsDormant) HideResourceNode(r.EntityId, dormant: true);
                }
            }

            for (int i = 0; i < _keysToRemove.Count; i++)
            {
                _resources.Remove(_keysToRemove[i]);
                HideResourceNode(_keysToRemove[i], dormant: false);
            }
            _keysToRemove.Clear();
        }

        private void HideResourceNode(EntityId resourceId, bool dormant)
        {
            var node = _worldRegistry?.GetEntity(resourceId);
            if (node == null) return;
            if (dormant) _dormantNodes[resourceId] = node;
            _worldRegistry.Unregister(node);
        }

        public void ResetState()
        {
            SessionEpoch++;

            // Decisions in flight belong to the old session: wait for them, release their snapshots, drop them
            Cognition.Reset();
            while (_intentQueue.TryDequeue(out _)) { }

            // Player commands queued before a respawn or a load belong to the old session
            if (_interventionService != null)
            {
                while (_interventionService.TryDequeueInfluence(out _)) { }
            }
            _activeIntents.Clear();
            _activeIntentOrder.Clear();
            _resources.Clear();
            _dormantNodes.Clear();
            _supplyService?.Reset();
            _economyLedger?.Reset();
            _anchors.Clear();
            _lodging.Reset();
            _harvested.Clear();
            _market.Reset();
            _keysToRemove.Clear();
            _hearthPawnsBuffer.Clear();
            _activeAnchorsBuffer.Clear();
            if (_groupRegistry != null)
            {
                _groupRegistry.Clear();
            }
            Guild.Reset();

            // Walls from the torn-down world (including injected chaos walls) must not outlive it
            _spatialPartition?.ResetStatic();
        }
    }
}
