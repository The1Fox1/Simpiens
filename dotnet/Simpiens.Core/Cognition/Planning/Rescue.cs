using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Memory;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Spatial;

namespace Simpiens.Cognition.Planning
{
    /// <summary>
    /// Who comes for the fallen (Epic 10 Part 3, K5.3, Q3b), read off the main thread from the snapshot's copies
    /// (<see cref="FallenView"/>). A crawler collapsed in danger cries out; a crawler on its feet within earshot
    /// (<see cref="CryRadius"/>, walls notwithstanding) comes if it counts the fallen a friend (warmth
    /// <see cref="FriendWarmth"/> or more) or a party mate, and would itself go where the fallen lies
    /// (<see cref="Vitality.WouldEnterAt(in AgentContext, Vec2)"/>). Nobody else is carrying it. The nearest first, then the
    /// lowest id. With a medicine that stands the fallen up (a draught: its heal reaches the recovery line) it hands that
    /// over; otherwise it carries the fallen out. Allocates nothing.
    /// </summary>
    public static class Rescue
    {
        /// <summary>How far the fallen's cry carries (game units, through walls): about a room and the next.</summary>
        public const float CryRadius = 15f;

        /// <summary>The warmth that makes a friend (Q4's grief uses the same line).</summary>
        public const int FriendWarmth = 30;

        public static bool TryChooseFallen(in AgentContext context, out FallenView chosen, out ItemType medicine)
        {
            chosen = default;
            medicine = ItemType.None;
            var snapshot = context.Snapshot;
            if (snapshot == null || snapshot.ActiveFallenCount == 0 || context.IsDowned || context.StruckRecently) return false;
            var fallen = snapshot.Fallen;
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < fallen.Length; i++)
            {
                ref readonly var f = ref fallen[i];
                if (f.Id == context.AgentId || (f.CarriedBy != default && f.CarriedBy != context.AgentId)) continue;
                float distance = (f.Position - context.Position).SqrMagnitude;
                if (distance > CryRadius * CryRadius || distance > best) continue;
                if (distance == best && found && f.Id.CompareTo(chosen.Id) >= 0) continue;
                if (!IsFriend(context, f)) continue;
                if (!Vitality.WouldEnterAt(context, f.Position) && !AlreadyThere(context, f.Position)) continue;
                best = distance;
                chosen = f;
                found = true;
            }
            if (found) medicine = StandingMedicine(context);
            return found;
        }

        /// <summary>
        /// True when the rescuer already stands in the hazard sector where the fallen lies: a party mate working beside it needn't
        /// pass the lone crawler's judgement to cross the room it is in (D30).
        /// </summary>
        public static bool AlreadyThere(in AgentContext context, Vec2 position)
        {
            var map = context.Snapshot?.Map;
            if (map == null) return false;
            var here = map.FindSector(context.Position);
            return here.Hazard.IsHazardous && here.Id == map.FindSector(position).Id;
        }

        /// <summary>A friend (its warmth toward the fallen) or a party mate.</summary>
        public static bool IsFriend(in AgentContext context, in FallenView fallen)
        {
            if (fallen.PartyGroupId != 0 && fallen.PartyGroupId == context.PartyGroupId) return true;
            return context.Memory != null && context.Memory.GetAffinity(fallen.Id, context.CurrentTick).Warmth >= FriendWarmth;
        }

        /// <summary>The first medicine it carries (id order) whose heal alone reaches the recovery line from nothing; none if none.</summary>
        public static ItemType StandingMedicine(in AgentContext context)
        {
            if (context.Inventory == null) return ItemType.None;
            var medicines = ItemCatalog.InCategory(ItemCategory.Medicine);
            for (int i = 0; i < medicines.Count; i++)
            {
                if (!context.Inventory.HasItem(medicines[i])) continue;
                var definition = ItemCatalog.Get(medicines[i]);
                for (int e = 0; e < definition.ConsumeEffectCount; e++)
                {
                    var effect = definition.GetConsumeEffect(e);
                    if (effect.Stat == StatKind.Health && effect.Amount >= Vitality.RecoveredAt) return medicines[i];
                }
            }
            return ItemType.None;
        }

        /// <summary>A rescue remembered (main thread): aid given and received, and both warm to each other and trust the more.</summary>
        public static void Remember(EntityId rescuerId, AgentState rescuer, AgentState fallen, uint tick)
        {
            if (rescuer == null || fallen == null) return;
            var at = GridPoint.RoundToInt(fallen.Position);
            rescuer.Memory?.EventLedger?.AddEvent(new MemoryEvent(MemoryEventType.AidGiven, fallen.AgentId, at, tick));
            fallen.Memory?.EventLedger?.AddEvent(new MemoryEvent(MemoryEventType.AidReceived, rescuerId, at, tick));
            rescuer.Memory?.RecordInteraction(fallen.AgentId, deltaWarmth: 10, deltaTrust: 5, deltaFear: 0, deltaRespect: 5, tick);
            fallen.Memory?.RecordInteraction(rescuerId, deltaWarmth: 25, deltaTrust: 25, deltaFear: 0, deltaRespect: 15, tick);
        }
    }
}
