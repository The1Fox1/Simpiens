using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Simpiens.Cognition.Testing
{
    public class EdgeCaseInjector : ITickable
    {
        private readonly ISpatialPartition _spatialPartition;
        private readonly NodeFactory _nodeFactory;
        private readonly IWorldRegistry _registry;

        [Inject]
        public EdgeCaseInjector(ISpatialPartition spatialPartition, NodeFactory nodeFactory, IWorldRegistry registry)
        {
            _spatialPartition = spatialPartition;
            _nodeFactory = nodeFactory;
            _registry = registry;
        }

        public void Tick()
        {
            // Press Space to inject an edge case
            if (UnityEngine.InputSystem.Keyboard.current != null && 
                UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                InjectWall();
            }
        }

        private void InjectWall()
        {
            var snapshot = _spatialPartition.GetActiveSnapshot();
            if (snapshot == null) return;

            // Pick a random spot near the center
            Vector2 randomPos = Random.insideUnitCircle * 4f;

            Debug.Log($"[EdgeCaseInjector] Spawning Wall at {randomPos} to invalidate paths!");

            // Create a Node that acts as a Wall
            var config = new NodeConfiguration(speed: 0f);
            var nodeGo = new GameObject("InjectedWall");
            nodeGo.transform.position = randomPos;

            var sr = nodeGo.AddComponent<SpriteRenderer>();
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.gray);
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

            var rb = nodeGo.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            var col = nodeGo.AddComponent<CircleCollider2D>();
            col.radius = 0.5f;

            var controller = nodeGo.AddComponent<NodeController>();
            controller.Type = EntityType.Wall;
            controller.Initialize(GUID.Generate(), config);

            _registry.RegisterNode(controller);
        }
    }
}
