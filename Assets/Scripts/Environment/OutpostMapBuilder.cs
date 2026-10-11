using System.Collections.Generic;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using UnityEngine;
using VContainer;
using Simpiens.Diagnostics;
using ZLogger;

namespace Simpiens.Environment
{
    /// <summary>
    /// Pure C# procedural world builder for the utilitarian 9-sector outpost map.
    /// Constructs grid-aligned static wall colliders and leaves open 2.5m-3.0m door portals
    /// with zero allocations during runtime simulation ticks.
    /// </summary>
    public class OutpostMapBuilder : IOutpostMapBuilder
    {
        // World Dimensions
        public const float WorldWidth = 36f;
        public const float WorldHeight = 28f;
        public const float MinX = -18f;
        public const float MaxX = 18f;
        public const float MinY = -14f;
        public const float MaxY = 14f;

        // Interior Divider Offsets
        public const float InteriorDividerX1 = -6f;
        public const float InteriorDividerX2 = 6f;
        public const float InteriorDividerY1 = -5f;
        public const float InteriorDividerY2 = 5f;

        // Canonical Doorway Centers
        public static readonly Vector2 DoorNorth = new Vector2(0f, 5f);
        public static readonly Vector2 DoorSouth = new Vector2(0f, -5f);
        public static readonly Vector2 DoorWest  = new Vector2(-6f, 0f);
        public static readonly Vector2 DoorEast  = new Vector2(6f, 0f);
        public static readonly Vector2 DoorW1    = new Vector2(-12f, 5f);
        public static readonly Vector2 DoorE1    = new Vector2(12f, 5f);
        public static readonly Vector2 DoorW2    = new Vector2(-12f, -5f);
        public static readonly Vector2 DoorE2    = new Vector2(12f, -5f);
        public static readonly Vector2 DoorSW    = new Vector2(-6f, -9f);
        public static readonly Vector2 DoorSE    = new Vector2(6f, -9f);

        private static readonly Vector2[] _canonicalDoors = new Vector2[]
        {
            DoorNorth,
            DoorSouth,
            DoorWest,
            DoorEast,
            DoorW1,
            DoorE1,
            DoorW2,
            DoorE2,
            DoorSW,
            DoorSE
        };

        private readonly IWorldRegistry _registry;
        private readonly List<NodeController> _wallNodes = new List<NodeController>(256);
        private GameObject _mapRoot;

        public int WallCount => _wallNodes.Count;
        public IReadOnlyList<Vector2> DoorwayPositions => _canonicalDoors;

        [Inject]
        public OutpostMapBuilder(IWorldRegistry registry = null)
        {
            _registry = registry;
        }

        public void BuildMap()
        {
            ClearMap();

            _mapRoot = new GameObject("[OutpostMap]");
            if (Application.isPlaying)
            {
                Object.DontDestroyOnLoad(_mapRoot);
            }

            SpawnPerimeterWalls(_mapRoot);
            SpawnInteriorHorizontalDividers(_mapRoot);
            SpawnInteriorVerticalDividers(_mapRoot);

            SimLog.Bootstrap.ZLogInformation($"Outpost map constructed: {_wallNodes.Count} wall nodes, 10 open doorways");
        }

        public void ClearMap()
        {
            if (_wallNodes.Count > 0 && _registry != null)
            {
                int count = _wallNodes.Count;
                for (int i = 0; i < count; i++)
                {
                    var node = _wallNodes[i];
                    if (node != null)
                    {
                        _registry.UnregisterNode(node);
                    }
                }
            }
            _wallNodes.Clear();

            if (_mapRoot != null)
            {
                Object.DestroyImmediate(_mapRoot);
                _mapRoot = null;
            }
            else
            {
                var existing = GameObject.Find("[OutpostMap]");
                if (existing != null)
                {
                    Object.DestroyImmediate(existing);
                }
            }
        }

        private void SpawnPerimeterWalls(GameObject root)
        {
            // North perimeter wall (Y = 14, X from -18 to 18)
            for (int x = -18; x <= 18; x++)
            {
                SpawnWallNode(root, new Vector2(x, MaxY));
            }

            // South perimeter wall (Y = -14, X from -18 to 18)
            for (int x = -18; x <= 18; x++)
            {
                SpawnWallNode(root, new Vector2(x, MinY));
            }

            // West perimeter wall (X = -18, Y from -13 to 13)
            for (int y = -13; y <= 13; y++)
            {
                SpawnWallNode(root, new Vector2(MinX, y));
            }

            // East perimeter wall (X = 18, Y from -13 to 13)
            for (int y = -13; y <= 13; y++)
            {
                SpawnWallNode(root, new Vector2(MaxX, y));
            }
        }

        private void SpawnInteriorHorizontalDividers(GameObject root)
        {
            // Horizontal dividers at Y = 5 (between North sectors & Center sectors)
            // and Y = -5 (between Center sectors & South sectors)
            float[] dividerY = new float[] { InteriorDividerY2, InteriorDividerY1 };

            for (int d = 0; d < dividerY.Length; d++)
            {
                float y = dividerY[d];

                for (int x = -17; x <= 17; x++)
                {
                    // Door W1 / W2 gap: X in [-13, -11]
                    if (x >= -13 && x <= -11) continue;

                    // Door North / South gap: X in [-1, 1]
                    if (x >= -1 && x <= 1) continue;

                    // Door E1 / E2 gap: X in [11, 13]
                    if (x >= 11 && x <= 13) continue;

                    SpawnWallNode(root, new Vector2(x, y));
                }
            }
        }

        private void SpawnInteriorVerticalDividers(GameObject root)
        {
            // Vertical dividers at X = -6 and X = 6
            float[] dividerX = new float[] { InteriorDividerX1, InteriorDividerX2 };

            for (int d = 0; d < dividerX.Length; d++)
            {
                float x = dividerX[d];

                // North section: Y in [6, 13] (Solid dividing wall between Orchard & Quarters / Directive Yard)
                for (int y = 6; y <= 13; y++)
                {
                    SpawnWallNode(root, new Vector2(x, y));
                }

                // Center section: Y in [-4, 4] (Leaves 3-unit door opening at Y in [-1, 1])
                for (int y = -4; y <= 4; y++)
                {
                    if (y >= -1 && y <= 1) continue; // Door West / Door East gap
                    SpawnWallNode(root, new Vector2(x, y));
                }

                // South section: Y in [-13, -6] (Leaves 3-unit door opening at Y in [-10, -8])
                for (int y = -13; y <= -6; y++)
                {
                    if (y >= -10 && y <= -8) continue; // Door SW / Door SE gap
                    SpawnWallNode(root, new Vector2(x, y));
                }
            }
        }

        private void SpawnWallNode(GameObject root, Vector2 pos)
        {
            var wallGo = new GameObject($"Wall_{pos.x:F0}_{pos.y:F0}");
            wallGo.transform.SetParent(root.transform);
            wallGo.transform.position = pos;

            var sr = wallGo.AddComponent<SpriteRenderer>();
            sr.sprite = AgentSpriteLibrary.WallSprite;
            sr.sortingOrder = 0;

            var rb = wallGo.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Static;

            var col = wallGo.AddComponent<CircleCollider2D>();
            col.radius = 0.55f;

            var box = wallGo.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1.02f, 1.02f);

            var controller = wallGo.AddComponent<NodeController>();
            controller.Type = EntityType.Wall;
            controller.Initialize(UnityEngine.GUID.Generate(), new NodeConfiguration(speed: 0f));

            _wallNodes.Add(controller);
            _registry?.RegisterNode(controller);
        }
    }
}
