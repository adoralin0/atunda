using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Debug viewer for npz-json-v1 motion (ATUNDA / SAM-body4d).
/// Draws a stick figure from 3D keypoints so you can verify parsing before retargeting to an avatar.
/// </summary>
[DefaultExecutionOrder(-200)]
public class NpzStickFigureVisualizer : MonoBehaviour
{
    [Header("Source")]
    public TextAsset sourceJson;
    public string jsonFileName = "atunda/1.json";
    public bool loadFromStreamingAssets = true;
    public bool preferCameraSpace = true;

    [Header("Playback")]
    public bool playOnStart = false;
    public bool loop = true;
    public float playbackSpeed = 1f;

    [Header("Pose")]
    public float poseScale = 1.25f;
    public bool centerOnHips = true;
    public bool applyCameraTranslation = true;
    public bool flipY = true;
    public bool flipZ = false;
    public Vector3 positionOffset = Vector3.zero;
    public Vector3 rotationOffsetEuler = Vector3.zero;

    [Header("Avatar align")]
    [Tooltip("When enabled, stick pose follows the avatar pelvis (hides world travel on the overlay).")]
    public bool alignToAvatarPelvis = false;
    public bool autoScaleToAvatar = true;
    public float skeletonScaleMultiplier = 1f;

    [Header("Root motion (pred_cam_t)")]
    [Tooltip("Apply mean-centered SAM pred_cam_t so the stick travels in world space.")]
    public bool moveRootInWorldSpace = true;

    [Header("Drawing")]
    public bool headlessPlayback = false;
    public bool drawJointMarkers = true;
    public float jointRadius = 0.02f;
    public float lineWidth = 0.015f;
    public Color jointColor = Color.white;
    public Color lineColor = Color.green;

    static readonly (string a, string b)[] BonePairs =
    {
        ("neck", "nose"),
        ("nose", "left-eye"),
        ("nose", "right-eye"),
        ("left-eye", "left-ear"),
        ("right-eye", "right-ear"),
        ("neck", "left-shoulder"),
        ("neck", "right-shoulder"),
        ("left-shoulder", "right-shoulder"),
        ("left-shoulder", "left-elbow"),
        ("left-elbow", "left-wrist"),
        ("right-shoulder", "right-elbow"),
        ("right-elbow", "right-wrist"),
        ("left-hip", "right-hip"),
        ("left-hip", "left-knee"),
        ("left-knee", "left-ankle"),
        ("left-ankle", "left-heel"),
        ("left-ankle", "left-big-toe-tip"),
        ("right-hip", "right-knee"),
        ("right-knee", "right-ankle"),
        ("right-ankle", "right-heel"),
        ("right-ankle", "right-big-toe-tip"),
    };

    static readonly string[] CoreJoints =
    {
        "neck", "nose", "left-shoulder", "right-shoulder", "left-elbow", "right-elbow",
        "left-wrist", "right-wrist", "left-hip", "right-hip", "left-knee", "right-knee",
        "left-ankle", "right-ankle",
    };

    NpzMotionClip clip;
    float frameAccumulator;
    int currentFrameIndex;
    Transform followPelvis;
    Transform followLeftFoot;
    Transform followRightFoot;
    float followSkeletonScale = 1f;
    bool loggedSkeletonScale;
    Vector3 playbackOrigin;
    Vector3 frameZeroRawHipMid;
    bool hasFrameZeroRawHipMid;
    bool loggedRootTravel;
    bool drivenExternally;
    readonly Dictionary<string, Vector3> smoothedJointPositions = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
    [SerializeField] float displaySmoothing = 12f;

    public bool IsClipLoaded => clip?.frames != null && clip.frames.Length > 0;
    public NpzMotionClip MotionClip => clip;
    public int CurrentFrameIndex => currentFrameIndex;
    public float FrameAccumulator => frameAccumulator;
    public float EffectiveFps => clip != null && clip.fps > 0f ? clip.fps : 60f;
    public float PoseScale => poseScale;
    public float PlaybackSpeedSetting => playbackSpeed;
    public Vector3 RotationOffsetEuler => rotationOffsetEuler;
    public Vector3 PositionOffset => positionOffset;
    public bool ApplyCameraTranslation => applyCameraTranslation;
    public Vector3 AxisFlip => GetAxisFlip();

    public void SetExternalPlaybackDrive(bool enabled)
    {
        drivenExternally = enabled;
    }

