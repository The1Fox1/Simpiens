using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;

namespace Simpiens.Cognition
{
    /// <summary>
    /// Consuming an item: its definition's effects on the crawler's stats, then one unit leaves the pack
    /// (.agents/item_system_plan.md §3.6). Main thread (single writer).
    /// </summary>
    public static class ItemUse
    {
        /// <summary>Upper bounds of the stats an effect can raise; a lowering effect stops at 0.</summary>
        public const float NeedMax = 100f;

        /// <summary>
        /// Applies one consumption of <paramref name="item"/> and removes a unit from the pack. False, changing nothing,
        /// when the crawler holds none or the item isn't consumed.
        /// </summary>
        public static bool Consume(AgentState agent, ItemType item)
        {
            var definition = ItemCatalog.Get(item);
            if (agent?.Inventory == null || definition == null || definition.ConsumeEffectCount == 0) return false;
            if (!agent.Inventory.HasItem(item)) return false;
            Apply(agent, definition);
            agent.Inventory.TryRemoveItem(item, 1);
            return true;
        }

        /// <summary>
        /// Each effect moves its stat, bounded only in its own direction: a cut stops at 0, a gain at the stat's maximum
        /// (as the code it replaced: Max(0, hunger − 40), Min(100, health + 30)).
        /// </summary>
        public static void Apply(AgentState agent, ItemDefinition item)
        {
            for (int i = 0; i < item.ConsumeEffectCount; i++)
            {
                var effect = item.GetConsumeEffect(i);
                switch (effect.Stat)
                {
                    case StatKind.Health: agent.Health = Move(agent.Health, effect.Amount, Vitality.MaxHealth); break;
                    case StatKind.Hunger: agent.Hunger = Move(agent.Hunger, effect.Amount, NeedMax); break;
                    case StatKind.Energy: agent.Energy = Move(agent.Energy, effect.Amount, NeedMax); break;
                    case StatKind.Social: agent.Social = Move(agent.Social, effect.Amount, NeedMax); break;
                    case StatKind.Frustration: agent.Frustration = Move(agent.Frustration, effect.Amount, NeedMax); break;
                }
            }
        }

        private static float Move(float value, float amount, float max) =>
            amount < 0f ? SimMath.Max(0f, value + amount) : SimMath.Min(max, value + amount);
    }
}
