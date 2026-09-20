using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Simpiens.Core.Editor
{
    public static class BatchRunner
    {
        public static void Run()
        {
            Debug.Log("[BatchRunner] Opening scene and starting play mode...");
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            EditorApplication.isPlaying = true;
        }
    }
}
