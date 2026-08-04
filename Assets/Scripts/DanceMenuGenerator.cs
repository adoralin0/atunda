using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DefaultExecutionOrder(-50)]
public class DanceMenuGenerator : MonoBehaviour
{
    [Header("Setup")]
    public GameObject prefab;
    public AvatarAnimationPlayer realDancer;

    [Header("Sources")]
    public bool includeResourcesDances = false;
    public bool includeAtundaStreamingAssets = true;
    public string atundaSubfolder = "atunda";

    [Header("Scroll")]
    public float scrollSensitivity = 12f;
    public float scrollDecelerationRate = 0.05f;
    public bool useSolidScrollPanel = true;
    public Color scrollPanelBackgroundColor = new Color(104f / 255f, 111f / 255f, 153f / 255f, 1f);
    public Color scrollBarColor = new Color(82f / 255f, 88f / 255f, 122f / 255f, 1f);
    public Color scrollBarHandleColor = new Color(137f / 255f, 143f / 255f, 184f / 255f, 1f);
    public bool reserveSpaceForScrollbar = false;
    [Tooltip("Corner roundness for the dance list scroll panel.")]
    public float scrollPanelCornerRadius = 18f;
    [Tooltip("Thickness of the border around the dance list.")]
    public float scrollPanelBorderWidth = 8f;
    [Tooltip("Extra top/bottom inset so cards stop short of the scroll strip outline.")]
    public float scrollContentVerticalInset = 12f;
    // Same as AvatarUIItem selection outline (darker panel blue).
    public Color scrollPanelBorderColor = new Color(78f / 255f, 84f / 255f, 118f / 255f, 1f);
    [Tooltip("Gap between individual dance cards.")]
    public float danceCardSpacing = 14f;
    public int danceListPadding = 12;

    [Header("Selected Dance Label")]
    public TextMeshProUGUI selectedDanceLabel;
    public TMP_FontAsset menuLabelFont;
    public float menuLabelFontSize = 30f;
    public float selectedDanceLabelFontSize = 40f;

    void Start()
    {
        ApplyScrollSettings();
        StartCoroutine(InitializeMenuAsync());
    }

    void ApplyScrollSettings()
    {
        ScrollRect scrollRect = GetComponentInParent<ScrollRect>();
        if (scrollRect == null)
        {
            return;
        }

        scrollRect.scrollSensitivity = scrollSensitivity;
        scrollRect.decelerationRate = scrollDecelerationRate;
        // Never use ExpandViewport — it leaves a permanent empty strip on the right for the
        // scrollbar. Overlay/autohide keeps cards full-width in this narrow panel.
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        if (!reserveSpaceForScrollbar && scrollRect.verticalScrollbar != null)
        {
            scrollRect.verticalScrollbar.gameObject.SetActive(false);
        }

        ApplyScrollPanelStyle(scrollRect);
        ApplyScrollbarStyle(scrollRect);
    }