    public void SyncPlayback(float accumulator)
    {
        frameAccumulator = NormalizeAccumulator(accumulator);
        currentFrameIndex = GetFrameIndexFromAccumulator();
    }

    public void SetPlaybackOrigin(Vector3 worldOrigin)
    {
        playbackOrigin = worldOrigin;
    }

    float NormalizeAccumulator(float accumulator)
    {
        if (clip?.frames == null || clip.frames.Length == 0)
        {
            return accumulator;
        }

        if (!loop)
        {
            return Mathf.Clamp(accumulator, 0f, clip.frames.Length - 1);
        }

        return accumulator;
    }

    public bool TryGetCurrentMotionFrame(out NpzMotionFrame frame)
    {
        frame = default;
        if (!IsClipLoaded || currentFrameIndex < 0 || currentFrameIndex >= clip.frames.Length)
        {
            return false;
        }

        frame = clip.frames[currentFrameIndex];
        return true;
    }

    public bool TryGetPlaybackSample(out NpzMotionFrame frame0, out NpzMotionFrame frame1, out float blend)
    {
        frame0 = default;
        frame1 = default;
        blend = 0f;
        if (!IsClipLoaded)
        {
            return false;
        }

        int index0 = GetFrameIndexFromAccumulator();
        int index1 = GetNextFrameIndex(index0);
        frame0 = clip.frames[index0];
        frame1 = clip.frames[index1];
        blend = GetFrameBlend();
        return true;
    }

    public bool TryGetHipWorldPosition(out Vector3 worldPos)
    {
        worldPos = default;
        if (!IsClipLoaded || currentFrameIndex < 0 || currentFrameIndex >= clip.frames.Length)
        {
            return false;
        }

        if (!TryGetJointWorldPosition("left-hip", out Vector3 left)
            || !TryGetJointWorldPosition("right-hip", out Vector3 right))
        {
            return false;
        }

        worldPos = (left + right) * 0.5f;
        return true;
    }

    public bool TryGetJointWorldPosition(string jointName, out Vector3 worldPos)
    {
        if (TryGetDisplayedJointWorldPosition(jointName, out worldPos))
        {
            return true;
        }

        return TryGetInterpolatedJointWorldPosition(jointName, out worldPos);
    }

    /// <summary>
    /// Sub-frame joint position without display smoothing (consistent skeleton for IK retarget).
    /// </summary>
    public bool TryGetInterpolatedJointWorldPosition(string jointName, out Vector3 worldPos)
    {
        worldPos = default;
        if (!IsClipLoaded || string.IsNullOrEmpty(jointName))
        {
            return false;
        }

        int frame0 = GetFrameIndexFromAccumulator();
        int frame1 = GetNextFrameIndex(frame0);
        float blend = GetFrameBlend();
        if (!NpzJsonMotionParser.TryGetPoint(clip.frames[frame0], jointName, out _))
        {
            return false;
        }

        worldPos = MapPointInterpolated(clip.frames[frame0], clip.frames[frame1], blend, jointName);
        return true;
    }

    public bool TryGetDisplayedJointWorldPosition(string jointName, out Vector3 worldPos)
    {
        worldPos = default;
        if (string.IsNullOrEmpty(jointName))
        {
            return false;
        }

        if (jointMarkers.TryGetValue(jointName, out Transform marker)
            && marker != null
            && marker.gameObject.activeSelf)
        {
            worldPos = marker.position;
            return true;
        }

        return false;
    }

