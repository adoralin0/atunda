using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps exported dance preview PNGs imported as sprites for scrollbar tiles.
/// </summary>
public static class DancePreviewImageImporter
{
    const string PreviewFolder = "Assets/UPoseExports/DancePNGs";

    [MenuItem("UPose/Refresh dance menu preview images")]
    public static void RefreshDanceMenuPreviewImages()
    {
        DancePreviewImageCatalog.ClearCache();
        DanceMiddleFramePngExporter.RefreshOpenDanceMenuPreviews();
        EditorUtility.DisplayDialog(
            "Dance preview PNGs",
            "Refreshed scrollbar preview images from Assets/UPoseExports/DancePNGs.",
            "OK");
    }

    [MenuItem("UPose/Copy dance preview PNGs to Resources")]
    public static void CopyPreviewsToResources()
    {
        string sourceFolder = Path.Combine(Application.dataPath, "UPoseExports", "DancePNGs");
        string targetFolder = Path.Combine(Application.dataPath, "Resources", DancePreviewImageCatalog.ResourcesFolder);
        Directory.CreateDirectory(sourceFolder);
        Directory.CreateDirectory(targetFolder);

        string[] files = Directory.GetFiles(sourceFolder, "*.png");
        if (files.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "Dance preview PNGs",
                "No PNG files were found in:\n" + sourceFolder,
                "OK");
            return;
        }

        int copied = 0;
        for (int i = 0; i < files.Length; i++)
        {
            string fileName = Path.GetFileName(files[i]);
            File.Copy(files[i], Path.Combine(targetFolder, fileName), overwrite: true);
            copied++;
        }

        AssetDatabase.Refresh();
        DancePreviewImageCatalog.ClearCache();
        DanceMiddleFramePngExporter.RefreshOpenDanceMenuPreviews();

        EditorUtility.DisplayDialog(
            "Dance preview PNGs",
            "Copied " + copied + " PNG files to Assets/Resources/" + DancePreviewImageCatalog.ResourcesFolder + ".",
            "OK");
    }

    public class SpritePostprocessor : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(PreviewFolder, System.StringComparison.OrdinalIgnoreCase)
                && !assetPath.Contains("/Resources/" + DancePreviewImageCatalog.ResourcesFolder + "/"))
            {
                return;
            }

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
        }
    }
}
