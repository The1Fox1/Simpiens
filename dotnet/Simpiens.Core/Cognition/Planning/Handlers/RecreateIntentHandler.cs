using Simpiens.Cognition.Contracts;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Life;

namespace Simpiens.Cognition.Planning.Handlers
{
    /// <summary>
    /// Executes a RecreateIntent (Epic LIFE-1 plan 2, L2.5, D14): the crawler walks to its seat at the venue on its path, then
    /// spends the activity's spell there, posed as its row says: fun (and company) filled on the day's pace, a supper's meal
    /// eaten as it begins, and the activity's thought thought as it ends. A walk strolls from spot to spot among the venue's
    /// seats. What it costs (an ale, a stew) is bought from the stall's stock as it begins (plan 2's T.5). Struck, starving or ordered away, the runtime cuts it short
    /// as any intent. Main thread.
    /// </summary>
    public class RecreateIntentHandler : IntentHandlerBase<RecreateIntent>
    {
        /// <summary>How near a seat counts as at it.</summary>
        public const float SeatReach = 0.5f;

        protected override IntentResult Execute(WorldEntity node, RecreateIntent recreate, ActiveIntentState state, IIntentExecutionContext context)
        {
            var agent = node.Agent;
            var venues = context.Map?.Venues;
            var row = ActivityTable.Get(recreate.Activity);
            if (agent == null || venues == null || (uint)recreate.VenueIndex >= (uint)venues.Count || row == null || !row.IsRecreation) return IntentResult.TargetMissing;
            var venue = venues[recreate.VenueIndex];
            if ((uint)recreate.Seat >= (uint)venue.Seats.Count) return IntentResult.TargetMissing;

            // The walk there first, nothing filled yet
            if (!recreate.Begun)
            {
                var seat = venue.Seats[recreate.Seat];
                if ((seat - node.Position).SqrMagnitude > SeatReach * SeatReach)
                {
                    if (recreate.Path == null) return IntentResult.PathBlocked;
                    var walked = context.ExecutePathMovement(node, recreate.Path, state);
                    if (walked != IntentResult.Success) return walked;
                    if ((seat - node.Position).SqrMagnitude > SeatReach * SeatReach * 4f) return IntentResult.PathBlocked;
                }
                if (row.Cost != Simpiens.Simulation.Economy.ItemType.None && !Pay(node, row.Cost, context)) return IntentResult.Failed; // out of it, or short of gold
                recreate.Begun = true;
                state.ElapsedTime = 0f;
                if (row.Hunger < 0) agent.Hunger = SimMath.Max(0f, (float)(agent.Hunger + row.Hunger)); // a supper eaten
            }

            // The spell: its fills on the day's pace, a walk from spot to spot
            float dt = context.FixedTickDeltaTime;
            state.ElapsedTime += dt;
            agent.VisualState = row.Pose;
            agent.Entertain((float)(dt * DayClock.PerSecond(row.FunPerHour)));
            if (row.SocialPerHour > 0) agent.Social = SimMath.Min(100f, (float)(agent.Social + (float)(dt * DayClock.PerSecond(row.SocialPerHour))));
            if (row.Pose == AgentVisualState.Walking) Stroll(node, recreate, venue, context);

            if (state.ElapsedTime < row.Minutes * DayClock.TicksPerMinute * context.FixedTickDeltaTime) return IntentResult.InProgress;
            if (row.Thought != Thought.None) agent.Think(row.Thought, context.CurrentTick);
            agent.VisualState = AgentVisualState.Idle;
            return IntentResult.Success;
        }

        /// <summary>
        /// Buys <paramref name="item"/> from the stall's stock at its price, as the activity begins (Epic LIFE-1 plan 2, T.5, D19):
        /// gold to the treasury through the ledger as any sale, one off the shelf, served and taken there and then (it never enters
        /// the pack). False, with nothing changed, when none is in stock or the purse can't pay.
        /// </summary>
        private static bool Pay(WorldEntity node, Simpiens.Simulation.Economy.ItemType item, IIntentExecutionContext context)
        {
            var agent = node.Agent;
            var stalls = context.ActiveStalls;
            if (stalls == null || stalls.Count == 0 || agent?.Inventory == null) return false;
            var stall = stalls[0];
            uint price = stall.SellPriceOf(item);
            int stock = stall.SellStockOf(item);
            if (price == 0 || stock <= 0 || agent.Inventory.Gold < price) return false;
            if (context.EconomyLedger != null)
            {
                if (!context.EconomyLedger.TryExecuteStallPurchase(stall.StallEntityId, ref stall, agent.Inventory, price)) return false;
            }
            else
            {
                agent.Inventory.Gold -= (int)price;
                stall = stall.WithTreasury(stall.TreasuryGold + price);
            }
            context.UpdateStall(stall.WithStock(item, (ushort)(stock - 1)));
            context.RecordServed(node.Id, item, price);
            return true;
        }

        /// <summary>A step toward the walk's next spot; there, or blocked, the spot after it.</summary>
        private static void Stroll(WorldEntity node, RecreateIntent recreate, in Simpiens.Simulation.World.VenueDefinition venue, IIntentExecutionContext context)
        {
            var spot = venue.Seats[recreate.Stop];
            var toward = spot - node.Position;
            float distance = toward.Magnitude;
            float step = SimMath.Min((float)(node.BaseSpeed * 0.5f * context.FixedTickDeltaTime), distance); // an amble, half its pace
            if (distance <= SeatReach || step <= 0f || !context.TryDisplacePawn(node, node.Position + toward.Normalized * step, respectPawns: true))
            {
                recreate.Stop = (recreate.Stop + 1) % venue.Seats.Count;
            }
        }
    }
}
