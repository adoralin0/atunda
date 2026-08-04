using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Loads exported dance avatar PNGs for scrollbar menu tiles.
/// Editor: Assets/UPoseExports/DancePNGs. Builds: Resources/DancePreviews,
/// with StreamingAssets/dance_previews as a WebGL-friendly fallback.
/// </summary>
public static class DancePreviewImageCatalog
{
    public const string EditorFolder = "Assets/UPoseExports/DancePNGs";
    public const string ResourcesFolder = "DancePreviews";
    public const string StreamingFolder = "dance_previews";

    static readonly Dictionary<string, Sprite> cache =
        new Dictionary<string, Sprite>(System.StringComparer.OrdinalIgnoreCase);

    static readonly HashSet<string> loggedMissing =
        new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

    static readonly HashSet<string> streamingLoadAttempted =
        new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Old dance ids → current preview file names after renames.</summary>
    static readonly Dictionary<string, string> LegacyPreviewIds =
        new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "Alkayida", "Al-Qaeda move" },
            { "Al-Qaeda move", "Alkayida" },
        };

    public static Sprite TryLoadSprite(string danceId, bool logIfMissing = true)
    {
        if (string.IsNullOrWhiteSpace(danceId))
        {
            return null;
        }

        string key = NormalizeDanceId(danceId);
        if (cache.TryGetValue(key, out Sprite cached) && cached != null)
        {
            return cached;
        }

        Sprite sprite = LoadSprite(key);
        if (sprite != null)
        {
            cache[key] = sprite;
            return sprite;
        }

        if (logIfMissing && loggedMissing.Add(key))
        {
            Debug.LogWarning(
                "Dance preview PNG not found for '" + key + "'. Expected file:\n" +
                EditorFolder + "/" + ToPreviewFileName(key) +
                "\nor StreamingAssets/" + StreamingFolder + "/" + ToPreviewFileName(key) +
                "\nExport with UPose > Export all dance avatar PNGs into Assets/UPoseExports/DancePNGs.");
        }

        return null;
    }

    /// <summary>
    /// WebGL cannot read StreamingAssets from disk; fetch missing previews over HTTP.
    /// </summary>
    public static IEnumerator EnsureStreamingSprites(IEnumerable<string> danceIds)
    {
        if (danceIds == null)
        {
            yield break;
        }

        foreach (string danceId in danceIds)
        {
            if (string.IsNullOrWhiteSpace(danceId))
            {
                continue;
            }

            string key = NormalizeDanceId(danceId);
            if (cache.TryGetValue(key, out Sprite cached) && cached != null)
            {
                continue;
            }

            // Prefer Resources / editor / local disk first.
            Sprite local = LoadSprite(key);
            if (local != null)
            {
                cache[key] = local;
                continue;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            yield return LoadStreamingSpriteWeb(key);
#else
            yield return null;
#endif
        }
    }

    public static void ClearCache()
    {
        cache.Clear();
        loggedMissing.Clear();
        streamingLoadAttempted.Clear();
    }

    public static string ToPreviewFileName(string danceId)
    {
        return SanitizeFileName(NormalizeDanceId(danceId)) + ".png";
    }

    public static string NormalizeDanceId(string danceId)
    {
        string normalized = danceId.Replace("\\", "/").Trim();
        if (normalized.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(0, normalized.Length - 5);
        }

        int slash = normalized.LastIndexOf('/');
        if (slash >= 0 && slash < normalized.Length - 1)
        {
            normalized = normalized.Substring(slash + 1);
        }

        return normalized;
    }

    static Sprite LoadSprite(string danceId)
    {
        Sprite sprite = LoadSpriteByFileStem(SanitizeFileName(danceId), danceId);
        if (sprite != null)
        {
            return sprite;
        }

        if (LegacyPreviewIds.TryGetValue(danceId, out string legacyId))
        {
            sprite = LoadSpriteByFileStem(SanitizeFileName(legacyId), danceId);
            if (sprite != null)
            {
                return sprite;
            }
        }

        return null;
    }

    static Sprite LoadSpriteByFileStem(string fileName, string cacheDanceId)
    {
        string resourcesKey = ResourcesFolder + "/" + fileName;

        Sprite resourcesSprite = Resources.Load<Sprite>(resourcesKey);
        if (resourcesSprite != null)
        {
            return resourcesSprite;
        }

        Texture2D resourcesTexture = Resources.Load<Texture2D>(resourcesKey);
        if (resourcesTexture != null)
        {
            return CacheRuntimeSprite(resourcesTexture, cacheDanceId);
        }

#if UNITY_EDITOR
        string editorAssetPath = EditorFolder + "/" + fileName + ".png";
        Sprite editorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(editorAssetPath);
        if (editorSprite != null)
        {
            return editorSprite;
        }

        Texture2D editorTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(editorAssetPath);
        if (editorTexture != null)
        {
            return CacheRuntimeSprite(editorTexture, cacheDanceId);
        }

        string editorDiskPath = Path.Combine(Application.dataPath, "UPoseExports", "DancePNGs", fileName + ".png");
        Sprite editorDiskSprite = LoadSpriteFromDisk(editorDiskPath, cacheDanceId);
        if (editorDiskSprite != null)
        {
            return editorDiskSprite;
        }
#endif

        string streamingDiskPath = Path.Combine(Application.streamingAssetsPath, StreamingFolder, fileName + ".png");
        Sprite streamingSprite = LoadSpriteFromDisk(streamingDiskPath, cacheDanceId);
        if (streamingSprite != null)
        {
            return streamingSprite;
        }

        return null;
    }

    static IEnumerator LoadStreamingSpriteWeb(string danceId)
    {
        if (!streamingLoadAttempted.Add(danceId))
        {
            yield break;
        }

        List<string> candidates = new List<string> { danceId };
        if (LegacyPreviewIds.TryGetValue(danceId, out string legacyId))
        {
            candidates.Add(legacyId);
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            string url = AtundaDanceCatalog.BuildStreamingUrl(
                StreamingFolder,
                SanitizeFileName(candidates[i]) + ".png");

            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url, nonReadable: false))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    continue;
                }

                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                if (texture == null)
                {
                    continue;
                }

                texture.name = SanitizeFileName(danceId);
                cache[danceId] = CacheRuntimeSprite(texture, danceId);
                yield break;
            }
        }
    }

    static Sprite LoadSpriteFromDisk(string diskPath, string danceId)
    {
        if (!File.Exists(diskPath))
        {
            return null;
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(diskPath);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!texture.LoadImage(bytes))
            {
                Object.Destroy(texture);
                return null;
            }

            texture.name = Path.GetFileNameWithoutExtension(diskPath);
            return CacheRuntimeSprite(texture, danceId);
        }
        catch (IOException ex)
        {
            Debug.LogWarning("Failed to read dance preview PNG at '" + diskPath + "': " + ex.Message);
            return null;
        }
    }

    static Sprite CacheRuntimeSprite(Texture2D texture, string danceId)
    {
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        sprite.name = danceId + "_Preview";
        cache[danceId] = sprite;
        return sprite;
    }

    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "Dance";
        }

        char[] invalidChars = Path.GetInvalidFileNameChars();
        string sanitized = fileName;
        for (int i = 0; i < invalidChars.Length; i++)
        {
            sanitized = sanitized.Replace(invalidChars[i].ToString(), "_");
        }

        return sanitized;
    }
}
