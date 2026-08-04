using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

public static class UPoseDemoBuilder
{
    [MenuItem("UPose/Build simple demo scene")]
    public static void BuildDemoScene()
    {
        // Create a new scene and add the avatar player
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Avatar player (will attempt to play legacy 19-float dance from Resources/Dances)
        GameObject avatarPlayer = new GameObject("AvatarPlayer");
        avatarPlayer.AddComponent< AvatarAnimationPlayer >();
        avatarPlayer.AddComponent< AvatarDanceDemoLauncher >();

        // Save scene
        string scenesDir = "Assets/Scenes";
        if (!Directory.Exists(scenesDir)) Directory.CreateDirectory(scenesDir);
        string scenePath = Path.Combine(scenesDir, "UPose_Demo.unity");
        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("UPose Demo", "Created demo scene: " + scenePath + "\nThe demo will attempt to play a legacy dance from Resources/Dances if available.", "OK");
    }
}