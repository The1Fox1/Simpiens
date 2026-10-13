using Simpiens.Simulation.World;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Simpiens.Entities
{
    /// <summary>
    /// View of a map definition: floors, rock and walls on three tilemaps (no colliders, no simulation entities), text
    /// labels, fog over uncharted sectors (<see cref="ShroudView"/>) and the camera (<see cref="CameraController"/>). Rebuilt whenever a map is loaded. Holds no simulation state.
    ///
    /// Tiles are unit cells aligned with the map's wall cells (<see cref="CellOrigin"/>): Floor 1's walls are centred on
    /// whole numbers, the Dev Map's on half numbers in y. A wall or rock tile marks a cell whose centre lies inside a
    /// wall rect; every other cell in the playable bounds is floor, tinted by the sector it lies in.
    /// </summary>
    public static class MapView
    {
        public const string RootName = "[Map]";

        private static Tile _floorTile;
        private static Tile _rockTile;
        private static Tile _wallTile;

        /// <summary>
        /// Destroys any previous map view, draws <paramref name="map"/> with fog over its uncharted sectors (read from
        /// <paramref name="sectors"/>; the definition's defaults without one) and gives the main camera a
        /// <see cref="CameraController"/> framing the whole map.
        /// </summary>
        public static GameObject Show(MapDefinition map, ISectorRegistry sectors = null, WorldSelection selection = null)
        {
            Clear();
            if (map == null) return null;

            var root = Build(map, RootName);
            root.AddComponent<ShroudView>().Initialize(map, sectors);
            if (CameraController.AttachToMain(map.CameraFrame, selection) == null) FrameCamera(map.CameraFrame);
            return root;
        }

        public static void Clear()
        {
            var existing = GameObject.Find(RootName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }
        }

        /// <summary>Builds the view of <paramref name="map"/> under a new root (validators build their own and destroy it).</summary>
        internal static GameObject Build(MapDefinition map, string rootName)
        {
            var root = new GameObject(rootName);

            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(root.transform);
            var origin = CellOrigin(map);
            gridGo.transform.position = new Vector3(origin.x - 0.5f, origin.y - 0.5f, 0f); // cell (i, j) centred on origin + (i, j)
            var grid = gridGo.AddComponent<Grid>();
            grid.cellSize = Vector3.one;

            var floor = CreateTilemap(gridGo.transform, "Floor", RenderOrder.Floor);
            var rock = CreateTilemap(gridGo.transform, "Rock", RenderOrder.Rock);
            var walls = CreateTilemap(gridGo.transform, "Walls", RenderOrder.Walls);
            EnsureTiles();

            for (int i = 0; i < map.Walls.Count; i++)
            {
                var solid = map.Walls[i].Solid;
                bool isRock = IsRock(solid);
                CellRange(solid, origin, out int minX, out int maxX, out int minY, out int maxY);
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        var cell = new Vector3Int(x, y, 0);
                        if (isRock) rock.SetTile(cell, _rockTile);
                        else walls.SetTile(cell, _wallTile);
                    }
                }
            }

            CellRange(map.PlayableBounds, origin, out int bMinX, out int bMaxX, out int bMinY, out int bMaxY);
            for (int y = bMinY; y <= bMaxY; y++)
            {
                for (int x = bMinX; x <= bMaxX; x++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    if (walls.HasTile(cell) || rock.HasTile(cell)) continue;
                    floor.SetTile(cell, _floorTile);
                    floor.SetColor(cell, TryGetSectorIndex(map, origin + new Vector2(x, y), out int sectorIndex)
                        ? FloorTint(map, map.Sectors[sectorIndex])
                        : NeutralFloor);
                }
            }

            var labels = new GameObject("Labels");
            labels.transform.SetParent(root.transform);
            for (int i = 0; i < map.Labels.Count; i++)
            {
                DrawLabel(labels.transform, map.Labels[i]);
            }
            return root;
        }

        /// <summary>A wall rect wider than one cell both ways is solid rock outside the floor, not a wall run.</summary>
        internal static bool IsRock(Rect solid)
        {
            float unit = 2f * MapDefinitionBuilder.WallHalfExtent + 0.01f;
            return solid.width > unit && solid.height > unit;
        }

        /// <summary>
        /// World position of tile (0, 0)'s centre: the fractional part of the first wall cell's centre on each axis, so
        /// tiles line up with the map's wall cells. A map's walls share one alignment.
        /// </summary>
        internal static Vector2 CellOrigin(MapDefinition map)
        {
            if (map.Walls.Count == 0) return Vector2.zero;
            var solid = map.Walls[0].Solid;
            float inset = IsRock(solid) ? 0.5f : MapDefinitionBuilder.WallHalfExtent;
            return new Vector2(Fraction(solid.xMin + inset), Fraction(solid.yMin + inset));
        }

        private static float Fraction(float v)
        {
            float f = v - Mathf.Floor(v);
            return Mathf.Abs(f - 0.5f) < 0.01f ? 0.5f : 0f;
        }

        /// <summary>World position of a tile's centre.</summary>
        internal static Vector2 CellCentre(Vector2 origin, int x, int y) => origin + new Vector2(x, y);

        /// <summary>The tile whose square contains the world point.</summary>
        internal static Vector3Int CellAt(Vector2 origin, Vector2 world)
            => new Vector3Int(Mathf.RoundToInt(world.x - origin.x), Mathf.RoundToInt(world.y - origin.y), 0);

        /// <summary>The cells whose centres lie inside <paramref name="rect"/>.</summary>
        internal static void CellRange(Rect rect, Vector2 origin, out int minX, out int maxX, out int minY, out int maxY)
        {
            minX = Mathf.CeilToInt(rect.xMin - origin.x);
            maxX = Mathf.FloorToInt(rect.xMax - origin.x);
            minY = Mathf.CeilToInt(rect.yMin - origin.y);
            maxY = Mathf.FloorToInt(rect.yMax - origin.y);
        }

        /// <summary>The first sector containing the point, in definition order (as <see cref="ISectorRegistry.GetSectorAt"/>).</summary>
        internal static bool TryGetSectorIndex(MapDefinition map, Vector2 point, out int index)
        {
            for (int i = 0; i < map.Sectors.Count; i++)
            {
                if (map.Sectors[i].Contains(point)) { index = i; return true; }
            }
            index = -1;
            return false;
        }

        /// <summary>
        /// A sector's floor: a muted version of the colour of the map label inside it, so each room reads as its own
        /// place with no extra data. Neutral when the sector has no label.
        /// </summary>
        private static readonly Color NeutralFloor = new Color(0.2f, 0.21f, 0.24f);

        private static Color FloorTint(MapDefinition map, in SectorDefinition sector)
        {
            for (int i = 0; i < map.Labels.Count; i++)
            {
                if (sector.Contains(map.Labels[i].Position)) return Color.Lerp(NeutralFloor, map.Labels[i].Color, 0.3f);
            }
            return NeutralFloor;
        }

        /// <summary>Fits the orthographic main camera to <paramref name="frame"/> (both axes visible).</summary>
        public static void FrameCamera(Rect frame)
        {
            var cam = Camera.main;
            if (cam == null || !cam.orthographic) return;

            float aspect = cam.aspect > 0f ? cam.aspect : 16f / 9f;
            cam.orthographicSize = Mathf.Max(frame.height * 0.5f, frame.width * 0.5f / aspect);
            cam.transform.position = new Vector3(frame.center.x, frame.center.y, cam.transform.position.z);
        }

        private static Tilemap CreateTilemap(Transform grid, string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(grid, false);
            var tilemap = go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            renderer.mode = TilemapRenderer.Mode.Chunk;
            return tilemap;
        }

        private static void EnsureTiles()
        {
            if (_floorTile != null) return;
            _floorTile = CreateTile(AgentSpriteLibrary.FloorSprite, "Floor");
            _rockTile = CreateTile(AgentSpriteLibrary.RockSprite, "Rock");
            _wallTile = CreateTile(AgentSpriteLibrary.WallSprite, "Wall");
        }

        private static Tile CreateTile(Sprite sprite, string name)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = name;
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            tile.flags = TileFlags.None; // per-cell colour (floor tints)
            return tile;
        }

        private static void DrawLabel(Transform parent, in MapLabel label)
        {
            var textGo = new GameObject($"Label_{label.Text}");
            textGo.transform.SetParent(parent);
            textGo.transform.position = new Vector3(label.Position.x, label.Position.y, 0f);

            var textMesh = textGo.AddComponent<TextMesh>();
            textMesh.text = label.Text;
            textMesh.fontSize = 28;
            textMesh.characterSize = 0.08f;
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.color = label.Color;

            var mr = textGo.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sortingOrder = RenderOrder.Labels;
            }
        }
    }
}