    void ApplyScrollPanelStyle(ScrollRect scrollRect)
    {
        RectTransform scrollRoot = scrollRect.transform as RectTransform;
        Image rootImage = scrollRect.GetComponent<Image>();
        if (rootImage != null)
        {
            rootImage.sprite = null;
            rootImage.color = new Color(1f, 1f, 1f, 0f);
            rootImage.raycastTarget = false;
        }

        RectTransform viewport = scrollRect.viewport;
        if (viewport == null)
        {
            return;
        }

        // Reparent content first — destroying ClipArea would delete the dance list with it.
        if (scrollRect.content != null && scrollRect.content.parent != viewport)
        {
            scrollRect.content.SetParent(viewport, false);
        }

        DestroyChildIfExists(viewport, "PanelBackground");
        DestroyChildIfExists(viewport, "PanelBorder");
        DestroyChildIfExists(viewport, "ClipArea");

        float borderWidth = useSolidScrollPanel ? Mathf.Max(0f, scrollPanelBorderWidth) : 0f;
        bool useRoundedCorners = useSolidScrollPanel && scrollPanelCornerRadius > 0.5f;

        if (useSolidScrollPanel)
        {
            EnsureScrollChrome(scrollRoot, borderWidth, useRoundedCorners);
        }

        // Scene viewport can be serialized at 0x0 — always stretch-fill the scroll root.
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.pivot = new Vector2(0.5f, 0.5f);
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;
        viewport.localScale = Vector3.one;

        Mask legacyMask = viewport.GetComponent<Mask>();
        if (legacyMask != null)
        {
            legacyMask.enabled = false;
            legacyMask.showMaskGraphic = false;
        }

        RectMask2D rectMask = viewport.GetComponent<RectMask2D>();
        if (rectMask == null)
        {
            rectMask = viewport.gameObject.AddComponent<RectMask2D>();
        }

        rectMask.enabled = true;
        float verticalInset = borderWidth + Mathf.Max(0f, scrollContentVerticalInset);
        // RectMask2D padding: left, bottom, right, top
        rectMask.padding = new Vector4(borderWidth, verticalInset, borderWidth, verticalInset);

        Image viewportImage = viewport.GetComponent<Image>();
        if (viewportImage == null)
        {
            viewportImage = viewport.gameObject.AddComponent<Image>();
        }

        viewportImage.sprite = null;
        viewportImage.color = new Color(1f, 1f, 1f, 0f);
        viewportImage.raycastTarget = true;

        if (scrollRect.content != null)
        {
            scrollRect.content.SetParent(viewport, false);
            scrollRect.content.SetAsLastSibling();
        }

        Transform border = scrollRoot.Find("PanelBorder");
        Transform background = scrollRoot.Find("PanelBackground");
        if (border != null)
        {
            border.SetAsFirstSibling();
        }

        if (background != null)
        {
            background.SetSiblingIndex(border != null ? 1 : 0);
        }

        viewport.SetAsLastSibling();
        if (scrollRect.verticalScrollbar != null)
        {
            scrollRect.verticalScrollbar.transform.SetAsLastSibling();
        }
    }

    void EnsureScrollChrome(RectTransform scrollRoot, float borderWidth, bool useRoundedCorners)
    {
        Transform border = scrollRoot.Find("PanelBorder");
        if (border == null)
        {
            GameObject borderObject = new GameObject(
                "PanelBorder",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            borderObject.transform.SetParent(scrollRoot, false);
            border = borderObject.transform;
        }

        border.gameObject.SetActive(borderWidth > 0.01f);
        RectTransform borderRect = border as RectTransform;
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = Vector2.zero;
        borderRect.offsetMax = Vector2.zero;
        borderRect.localScale = Vector3.one;

        Image borderImage = border.GetComponent<Image>();
        if (useRoundedCorners)
        {
            ApplyRoundedImage(borderImage, scrollPanelBorderColor);
        }
        else
        {
            ApplySolidImage(borderImage, scrollPanelBorderColor);
        }

        borderImage.raycastTarget = false;

        Transform background = scrollRoot.Find("PanelBackground");
        if (background == null)
        {
            GameObject backgroundObject = new GameObject(
                "PanelBackground",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            backgroundObject.transform.SetParent(scrollRoot, false);
            background = backgroundObject.transform;
        }

        background.gameObject.SetActive(true);
        RectTransform backgroundRect = background as RectTransform;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = new Vector2(borderWidth, borderWidth);
        backgroundRect.offsetMax = new Vector2(-borderWidth, -borderWidth);
        backgroundRect.localScale = Vector3.one;

        Image backgroundImage = background.GetComponent<Image>();
        if (useRoundedCorners)
        {
            int innerRadius = Mathf.Max(
                4,
                Mathf.RoundToInt(scrollPanelCornerRadius) - Mathf.RoundToInt(borderWidth));
            ApplyRoundedSprite(backgroundImage, scrollPanelBackgroundColor, innerRadius);
        }
        else
        {
            ApplySolidImage(backgroundImage, scrollPanelBackgroundColor);
        }

        backgroundImage.raycastTarget = false;
    }

    void DestroyChildIfExists(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(child.gameObject);
        }
        else
        {
            DestroyImmediate(child.gameObject);
        }
    }

    void ApplyScrollbarStyle(ScrollRect scrollRect)
    {
        Scrollbar verticalScrollbar = scrollRect.verticalScrollbar;
        if (verticalScrollbar == null)
        {
            return;
        }

        verticalScrollbar.gameObject.SetActive(reserveSpaceForScrollbar);

        if (!reserveSpaceForScrollbar)
        {
            return;
        }

        Image trackImage = verticalScrollbar.GetComponent<Image>();
        if (trackImage != null)
        {
            ApplySolidImage(trackImage, scrollBarColor);
        }

        if (verticalScrollbar.targetGraphic is Image handleImage)
        {
            ApplySolidImage(handleImage, scrollBarHandleColor);
        }

        ColorBlock colors = verticalScrollbar.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.5f);
        verticalScrollbar.colors = colors;
    }

