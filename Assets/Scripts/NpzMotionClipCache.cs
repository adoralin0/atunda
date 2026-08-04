using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Parses npz-json dance files once and reuses the clip for stick + avatar playback.
/// WebGL loads are async; Editor/Standalone can still read from disk synchronously.
/// </summary>
public static class NpzMotionClipCache
{
    static readonly Dictionary<string, NpzMotionClip> Clips =
        new Dictionary<string, NpzMotionClip>(System.StringComparer.OrdinalIgnoreCase);

    static readonly Dictionary<string, LoadOperation> InFlight =
        new Dictionary<string, LoadOperation>(System.StringComparer.OrdinalIgnoreCase);

    sealed class LoadOperation
    {
        public bool done;
        public bool success;
        public readonly List<System.Action<bool>> callbacks = new List<System.Action<bool>>();
    }

    public static bool IsCached(string subfolder, string danceId)
    {
        return TryGetCached(subfolder, danceId, out _);
    }

    public static bool TryGetCached(string subfolder, string danceId, out NpzMotionClip clip)
    {
        clip = null;
        if (!TryBuildCacheKey(subfolder, danceId, out string cacheKey))
        {
            return false;
        }

        return Clips.TryGetValue(cacheKey, out clip) && clip?.frames != null && clip.frames.Length > 0;
    }

    public static bool TryLoad(
        string subfolder,
        string danceId,
        bool preferCameraSpace,
        out NpzMotionClip clip)
    {
        clip = null;
        if (!TryBuildCacheKey(subfolder, danceId, out string cacheKey))
        {
            return false;
        }

        if (Clips.TryGetValue(cacheKey, out clip) && clip?.frames != null && clip.frames.Length > 0)
        {
            return true;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        clip = null;
        return false;
#else
        if (!TryReadStreamingJsonSync(cacheKey, out string jsonText))
        {
            return false;
        }

        return TryParseAndStore(cacheKey, jsonText, preferCameraSpace, out clip);
#endif
    }

    public static bool TryLoadFromJsonPath(string jsonPath, bool preferCameraSpace, out NpzMotionClip clip)
    {
        clip = null;
        if (string.IsNullOrWhiteSpace(jsonPath))
        {
            return false;
        }

        string normalized = jsonPath.Replace("\\", "/").Replace(".json", "");
        int slash = normalized.LastIndexOf('/');
        if (slash >= 0)
        {
            return TryLoad(normalized.Substring(0, slash), normalized.Substring(slash + 1), preferCameraSpace, out clip);
        }

        return TryLoad("atunda", normalized, preferCameraSpace, out clip);
    }

    public static void Release(string subfolder, string danceId)
    {
        if (!TryBuildCacheKey(subfolder, danceId, out string cacheKey))
        {
            return;
        }

        Clips.Remove(cacheKey);
    }

    public static IEnumerator LoadAsync(
        string subfolder,
        string danceId,
        bool preferCameraSpace,
        System.Action<bool> onComplete = null)
    {
        if (!TryBuildCacheKey(subfolder, danceId, out string cacheKey))
        {
            onComplete?.Invoke(false);
            yield break;
        }

        if (Clips.TryGetValue(cacheKey, out NpzMotionClip cached) && cached?.frames != null && cached.frames.Length > 0)
        {
            onComplete?.Invoke(true);
            yield break;
        }

        if (InFlight.TryGetValue(cacheKey, out LoadOperation existing))
        {
            bool finished = false;
            bool success = false;
            existing.callbacks.Add(result =>
            {
                success = result;
                finished = true;
            });

            while (!finished)
            {
                yield return null;
            }

            onComplete?.Invoke(success);
            yield break;
        }

        LoadOperation operation = new LoadOperation();
        InFlight[cacheKey] = operation;

        string jsonText = null;
        bool readOk = false;

#if UNITY_WEBGL && !UNITY_EDITOR
        yield return ReadStreamingJsonWebAsync(cacheKey, value => jsonText = value, result => readOk = result);
#else
        readOk = TryReadStreamingJsonSync(cacheKey, out jsonText);
#endif

        bool parsedOk = readOk && TryParseAndStore(cacheKey, jsonText, preferCameraSpace, out _);

        operation.done = true;
        operation.success = parsedOk;
        InFlight.Remove(cacheKey);

        onComplete?.Invoke(parsedOk);
        for (int i = 0; i < operation.callbacks.Count; i++)
        {
            operation.callbacks[i]?.Invoke(parsedOk);
        }
    }

    static bool TryBuildCacheKey(string subfolder, string danceId, out string cacheKey)
    {
        cacheKey = null;
        if (string.IsNullOrWhiteSpace(danceId))
        {
            return false;
        }

        string cleanId = danceId.Replace(".json", "").Replace("\\", "/");
        string folder = string.IsNullOrWhiteSpace(subfolder) ? "atunda" : subfolder.Replace("\\", "/");
        if (cleanId.Contains("/"))
        {
            int slash = cleanId.LastIndexOf('/');
            folder = cleanId.Substring(0, slash);
            cleanId = cleanId.Substring(slash + 1);
        }

        cacheKey = folder + "/" + cleanId;
        return true;
    }

    static bool TryParseAndStore(string cacheKey, string jsonText, bool preferCameraSpace, out NpzMotionClip clip)
    {
        clip = null;
        if (string.IsNullOrEmpty(jsonText) || !jsonText.Contains("npz-json"))
        {
            return false;
        }

        try
        {
            clip = NpzJsonMotionParser.Parse(jsonText, preferCameraSpace);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("NpzMotionClipCache: failed to parse '" + cacheKey + "': " + ex.Message);
            clip = null;
            return false;
        }

        if (clip?.frames == null || clip.frames.Length == 0)
        {
            clip = null;
            return false;
        }

        Clips[cacheKey] = clip;
        return true;
    }

    static bool TryReadStreamingJsonSync(string cacheKey, out string jsonText)
    {
        jsonText = null;
        if (!TrySplitCacheKey(cacheKey, out string subfolder, out string cleanId))
        {
            return false;
        }

        string path = Path.Combine(Application.streamingAssetsPath, subfolder, cleanId + ".json");
        if (!File.Exists(path))
        {
            return false;
        }

        jsonText = File.ReadAllText(path);
        return !string.IsNullOrEmpty(jsonText);
    }

    static IEnumerator ReadStreamingJsonWebAsync(
        string cacheKey,
        System.Action<string> assignText,
        System.Action<bool> assignSuccess)
    {
        assignText(null);
        assignSuccess(false);

        if (!TrySplitCacheKey(cacheKey, out string subfolder, out string cleanId))
        {
            yield break;
        }

        string url = AtundaDanceCatalog.BuildStreamingUrl(subfolder, cleanId + ".json");
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("NpzMotionClipCache: failed to download '" + url + "': " + request.error);
                yield break;
            }

            assignText(request.downloadHandler.text);
            assignSuccess(!string.IsNullOrEmpty(request.downloadHandler.text));
        }
    }

    static bool TrySplitCacheKey(string cacheKey, out string subfolder, out string cleanId)
    {
        subfolder = "atunda";
        cleanId = null;
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return false;
        }

        int slash = cacheKey.LastIndexOf('/');
        if (slash >= 0)
        {
            subfolder = cacheKey.Substring(0, slash);
            cleanId = cacheKey.Substring(slash + 1);
        }
        else
        {
            cleanId = cacheKey;
        }

        return !string.IsNullOrWhiteSpace(cleanId);
    }
}
