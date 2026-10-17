using Simpiens.Cognition.Contracts;
namespace Simpiens.Cognition.Planning
{
    /// <summary>
    /// Static reachability over the snapshot's StaticNavGrid connected regions: can this agent walk to a point
    /// at all (ignoring other pawns)? Used to keep goals/actions from targeting stalls and bounties behind walls.
    /// Unknown (no snapshot/grid/labels) is treated as reachable so headless validators are never blocked.
    /// </summary>
    public static class Reachability
    {
        public static bool IsReachable(in AgentContext context, Vec2 target)
        {
            var grid = context.Snapshot != null ? context.Snapshot.StaticGrid : null;
            return grid == null || grid.AreConnected(context.Position, target);
        }
    }
}
