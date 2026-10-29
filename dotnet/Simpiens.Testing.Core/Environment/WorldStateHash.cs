using System.Collections.Generic;
using System.Globalization;
using Simpiens.Cognition;
using Simpiens.Core.Session;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.World;
namespace Simpiens.Testing.Environment
{
    /// <summary>
    /// One sample of the authoritative world, hashed (Epic MAP Phase 5 determinism check). FNV-1a over plain data
    /// only, in id order: each agent's position (to 1/1000 unit), needs (to 0.01), gold, inventory, current intent
    /// and goal; stall stocks and treasuries; bounties; the charted-sector mask; ledger totals. No view state.
    /// Per-part hashes and per-agent descriptions name what differs when two samples disagree.
    /// </summary>
    public sealed class WorldStateSample
    {
        public long Tick;
        public ulong Total;
        public ulong Economy;
        public ulong Bounties;
        public ulong Map;
        public readonly List<string> AgentNames = new List<string>(16);
        public readonly List<ulong> AgentHashes = new List<ulong>(16);
        public readonly List<string> AgentLines = new List<string>(16);

        /// <summary>Where this sample first differs from <paramref name="other"/>, or null if identical.</summary>
        public string DescribeDifference(WorldStateSample other)
        {
            if (other == null) return "no sample to compare";
            if (Total == other.Total) return null;
            var parts = new List<string>(4);
            if (AgentNames.Count != other.AgentNames.Count) parts.Add($"agent count {AgentNames.Count} vs {other.AgentNames.Count}");
            for (int i = 0; i < AgentHashes.Count && i < other.AgentHashes.Count; i++)
            {
                if (AgentHashes[i] != other.AgentHashes[i])
                {
                    parts.Add($"{AgentNames[i]}: [{AgentLines[i]}] vs [{other.AgentLines[i]}]");
                    if (parts.Count >= 3) break;
                }
            }
            if (Economy != other.Economy) parts.Add("stalls or ledger");
            if (Bounties != other.Bounties) parts.Add("bounties");
            if (Map != other.Map) parts.Add("charted sectors");
            return string.Join("; ", parts);
        }
    }

