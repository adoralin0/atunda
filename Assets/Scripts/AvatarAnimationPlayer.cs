using System.Collections;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

[DefaultExecutionOrder(-100)]
public class AvatarAnimationPlayer : MonoBehaviour
{
    [Header("Playback")]
    public float fps = 30f;
    public bool loop = true;
    public float playbackSpeed = 1f;
    public bool isPlaying = false;

    [Header("Sources")]
    public bool preferResourcesDance = false;
    public bool useLiveIkWhenStickSynced = true;
    public bool useStickFigurePoseWhenSynced = true;
    public bool loadStreamingNpzForSync = true;
    public string streamingNpzSubfolder = "atunda";
    public bool syncWithStickFigure = true;

    [Header("Root motion")]
    public bool applyRootMotion = true;
    public float rootMotionScale = 1.25f;

    [Header("Stick overlay")]
    public bool driveStickOverlay = true;

    private float[][] rotationFrames;
    private NpzMotionClip npzClip;
    private bool useLiveNpzIk;
    private bool useStickFigurePose;
    private Vector3 npzAxisFlip = new Vector3(1f, -1f, 1f);

    private NpzStickFigureVisualizer syncedStick;
    private float frameAccumulator;
    private bool skipNextAdvance;
    private float activeRootMotionScale = 1.25f;
    private Vector3 rootRotationOffsetEuler = Vector3.zero;
    private float[] currentRotations = new float[0];
    private Vector3 currentRootOffset = Vector3.zero;
    private ReadyPlayerAvatar avatarDriver;
    private bool ownsStickPlaybackDrive;
    private Coroutine webLoadRoutine;
    private string pendingWebLoadId;
    private string activeStreamingDanceId;

    public string CurrentDanceId { get; private set; }
    public float FrameAccumulator => frameAccumulator;
    public bool HasRootMotionData { get; private set; }
    public bool IsStickSynced => syncedStick != null;
    public bool UseStickFigurePose => useStickFigurePose;
    public NpzStickFigureVisualizer SyncedStick => syncedStick;

    public void SeekToFrame(int frameIndex)
    {
        int frameCount = GetTimingFrameCount();
        if (frameCount <= 0)
        {
            return;
        }

        frameIndex = Mathf.Clamp(frameIndex, 0, frameCount - 1);
        frameAccumulator = frameIndex;
        skipNextAdvance = true;
        ApplyCurrentFrame();
    }

    void Awake()
    {
        avatarDriver = GetComponent<ReadyPlayerAvatar>();
        if (IsMenuPreview())
        {
            driveStickOverlay = false;
            applyRootMotion = false;
            preferResourcesDance = false;
            syncWithStickFigure = true;
            useStickFigurePoseWhenSynced = true;
            useLiveIkWhenStickSynced = false;
        }
    }

    void OnDisable()
    {
        StopDance();
    }

    bool IsMenuPreview()
    {
        return gameObject.name.StartsWith("Preview", System.StringComparison.OrdinalIgnoreCase);
    }

    public void PlayDance(string fileName)
    {
        string cleanPath = fileName.Replace(".json", "");
#if UNITY_WEBGL && !UNITY_EDITOR
        if (loadStreamingNpzForSync && !NpzMotionClipCache.IsCached(streamingNpzSubfolder, cleanPath))
        {
            StopDance();
            if (webLoadRoutine != null)
            {
                StopCoroutine(webLoadRoutine);
            }

            pendingWebLoadId = cleanPath;
            webLoadRoutine = StartCoroutine(PlayDanceWhenCached(fileName));
            return;
        }
#endif
        PlayDanceInternal(fileName);
    }

    IEnumerator PlayDanceWhenCached(string fileName)
    {
        string cleanPath = fileName.Replace(".json", "");
        string requestedId = cleanPath;
        bool loaded = false;

        NpzMotionClipPreloader.Prioritize(cleanPath);
        yield return NpzMotionClipCache.LoadAsync(streamingNpzSubfolder, cleanPath, true, ok => loaded = ok);

        if (!loaded || pendingWebLoadId != requestedId)
        {
            yield break;
        }

        pendingWebLoadId = null;
        webLoadRoutine = null;
        PlayDanceInternal(fileName);
    }

