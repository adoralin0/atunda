using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Keeps menu preview avatars off the main stage while AvatarCamera still renders them into the scroll menu.
/// </summary>
[DefaultExecutionOrder(-200)]
public class PreviewAvatarStage : MonoBehaviour
{
    public enum PreviewBackgroundMode
    {
        Scene,
        SolidColor,
        Transparent
    }

    static bool initialized;

    // Matches Left_Arrow button face from UI_grey_buttons_light_1_5 (#686F99).
    static readonly Color DefaultLeftButtonBackground = new Color(104f / 255f, 111f / 255f, 153f / 255f, 1f);
    static readonly Color DefaultPreviewFloorColor = new Color(104f / 255f, 108f / 255f, 118f / 255f, 1f);

    [SerializeField] Vector3 stageOffset = new Vector3(5000f, 0f, 0f);
    [SerializeField] string previewLayerName = "UI_Avatar";
    [SerializeField] PreviewBackgroundMode previewBackgroundMode = PreviewBackgroundMode.SolidColor;
    [SerializeField] Color previewBackgroundColor = DefaultLeftButtonBackground;
    [SerializeField] Color previewFloorColor = DefaultPreviewFloorColor;

    void Awake()
    {
        TryInitializeFrom(transform);
    }

    void Start()
    {
        TryInitializeFrom(transform);
    }

    public static void TryInitializeFrom(Transform previewParent)
    {
        if (initialized)
        {
            return;
        }

        Transform stageRoot = ResolveStageRoot(previewParent);
        if (stageRoot == null)
        {
            return;
        }

        Vector3 offset = new Vector3(5000f, 0f, 0f);
        string layerName = "UI_Avatar";
        PreviewBackgroundMode backgroundMode = PreviewBackgroundMode.SolidColor;
        Color backgroundColor = DefaultLeftButtonBackground;
        Color floorColor = DefaultPreviewFloorColor;
        if (previewParent != null)
        {
            PreviewAvatarStage stage = previewParent.GetComponent<PreviewAvatarStage>();
            if (stage != null)
            {
                offset = stage.stageOffset;
                layerName = stage.previewLayerName;
                backgroundMode = stage.previewBackgroundMode;
                backgroundColor = stage.previewBackgroundColor;
                floorColor = stage.previewFloorColor;
            }
        }

        initialized = true;
        stageRoot.position += offset;

        Camera avatarCamera = FindAvatarCamera();
        if (avatarCamera != null)
        {
            avatarCamera.enabled = true;
            avatarCamera.gameObject.SetActive(true);
            ApplyPreviewBackground(stageRoot, avatarCamera, backgroundMode, backgroundColor, floorColor);
        }

        HidePreviewLayerFromMainCameras(layerName);
        PreviewDanceStickFigure.TryInitializeFrom(previewParent);
    }

    static void ApplyPreviewBackground(
        Transform stageRoot,
        Camera avatarCamera,
        PreviewBackgroundMode backgroundMode,
        Color backgroundColor,
        Color floorColor)
    {
        switch (backgroundMode)
        {
            case PreviewBackgroundMode.SolidColor:
                ConfigurePreviewFloor(stageRoot, floorColor);
                ConfigureSolidAvatarCamera(avatarCamera, backgroundColor);
                break;
            case PreviewBackgroundMode.Transparent:
                HidePreviewFloor(stageRoot);
                ConfigureTransparentAvatarCamera(avatarCamera);
                break;
            case PreviewBackgroundMode.Scene:
                break;
        }
    }

    static Transform ResolveStageRoot(Transform previewParent)
    {
        Camera avatarCamera = FindAvatarCamera();
        if (avatarCamera != null && avatarCamera.transform.parent != null)
        {
            return avatarCamera.transform.parent;
        }

        if (previewParent == null)
        {
            return null;
        }

        return previewParent.parent != null ? previewParent.parent : previewParent;
    }

    static Camera FindAvatarCamera()
    {
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera cam = cameras[i];
            if (cam != null
                && cam.gameObject.name.IndexOf("AvatarCamera", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return cam;
            }
        }

        return null;
    }

    static void HidePreviewFloor(Transform stageRoot)
    {
        Transform floor = stageRoot.Find("Scrollbar_Floor");
        if (floor != null)
        {
            floor.gameObject.SetActive(false);
        }
    }

    static void ConfigurePreviewFloor(Transform stageRoot, Color floorColor)
    {
        Transform floor = stageRoot.Find("Scrollbar_Floor");
        if (floor == null)
        {
            return;
        }

        floor.gameObject.SetActive(true);

        MeshRenderer renderer = floor.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            return;
        }

        Material material = renderer.material;
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", floorColor);
        }
        else if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", floorColor);
        }
    }

    static void ConfigureSolidAvatarCamera(Camera avatarCamera, Color backgroundColor)
    {
        avatarCamera.clearFlags = CameraClearFlags.SolidColor;
        avatarCamera.backgroundColor = backgroundColor;

        UniversalAdditionalCameraData urpData = avatarCamera.GetComponent<UniversalAdditionalCameraData>();
        if (urpData != null)
        {
            urpData.renderPostProcessing = false;
        }
    }

    static void ConfigureTransparentAvatarCamera(Camera avatarCamera)
    {
        avatarCamera.clearFlags = CameraClearFlags.SolidColor;
        avatarCamera.backgroundColor = Color.clear;

        UniversalAdditionalCameraData urpData = avatarCamera.GetComponent<UniversalAdditionalCameraData>();
        if (urpData != null)
        {
            urpData.renderPostProcessing = false;
        }
    }

    static void HidePreviewLayerFromMainCameras(string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0)
        {
            return;
        }

        int excludeMask = ~(1 << layer);
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera cam = cameras[i];
            if (cam == null
                || cam.gameObject.name.IndexOf("AvatarCamera", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            cam.cullingMask &= excludeMask;
        }
    }
}
