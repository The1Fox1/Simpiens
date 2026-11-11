using Simpiens.Cognition.Contracts;

namespace Simpiens.Cognition.Planning.Goals
{
    /// <summary>
    /// Goal (Epic 10 Part 3, K3.3, Q6): a bold resident buys a weapon at the stall, so that it fights back when a creature
    /// strikes it, and a better one when its purse spares it (a blade over a cudgel). DesiredState: HasWeapon = true (armed with
    /// the best it would buy), reached through TravelToStall → BuyWeapon. Only a resident (drifters pass through) who would
    /// venture into danger, not hungry or worn out, who finds a better weapon in stock at or under its own price with a
    /// ration's price to spare (ResourceTargeting.TryChooseWeapon, by stats), as Majesty's heroes buy their own arms.
    /// </summary>
    public class ArmForFightGoal : GoapGoal
    {
        /// <summary>Under equipping for work (35), above selling salvage (30): a purse that spares it arms the bold before its next round.</summary>
        public const float Priority = 33f;

        public ArmForFightGoal() : base("ArmForFight", WorldState.Empty.With(StateFact.HasWeapon, true))
        {
        }

        public override float CalculatePriority(AgentContext context) => Priority;

        public override bool IsValid(AgentContext context)
        {
            if (context.IsDrifter || context.Inventory == null) return false;
            if (context.Hunger >= 60f || context.Energy <= 25f) return false;
            return ResourceTargeting.TryChooseWeapon(context, out _);
        }
    }
}
