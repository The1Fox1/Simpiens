using System.Threading;
using System.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Simulation.Economy;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action (Epic 10 Part 3, K5.3): goes to the fallen it would rescue (<see cref="Rescue.TryChooseFallen"/>) and hands over a
    /// draught (<see cref="GiveMedicineIntent"/>), or carries it out toward the bunk nearest where it lies (<see cref="CarryIntent"/>).
    /// Preconditions: none. Effects: FallenRescued = true.
    /// </summary>
    public class RescueAction : GoapAction
    {
        private readonly IAsyncPathfinder _pathfinder;

        public RescueAction(IAsyncPathfinder pathfinder = null, float cost = 1.0f) : base("Rescue", cost)
        {
            _pathfinder = pathfinder;
            Preconditions = WorldState.Empty;
            Effects = WorldState.Empty.With(StateFact.FallenRescued, true);
        }

        public override bool IsValid(AgentContext context) => Rescue.TryChooseFallen(context, out _, out _);

        public override AgentIntent CreateIntent(AgentContext context)
        {
            if (!Rescue.TryChooseFallen(context, out var fallen, out var medicine)) return new IdleIntent(context.AgentId, duration: 1.0f);
            return medicine != ItemType.None
                ? new GiveMedicineIntent(context.AgentId, fallen.Id, fallen.Position, medicine, null)
                : new CarryIntent(context.AgentId, fallen.Id, fallen.Position, null, null);
        }

        public override async ValueTask<AgentIntent> CreateIntentAsync(AgentContext context, CancellationToken ct = default)
        {
            if (!Rescue.TryChooseFallen(context, out var fallen, out var medicine)) return new IdleIntent(context.AgentId, duration: 1.0f);
            if (_pathfinder == null || context.Snapshot == null) return CreateIntent(context);
            PathResponse toFallen = null;
            if ((fallen.Position - context.Position).SqrMagnitude > Handlers.CarryIntentHandler.Reach * Handlers.CarryIntentHandler.Reach)
            {
                toFallen = await _pathfinder.CalculatePathAsync(new PathRequest { StartPosition = context.Position, TargetPosition = fallen.Position, RequesterId = context.AgentId }, context.Snapshot);
                if (!ResourceTargeting.IsReachable(toFallen, fallen.Position))
                {
                    toFallen?.ReturnToPool();
                    return new IdleIntent(context.AgentId, duration: 1.0f); // heard but out of reach: it decides afresh
                }
            }
            if (medicine != ItemType.None) return new GiveMedicineIntent(context.AgentId, fallen.Id, fallen.Position, medicine, toFallen);
            // The way out: from where it lies to the nearest bunk (the bunks are out of danger)
            PathResponse outPath = null;
            if (Vitality.TryGetRefugeFrom(context.Snapshot?.Map, fallen.Position, out var refuge))
            {
                outPath = await _pathfinder.CalculatePathAsync(new PathRequest { StartPosition = fallen.Position, TargetPosition = refuge, RequesterId = context.AgentId }, context.Snapshot);
                if (outPath != null && !outPath.IsValid)
                {
                    outPath.ReturnToPool();
                    outPath = null;
                }
            }
            if (outPath == null)
            {
                toFallen?.ReturnToPool();
                return new IdleIntent(context.AgentId, duration: 1.0f);
            }
            return new CarryIntent(context.AgentId, fallen.Id, fallen.Position, toFallen, outPath);
        }
    }
}
