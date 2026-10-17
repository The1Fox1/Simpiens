using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Cognition.Evaluators;
using Simpiens.Cognition.Planning;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;

namespace Simpiens.Testing.Environment
{
    /// <summary>A crawler built for a validator: its state, its entity in the world and its tick, with no scene object.</summary>
    public sealed class TestCrawler
    {
        public WorldEntity Node;
        public AgentState Agent;
        public AgentRuntime Runtime;

        public EntityId Id => Node.Id;

        /// <summary>One fixed tick of this crawler (what the session does after the simulation's tick).</summary>
        public void Tick(long tick) => Runtime.Tick(tick);

        /// <summary>A tick at the clock's current tick (0 without a clock), as AutonomousAgent.ManualUpdate did.</summary>
        public void ManualUpdate() => Runtime.Tick(Runtime.CurrentSimTick < 0 ? 0 : Runtime.CurrentSimTick);
    }

    /// <summary>
    /// Headless stand-ins for the GameObjects validators used to build (Epic CORE C5). Defaults reproduce what those
    /// objects gave the simulation: a radius of 0.5 (the default circle collider) and a speed of 2.
    /// </summary>
    public static class HeadlessEntities
    {
        public const float DefaultRadius = 0.5f;
        public const float DefaultSpeed = 2f;

        public static TestCrawler Crawler(
            IWorldRegistry registry,
            ISimulationManager sim,
            ISimulationClock clock,
            Vec2 position,
            EntityId id = default,
            ISpatialPartition partition = null,
            ICognitiveEvaluator[] evaluators = null,
            IGoapPlanner planner = null,
            ActionGraph actionGraph = null,
            List<GoapGoal> goals = null,
            IAgentCognitionService cognition = null,
            string name = null,
            float radius = DefaultRadius,
            float baseSpeed = DefaultSpeed,
            bool register = true)
        {
            if (id == default) id = EntityId.Generate();
            var state = new AgentState(id);
            var entity = new WorldEntity(id, EntityType.Pawn, position, radius, baseSpeed, state, name ?? "TestCrawler");
            if (register) registry?.Register(entity);
            var runtime = new AgentRuntime(state, partition, evaluators, sim, clock, planner, actionGraph, goals, cognition);
            return new TestCrawler { Node = entity, Agent = state, Runtime = runtime };
        }

        public static WorldEntity Prop(
            IWorldRegistry registry,
            EntityType type,
            Vec2 position,
            EntityId id = default,
            float radius = DefaultRadius,
            ItemType resourceKind = default,
            string name = null,
            bool register = true)
        {
            if (id == default) id = EntityId.Generate();
            var entity = new WorldEntity(id, type, position, radius, name: name ?? type.ToString()) { ResourceKind = resourceKind };
            if (register) registry?.Register(entity);
            return entity;
        }

        /// <summary>Takes a crawler out of the world and stops it (what destroying its GameObject used to do).</summary>
        public static void Remove(IWorldRegistry registry, TestCrawler crawler)
        {
            if (crawler == null) return;
            registry?.Unregister(crawler.Node);
            crawler.Runtime?.Dispose();
        }
    }
}
