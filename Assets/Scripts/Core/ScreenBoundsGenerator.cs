using UnityEngine;
using VContainer.Unity;
using Simpiens.Diagnostics;
using ZLogger;

namespace Simpiens.Core
{
    /// <summary>
    /// Automatically generates EdgeCollider2D boundaries around the main camera's viewport.
    /// Implements IStartable to ensure it runs after the scene loads and Camera.main is available.
    /// </summary>
    public class ScreenBoundsGenerator : IStartable
    {
        private readonly float _wallThickness = 1f;

        public void Start()
        {
            GenerateBounds();
        }

        private void GenerateBounds()
        {
            Camera cam = Camera.main;
            if (cam == null || !cam.orthographic)
            {
                SimLog.Bootstrap.ZLogError($"ScreenBoundsGenerator requires an orthographic main camera in the scene");
                return;
            }

            // Ensure camera frames the 36x28 utilitarian outpost map with generous margins
            if (cam.orthographicSize < 14.5f)
            {
                cam.orthographicSize = 14.5f;
            }
            cam.transform.position = new Vector3(0f, 0f, cam.transform.position.z);

            // Calculate screen dimensions in world space
            float screenHeight = cam.orthographicSize * 2f;
            float screenWidth = screenHeight * cam.aspect;

            // Half dimensions for positioning
            float halfWidth = screenWidth / 2f;
            float halfHeight = screenHeight / 2f;

            // Create a root object for organization
            GameObject boundsRoot = new GameObject("[ScreenBounds]");
            Object.DontDestroyOnLoad(boundsRoot);

            // Create 4 wall GameObjects centered around camera position
            Vector2 camPos = cam.transform.position;
            CreateWall("TopWall", new Vector2(camPos.x, camPos.y + halfHeight + _wallThickness / 2f), new Vector2(screenWidth + _wallThickness * 2, _wallThickness), boundsRoot.transform);
            CreateWall("BottomWall", new Vector2(camPos.x, camPos.y - halfHeight - _wallThickness / 2f), new Vector2(screenWidth + _wallThickness * 2, _wallThickness), boundsRoot.transform);
            CreateWall("LeftWall", new Vector2(camPos.x - halfWidth - _wallThickness / 2f, camPos.y), new Vector2(_wallThickness, screenHeight + _wallThickness * 2), boundsRoot.transform);
            CreateWall("RightWall", new Vector2(camPos.x + halfWidth + _wallThickness / 2f, camPos.y), new Vector2(_wallThickness, screenHeight + _wallThickness * 2), boundsRoot.transform);
        }

        private void CreateWall(string wallName, Vector2 position, Vector2 size, Transform parent)
        {
            GameObject wall = new GameObject(wallName);
            wall.transform.SetParent(parent);
            wall.transform.position = position;

            BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
            collider.size = size;
        }
    }
}
