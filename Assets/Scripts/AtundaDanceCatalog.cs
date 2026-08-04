using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Lists dances in StreamingAssets/atunda. WebGL cannot scan folders, so it reads dances.manifest.json.
/// </summary>
public static class AtundaDanceCatalog
{
    const string ManifestFileName = "dances.manifest.json";

    static bool manifestLoaded;
    static readonly Dictionary<string, List<string>> manifestBySubfolder =
        new Dictionary<string, List<string>>(System.StringComparer.OrdinalIgnoreCase);

    public static bool IsManifestReady(string subfolder)
    {
        return manifestLoaded || manifestBySubfolder.ContainsKey(NormalizeSubfolder(subfolder));
    }

    public static IEnumerator EnsureManifestLoaded(string subfolder)
    {
        subfolder = NormalizeSubfolder(subfolder);
        if (manifestBySubfolder.ContainsKey(subfolder))
        {
            yield break;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        yield return LoadManifestWeb(subfolder);
#else
        LoadManifestLocal(subfolder);
        yield break;
#endif
    }

    public static List<string> GetDanceIds(string subfolder)
    {
        subfolder = NormalizeSubfolder(subfolder);
        if (manifestBySubfolder.TryGetValue(subfolder, out List<string> cached))
        {
            return new List<string>(cached);
        }

        LoadManifestLocal(subfolder);
        if (manifestBySubfolder.TryGetValue(subfolder, out cached))
        {
            return new List<string>(cached);
        }

        return new List<string>();
    }

    static void LoadManifestLocal(string subfolder)
    {
        List<string> ids = new List<string>();
        string folderPath = Path.Combine(Application.streamingAssetsPath, subfolder);
        string manifestPath = Path.Combine(folderPath, ManifestFileName);

        if (File.Exists(manifestPath))
        {
            TryParseManifest(File.ReadAllText(manifestPath), ids);
        }

        if (ids.Count == 0 && Directory.Exists(folderPath))
        {
            foreach (string filePath in Directory.GetFiles(folderPath, "*.json"))
            {
                string name = Path.GetFileNameWithoutExtension(filePath);
                if (string.IsNullOrEmpty(name)
                    || string.Equals(name, Path.GetFileNameWithoutExtension(ManifestFileName), System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ids.Add(name);
            }
        }

        manifestBySubfolder[subfolder] = ids;
        manifestLoaded = true;

        if (ids.Count == 0)
        {
            Debug.LogWarning("AtundaDanceCatalog: no dances found for subfolder '" + subfolder + "'.");
        }
    }

    static IEnumerator LoadManifestWeb(string subfolder)
    {
        List<string> ids = new List<string>();
        string manifestUrl = BuildStreamingUrl(subfolder, ManifestFileName);

        using (UnityWebRequest request = UnityWebRequest.Get(manifestUrl))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                TryParseManifest(request.downloadHandler.text, ids);
            }
            else
            {
                Debug.LogWarning(
                    "AtundaDanceCatalog: failed to load manifest at '" + manifestUrl +
                    "': " + request.error);
            }
        }

        manifestBySubfolder[subfolder] = ids;
        manifestLoaded = true;

        if (ids.Count == 0)
        {
            Debug.LogWarning("AtundaDanceCatalog: WebGL manifest was empty for '" + subfolder + "'.");
        }
        else
        {
            Debug.Log("AtundaDanceCatalog: loaded " + ids.Count + " dances from manifest.");
        }
    }

    static bool TryParseManifest(string json, List<string> ids)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            string[] parsed = JsonHelper.FromJsonArray<string>(json);
            if (parsed == null || parsed.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < parsed.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(parsed[i]))
                {
                    ids.Add(parsed[i].Trim());
                }
            }

            return ids.Count > 0;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("AtundaDanceCatalog: manifest parse failed: " + ex.Message);
            return false;
        }
    }

    public static string BuildStreamingUrl(string subfolder, string fileName)
    {
        string basePath = Application.streamingAssetsPath.TrimEnd('/');
        string relative = subfolder.Replace("\\", "/").Trim('/') + "/" + fileName.Replace("\\", "/");
        string combined = basePath + "/" + relative;
        return combined.Replace(" ", "%20");
    }

    static string NormalizeSubfolder(string subfolder)
    {
        return string.IsNullOrWhiteSpace(subfolder) ? "atunda" : subfolder.Replace("\\", "/").Trim('/');
    }
}

/// <summary>Minimal JSON array parser for string[] without pulling in Newtonsoft.</summary>
static class JsonHelper
{
    public static T[] FromJsonArray<T>(string json)
    {
        string wrapped = "{\"items\":" + json + "}";
        Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(wrapped);
        return wrapper?.items;
    }

    [System.Serializable]
    class Wrapper<T>
    {
        public T[] items;
    }
}
