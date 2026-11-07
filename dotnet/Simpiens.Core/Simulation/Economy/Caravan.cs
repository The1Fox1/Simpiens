using System;
using System.Collections.Generic;

namespace Simpiens.Simulation.Economy
{
    /// <summary>Where the trade caravan is in its round (Epic 10 Part 2, the stretch, D70).</summary>
    public enum CaravanStage : byte
    {
        /// <summary>Off the map, between visits.</summary>
        Away = 0,
        /// <summary>Walking in from the road to the stall, to arrive on the visit's tick.</summary>
        Arriving = 1,
        /// <summary>At the stall, for <see cref="Caravan.TradeTicks"/> after the visit.</summary>
        Trading = 2,
        /// <summary>Walking back out to the road.</summary>
        Leaving = 3
    }

    /// <summary>The trade caravan at one tick: its stage, where it is, and how far along its route (0 at the road, 1 at the stall).</summary>
    public readonly struct CaravanState
    {
        public readonly CaravanStage Stage;
        public readonly Vec2 Position;
        public readonly float Progress;

        public CaravanState(CaravanStage stage, Vec2 position, float progress)
        {
            Stage = stage;
            Position = position;
            Progress = progress;
        }

        public bool IsOnTheMap => Stage != CaravanStage.Away;
    }

    /// <summary>
    /// The trade caravan as a figure on the map (Epic 10 Part 2, the stretch, D70). The supply visit stays on its schedule
    /// (<see cref="SupplyTuning.RestockIntervalTicks"/>); the caravan's place is a pure function of the clock: it sets out
    /// from where the road meets the map so as to reach the stall on the visit's tick, trades there a game hour, and walks
    /// back out the way it came. So it changes no timing, needs nothing saved (a load at any tick puts it where it was),
    /// and, not being an entity, no crawler perceives it or bumps into it.
    /// </summary>
    public static class Caravan
    {
        /// <summary>A laden porter's pace, in world units a tick: a unit a second, slower than a crawler.</summary>
        public const float UnitsPerTick = 0.1f;

        /// <summary>How long it stays at the stall after the visit.</summary>
        public static readonly uint TradeTicks = DayClock.Hours(1);

        /// <summary>Where the caravan is at <paramref name="tick"/>, for visits every <paramref name="visitInterval"/> ticks along <paramref name="route"/>.</summary>
        public static CaravanState At(long tick, uint visitInterval, IReadOnlyList<Vec2> route)
        {
            if (visitInterval == 0 || route == null || route.Count < 2 || tick < 0) return default;
            float length = Length(route);
            long walk = (long)Math.Ceiling(length / UnitsPerTick);
            long last = tick / visitInterval * visitInterval; // the latest visit at or before this tick; tick 0 is no visit
            long next = last + visitInterval;

            if (tick >= next - walk)
            {
                float walked = (tick - (next - walk)) * UnitsPerTick;
                return new CaravanState(CaravanStage.Arriving, PointAlong(route, walked), Math.Min(1f, walked / length));
            }
            if (last > 0 && tick < last + TradeTicks) return new CaravanState(CaravanStage.Trading, route[route.Count - 1], 1f);
            if (last > 0 && tick < last + TradeTicks + walk)
            {
                float walked = (tick - (last + TradeTicks)) * UnitsPerTick;
                float along = Math.Max(0f, length - walked);
                return new CaravanState(CaravanStage.Leaving, PointAlong(route, along), along / length);
            }
            return default;
        }

        /// <summary>The route's length, waypoint to waypoint.</summary>
        public static float Length(IReadOnlyList<Vec2> route)
        {
            float length = 0f;
            for (int i = 1; i < route.Count; i++) length += Vec2.Distance(route[i - 1], route[i]);
            return length;
        }

        /// <summary>The point <paramref name="distance"/> along <paramref name="route"/> from its start, clamped to its ends.</summary>
        public static Vec2 PointAlong(IReadOnlyList<Vec2> route, float distance)
        {
            if (distance <= 0f) return route[0];
            for (int i = 1; i < route.Count; i++)
            {
                float segment = Vec2.Distance(route[i - 1], route[i]);
                if (distance <= segment && segment > 0f) return Vec2.Lerp(route[i - 1], route[i], distance / segment);
                distance -= segment;
            }
            return route[route.Count - 1];
        }
    }
}
