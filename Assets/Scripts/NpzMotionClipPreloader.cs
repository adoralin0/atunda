using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Downloads and parses dance clips in the background so hover/click does not freeze WebGL.
/// </summary>
public class NpzMotionClipPreloader : MonoBehaviour
{
    static NpzMotionClipPreloader activeInstance;

    readonly Queue<string> preloadQueue = new Queue<string>();
    readonly HashSet<string> queuedIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
    string subfolder = "atunda";
    Coroutine preloadRoutine;
    string priorityDanceId;

    public static void BeginPreload(MonoBehaviour host, string danceSubfolder, IList<string> danceIds)
    {
        if (host == null || danceIds == null || danceIds.Count == 0)
        {
            return;
        }

        NpzMotionClipPreloader preloader = host.GetComponent<NpzMotionClipPreloader>();
        if (preloader == null)
        {
            preloader = host.gameObject.AddComponent<NpzMotionClipPreloader>();
        }

        preloader.ConfigureAndStart(danceSubfolder, danceIds);
    }

    public static void Prioritize(string danceId)
    {
        if (activeInstance == null || string.IsNullOrWhiteSpace(danceId))
        {
            return;
        }

        activeInstance.priorityDanceId = danceId.Replace(".json", "");
    }

    void ConfigureAndStart(string danceSubfolder, IList<string> danceIds)
    {
        activeInstance = this;
        subfolder = string.IsNullOrWhiteSpace(danceSubfolder) ? "atunda" : danceSubfolder;
        preloadQueue.Clear();
        queuedIds.Clear();

        if (preloadRoutine != null)
        {
            StopCoroutine(preloadRoutine);
            preloadRoutine = null;
        }
    }

    IEnumerator PreloadRoutine()
    {
        while (preloadQueue.Count > 0 || !string.IsNullOrEmpty(priorityDanceId))
        {
            string danceId = DequeueNext();
            if (string.IsNullOrWhiteSpace(danceId))
            {
                yield return null;
                continue;
            }

            if (NpzMotionClipCache.IsCached(subfolder, danceId))
            {
                yield return null;
                continue;
            }

            bool loaded = false;
            yield return NpzMotionClipCache.LoadAsync(subfolder, danceId, true, ok => loaded = ok);

            if (loaded)
            {
                Debug.Log("NpzMotionClipPreloader: cached '" + danceId + "'.");
            }

            yield return null;
        }

        preloadRoutine = null;
    }

    string DequeueNext()
    {
        if (!string.IsNullOrEmpty(priorityDanceId))
        {
            string danceId = priorityDanceId;
            priorityDanceId = null;
            queuedIds.Remove(danceId);
            return danceId;
        }

        while (preloadQueue.Count > 0)
        {
            string next = preloadQueue.Dequeue();
            queuedIds.Remove(next);
            return next;
        }

        return null;
    }

    void OnDestroy()
    {
        if (activeInstance == this)
        {
            activeInstance = null;
        }
    }
}