    void PlayDanceInternal(string fileName)
    {
        string cleanPath = fileName.Replace(".json", "");
        ResetPlaybackState();

        bool isPreview = IsMenuPreview();
        bool hasResources = preferResourcesDance && TryLoadResourcesRotations(cleanPath);
        bool stickSynced = false;

        if (isPreview && syncWithStickFigure && loadStreamingNpzForSync)
        {
            PreviewDanceStickFigure.AlignToPreviewAvatar(transform);
            NpzStickFigureVisualizer previewStick = PreviewDanceStickFigure.GetStick();
            if (previewStick != null)
            {
                previewStick.TryLoadDance(cleanPath, streamingNpzSubfolder);
                stickSynced = TrySyncFromStickFigure(cleanPath, claimPlaybackDrive: true, previewStick);
                if (stickSynced && syncedStick != null)
                {
                    npzClip = syncedStick.MotionClip;
                }
            }
        }
        else if (!isPreview && syncWithStickFigure && loadStreamingNpzForSync)
        {
            TryEnsureStickFigureLoaded(cleanPath);
            stickSynced = TrySyncFromStickFigure(cleanPath, claimPlaybackDrive: true);
            if (stickSynced && syncedStick != null)
            {
                npzClip = syncedStick.MotionClip;
            }
        }

        bool hasNpz = npzClip != null && npzClip.frames != null && npzClip.frames.Length > 0;
        if (!hasNpz && loadStreamingNpzForSync)
        {
            hasNpz = TryLoadStreamingNpz(cleanPath);
        }

        if (!hasResources && !hasNpz)
        {
            Debug.LogWarning("Dance not found: " + cleanPath);
            return;
        }

        if (stickSynced)
        {
            activeStreamingDanceId = cleanPath;
            Debug.Log(
                "AvatarAnimationPlayer on '" + gameObject.name + "': Synced to stick '" + syncedStick.name +
                "' for '" + cleanPath + "' at frame " + syncedStick.CurrentFrameIndex +
                ", poseScale=" + activeRootMotionScale);
        }
        else if (syncWithStickFigure && hasNpz)
        {
            Debug.LogWarning(
                "AvatarAnimationPlayer: No active NpzStickFigureVisualizer found for dance '" +
                cleanPath + "'.");
        }

        useStickFigurePose = stickSynced && useStickFigurePoseWhenSynced;
        if (useStickFigurePose && syncedStick != null)
        {
            syncedStick.alignToAvatarPelvis = false;
            syncedStick.ClearFollowPelvis();
        }

        useLiveNpzIk = !useStickFigurePose && hasNpz && (stickSynced && useLiveIkWhenStickSynced || !hasResources);
        HasRootMotionData = useStickFigurePose
            || useLiveNpzIk
            || (hasResources && rotationFrames[0].Length >= RpmDanceConverter.FrameWithRootCount);

        isPlaying = true;
        CurrentDanceId = cleanPath;
        skipNextAdvance = true;

        if (driveStickOverlay && syncedStick != null && syncedStick.alignToAvatarPelvis
            && !useStickFigurePoseWhenSynced && avatarDriver != null)
        {
            Transform pelvis = avatarDriver.GetPelvisTransform();
            if (pelvis != null)
            {
                syncedStick.SetFollowPelvis(pelvis);
            }
        }

        ApplyCurrentFrame();

        string source = useStickFigurePose
            ? "stick figure keypoints"
            : useLiveNpzIk
                ? "StreamingAssets npz (live IK)"
                : hasResources ? "Resources/Dances (baked)" : "StreamingAssets npz";
        Debug.Log(
            "AvatarAnimationPlayer on '" + gameObject.name + "': Playing '" + cleanPath + "' from " + source +
            (IsStickSynced ? ", stick-synced" : ""));
    }

