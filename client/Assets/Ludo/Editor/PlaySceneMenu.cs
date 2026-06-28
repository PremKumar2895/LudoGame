using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LudoGame.EditorTools
{
    /// <summary>Menu: "Ludo ▸ New Play Scene (vs Bots)" — creates a ready-to-play scene in one click.</summary>
    public static class PlaySceneMenu
    {
        [MenuItem("Ludo/New Play Scene (vs Bots)")]
        public static void NewPlayScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("LudoGame");
            go.AddComponent<GameDirector>();
            Selection.activeGameObject = go;
            Debug.Log("[Ludo] Play scene ready. Press Play. Tap to roll on your turn, then tap a highlighted token to move.");
        }
    }
}
