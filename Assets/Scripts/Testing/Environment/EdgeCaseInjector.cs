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
        private readonly IWorldRegistry _registry;

        [Inject]
        private readonly IEntityIdAllocator _idAllocator;

        public EdgeCaseInjector(ISpatialPartition spatialPartition, IWorldRegistry registry, IEntityIdAllocator idAllocator = null)
        {
            _idAllocator = idAllocator;
            _spatialPartition = spatialPartition;
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

        public void InjectWall(Vec2? position = null)
        {
            var snapshot = _spatialPartition?.GetActiveSnapshot();
            if (snapshot == null) return;

            // Pick a random spot near the center if not specified
            Vec2 targetPos = position ?? (Random.insideUnitCircle * 4f).ToSim();

            // Defensive clearance: Ensure we don't spawn a wall directly on top of an existing entity
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var hit = Physics2D.OverlapCircle(targetPos.ToUnity(), 0.7f);
                if (hit != null && hit.GetComponent<NodeController>() != null)
                {
                    targetPos += new Vec2(0.5f, 0.5f);
                }
                else
                {
                    break;
                }
            }

            Debug.Log($"[EdgeCaseInjector] Spawning Wall at {targetPos} to invalidate paths!");

            // A wall entity (the simulation's), drawn by a node view
            var root = GameObject.Find("[DynamicObstacles]");
            if (root == null) root = new GameObject("[DynamicObstacles]");
            var nodeGo = new GameObject("InjectedWall");
            nodeGo.transform.SetParent(root.transform);
            nodeGo.transform.position = targetPos.ToUnity();

            var sr = nodeGo.AddComponent<SpriteRenderer>();
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.gray);
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

            var rb = nodeGo.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            var col = nodeGo.AddComponent<CircleCollider2D>();
            col.radius = 0.5f;

            var entity = new WorldEntity(_idAllocator != null ? _idAllocator.Next() : EntityId.Generate(), EntityType.Wall, targetPos, col.radius, name: nodeGo.name);
            nodeGo.AddComponent<NodeController>().Bind(entity);

            _registry.Register(entity);
        }
    }
}
