using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Skills;

namespace Simpiens.Cognition.Planning
{
    /// <summary>
    /// The drill yard's rule for cognition (Epic 11, S3.4, Q10, D10): where a built yard stands, what a crawler drills there
    /// (the fight work of the weapon it wields: from range with a ranged weapon, in reach otherwise or unarmed), and whether
    /// a guild member would drill now: a resident member with no orders (no expedition, no shift), fed and rested, its drill
    /// skill below the yard's ceiling.
    /// </summary>
    public static class Drill
    {
        /// <summary>Above this hunger a member eats first, and under this energy it rests first: drill is for free hours.</summary>
        public const float MaxHunger = 50f, MinEnergy = 50f;

        /// <summary>The first built drill yard on the map, by station index; false with none standing.</summary>
        public static bool TryFindYard(in AgentContext context, out int index, out Vec2 position, out float radius)
        {
            index = -1;
            position = Vec2.Zero;
            radius = 0f;
            var snapshot = context.Snapshot;
            var stations = snapshot?.Map?.Stations;
            if (stations == null) return false;
            for (int i = 0; i < stations.Count; i++)
            {
                if (stations[i].Kind != StationKind.DrillYard || !snapshot.IsStationBuilt(i)) continue;
                index = i;
                position = stations[i].Position;
                radius = stations[i].Radius;
                return true;
            }
            return false;
        }

        /// <summary>The fight work a crawler wielding what <paramref name="inventory"/> holds drills: from range with a ranged weapon, in reach otherwise.</summary>
        public static SkillWork WorkOf(Simpiens.Cognition.Inventory.IAgentInventory inventory)
            => SkillWork.Fight(ranged: inventory != null && CrawlerStats.Get(inventory, StatKind.Range) > 0f);

        /// <summary>Whether this member would drill now (see the class).</summary>
        public static bool WouldDrill(in AgentContext context)
        {
            if (!context.IsGuildMember || context.IsDrifter || context.IsDowned) return false;
            if (context.Expedition.IsActive || context.Station.IsActive) return false;
            if (context.Hunger >= MaxHunger || context.Energy <= MinEnergy) return false;
            if (CrawlerSkills.Level(context.Skills, context.Inventory, WorkOf(context.Inventory)) >= SkillRules.YardCeiling) return false;
            return TryFindYard(context, out _, out _, out _);
        }
    }
}
