using UnityEngine;
using VContainer;
using VContainer.Unity;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Simpiens.Entities
{
    /// <summary>
    /// Code-first factory for generating Node GameObjects dynamically.
    /// </summary>
    public class NodeFactory
    {
        private readonly IObjectResolver _resolver;
        private Transform _nodeRoot;

        [Inject]
        public NodeFactory(IObjectResolver resolver)
        {
            _resolver = resolver;
        }

        public void CreateNode(Vector2 position, NodeConfiguration? config = null)
        {
            if (_nodeRoot == null)
            {
                _nodeRoot = new GameObject("[Nodes]").transform;
                Object.DontDestroyOnLoad(_nodeRoot.gameObject);
            }

            var configuration = config ?? NodeConfiguration.Default;

            GameObject nodeGo = new GameObject("Node");
            nodeGo.transform.SetParent(_nodeRoot);
            nodeGo.transform.position = position;

            // Add standard components
            var sr = nodeGo.AddComponent<SpriteRenderer>();
            
            // In a real scenario, we'd load the Sprite via Addressables using an async Task/Coroutine.
            // Addressables.LoadAssetAsync<Sprite>("NodeSprite").Completed += handle => { sr.sprite = handle.Result; };
            // For now, we'll just set a temporary color/scale so it's visible without a sprite
            sr.color = Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.8f, 1f); // Bright random colors
            
            // Temporary default visualization until Addressables sprite is wired
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            
            // By default, Unity uses 100 pixels per unit. A 1x1 pixel texture becomes 0.01 units large!
            // We set pixelsPerUnit to 1f here so the 1x1 pixel texture perfectly fills a 1x1 world unit area,
            // matching our CircleCollider2D radius of 0.5f (diameter of 1).
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            nodeGo.transform.localScale = Vector3.one;

            var rb = nodeGo.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            
            // Bouncy physics material to make them bounce off bounds and each other
            var physicsMaterial = new PhysicsMaterial2D("Bouncy") { bounciness = 1f, friction = 0f };
            rb.sharedMaterial = physicsMaterial;

            var col = nodeGo.AddComponent<CircleCollider2D>();
            col.radius = 0.5f;

            var controller = nodeGo.AddComponent<NodeController>();
            
            // Inject the configuration rather than relying on Inspector fields
            _resolver.Inject(controller); // If NodeController had [Inject] dependencies from VContainer
            controller.Initialize(configuration);
        }
    }
}
