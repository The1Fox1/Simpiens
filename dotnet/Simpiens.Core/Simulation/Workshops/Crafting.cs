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
        public static CraftStep Work(ref StationState station, ref TradeStallData stall, out bool stallChanged)
        {
            stallChanged = false;
            if (!station.Built || !station.HasOrder) return CraftStep.Idle;
            var recipe = RecipeBook.For(station.Order);
            if (recipe == null || recipe.Station != station.Kind) return CraftStep.Idle;

            // A finished make waits for room before anything else is started
            if (station.HeldOutput > 0)
            {
                if (!TryStock(ref stall, station.Order, station.HeldOutput)) return CraftStep.Holding;
                station.HeldOutput = 0;
                stallChanged = true;
                CloseIfDone(ref station);
                return CraftStep.Made;
            }

            if (!station.InputsTaken)
            {
                if (!HasInputs(stall, recipe)) return CraftStep.WaitingForInputs;
                for (int i = 0; i < recipe.InputCount; i++)
                {
                    var input = recipe.GetInput(i);
                    stall = stall.WithStock(input.Item, (ushort)(stall.StockOf(input.Item) - input.Count));
                }
                station.InputsTaken = true;
                station.Progress = 0;
                stallChanged = true;
            }

            station.Progress++;
            if (station.Progress < recipe.WorkTicks) return CraftStep.Working;

            station.InputsTaken = false;
            station.Progress = 0;
            station.Remaining--;
            station.Made++;
            if (!TryStock(ref stall, station.Order, recipe.OutputCount))
            {
                station.HeldOutput = recipe.OutputCount;
                return CraftStep.Holding;
            }
            stallChanged = true;
            CloseIfDone(ref station);
            return CraftStep.Made;
        }

        /// <summary>The stall holds every input of one make.</summary>
        public static bool HasInputs(in TradeStallData stall, Recipe recipe)
        {
            for (int i = 0; i < recipe.InputCount; i++)
            {
                var input = recipe.GetInput(i);
                if (stall.StockOf(input.Item) < input.Count) return false;
            }
            return true;
        }

        /// <summary>The stall has room for <paramref name="count"/> more of <paramref name="item"/>: it trades it, and the stock fits.</summary>
        public static bool HasRoom(in TradeStallData stall, ItemType item, int count)
            => stall.Trades(item) && stall.StockOf(item) + count <= ushort.MaxValue;

        private static bool TryStock(ref TradeStallData stall, ItemType item, int count)
        {
            if (!HasRoom(stall, item, count)) return false;
            stall = stall.WithStock(item, (ushort)(stall.StockOf(item) + count));
            return true;
        }

        private static void CloseIfDone(ref StationState station)
        {
            if (station.Remaining == 0 && station.HeldOutput == 0) station.Order = ItemType.None;
        }
    }
}
