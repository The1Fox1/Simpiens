using System.Text;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Workshops;
using static System.FormattableString;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>
    /// A masterpiece in words (Epic 11, S8.5, D45): its name as a piece's line shows it ("The Quiet Fang"), its story for its
    /// tip ("The Quiet Fang, a masterpiece. Made by Tamsin at the forge on day 6. Worn by Bram, its second owner. 4 kills,
    /// Ama's Bane among them."), what a maker made, and what waits on the stall's shelf. Read from the workshops' records and
    /// the session's names, the departed's too; where a piece is now is found (a crawler wearing or carrying it, the
    /// shelf, its station, the ground), or it is lost.
    /// </summary>
    public static class MasterpieceTexts
    {
        /// <summary>The outpost's masterpieces, or none before a session.</summary>
        public static MasterpieceService Of(DashboardContext context) => context?.Simulation?.Workshops?.Masterpieces;

        /// <summary>A masterpiece's name as a line shows it, its first letter raised: "The Quiet Fang", "Ama's Oath"; empty for none.</summary>
        public static string NameOf(DashboardContext context, ushort id)
        {
            var masterpieces = Of(context);
            if (masterpieces == null || !masterpieces.TryGet(id, out var record)) return string.Empty;
            return Capital(MasterpieceNames.NameOf(record, context.GivenNameOf));
        }

        /// <summary>A masterpiece's story, a sentence a line, for its tip; empty for none.</summary>
        public static string StoryOf(DashboardContext context, ushort id)
        {
            var masterpieces = Of(context);
            if (masterpieces == null || !masterpieces.TryGet(id, out var record)) return string.Empty;
            var story = new StringBuilder();
            story.Append(Capital(MasterpieceNames.NameOf(record, context.GivenNameOf))).Append(", a masterpiece.");
            string at = WorkshopRules.StationName(record.Station);
            story.Append('\n').Append(Invariant($"Made by {context.GivenNameOf(record.Maker)} at the {at} on day {1 + record.MadeAt / DayClock.TicksPerDay}"));
            if (record.Form == MasterpieceForm.Grief && record.NamedFor != default) story.Append(", for ").Append(context.GivenNameOf(record.NamedFor));
            story.Append('.');
            story.Append('\n').Append(WhereIs(context, record));
            if (record.Kills > 0)
            {
                story.Append('\n').Append(record.Kills == 1 ? "A kill" : Invariant($"{record.Kills} kills"));
                if (record.NamedSlain > 0)
                {
                    string named = Simpiens.Simulation.Creatures.CreatureTitleTable.NameOf(context.GivenNameOf(record.SlainVictim), record.SlainRank);
                    story.Append(record.Kills == 1 ? ": " : ", ").Append(named).Append(record.Kills == 1 ? "." : " among them.");
                }
                else story.Append('.');
            }
            return story.ToString();
        }

        /// <summary>What <paramref name="maker"/> made, for its legend line: "made the Quiet Fang", "made the Quiet Fang and Ama's Oath"; empty for nothing.</summary>
        public static string MadeBy(DashboardContext context, EntityId maker)
        {
            var masterpieces = Of(context);
            if (masterpieces == null || maker == default) return string.Empty;
            var names = new StringBuilder();
            int found = 0;
            for (int i = 0; i < masterpieces.Count; i++)
            {
                var record = masterpieces[i];
                if (record.Maker != maker) continue;
                found++;
                if (found > 2) continue; // the first two by name, the rest counted
                if (found == 2) names.Append(" and ");
                names.Append(MasterpieceNames.NameOf(record, context.GivenNameOf));
            }
            if (found == 0) return string.Empty;
            return found <= 2 ? "made " + names : Invariant($"made {names} and {found - 2} more");
        }

        /// <summary>
        /// The masterpieces of <paramref name="item"/> waiting on the stall's shelf, for its row's stock words (the next sale or
        /// gift hands the first out): "masterpiece the Quiet Fang by Tamsin", "2 masterpieces, the Quiet Fang first"; empty for none.
        /// </summary>
        public static string OnShelf(DashboardContext context, ItemType item)
        {
            var masterpieces = Of(context);
            if (masterpieces == null) return string.Empty;
            int count = 0;
            string first = string.Empty, maker = string.Empty;
            for (int i = 0; i < masterpieces.Count; i++)
            {
                var record = masterpieces[i];
                if (record.Place != MasterpiecePlace.Shelf || record.Item != item) continue;
                if (count++ > 0) continue;
                first = MasterpieceNames.NameOf(record, context.GivenNameOf);
                maker = context.GivenNameOf(record.Maker);
            }
            if (count == 0) return string.Empty;
            return count == 1 ? $"masterpiece {first} by {maker}" : Invariant($"{count} masterpieces, {first} first");
        }

        // Where it is now: found, not tracked (D40)
        private static string WhereIs(DashboardContext context, in MasterpieceRecord record)
        {
            if (record.Place == MasterpiecePlace.Shelf) return "On the stall's shelf.";
            if (record.Place == MasterpiecePlace.Held) return Invariant($"Waiting at the {WorkshopRules.StationName(record.Station)} for room on the shelf.");
            string owners = record.OwnerCount <= 1 ? "its first owner" : Invariant($"its {Ordinal(record.OwnerCount)} owner");
            var entities = context.Registry?.ActiveEntities;
            for (int i = 0; entities != null && i < entities.Count; i++)
            {
                var inventory = entities[i]?.Agent?.Inventory;
                if (inventory == null || entities[i].Agent.HasLeft) continue;
                var slots = EquipmentSlots.All;
                for (int s = 0; s < slots.Count; s++)
                {
                    if (inventory.GetEquipped(slots[s].Id).Masterpiece == record.Id)
                        return Invariant($"Worn by {context.GivenNameOf(entities[i].Id)}, {owners}.");
                }
                for (int s = 0; s < inventory.Capacity; s++)
                {
                    if (inventory.GetSlot(s).Masterpiece == record.Id) return Invariant($"Carried by {context.GivenNameOf(entities[i].Id)}, {owners}.");
                }
            }
            var piles = context.Simulation?.Piles?.Piles;
            for (int p = 0; piles != null && p < piles.Count; p++)
            {
                for (int s = 0; s < piles[p].Count; s++)
                {
                    if (piles[p].StackAt(s).Masterpiece == record.Id) return "On the ground.";
                }
            }
            return record.OwnerCount > 0 ? Invariant($"Lost; last owned by {context.GivenNameOf(record.LastOwner)}.") : "Lost.";
        }

        private static string Ordinal(int n) => n switch { 2 => "second", 3 => "third", 4 => "fourth", 5 => "fifth", 6 => "sixth", 7 => "seventh", 8 => "eighth", 9 => "ninth", 10 => "tenth", _ => Invariant($"{n}th") };

        private static string Capital(string text) => string.IsNullOrEmpty(text) ? string.Empty : char.ToUpperInvariant(text[0]) + text.Substring(1);
    }
}
