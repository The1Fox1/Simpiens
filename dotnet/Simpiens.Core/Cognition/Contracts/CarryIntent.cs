using Simpiens.Cognition.Pathfinding;

namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// A rescuer carries a fallen crawler out of danger (Epic 10 Part 3, K5.2, A7): the walk to it, the lift, then the carry
    /// at half pace toward the bunks on the path out, setting it down as soon as both are out of danger.
    /// </summary>
    public class CarryIntent : AgentIntent
    {
        public readonly EntityId FallenId;
        public readonly Vec2 FallenPosition;
        public readonly PathResponse PathToFallen;
        public readonly PathResponse PathOut;

        public CarryIntent(EntityId agentId, EntityId fallenId, Vec2 fallenPosition, PathResponse pathToFallen, PathResponse pathOut) : base(agentId)
        {
            FallenId = fallenId;
            FallenPosition = fallenPosition;
            PathToFallen = pathToFallen;
            PathOut = pathOut;
        }
    }

    /// <summary>A rescuer hands the fallen a medicine it carries, which the fallen takes there (Epic 10 Part 3, K5.3, Q3: a draught stands it up).</summary>
    public class GiveMedicineIntent : AgentIntent
    {
        public readonly EntityId FallenId;
        public readonly Vec2 FallenPosition;
        public readonly Simpiens.Simulation.Economy.ItemType Medicine;
        public readonly PathResponse Path;

        public GiveMedicineIntent(EntityId agentId, EntityId fallenId, Vec2 fallenPosition, Simpiens.Simulation.Economy.ItemType medicine, PathResponse path) : base(agentId)
        {
            FallenId = fallenId;
            FallenPosition = fallenPosition;
            Medicine = medicine;
            Path = path;
        }
    }
}
