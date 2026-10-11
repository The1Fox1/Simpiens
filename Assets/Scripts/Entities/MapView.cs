using Simpiens.Simulation.World;
using UnityEngine;

namespace Simpiens.Entities
{
    /// <summary>
    /// View of a map definition: wall cells as plain sprites (no colliders, no simulation entities), text labels,
    /// and camera framing. Rebuilt whenever a map is loaded. Holds no simulation state.
    /// </summary>
    public static class MapView
    {
        public const string RootName = "[Map]";

        /// <summary>Destroys any previous map view, draws <paramref name="map"/> and frames the main camera on it.</summary>
        public static GameObject Show(MapDefinition map)
        {
            Clear();
            if (map == null) return null;

            var root = new GameObject(RootName);
            var walls = new GameObject("Walls");
            walls.transform.SetParent(root.transform);
            var labels = new GameObject("Labels");
            labels.transform.SetParent(root.transform);

            for (int i = 0; i < map.Walls.Count; i++)
            {
                DrawWall(walls.transform, map.Walls[i].Solid);
            }
            for (int i = 0; i < map.Labels.Count; i++)
            {
                DrawLabel(labels.transform, map.Labels[i]);
            }

            FrameCamera(map.CameraFrame);
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

        /// <summary>Fits the orthographic main camera to <paramref name="frame"/> (both axes visible).</summary>
        public static void FrameCamera(Rect frame)
        {
            var cam = Camera.main;
            if (cam == null || !cam.orthographic) return;

            float aspect = cam.aspect > 0f ? cam.aspect : 16f / 9f;
            cam.orthographicSize = Mathf.Max(frame.height * 0.5f, frame.width * 0.5f / aspect);
            cam.transform.position = new Vector3(frame.center.x, frame.center.y, cam.transform.position.z);
        }

        private static void DrawWall(Transform parent, Rect solid)
        {
            // Walls are authored as runs of unit cells (MapDefinitionBuilder.WallRun): one sprite per cell centre
            float inset = MapDefinitionBuilder.WallHalfExtent;
            float startX = solid.xMin + inset;
            float startY = solid.yMin + inset;
            int countX = Mathf.Max(1, Mathf.RoundToInt(solid.width - 2f * inset) + 1);
            int countY = Mathf.Max(1, Mathf.RoundToInt(solid.height - 2f * inset) + 1);

            for (int iy = 0; iy < countY; iy++)
            {
                for (int ix = 0; ix < countX; ix++)
                {
                    var pos = new Vector2(startX + ix, startY + iy);
                    var cell = new GameObject($"Wall_{pos.x:F1}_{pos.y:F1}");
                    cell.transform.SetParent(parent);
                    cell.transform.position = pos;
                    var sr = cell.AddComponent<SpriteRenderer>();
                    sr.sprite = AgentSpriteLibrary.WallSprite;
                    sr.sortingOrder = 0;
                }
            }
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
                mr.sortingOrder = 10;
            }
        }
    }
}