    static Sprite solidUiSprite;
    static Sprite roundedUiSprite;
    static int roundedUiSpriteRadius;

    static void ApplySolidImage(Image image, Color color)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = GetSolidUiSprite();
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = color;
    }

    void ApplyRoundedImage(Image image, Color color)
    {
        if (image == null)
        {
            return;
        }

        int radiusPx = Mathf.Clamp(Mathf.RoundToInt(scrollPanelCornerRadius), 4, 28);
        ApplyRoundedSprite(image, color, radiusPx);
    }

    public static void ApplyRoundedSprite(Image image, Color color, int radiusPx)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = GetRoundedUiSprite(radiusPx);
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
        image.preserveAspect = false;
        image.color = color;
        image.fillCenter = true;
    }

    public static Sprite GetRoundedUiSprite(int radiusPx)
    {
        if (roundedUiSprite != null && roundedUiSpriteRadius == radiusPx)
        {
            return roundedUiSprite;
        }

        const int size = 64;
        radiusPx = Mathf.Clamp(radiusPx, 4, size / 2);

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[size * size];
        Color32 opaque = new Color32(255, 255, 255, 255);
        Color32 clear = new Color32(255, 255, 255, 0);
        float radius = radiusPx;
        float radiusSq = radius * radius;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = 0f;
                float dy = 0f;

                if (x < radius && y < radius)
                {
                    dx = radius - 1f - x;
                    dy = radius - 1f - y;
                }
                else if (x >= size - radius && y < radius)
                {
                    dx = x - (size - radius);
                    dy = radius - 1f - y;
                }
                else if (x < radius && y >= size - radius)
                {
                    dx = radius - 1f - x;
                    dy = y - (size - radius);
                }
                else if (x >= size - radius && y >= size - radius)
                {
                    dx = x - (size - radius);
                    dy = y - (size - radius);
                }

                bool inside = (dx == 0f && dy == 0f) || (dx * dx + dy * dy) <= radiusSq;
                pixels[y * size + x] = inside ? opaque : clear;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);

        Vector4 border = new Vector4(radiusPx, radiusPx, radiusPx, radiusPx);
        roundedUiSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0u,
            SpriteMeshType.FullRect,
            border);
        roundedUiSpriteRadius = radiusPx;
        return roundedUiSprite;
    }

    static Sprite GetSolidUiSprite()
    {
        if (solidUiSprite != null)
        {
            return solidUiSprite;
        }

        Texture2D texture = Texture2D.whiteTexture;
        solidUiSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        return solidUiSprite;
    }

    IEnumerator InitializeMenuAsync()
    {
        if (includeAtundaStreamingAssets)
        {
            yield return AtundaDanceCatalog.EnsureManifestLoaded(atundaSubfolder);
        }

        GenerateMenu();

        List<string> danceIds = CollectDanceIds();
        yield return DancePreviewImageCatalog.EnsureStreamingSprites(danceIds);

        AvatarUIItem[] items = GetComponentsInChildren<AvatarUIItem>(true);
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null)
            {
                items[i].RefreshPreviewImage();
            }
        }
    }

    IEnumerator RefreshMenuPreviewsNextFrame()
    {
        yield return null;
        DancePreviewImageCatalog.ClearCache();
        AvatarUIItem[] items = GetComponentsInChildren<AvatarUIItem>(true);
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null)
            {
                items[i].RefreshPreviewImage();
            }
        }
    }

    public void GenerateMenu()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        ApplyMenuListSpacing();

        List<string> danceIds = CollectDanceIds();
        danceIds.Sort(CompareDanceIds);

        foreach (string danceId in danceIds)
        {
            GameObject newBox = Instantiate(prefab, transform);
            SetLayerRecursively(newBox, gameObject.layer);
            AvatarUIItem itemScript = newBox.GetComponent<AvatarUIItem>();
            if (itemScript != null)
            {
                itemScript.jsonFileName = danceId;
                itemScript.realDancer = realDancer;
                itemScript.RefreshPreviewImage();
            }

            TextMeshProUGUI label = newBox.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.text = ToDisplayName(danceId);
                if (menuLabelFont != null)
                {
                    label.font = menuLabelFont;
                }

                AvatarUIItem item = newBox.GetComponent<AvatarUIItem>();
                if (item != null)
                {
                    item.ConfigureDanceLabel(label, menuLabelFontSize);
                }
                else
                {
                    label.fontSize = menuLabelFontSize;
                    label.raycastTarget = false;
                    label.transform.SetAsLastSibling();
                }
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
        ApplyScrollSettings();
        ApplyMenuListSpacing();

        if (selectedDanceLabel != null)
        {
            selectedDanceLabel.fontSize = selectedDanceLabelFontSize;
        }

        CharacterSelector selector = FindFirstObjectByType<CharacterSelector>();
        if (selector != null)
        {
            selector.RefreshMenuBindings();
        }

        DancePreviewImageCatalog.ClearCache();
        AvatarUIItem.SetSelectedDanceLabel(selectedDanceLabel);
        StartCoroutine(RefreshMenuPreviewsNextFrame());
    }

    void ApplyMenuListSpacing()
    {
        VerticalLayoutGroup layout = GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            return;
        }

        int pad = Mathf.Max(0, danceListPadding);
        int sidePad = pad + Mathf.RoundToInt(useSolidScrollPanel ? Mathf.Max(0f, scrollPanelBorderWidth) : 0f);
        int vertPad = sidePad + Mathf.RoundToInt(Mathf.Max(0f, scrollContentVerticalInset));
        layout.padding = new RectOffset(sidePad, sidePad, vertPad, vertPad);
        layout.spacing = Mathf.Max(0f, danceCardSpacing);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
    }

    List<string> CollectDanceIds()
    {
        HashSet<string> seen = new HashSet<string>();
        List<string> danceIds = new List<string>();

        if (includeResourcesDances)
        {
            TextAsset[] danceFiles = Resources.LoadAll<TextAsset>("Dances");
            foreach (TextAsset file in danceFiles)
            {
                if (file == null || string.IsNullOrEmpty(file.name))
                {
                    continue;
                }

                if (seen.Add(file.name))
                {
                    danceIds.Add(file.name);
                }
            }
        }

        if (includeAtundaStreamingAssets)
        {
            foreach (string danceId in AtundaDanceCatalog.GetDanceIds(atundaSubfolder))
            {
                if (string.IsNullOrEmpty(danceId))
                {
                    continue;
                }

                if (seen.Add(danceId))
                {
                    danceIds.Add(danceId);
                }
            }
        }

        return danceIds;
    }

    static string ToDisplayName(string danceId)
    {
        if (string.IsNullOrEmpty(danceId))
        {
            return string.Empty;
        }

        return danceId.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase)
            ? danceId.Substring(0, danceId.Length - 5)
            : danceId;
    }

    static int CompareDanceIds(string a, string b)
    {
        // Alphabetical (case-insensitive), including Gwara under G.
        if (TryParseNumericPrefix(a, out int aNum) && TryParseNumericPrefix(b, out int bNum))
        {
            int cmp = aNum.CompareTo(bNum);
            if (cmp != 0)
            {
                return cmp;
            }
        }

        return string.Compare(a, b, System.StringComparison.OrdinalIgnoreCase);
    }

    static bool TryParseNumericPrefix(string danceId, out int value)
    {
        value = 0;
        if (string.IsNullOrEmpty(danceId))
        {
            return false;
        }

        int end = 0;
        while (end < danceId.Length && char.IsDigit(danceId[end]))
        {
            end++;
        }

        return end > 0 && int.TryParse(danceId.Substring(0, end), out value);
    }

    static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        Transform root = obj.transform;
        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursively(root.GetChild(i).gameObject, layer);
        }
    }
}
