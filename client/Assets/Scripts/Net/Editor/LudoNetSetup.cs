// Builds the Fusion online test setup in ONE click (so we don't hand-drag prefabs/refs).
// Gated on FUSION2 (active now that Fusion 2 is imported). Lives in Assembly-CSharp-Editor.
#if FUSION2
using System.IO;
using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LudoGame.Net;

namespace LudoGame.Net.EditorTools
{
    public static class LudoNetSetup
    {
        private const string Dir = "Assets/Ludo/Online";
        private const string PrefabPath = Dir + "/LudoNetSession.prefab";
        private const string ScenePath = Dir + "/OnlineTest.unity";

        [MenuItem("Ludo/Setup Fusion Online Test")]
        public static void Setup()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Ludo")) AssetDatabase.CreateFolder("Assets", "Ludo");
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Ludo", "Online");

            // 1) Networked session prefab: NetworkObject + LudoNetSession.
            var temp = new GameObject("LudoNetSession");
            temp.AddComponent<NetworkObject>();
            temp.AddComponent<LudoNetSession>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
            Object.DestroyImmediate(temp);

            // 2) Dedicated test scene with the launcher; assign the prefab into its serialized field.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var launcher = new GameObject("LudoNetLauncher");
            var boot = launcher.AddComponent<LudoNetBootstrap>();
            launcher.AddComponent<OnlineGameView>();

            var so = new SerializedObject(boot);
            var sessionProp = so.FindProperty("_sessionPrefab");
            if (sessionProp != null)
            {
                sessionProp.objectReferenceValue = prefab != null ? prefab.GetComponent<NetworkObject>() : null;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.SaveScene(scene, ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = launcher;

            Debug.Log("[Ludo] Online test ready.\n" +
                      "• Prefab: " + PrefabPath + "\n" +
                      "• Scene : " + ScenePath + " (now open)\n" +
                      "Press PLAY, then click HOST. For a 2nd player: clone with ParrelSync or make a Build and Join the same room code.");
        }
    }
}
#endif