    bool TryLoadResourcesRotations(string cleanPath)
    {
        TextAsset jsonFile = Resources.Load<TextAsset>("Dances/" + cleanPath);
        if (jsonFile == null)
        {
            return false;
        }

        if (jsonFile.text.Contains("\"format\": \"npz-json\""))
        {
            return false;
        }

        float[][] frames = JsonConvert.DeserializeObject<float[][]>(jsonFile.text);
        if (frames == null || frames.Length == 0 || frames[0] == null)
        {
            Debug.LogWarning("Invalid dance data in Resources/Dances: " + cleanPath);
            return false;
        }

        int frameLength = frames[0].Length;
        if (frameLength != RpmDanceConverter.RotationCount && frameLength != RpmDanceConverter.FrameWithRootCount)
        {
            Debug.LogWarning(
                "Invalid legacy dance (need float[][19] or float[][22]) in Resources/Dances: " + cleanPath);
            return false;
        }

        rotationFrames = frames;
        activeRootMotionScale = rootMotionScale;
        rootRotationOffsetEuler = Vector3.zero;
        frameAccumulator = 0f;
        fps = 30f;

        if (Mathf.Abs(frames[0][0]) > 90f)
        {
            Debug.LogWarning(
                "Dance '" + cleanPath + "' frame 0 hipsY=" + frames[0][0] +
                " looks like a bad IK convert. Re-run UPose > Build dance from atunda/1.json.");
        }

        return true;
    }

    bool TryLoadStreamingNpz(string danceId)
    {
        if (!NpzMotionClipCache.TryLoad(streamingNpzSubfolder, danceId, true, out npzClip))
        {
            npzClip = null;
            return false;
        }

        activeStreamingDanceId = danceId;

        if (rotationFrames == null)
        {
            fps = npzClip.fps > 0f ? npzClip.fps : 60f;
            activeRootMotionScale = rootMotionScale;
        }

        return true;
    }

    bool TrySyncFromStickFigure(string danceId, bool claimPlaybackDrive, NpzStickFigureVisualizer requiredStick = null)
    {
        if (requiredStick != null)
        {
            if (requiredStick.IsClipLoaded && requiredStick.MatchesDanceId(danceId))
            {
                return BindSyncedStick(requiredStick, danceId, claimPlaybackDrive);
            }

            syncedStick = null;
            return false;
        }

        NpzStickFigureVisualizer[] visualizers = FindObjectsByType<NpzStickFigureVisualizer>(FindObjectsSortMode.None);
        NpzStickFigureVisualizer best = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < visualizers.Length; i++)
        {
            NpzStickFigureVisualizer stick = visualizers[i];
            if (stick == null || stick.headlessPlayback || !stick.IsClipLoaded || !stick.MatchesDanceId(danceId))
            {
                continue;
            }

            int score = 0;
            if (string.Equals(stick.gameObject.name, "NpzStickFigure", System.StringComparison.OrdinalIgnoreCase))
            {
                score += 1000;
            }

            if (stick.gameObject.activeInHierarchy)
            {
                score += 100;
            }

            score += Mathf.RoundToInt(stick.PoseScale * 10f);

            if (score > bestScore)
            {
                bestScore = score;
                best = stick;
            }
        }

        if (best == null)
        {
            syncedStick = null;
            return false;
        }