    public static class WorldStateHash
    {
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        /// <param name="includeWork">False leaves out each agent's intent and goal: a load drops work in progress, so a
        /// world before a save and after its load compare equal without them (Milestone 1 save/load, D3).</param>
        public static WorldStateSample Capture(long tick, SessionHost host, ISimulationManager sim, ISectorRegistry sectors, bool includeWork = true)
        {
            var sample = new WorldStateSample { Tick = tick };

            // Crawlers in id order (stopped ones left out)
            var ordered = new List<int>(host != null ? host.Crawlers.Count : 0);
            if (host != null)
            {
                for (int i = 0; i < host.Crawlers.Count; i++)
                {
                    if (!host.Crawlers[i].IsDisposed) ordered.Add(i);
                }
                ordered.Sort((a, b) => host.CrawlerStates[a].AgentId.CompareTo(host.CrawlerStates[b].AgentId));
            }

            ulong total = Offset;
            Mix(ref total, (ulong)tick);
            Mix(ref total, host != null ? host.NextDrifterTick : -1L); // who comes in next (Epic 10 G1)
            for (int i = 0; i < ordered.Count; i++)
            {
                var s = host.CrawlerStates[ordered[i]];
                ulong h = Offset;
                Mix(ref h, s.AgentId.GetHashCode());
                Mix(ref h, Quantize(s.Position.X, 1000f));
                Mix(ref h, Quantize(s.Position.Y, 1000f));
                Mix(ref h, Quantize(s.Hunger, 100f));
                Mix(ref h, Quantize(s.Energy, 100f));
                Mix(ref h, Quantize(s.Social, 100f));
                Mix(ref h, (long)s.DepartsAtTick); // 0 for a resident; a drifter's departure (Epic 10 G1)
                var inv = s.Inventory;
                Mix(ref h, inv != null ? inv.Gold : -1);
                if (inv != null)
                {
                    Mix(ref h, inv.GetItemCount(ItemType.Ration));
                    Mix(ref h, inv.GetItemCount(ItemType.IronOre));
                    Mix(ref h, inv.GetItemCount(ItemType.MedicinalHerb));
                    Mix(ref h, inv.GetItemCount(ItemType.Glowstone)); // the rare salvage (Epic 10 G2)
                    // Equipment and its wear (G3): the protection slot as G3 hashed its one gear slot; the others only when filled
                    // (item system I4), so hashes recorded before the slots existed still match
                    var protection = inv.GetEquipped(Simpiens.Simulation.Items.EquipmentSlot.Protection);
                    Mix(ref h, protection.IsEmpty ? -1 : protection.Durability);
                    for (int slot = 1; slot < Simpiens.Cognition.Inventory.AgentInventory.EquipmentSlots; slot++)
                    {
                        var equipped = inv.GetEquipped((Simpiens.Simulation.Items.EquipmentSlot)slot);
                        if (equipped.IsEmpty) continue;
                        Mix(ref h, slot);
                        Mix(ref h, (int)equipped.Type);
                        Mix(ref h, equipped.Durability);
                    }
                }
                Mix(ref h, Quantize(s.Health, 100f)); // health (G2)
                string intent = !includeWork ? "-" : s.CurrentIntent != null ? s.CurrentIntent.GetType().Name : "none";
                string goal = !includeWork ? "-" : s.ActiveGoal != null ? s.ActiveGoal.Name : "none";
                Mix(ref h, intent);
                Mix(ref h, goal);

                sample.AgentNames.Add(host.CrawlerNames[ordered[i]]);
                sample.AgentHashes.Add(h);
                sample.AgentLines.Add(string.Format(CultureInfo.InvariantCulture,
                    "pos ({0:F3},{1:F3}) hunger {2:F2} energy {3:F2} gold {4} intent {5} goal {6}",
                    s.Position.X, s.Position.Y, s.Hunger, s.Energy, inv != null ? inv.Gold : -1, intent, goal));
                Mix(ref total, h);
            }

            // Stalls and the ledger
            ulong economy = Offset;
            if (sim != null)
            {
                var stalls = sim.ActiveStalls;
                for (int i = 0; i < stalls.Count; i++)
                {
                    var st = stalls[i];
                    Mix(ref economy, st.StallEntityId.GetHashCode());
                    Mix(ref economy, st.RationStock);
                    Mix(ref economy, st.OreStock);
                    Mix(ref economy, st.HerbStock);
                    Mix(ref economy, st.GlowstoneStock); // Epic 10 G2
                    Mix(ref economy, st.HazardGearStock); // Epic 10 G3
                    Mix(ref economy, st.TreasuryGold);
                }
                var ledger = sim.EconomyLedger;
                if (ledger != null)
                {
                    Mix(ref economy, ledger.TotalEscrowGold);
                    Mix(ref economy, ledger.TotalFaucetedGold);
                    Mix(ref economy, ledger.TotalSinkedGold);
                }
            }

            // Bounties (the manager keeps them sorted by id)
            ulong bounties = Offset;
            if (sim != null)
            {
                var list = sim.ActiveBounties;
                for (int i = 0; i < list.Count; i++)
                {
                    var b = list[i];
                    Mix(ref bounties, b.BountyId);
                    Mix(ref bounties, (int)b.Status);
                    Mix(ref bounties, b.ClaimantId.GetHashCode());
                }
            }

            ulong map = Offset;
            Mix(ref map, sectors != null ? sectors.ExploredMask : 0UL);

            Mix(ref total, economy);
            Mix(ref total, bounties);
            Mix(ref total, map);
            sample.Economy = economy;
            sample.Bounties = bounties;
            sample.Map = map;
            sample.Total = total;
            return sample;
        }

        private static long Quantize(float value, float scale) => (long)SimMath.Round(value * scale);

        private static void Mix(ref ulong h, ulong value)
        {
            for (int i = 0; i < 8; i++)
            {
                h ^= (value >> (i * 8)) & 0xFF;
                h *= Prime;
            }
        }

        private static void Mix(ref ulong h, long value) => Mix(ref h, unchecked((ulong)value));
        private static void Mix(ref ulong h, int value) => Mix(ref h, unchecked((ulong)(long)value));
        private static void Mix(ref ulong h, uint value) => Mix(ref h, (ulong)value);

        private static void Mix(ref ulong h, string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                h ^= value[i];
                h *= Prime;
            }
        }
    }
}
