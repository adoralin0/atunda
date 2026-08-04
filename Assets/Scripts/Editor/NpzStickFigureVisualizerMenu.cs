using UnityEditor;
using UnityEngine;

public static class NpzStickFigureVisualizerMenu
{
    [MenuItem("UPose/Create Stick Figure Visualizer")]
    static void CreateVisualizer()
    {
        GameObject go = new GameObject("NpzStickFigure");
        NpzStickFigureVisualizer visualizer = go.AddComponent<NpzStickFigureVisualizer>();
        visualizer.jsonFileName = "atunda/1.json";
        visualizer.loadFromStreamingAssets = true;
        visualizer.playOnStart = true;
        visualizer.poseScale = 1.25f;
        visualizer.jointRadius = 0.02f;
        visualizer.lineWidth = 0.015f;
        Selection.activeGameObject = go;
        Undo.RegisterCreatedObjectUndo(go, "Create Stick Figure Visualizer");
        Debug.Log("Created NpzStickFigure. Press Play to preview atunda/1.json.");
    }
}