        return BindSyncedStick(best, danceId, claimPlaybackDrive);
    }

    bool BindSyncedStick(NpzStickFigureVisualizer stick, string danceId, bool claimPlaybackDrive)
    {
        fps = stick.EffectiveFps;
        playbackSpeed = stick.PlaybackSpeedSetting;
        activeRootMotionScale = stick.PoseScale;
        rootRotationOffsetEuler = stick.RotationOffsetEuler;
        npzAxisFlip = stick.AxisFlip;
        frameAccumulator = stick.FrameAccumulator;
        loop = stick.loop;
        if (!IsMenuPreview())
        {
            applyRootMotion = stick.ApplyCameraTranslation;
        }

        syncedStick = stick;
        frameAccumulator = NormalizeAccumulator(stick.FrameAccumulator, npzClip?.frames?.Length ?? syncedStick.MotionClip?.frames?.Length ?? 0);
        ownsStickPlaybackDrive = claimPlaybackDrive;
        if (claimPlaybackDrive)
        {
            syncedStick.SetExternalPlaybackDrive(true);
            syncedStick.SyncPlayback(frameAccumulator);
        }

        return true;
    }

    void TryEnsureStickFigureLoaded(string danceId)
    {
        NpzStickFigureVisualizer stick = FindPrimaryStickFigure();
        if (stick == null)
        {
            return;
        }

        if (stick.IsClipLoaded && stick.MatchesDanceId(danceId))
        {
            return;
        }

        if (stick.TryLoadDance(danceId, streamingNpzSubfolder))
        {
            Debug.Log(
                "AvatarAnimationPlayer: Loaded stick dance '" + danceId +
                "' on '" + stick.name + "'.");
        }
        else
        {
            Debug.LogWarning(
                "AvatarAnimationPlayer: Failed to load stick dance '" + danceId +
                "' from StreamingAssets/" + streamingNpzSubfolder + "/.");
        }
    }

    static NpzStickFigureVisualizer FindPrimaryStickFigure()
    {
        NpzStickFigureVisualizer[] visualizers = FindObjectsByType<NpzStickFigureVisualizer>(FindObjectsSortMode.None);
        NpzStickFigureVisualizer best = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < visualizers.Length; i++)
        {
            NpzStickFigureVisualizer stick = visualizers[i];
            if (stick == null || stick.headlessPlayback)
            {
                continue;
            }

            int score = 0;
            if (string.Equals(stick.gameObject.name, "NpzStickFigure", System.StringComparison.OrdinalIgnoreCase))
            {
                score += 1000;
            }

            if (stick.gameObject.activeInHierarchy)
            {
                score += 100;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = stick;
            }
        }

        return best;
    }

    float NormalizeAccumulator(float accumulator, int frameCount)
    {
        if (frameCount <= 0)
        {
            return accumulator;
        }

        if (!loop)
        {
            return Mathf.Clamp(accumulator, 0f, frameCount - 1);
        }

        return accumulator;
    }

    public void StopDance()
    {
        pendingWebLoadId = null;
        if (webLoadRoutine != null)
        {
            StopCoroutine(webLoadRoutine);
            webLoadRoutine = null;
        }

        ResetPlaybackState();
        CurrentDanceId = null;
    }

    void ResetPlaybackState()
    {
        if (!string.IsNullOrEmpty(activeStreamingDanceId))
        {
            NpzMotionClipCache.Release(streamingNpzSubfolder, activeStreamingDanceId);
            activeStreamingDanceId = null;
        }

        if (syncedStick != null)
        {
            syncedStick.ClearFollowPelvis();
            if (ownsStickPlaybackDrive)
            {
                syncedStick.SetExternalPlaybackDrive(false);
            }
        }

        ownsStickPlaybackDrive = false;
        isPlaying = false;
        rotationFrames = null;
        npzClip = null;
        useLiveNpzIk = false;
        useStickFigurePose = false;
        syncedStick = null;
        frameAccumulator = 0f;
        skipNextAdvance = false;
        currentRotations = new float[0];
        currentRootOffset = Vector3.zero;
        rootRotationOffsetEuler = Vector3.zero;
        HasRootMotionData = false;
    }

    public float[] getRotations()
    {
        return currentRotations;
    }

    public Vector3 GetRootOffset()
    {
        if (!applyRootMotion)
        {
            return Vector3.zero;
        }

        Quaternion rotation = Quaternion.Euler(rootRotationOffsetEuler);
        return rotation * currentRootOffset;
    }

    void Update()
    {
        if (!isPlaying)
        {
            return;
        }

        if (syncedStick != null && syncedStick.IsClipLoaded)
        {
            if (ownsStickPlaybackDrive)
            {
                AdvanceFrameAccumulator();
                syncedStick.SyncPlayback(frameAccumulator);
            }
            else
            {
                frameAccumulator = syncedStick.FrameAccumulator;
            }

            skipNextAdvance = false;
        }
        else if (skipNextAdvance)
        {
            skipNextAdvance = false;
        }
        else
        {
            AdvanceFrameAccumulator();
        }

        ApplyCurrentFrame();
    }

    void AdvanceFrameAccumulator()
    {
        int frameCount = GetTimingFrameCount();
        if (frameCount <= 0)
        {
            return;
        }

        float effectiveFps = fps * Mathf.Max(playbackSpeed, 0.0001f);
        frameAccumulator += Time.deltaTime * effectiveFps;
        frameAccumulator = NormalizeAccumulator(frameAccumulator, frameCount);

        if (!loop)
        {
            frameAccumulator = Mathf.Clamp(frameAccumulator, 0f, frameCount - 1);
        }
    }

    int GetTimingFrameCount()
    {
        if (syncedStick != null && syncedStick.IsClipLoaded && syncedStick.MotionClip?.frames != null)
        {
            return syncedStick.MotionClip.frames.Length;
        }

        if (npzClip?.frames != null && npzClip.frames.Length > 0)
        {
            return npzClip.frames.Length;
        }

        return rotationFrames?.Length ?? 0;
    }

    int GetFrameIndexFromAccumulator()
    {
        int frameCount = GetTimingFrameCount();
        if (frameCount <= 0)
        {
            return 0;
        }

        int frameIndex = Mathf.FloorToInt(frameAccumulator);
        if (loop)
        {
            frameIndex %= frameCount;
            if (frameIndex < 0)
            {
                frameIndex += frameCount;
            }
        }
        else
        {
            frameIndex = Mathf.Clamp(frameIndex, 0, frameCount - 1);
        }

        return frameIndex;
    }

    void ApplyCurrentFrame()
    {
        if (useStickFigurePose)
        {
            return;
        }

        if (useLiveNpzIk)
        {
            ApplyLiveNpzFrame();
            return;
        }

        if (rotationFrames == null || rotationFrames.Length == 0)
        {
            return;
        }

        int frameIndex = MapSyncedFrameToRotationIndex(rotationFrames.Length);
        float[] frame = rotationFrames[frameIndex];

        if (IsStickSynced && npzClip?.frames != null && npzClip.frames.Length > 0)
        {
            int stickFrame = GetSyncedFrameIndex(npzClip.frames.Length);
            frame = RpmDanceConverter.AppendRootOffset(frame, npzClip.frames[stickFrame], npzAxisFlip);
        }

        ApplyFrameData(frame);
    }

    void ApplyLiveNpzFrame()
    {
        if (npzClip?.frames == null || npzClip.frames.Length == 0)
        {
            return;
        }

        int frameIndex = GetSyncedFrameIndex(npzClip.frames.Length);
        float[] frame = RpmDanceConverter.ConvertFrame(npzClip.frames[frameIndex], npzAxisFlip);
        ApplyFrameData(frame);
    }

    int MapSyncedFrameToRotationIndex(int rotationFrameCount)
    {
        if (rotationFrameCount <= 1)
        {
            return 0;
        }

        if (syncedStick != null && syncedStick.IsClipLoaded && npzClip?.frames != null && npzClip.frames.Length > 1)
        {
            int stickFrame = Mathf.Clamp(syncedStick.CurrentFrameIndex, 0, npzClip.frames.Length - 1);
            float normalized = stickFrame / (float)(npzClip.frames.Length - 1);
            return Mathf.Clamp(Mathf.RoundToInt(normalized * (rotationFrameCount - 1)), 0, rotationFrameCount - 1);
        }

        return GetSyncedFrameIndex(rotationFrameCount);
    }

    int GetSyncedFrameIndex(int frameCount)
    {
        if (syncedStick != null && syncedStick.IsClipLoaded)
        {
            int stickFrame = syncedStick.CurrentFrameIndex;
            if (stickFrame < 0)
            {
                stickFrame = 0;
            }

            return Mathf.Clamp(stickFrame, 0, frameCount - 1);
        }

        int frameIndex = Mathf.FloorToInt(frameAccumulator);
        if (loop)
        {
            frameIndex %= frameCount;
            if (frameIndex < 0)
            {
                frameIndex += frameCount;
            }
        }
        else
        {
            frameIndex = Mathf.Clamp(frameIndex, 0, frameCount - 1);
        }

        return frameIndex;
    }

    void ApplyFrameData(float[] frame)
    {
        int rotationCount = Mathf.Min(frame.Length, RpmDanceConverter.RotationCount);
        currentRotations = new float[rotationCount];
        for (int i = 0; i < rotationCount; i++)
        {
            currentRotations[i] = frame[i];
        }

        currentRootOffset = HasRootMotionData && !useStickFigurePose
            ? RpmDanceConverter.ReadRootOffset(frame) * activeRootMotionScale
            : Vector3.zero;
    }
}
