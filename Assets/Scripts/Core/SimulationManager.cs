using UnityEngine;
using Simpiens.Entities;

namespace Simpiens.Core
{
    public class SimulationManager : MonoBehaviour
    {
        [Header("Simulation Settings")]
        [Tooltip("The prefab to spawn for each Simpien node.")]
        [SerializeField] private NodeController nodePrefab;
        [Tooltip("How many nodes to spawn at the start of the simulation.")]
        [SerializeField] private int initialNodeCount = 20;

        private Camera mainCamera;
        private Vector2 spawnBounds;

        private void Start()
        {
            mainCamera = Camera.main;
            // Calculate bounds slightly smaller than the screen to spawn them safely inside
            spawnBounds = mainCamera.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, mainCamera.transform.position.z)) * 0.9f;

            InitializeSimulation();
        }

        private void InitializeSimulation()
        {
            if (nodePrefab == null)
            {
                Debug.LogError("SimulationManager: Node Prefab is not assigned in the Inspector!");
                return;
            }

            for (int i = 0; i < initialNodeCount; i++)
            {
                SpawnNode();
            }
        }

        private void SpawnNode()
        {
            // Generate a random position within spawn bounds
            Vector2 spawnPosition = new Vector2(
                Random.Range(-spawnBounds.x, spawnBounds.x),
                Random.Range(-spawnBounds.y, spawnBounds.y)
            );

            // Instantiate the prefab (this creates a clone of the GameObject in the scene)
            Instantiate(nodePrefab, spawnPosition, Quaternion.identity);
        }
    }
}