    public bool MatchesDanceId(string danceId)
    {
        if (!IsClipLoaded || string.IsNullOrWhiteSpace(danceId))
        {
            return false;
        }

        string normalized = danceId.Replace(".json", "");
        if (string.IsNullOrWhiteSpace(jsonFileName))
        {
            return false;
        }

        string stickId = Path.GetFileNameWithoutExtension(jsonFileName);
        return string.Equals(stickId, normalized, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Load npz-json from StreamingAssets/{subfolder}/{danceId}.json and start playback.
    /// </summary>
    public bool TryLoadDance(string danceId, string subfolder = "atunda")
    {
        if (string.IsNullOrWhiteSpace(danceId))
        {
            return false;
        }

        string cleanId = danceId.Replace(".json", "").Replace("\\", "/");
        if (cleanId.Contains("/"))
        {
            jsonFileName = cleanId + ".json";
        }
        else
        {
            jsonFileName = subfolder + "/" + cleanId + ".json";
        }

        drivenExternally = false;
        return BeginPlaybackFromLoadedClip(cleanId, verboseLogs: !headlessPlayback);
    }

    bool BeginPlaybackFromLoadedClip(string danceId, bool verboseLogs)
    {
        if (!TryLoadClip(out NpzMotionClip loaded))
        {
            return false;
        }

        clip = loaded;
        CaptureFrameZeroHipOrigin();
        playbackOrigin = transform.position;
        frameAccumulator = 0f;
        currentFrameIndex = 0;
        smoothedJointPositions.Clear();

        if (!headlessPlayback && visualsRoot == null)
        {
            BuildVisuals();
        }

        if (verboseLogs)
        {
            LogFirstFrameDiagnostics();
            LogRootTravelRange();
        }

        ApplyFrame(0);
        return IsClipLoaded && MatchesDanceId(danceId);
    }

    Transform visualsRoot;
    LineRenderer[] boneLines;
    readonly Dictionary<string, Transform> jointMarkers = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
    Material lineMaterial;
    Material jointMaterial;

    void Start()
    {
        if (IsDuplicateNonPrimary())
        {
            return;
        }

        if (playOnStart)
        {
            LoadAndPlay();
        }
    }

    bool IsDuplicateNonPrimary()
    {
        if (string.Equals(gameObject.name, "NpzStickFigure", StringComparison.OrdinalIgnoreCase)
            || string.Equals(gameObject.name, PreviewDanceStickFigure.StickObjectName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        NpzStickFigureVisualizer[] visualizers = FindObjectsByType<NpzStickFigureVisualizer>(FindObjectsSortMode.None);
        for (int i = 0; i < visualizers.Length; i++)
        {
            NpzStickFigureVisualizer other = visualizers[i];
            if (other == null || other == this)
            {
                continue;
            }

            if (!string.Equals(other.gameObject.name, "NpzStickFigure", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(other.jsonFileName, jsonFileName, StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log(
                    "NpzStickFigureVisualizer: disabling duplicate on '" + name +
                    "' (using 'NpzStickFigure' instead).");
                gameObject.SetActive(false);
                return true;
            }
        }

        return false;
    }

    public void SetFollowPelvis(Transform pelvis)
    {
        followPelvis = pelvis;
    }

    public void ClearFollowPelvis()
    {
        followPelvis = null;
        followLeftFoot = null;
        followRightFoot = null;
        followSkeletonScale = 1f;
        loggedSkeletonScale = false;
    }

    public void AlignToPelvis(Transform pelvis, Transform leftFoot = null, Transform rightFoot = null)
    {
        if (!alignToAvatarPelvis || pelvis == null || !IsClipLoaded)
        {
            return;
        }

        followPelvis = pelvis;
        if (leftFoot != null)
        {
            followLeftFoot = leftFoot;
        }

        if (rightFoot != null)
        {
            followRightFoot = rightFoot;
        }

        NpzMotionFrame frame = clip.frames[currentFrameIndex];
        UpdateFollowSkeletonScale(frame, pelvis, followLeftFoot, followRightFoot);
    }

    void Update()
    {
        if (clip?.frames == null || clip.frames.Length == 0 || drivenExternally)
        {
            return;
        }

        float fps = clip.fps > 0f ? clip.fps : 60f;
        frameAccumulator = NormalizeAccumulator(frameAccumulator + Time.deltaTime * playbackSpeed * fps);

        int nextIndex = GetFrameIndexFromAccumulator();
        if (nextIndex != currentFrameIndex)
        {
            currentFrameIndex = nextIndex;
        }
    }

    void LateUpdate()
    {
        if (clip?.frames == null || clip.frames.Length == 0 || boneLines == null)
        {
            return;
        }

        RefreshInterpolatedVisuals();
    }

    int GetFrameIndexFromAccumulator()
    {
        int nextIndex = Mathf.FloorToInt(frameAccumulator);
        if (loop)
        {
            nextIndex %= clip.frames.Length;
            if (nextIndex < 0)
            {
                nextIndex += clip.frames.Length;
            }
        }
        else
        {
            nextIndex = Mathf.Clamp(nextIndex, 0, clip.frames.Length - 1);
        }

        return nextIndex;
    }

    float GetFrameBlend()
    {
        if (clip?.frames == null || clip.frames.Length <= 1)
        {
            return 0f;
        }

        return frameAccumulator - Mathf.Floor(frameAccumulator);
    }

    int GetNextFrameIndex(int frameIndex)
    {
        if (clip?.frames == null || clip.frames.Length <= 1)
        {
            return frameIndex;
        }

        if (loop)
        {
            return (frameIndex + 1) % clip.frames.Length;
        }

        return Mathf.Min(frameIndex + 1, clip.frames.Length - 1);
    }

    [ContextMenu("Load And Play")]
    public void LoadAndPlay()
    {
        if (!TryLoadClip(out NpzMotionClip loaded))
        {
            Debug.LogError("NpzStickFigureVisualizer: No JSON clip loaded.");
            return;
        }

        clip = loaded;
        CaptureFrameZeroHipOrigin();

        if (!headlessPlayback && visualsRoot == null)
        {
            BuildVisuals();
        }

        playbackOrigin = transform.position;
        frameAccumulator = 0f;
        currentFrameIndex = 0;
        smoothedJointPositions.Clear();
        LogFirstFrameDiagnostics();
        LogRootTravelRange();
        ApplyFrame(0);
    }

    bool TryLoadClip(out NpzMotionClip loaded)
    {
        loaded = null;
        if (sourceJson != null)
        {
            try
            {
                loaded = NpzJsonMotionParser.Parse(sourceJson.text, preferCameraSpace);
                return loaded?.frames != null && loaded.frames.Length > 0;
            }
            catch (Exception ex)
            {
                Debug.LogError("NpzStickFigureVisualizer: Failed to parse assigned TextAsset: " + ex.Message);
                return false;
            }
        }

        if (loadFromStreamingAssets && !string.IsNullOrWhiteSpace(jsonFileName))
        {
            return NpzMotionClipCache.TryLoadFromJsonPath(jsonFileName, preferCameraSpace, out loaded);
        }

        return false;
    }

    void RefreshInterpolatedVisuals()
    {
        int frame0 = GetFrameIndexFromAccumulator();
        currentFrameIndex = frame0;
        int frame1 = GetNextFrameIndex(frame0);
        float blend = GetFrameBlend();
        float smoothT = 1f - Mathf.Exp(-displaySmoothing * Time.deltaTime);

        if (followPelvis != null && alignToAvatarPelvis)
        {
            UpdateFollowSkeletonScale(clip.frames[frame0], followPelvis, followLeftFoot, followRightFoot);
        }

        for (int i = 0; i < BonePairs.Length; i++)
        {
            (string a, string b) pair = BonePairs[i];
            bool hasA = TryMapPointInterpolated(frame0, frame1, blend, pair.a, out Vector3 posA);
            bool hasB = TryMapPointInterpolated(frame0, frame1, blend, pair.b, out Vector3 posB);

            LineRenderer line = boneLines[i];
            line.enabled = hasA && hasB;
            if (hasA && hasB)
            {
                line.SetPosition(0, SmoothJointPosition(pair.a, posA, smoothT));
                line.SetPosition(1, SmoothJointPosition(pair.b, posB, smoothT));
            }
        }

        foreach (KeyValuePair<string, Transform> entry in jointMarkers)
        {
            if (TryMapPointInterpolated(frame0, frame1, blend, entry.Key, out Vector3 pos))
            {
                entry.Value.position = SmoothJointPosition(entry.Key, pos, smoothT);
                entry.Value.gameObject.SetActive(true);
            }
            else
            {
                smoothedJointPositions.Remove(entry.Key);
                entry.Value.gameObject.SetActive(false);
            }
        }
    }

    Vector3 SmoothJointPosition(string jointName, Vector3 target, float smoothT)
    {
        if (displaySmoothing <= 0f)
        {
            return target;
        }

        if (!smoothedJointPositions.TryGetValue(jointName, out Vector3 current))
        {
            smoothedJointPositions[jointName] = target;
            return target;
        }

        Vector3 next = Vector3.Lerp(current, target, smoothT);
        smoothedJointPositions[jointName] = next;
        return next;
    }

    void BuildVisuals()
    {
        ClearVisuals();

        visualsRoot = new GameObject("StickFigure").transform;
        visualsRoot.SetParent(transform, false);

        lineMaterial = new Material(Shader.Find("Sprites/Default"));
        jointMaterial = new Material(Shader.Find("Sprites/Default"));

        boneLines = new LineRenderer[BonePairs.Length];
        for (int i = 0; i < BonePairs.Length; i++)
        {
            GameObject lineObj = new GameObject(BonePairs[i].a + "->" + BonePairs[i].b);
            lineObj.transform.SetParent(visualsRoot, false);
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.material = lineMaterial;
            lr.startColor = lineColor;
            lr.endColor = lineColor;
            boneLines[i] = lr;
        }

        if (drawJointMarkers)
        {
            HashSet<string> jointsToDraw = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < BonePairs.Length; i++)
            {
                jointsToDraw.Add(BonePairs[i].a);
                jointsToDraw.Add(BonePairs[i].b);
            }

            foreach (string jointName in jointsToDraw)
            {
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = jointName;
                marker.transform.SetParent(visualsRoot, false);
                marker.transform.localScale = Vector3.one * jointRadius * 2f;
                Destroy(marker.GetComponent<Collider>());
                marker.GetComponent<Renderer>().material = jointMaterial;
                marker.GetComponent<Renderer>().material.color = jointColor;
                jointMarkers[jointName] = marker.transform;
            }
        }
    }

    void ClearVisuals()
    {
        jointMarkers.Clear();
        smoothedJointPositions.Clear();
        boneLines = null;

        if (visualsRoot != null)
        {
            Destroy(visualsRoot.gameObject);
            visualsRoot = null;
        }
    }

    void LogFirstFrameDiagnostics()
    {
        if (clip?.frames == null || clip.frames.Length == 0)
        {
            return;
        }

        NpzMotionFrame frame = clip.frames[0];
        Debug.Log(
            "NpzStickFigureVisualizer: Loaded clip frames=" + clip.frames.Length +
            ", fps=" + clip.fps +
            ", keypoints=" + (frame.keypoints?.Length ?? 0) +
            ", source=" + clip.source +
            ", applyCameraTranslation=" + applyCameraTranslation);

        if (applyCameraTranslation && NpzJsonMotionParser.TryGetRootOffset(frame, GetAxisFlip(), out Vector3 root0))
        {
            Debug.Log("  frame 0 pred_cam_t (centered, mapped)=" + root0);
            if (moveRootInWorldSpace)
            {
                Debug.Log("  frame 0 world root anchor=" + GetWorldRootAnchor(frame));
            }
        }

        for (int i = 0; i < CoreJoints.Length; i++)
        {
            if (NpzJsonMotionParser.TryGetPoint(frame, CoreJoints[i], out Vector3 raw))
            {
                Vector3 mapped = MapPoint(frame, CoreJoints[i]);
                Debug.Log("  joint '" + CoreJoints[i] + "' raw=" + raw + " mapped=" + mapped);
            }
            else
            {
                Debug.LogWarning("  joint '" + CoreJoints[i] + "' NOT FOUND in frame 0");
            }
        }
    }

    void LogRootTravelRange()
    {
        if (loggedRootTravel || clip?.frames == null || clip.frames.Length == 0 || !applyCameraTranslation)
        {
            return;
        }

        loggedRootTravel = true;
        Vector3 min = Vector3.positiveInfinity;
        Vector3 max = Vector3.negativeInfinity;
        for (int i = 0; i < clip.frames.Length; i++)
        {
            if (!NpzJsonMotionParser.TryGetRootOffset(clip.frames[i], GetAxisFlip(), out Vector3 root))
            {
                continue;
            }

            Vector3 scaled = root * poseScale;
            min = Vector3.Min(min, scaled);
            max = Vector3.Max(max, scaled);
        }

        Vector3 travel = max - min;
        Debug.Log(
            "NpzStickFigureVisualizer: pred_cam_t travel (scaled) X=" + travel.x.ToString("F2") +
            " Y=" + travel.y.ToString("F2") + " Z=" + travel.z.ToString("F2") +
            " over " + clip.frames.Length + " frames");
    }

    void ApplyFrame(int frameIndex)
    {
        if (clip?.frames == null || frameIndex < 0 || frameIndex >= clip.frames.Length)
        {
            return;
        }

        currentFrameIndex = frameIndex;
    }

    bool TryMapPointInterpolated(int frame0, int frame1, float blend, string jointName, out Vector3 mapped)
    {
        if (!NpzJsonMotionParser.TryGetPoint(clip.frames[frame0], jointName, out _))
        {
            mapped = default;
            return false;
        }

        if (blend <= 0f || frame0 == frame1)
        {
            mapped = MapPoint(clip.frames[frame0], jointName);
            return true;
        }

        if (!NpzJsonMotionParser.TryGetPoint(clip.frames[frame1], jointName, out _))
        {
            mapped = MapPoint(clip.frames[frame0], jointName);
            return true;
        }

        mapped = MapPointInterpolated(clip.frames[frame0], clip.frames[frame1], blend, jointName);
        return true;
    }

    Vector3 MapPointInterpolated(NpzMotionFrame frame0, NpzMotionFrame frame1, float blend, string jointName)
    {
        Quaternion stickRotation = Quaternion.Euler(rotationOffsetEuler);

        if (centerOnHips
            && TryGetRawHipMidMapped(frame0, out Vector3 hip0)
            && TryGetRawHipMidMapped(frame1, out Vector3 hip1))
        {
            Vector3 hipAnchor = playbackOrigin + positionOffset + stickRotation * Vector3.Lerp(hip0, hip1, blend);
            Vector3 poseLocal = Vector3.Lerp(
                MapPoseLocal(frame0, jointName),
                MapPoseLocal(frame1, jointName),
                blend);
            return hipAnchor + stickRotation * poseLocal;
        }

        return Vector3.Lerp(
            MapPoint(frame0, jointName),
            MapPoint(frame1, jointName),
            blend);
    }

    Vector3 GetWorldRootAnchor(NpzMotionFrame frame)
    {
        Vector3 anchor = playbackOrigin + positionOffset;
        if (moveRootInWorldSpace && applyCameraTranslation &&
            NpzJsonMotionParser.TryGetRootOffset(frame, GetAxisFlip(), out Vector3 root))
        {
            anchor += Quaternion.Euler(rotationOffsetEuler) * (root * poseScale);
        }

        return anchor;
    }

    void UpdateFollowSkeletonScale(NpzMotionFrame frame, Transform pelvis, Transform leftFoot, Transform rightFoot)
    {
        followSkeletonScale = skeletonScaleMultiplier;
        if (!autoScaleToAvatar || leftFoot == null || rightFoot == null)
        {
            return;
        }

        float stickLeg = ComputeStickLegLength(frame);
        if (stickLeg <= 1e-4f)
        {
            return;
        }

        Vector3 footMid = (leftFoot.position + rightFoot.position) * 0.5f;
        float avatarLeg = (footMid - pelvis.position).magnitude;
        followSkeletonScale = skeletonScaleMultiplier * (avatarLeg / stickLeg);

        if (!loggedSkeletonScale)
        {
            loggedSkeletonScale = true;
            Debug.Log(
                "NpzStickFigureVisualizer: skeleton scale " + followSkeletonScale.ToString("F2") +
                " (avatar leg " + avatarLeg.ToString("F2") + "m / stick leg " + stickLeg.ToString("F2") + "m)");
        }
    }

    float ComputeStickLegLength(NpzMotionFrame frame)
    {
        if (!TryGetLocalHipCenter(frame, out Vector3 localHip))
        {
            return 0f;
        }

        float left = 0f;
        float right = 0f;
        if (NpzJsonMotionParser.TryGetPoint(frame, "left-ankle", out _))
        {
            left = (MapPoseLocal(frame, "left-ankle") - localHip).magnitude;
        }

        if (NpzJsonMotionParser.TryGetPoint(frame, "right-ankle", out _))
        {
            right = (MapPoseLocal(frame, "right-ankle") - localHip).magnitude;
        }

        if (left <= 0f && right <= 0f)
        {
            return 0f;
        }

        if (left <= 0f)
        {
            return right;
        }

        if (right <= 0f)
        {
            return left;
        }

        return (left + right) * 0.5f;
    }

    static Quaternion GetAvatarSkeletonBasis(Transform pelvis)
    {
        Transform basisTransform = pelvis.parent != null ? pelvis.parent : pelvis;
        Vector3 forward = basisTransform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-6f)
        {
            forward = Vector3.forward;
        }

        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    bool TryGetLocalHipCenter(NpzMotionFrame frame, out Vector3 localHip)
    {
        localHip = default;
        if (!NpzJsonMotionParser.TryGetPoint(frame, "left-hip", out _)
            || !NpzJsonMotionParser.TryGetPoint(frame, "right-hip", out _))
        {
            return false;
        }

        Vector3 left = MapPoseLocal(frame, "left-hip");
        Vector3 right = MapPoseLocal(frame, "right-hip");
        localHip = (left + right) * 0.5f;
        return true;
    }

    Vector3 MapPoint(NpzMotionFrame frame, string jointName)
    {
        Vector3 poseLocal = MapPoseLocal(frame, jointName);
        Quaternion stickRotation = Quaternion.Euler(rotationOffsetEuler);

        if (followPelvis != null && alignToAvatarPelvis && TryGetPoseHipCenter(frame, out Vector3 poseHip))
        {
            Vector3 offsetFromHip = stickRotation * (poseLocal - poseHip) * followSkeletonScale;
            Vector3 rootWorld = Vector3.zero;
            if (applyCameraTranslation && moveRootInWorldSpace &&
                NpzJsonMotionParser.TryGetRootOffset(frame, GetAxisFlip(), out Vector3 root))
            {
                rootWorld = GetAvatarSkeletonBasis(followPelvis) * stickRotation * (root * poseScale);
            }

            return followPelvis.position + GetAvatarSkeletonBasis(followPelvis) * offsetFromHip + rootWorld;
        }

        if (centerOnHips && TryGetRawHipMidMapped(frame, out Vector3 rawHipMid))
        {
            Vector3 hipAnchor = playbackOrigin + positionOffset + stickRotation * rawHipMid;
            return hipAnchor + stickRotation * poseLocal;
        }

        Vector3 anchor = GetWorldRootAnchor(frame);
        return anchor + stickRotation * poseLocal;
    }

    void CaptureFrameZeroHipOrigin()
    {
        hasFrameZeroRawHipMid = false;
        frameZeroRawHipMid = Vector3.zero;
        if (clip?.frames == null || clip.frames.Length == 0)
        {
            return;
        }

        hasFrameZeroRawHipMid = TryGetRawHipMidFromFrame(clip.frames[0], out frameZeroRawHipMid);
    }

    bool TryGetRawHipMidFromFrame(NpzMotionFrame frame, out Vector3 mapped)
    {
        mapped = default;
        if (!NpzJsonMotionParser.TryGetPoint(frame, "left-hip", out Vector3 leftHip)
            || !NpzJsonMotionParser.TryGetPoint(frame, "right-hip", out Vector3 rightHip))
        {
            return false;
        }

        mapped = Vector3.Scale((leftHip + rightHip) * 0.5f * poseScale, GetAxisFlip());
        return true;
    }

    bool TryGetRawHipMidMapped(NpzMotionFrame frame, out Vector3 mapped)
    {
        if (!TryGetRawHipMidFromFrame(frame, out mapped))
        {
            return false;
        }

        if (hasFrameZeroRawHipMid)
        {
            mapped -= frameZeroRawHipMid;
        }

        return true;
    }

    bool TryGetPoseHipCenter(NpzMotionFrame frame, out Vector3 poseHip)
    {
        poseHip = default;
        if (!NpzJsonMotionParser.TryGetPoint(frame, "left-hip", out _)
            || !NpzJsonMotionParser.TryGetPoint(frame, "right-hip", out _))
        {
            return false;
        }

        poseHip = (MapPoseLocal(frame, "left-hip") + MapPoseLocal(frame, "right-hip")) * 0.5f;
        return true;
    }

    Vector3 MapPoseLocal(NpzMotionFrame frame, string jointName)
    {
        NpzJsonMotionParser.TryGetPoint(frame, jointName, out Vector3 raw);

        if (centerOnHips &&
            NpzJsonMotionParser.TryGetPoint(frame, "left-hip", out Vector3 leftHip) &&
            NpzJsonMotionParser.TryGetPoint(frame, "right-hip", out Vector3 rightHip))
        {
            raw -= (leftHip + rightHip) * 0.5f;
        }

        return Vector3.Scale(raw * poseScale, GetAxisFlip());
    }

    Vector3 MapPointLocal(NpzMotionFrame frame, string jointName)
    {
        return MapPoseLocal(frame, jointName);
    }

    Vector3 GetAxisFlip()
    {
        return new Vector3(1f, flipY ? -1f : 1f, flipZ ? -1f : 1f);
    }

    void OnDestroy()
    {
        ClearVisuals();
    }
}
