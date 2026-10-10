using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Simpiens.Testing.Environment
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

        public void InjectWall(Vector2? position = null)
        {
            var snapshot = _spatialPartition?.GetActiveSnapshot();
            if (snapshot == null) return;

            // Pick a random spot near the center if not specified
            Vector2 targetPos = position ?? (Random.insideUnitCircle * 4f);

            // Defensive clearance: Ensure we don't spawn a wall directly on top of an existing entity
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var hit = Physics2D.OverlapCircle(targetPos, 0.7f);
                if (hit != null && hit.GetComponent<NodeController>() != null)
                {
                    targetPos += new Vector2(0.5f, 0.5f);
                }
                else
                {
                    break;
                }
            }

            Debug.Log($"[EdgeCaseInjector] Spawning Wall at {targetPos} to invalidate paths!");

            // Create a Node that acts as a Wall
            var config = new NodeConfiguration(speed: 0f);
            var root = GameObject.Find("[DynamicObstacles]");
            if (root == null) root = new GameObject("[DynamicObstacles]");
            var nodeGo = new GameObject("InjectedWall");
            nodeGo.transform.SetParent(root.transform);
            nodeGo.transform.position = targetPos;

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
