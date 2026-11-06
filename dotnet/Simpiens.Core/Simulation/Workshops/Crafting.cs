using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;

namespace Simpiens.Simulation.Workshops
{
    /// <summary>
    /// The crafting rule (Epic 10 Part 2, W0.5), engine-free: one tick of an artisan's work at a station, on plain data. A
    /// make takes its recipe's inputs from the stall's stock when it begins, runs for the recipe's work time, and puts its
    /// output into the stall's stock (Q7), or holds it while the stall has no room (D3). Allocates nothing.
    /// </summary>
    public static class Crafting
    {
        /// <summary>
        /// One tick of work at <paramref name="station"/>, drawing on and stocking <paramref name="stall"/>.
        /// <paramref name="stallChanged"/> says whether the stall's stock moved.
        /// </summary>
        public static CraftStep Work(ref StationState station, ref TradeStallData stall, out bool stallChanged, byte grade = Grades.Standard)
        {
            stallChanged = false;
            if (!station.Built || !station.HasOrder) return CraftStep.Idle;
            var recipe = RecipeBook.For(station.Order);
            if (recipe == null || recipe.Station != station.Kind) return CraftStep.Idle;

            // A finished make waits for room before anything else is started
            if (station.HeldOutput > 0)
            {
                if (!TryStock(ref stall, station.Order, station.HeldOutput, station.HeldGrade)) return CraftStep.Holding;
                station.HeldOutput = 0;
                stallChanged = true;
                CloseIfDone(ref station);
                return CraftStep.Made;
            }

            if (!station.InputsTaken)
            {
                if (!HasInputs(stall, recipe, station.Repair)) return CraftStep.WaitingForInputs;
                for (int i = 0; i < recipe.InputCount; i++)
                {
                    var input = recipe.GetInput(i);
                    stall = stall.WithStock(input.Item, (ushort)(stall.StockOf(input.Item) - InputCount(input, station.Repair)));
                }
                // A repair takes the worn piece too (W4, D59)
                if (station.Repair) stall = stall.WithWorn(station.Order, (byte)(stall.WornOf(station.Order) - 1));
                station.InputsTaken = true;
                station.Progress = 0;
                stallChanged = true;
            }

            station.Progress++;
            if (station.Progress < WorkTicks(recipe, station.Repair)) return CraftStep.Working;

            station.InputsTaken = false;
            station.Progress = 0;
            station.Remaining--;
            if (station.Repair) grade = Grades.Standard; // mended to full, at Standard; a repair isn't a make, so no practice (D59)
            else station.Made++;
            if (!TryStock(ref stall, station.Order, recipe.OutputCount, grade))
            {
                station.HeldOutput = recipe.OutputCount;
                station.HeldGrade = grade; // made at its artisan's grade, whenever it is stocked (D57)
                return CraftStep.Holding;
            }
            stallChanged = true;
            CloseIfDone(ref station);
            return CraftStep.Made;
        }

        /// <summary>The stall holds every input of one make; of a repair, a worn piece too (D59).</summary>
        public static bool HasInputs(in TradeStallData stall, Recipe recipe, bool repair = false)
        {
            if (repair && stall.WornOf(recipe.Output) < 1) return false;
            for (int i = 0; i < recipe.InputCount; i++)
            {
                var input = recipe.GetInput(i);
                if (stall.StockOf(input.Item) < InputCount(input, repair)) return false;
            }
            return true;
        }

        /// <summary>
        /// How many of an input a make takes: a repair half, rounded down (W4, Q8, D59: half the ore of a new piece): a
        /// pickaxe's 3 ore are 1, and a respirator's one glowstone none, so mending spares the rare input.
        /// </summary>
        public static int InputCount(in RecipeInput input, bool repair) => repair ? input.Count / 2 : input.Count;

        /// <summary>The work a make takes: a repair half (D59).</summary>
        public static int WorkTicks(Recipe recipe, bool repair) => repair ? (recipe.WorkTicks + 1) / 2 : recipe.WorkTicks;

        /// <summary>Whether worn pieces of <paramref name="item"/> can be mended: something worn the stations make (D58); hazard gear, the caravan's, can't.</summary>
        public static bool Repairable(ItemType item) => Grades.Graded(item);

        /// <summary>
        /// A worn piece handed back at the stall (the guild's swap, D50; a trade-in, D53): onto the shelf of worn pieces if the
        /// forge can mend it (D58); otherwise back into stock while sound (above half its life), and discarded when worn down.
        /// </summary>
        public static TradeStallData HandBack(in TradeStallData stall, in ItemInstance piece)
        {
            var equipment = piece.IsEmpty ? null : ItemCatalog.Get(piece.Type)?.Equipment;
            if (equipment == null || !stall.Trades(piece.Type)) return stall;
            if (Repairable(piece.Type)) return stall.WithWorn(piece.Type, (byte)System.Math.Min(byte.MaxValue, stall.WornOf(piece.Type) + 1));
            bool sound = piece.Durability * 2 > equipment.DurabilityAt(piece.Quality);
            return sound ? stall.WithStock(piece.Type, (ushort)System.Math.Min(ushort.MaxValue, stall.StockOf(piece.Type) + 1)) : stall;
        }

        /// <summary>The stall has room for <paramref name="count"/> more of <paramref name="item"/>: it trades it, and the stock fits.</summary>
        public static bool HasRoom(in TradeStallData stall, ItemType item, int count)
            => stall.Trades(item) && stall.StockOf(item) + count <= ushort.MaxValue;

        private static bool TryStock(ref TradeStallData stall, ItemType item, int count, byte grade)
        {
            if (!HasRoom(stall, item, count)) return false;
            // Only what is worn takes a grade (D56): a draught is Standard whoever makes it
            stall = stall.WithStocked(item, (ushort)count, ItemCatalog.Get(item)?.Equipment != null ? grade : Grades.Standard);
            return true;
        }

        private static void CloseIfDone(ref StationState station)
        {
            if (station.Remaining == 0 && station.HeldOutput == 0) station.Order = ItemType.None;
        }
    }
}
