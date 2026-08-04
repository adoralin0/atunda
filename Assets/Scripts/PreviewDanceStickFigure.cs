using UnityEngine;

/// <summary>
/// Hidden stick figure used only for scrollbar preview retargeting (does not affect the main stage stick).
/// </summary>
[DefaultExecutionOrder(-190)]
public class PreviewDanceStickFigure : MonoBehaviour
{
    public const string StickObjectName = "PreviewNpzStickFigure";

    static PreviewDanceStickFigure instance;
    static NpzStickFigureVisualizer stick;

    [SerializeField] Transform previewAvatarAnchor;

    public static NpzStickFigureVisualizer GetStick()
    {
        return stick;
    }

    public static void TryInitializeFrom(Transform previewParent)
    {
        if (previewParent == null)
        {
            return;
        }

        PreviewDanceStickFigure driver = previewParent.GetComponent<PreviewDanceStickFigure>();
        if (driver == null)
        {
            driver = previewParent.gameObject.AddComponent<PreviewDanceStickFigure>();
        }

        driver.EnsureStick();
    }

    void Awake()
    {
        instance = this;
        EnsureStick();
    }

    void EnsureStick()
    {
        if (stick != null)
        {
            return;
        }

        Transform parent = previewAvatarAnchor != null ? previewAvatarAnchor : transform;
        GameObject stickObject = new GameObject(StickObjectName);
        stickObject.transform.SetParent(parent, false);
        stick = stickObject.AddComponent<NpzStickFigureVisualizer>();

        stick.headlessPlayback = true;
        stick.drawJointMarkers = false;
        stick.playOnStart = false;
        stick.loadFromStreamingAssets = true;
        stick.applyCameraTranslation = false;
        stick.moveRootInWorldSpace = false;
        stick.alignToAvatarPelvis = false;
        stick.centerOnHips = true;
        stick.flipY = true;
        stick.flipZ = false;
        stick.poseScale = 1.06f;
        stick.playbackSpeed = 1f;
        stick.loop = true;

        NpzStickFigureVisualizer mainStick = FindPrimaryStickFigure();
        if (mainStick != null)
        {
            stick.poseScale = mainStick.PoseScale;
            stick.rotationOffsetEuler = mainStick.RotationOffsetEuler;
            stick.positionOffset = mainStick.PositionOffset;
            stick.playbackSpeed = mainStick.PlaybackSpeedSetting;
            stick.flipY = mainStick.AxisFlip.y < 0f;
            stick.flipZ = mainStick.AxisFlip.z < 0f;
        }
    }

    public static void AlignToPreviewAvatar(Transform previewAvatar)
    {
        if (stick == null || previewAvatar == null)
        {
            return;
        }

        stick.transform.SetPositionAndRotation(previewAvatar.position, previewAvatar.rotation);
        if (stick.IsClipLoaded)
        {
            stick.SetPlaybackOrigin(stick.transform.position);
        }
    }

    static NpzStickFigureVisualizer FindPrimaryStickFigure()
    {
        NpzStickFigureVisualizer[] visualizers = FindObjectsByType<NpzStickFigureVisualizer>(FindObjectsSortMode.None);
        for (int i = 0; i < visualizers.Length; i++)
        {
            NpzStickFigureVisualizer candidate = visualizers[i];
            if (candidate == null || candidate.headlessPlayback)
            {
                continue;
            }

            if (string.Equals(candidate.gameObject.name, "NpzStickFigure", System.StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            stick = null;
        }
    }
}
