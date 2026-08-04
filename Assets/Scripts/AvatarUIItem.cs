using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One menu tile. Shows a static dance preview image; click applies the dance to the main avatar.
/// </summary>
public class AvatarUIItem : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("References")]
    public AvatarAnimationPlayer realDancer;
    public string jsonFileName = "Afefe_0";

    [Header("Interaction")]
    public Color selectedLabelColor = new Color(1f, 0.92f, 0.35f);
    [Tooltip("Multiplies the dance preview when selected (values above 1 brighten the PNG).")]
    public Color selectedPreviewTint = new Color(1.1f, 1.12f, 1.2f, 1f);
    [Range(0.8f, 1f)] public float pressScale = 0.9f;
    public float pressBounceSeconds = 0.14f;

    [Header("Card Look")]
    public float cardCornerRadius = 16f;
    public float cardBorderWidth = 7f;
    public Color cardBorderColor = new Color(78f / 255f, 84f / 255f, 118f / 255f, 1f);
    public Color cardFillColor = new Color(104f / 255f, 111f / 255f, 153f / 255f, 1f);
    public Color selectedCardFillColor = new Color(132f / 255f, 140f / 255f, 185f / 255f, 1f);

    static readonly List<AvatarUIItem> registeredItems = new List<AvatarUIItem>();
    static AvatarUIItem clickedItem;
    static TextMeshProUGUI selectedDanceLabel;
    static Sprite placeholderSprite;

    Image previewImage;
    Image cardFillImage;
    TextMeshProUGUI label;
    Color defaultLabelColor;
    bool isClicked;
    Vector3 restingScale = Vector3.one;
    Coroutine pressBounceRoutine;

    public static void SetSelectedDanceLabel(TextMeshProUGUI label)
    {
        selectedDanceLabel = label;
    }

    void OnEnable()
    {
        if (!registeredItems.Contains(this))
        {
            registeredItems.Add(this);
        }
    }

    void OnDisable()
    {
        registeredItems.Remove(this);

        if (clickedItem == this)
        {
            clickedItem = null;
        }
    }

    void Start()
    {
        restingScale = transform.localScale;
        if (restingScale.sqrMagnitude < 0.0001f)
        {
            restingScale = Vector3.one;
        }

        EnsureCardChrome();
        EnsurePreviewImage();
        label = GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
        {
            defaultLabelColor = label.color;
            ConfigureDanceLabel(label, label.fontSize > 1f ? label.fontSize : 24f);
        }

        RefreshPreviewImage();
        ApplySelectedVisual();
    }

    public void ConfigureDanceLabel(TextMeshProUGUI danceLabel, float preferredFontSize)
    {
        if (danceLabel == null)
        {
            return;
        }

        label = danceLabel;
        danceLabel.raycastTarget = false;
        // Must stay maskable so the scroll viewport can clip names that scroll off.
        danceLabel.maskable = true;
        danceLabel.enableAutoSizing = false;
        danceLabel.fontSize = Mathf.Max(12f, preferredFontSize);
        danceLabel.enableWordWrapping = true;
        danceLabel.overflowMode = TextOverflowModes.Overflow;
        danceLabel.alignment = TextAlignmentOptions.Top;
        danceLabel.horizontalAlignment = HorizontalAlignmentOptions.Center;
        danceLabel.verticalAlignment = VerticalAlignmentOptions.Top;

        RectTransform labelRect = danceLabel.rectTransform;
        labelRect.SetParent(transform, false);
        labelRect.SetAsLastSibling();
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = Vector2.zero;
        // Tall enough for two wrapped lines above the preview.
        labelRect.offsetMin = new Vector2(6f, -64f);
        labelRect.offsetMax = new Vector2(-6f, -4f);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        SetPressedVisual(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        SetPressedVisual(false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetPressedVisual(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Select();
    }

    /// <summary>Select this dance tile and play it on the bound dancer (same as a UI click).</summary>
    public void Select()
    {
        if (clickedItem != null && clickedItem != this)
        {
            clickedItem.SetClicked(false);
        }

        clickedItem = this;
        SetClicked(true);
        PlayPressBounce();

        if (realDancer != null)
        {
            realDancer.PlayDance(jsonFileName);
        }

        UpdateSelectedDanceLabel();
    }

    void SetPressedVisual(bool pressed)
    {
        if (pressBounceRoutine != null)
        {
            return;
        }

        transform.localScale = pressed ? restingScale * pressScale : restingScale;
    }

    void PlayPressBounce()
    {
        if (pressBounceRoutine != null)
        {
            StopCoroutine(pressBounceRoutine);
        }

        pressBounceRoutine = StartCoroutine(PressBounceCoroutine());
    }

    IEnumerator PressBounceCoroutine()
    {
        Vector3 pressed = restingScale * pressScale;
        transform.localScale = pressed;

        float duration = Mathf.Max(0.01f, pressBounceSeconds);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Ease out with a tiny overshoot so it feels clicky.
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float overshoot = 1f + 0.06f * Mathf.Sin(t * Mathf.PI);
            transform.localScale = Vector3.LerpUnclamped(pressed, restingScale * overshoot, eased);
            yield return null;
        }

        transform.localScale = restingScale;
        pressBounceRoutine = null;
    }

    /// <summary>
    /// Advances to the next dance in menu order.
    /// If nothing is selected, selects the first dance.
    /// Returns false if the menu has no items.
    /// </summary>
    public static bool SelectNextDance()
    {
        AvatarUIItem[] items = GetMenuItemsInOrder();
        if (items.Length == 0)
        {
            return false;
        }

        int nextIndex = 0;
        if (clickedItem != null)
        {
            int currentIndex = System.Array.IndexOf(items, clickedItem);
            if (currentIndex >= 0)
            {
                nextIndex = (currentIndex + 1) % items.Length;
            }
        }

        items[nextIndex].Select();
        return true;
    }

    /// <summary>
    /// Goes to the previous dance in menu order.
    /// If nothing is selected, selects the last dance.
    /// Returns false if the menu has no items.
    /// </summary>
    public static bool SelectPreviousDance()
    {
        AvatarUIItem[] items = GetMenuItemsInOrder();
        if (items.Length == 0)
        {
            return false;
        }

        int previousIndex = items.Length - 1;
        if (clickedItem != null)
        {
            int currentIndex = System.Array.IndexOf(items, clickedItem);
            if (currentIndex >= 0)
            {
                previousIndex = (currentIndex - 1 + items.Length) % items.Length;
            }
        }

        items[previousIndex].Select();
        return true;
    }

    static AvatarUIItem[] GetMenuItemsInOrder()
    {
        DanceMenuGenerator menu = Object.FindFirstObjectByType<DanceMenuGenerator>();
        if (menu == null)
        {
            return registeredItems.ToArray();
        }

        var ordered = new List<AvatarUIItem>();
        foreach (Transform child in menu.transform)
        {
            AvatarUIItem item = child.GetComponent<AvatarUIItem>();
            if (item != null)
            {
                ordered.Add(item);
            }
        }

        return ordered.ToArray();
    }

    public void RefreshPreviewImage()
    {
        EnsurePreviewImage();
        if (previewImage == null)
        {
            return;
        }

        Sprite sprite = DancePreviewImageCatalog.TryLoadSprite(jsonFileName);
        previewImage.sprite = sprite != null ? sprite : GetPlaceholderSprite();
        previewImage.preserveAspect = true;
        previewImage.type = Image.Type.Simple;
        previewImage.enabled = true;
        ApplyPreviewTint();
    }

    void EnsureCardChrome()
    {
        int radiusPx = Mathf.Clamp(Mathf.RoundToInt(cardCornerRadius), 6, 28);
        float border = Mathf.Max(1.5f, cardBorderWidth);

        Image rootImage = GetComponent<Image>();
        if (rootImage == null)
        {
            rootImage = gameObject.AddComponent<Image>();
        }

        DanceMenuGenerator.ApplyRoundedSprite(rootImage, cardBorderColor, radiusPx);
        rootImage.raycastTarget = true;
        rootImage.maskable = true;

        // Nested stencil Masks break parent scroll clipping; rounded sprites are enough.
        RectMask2D rectMask = GetComponent<RectMask2D>();
        if (rectMask != null)
        {
            rectMask.enabled = false;
        }

        Mask mask = GetComponent<Mask>();
        if (mask != null)
        {
            mask.enabled = false;
            mask.showMaskGraphic = false;
        }

        Transform fillTransform = transform.Find("CardFill");
        if (fillTransform == null)
        {
            GameObject fillObject = new GameObject(
                "CardFill",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            fillTransform = fillObject.transform;
            fillTransform.SetParent(transform, false);
        }

        fillTransform.SetAsFirstSibling();
        RectTransform fillRect = fillTransform as RectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(border, border);
        fillRect.offsetMax = new Vector2(-border, -border);
        fillRect.localScale = Vector3.one;

        cardFillImage = fillTransform.GetComponent<Image>();
        int innerRadius = Mathf.Max(4, radiusPx - Mathf.RoundToInt(border));
        DanceMenuGenerator.ApplyRoundedSprite(cardFillImage, cardFillColor, innerRadius);
        cardFillImage.raycastTarget = false;
        cardFillImage.maskable = true;
    }

    void EnsurePreviewImage()
    {
        EnsureCardChrome();

        Transform fillTransform = transform.Find("CardFill");
        Transform parentForPreview = fillTransform != null ? fillTransform : transform;

        if (previewImage != null)
        {
            if (previewImage.transform.parent != parentForPreview)
            {
                previewImage.transform.SetParent(parentForPreview, false);
            }

            ConfigurePreviewImage(previewImage);
            return;
        }

        Transform existing = transform.Find("DancePreview");
        if (existing == null && fillTransform != null)
        {
            existing = fillTransform.Find("DancePreview");
        }

        if (existing != null)
        {
            existing.SetParent(parentForPreview, false);
            previewImage = existing.GetComponent<Image>();
            if (previewImage != null)
            {
                ConfigurePreviewRect(existing as RectTransform);
                ConfigurePreviewImage(previewImage);
                return;
            }
        }

        GameObject previewObject = new GameObject(
            "DancePreview",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        previewObject.transform.SetParent(parentForPreview, false);
        previewObject.transform.SetAsFirstSibling();

        ConfigurePreviewRect(previewObject.GetComponent<RectTransform>());
        previewImage = previewObject.GetComponent<Image>();
        ConfigurePreviewImage(previewImage);
    }

    static void ConfigurePreviewRect(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    static void ConfigurePreviewImage(Image image)
    {
        image.raycastTarget = false;
        image.maskable = true;
        image.preserveAspect = true;
    }

    void SetClicked(bool clicked)
    {
        isClicked = clicked;
        ApplySelectedVisual();
    }

    void ApplySelectedVisual()
    {
        Transform legacyBorder = transform.Find("SelectionBorder");
        if (legacyBorder != null)
        {
            legacyBorder.gameObject.SetActive(false);
        }

        if (cardFillImage != null)
        {
            cardFillImage.color = isClicked ? selectedCardFillColor : cardFillColor;
        }

        ApplyPreviewTint();

        if (label == null)
        {
            return;
        }

        label.color = isClicked ? selectedLabelColor : defaultLabelColor;
        label.fontStyle = isClicked ? FontStyles.Bold : FontStyles.Normal;
    }

    void ApplyPreviewTint()
    {
        EnsurePreviewImage();
        if (previewImage == null)
        {
            return;
        }

        Color tint = isClicked ? selectedPreviewTint : Color.white;
        tint.a = 1f;
        previewImage.color = tint;
    }

    static Sprite GetPlaceholderSprite()
    {
        if (placeholderSprite != null)
        {
            return placeholderSprite;
        }

        const int size = 8;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
        Color fill = new Color(0.45f, 0.48f, 0.62f, 1f);
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = fill;
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        placeholderSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f);
        return placeholderSprite;
    }

    void UpdateSelectedDanceLabel()
    {
        if (selectedDanceLabel == null || string.IsNullOrEmpty(jsonFileName))
        {
            return;
        }

        selectedDanceLabel.text = jsonFileName;
    }

    public static void RestoreSelectionAfterCharacterSwap()
    {
        if (clickedItem == null)
        {
            return;
        }

        clickedItem.ReplaySelectedDance();
    }

    public void ReplaySelectedDance()
    {
        if (string.IsNullOrEmpty(jsonFileName))
        {
            return;
        }

        if (realDancer != null)
        {
            realDancer.PlayDance(jsonFileName);
        }

        UpdateSelectedDanceLabel();
    }

    public static void ClearClickedPreview()
    {
        if (clickedItem != null)
        {
            clickedItem.SetClicked(false);
            clickedItem = null;
        }
    }

    public static void ClearHoverPreview()
    {
    }

    /// <summary>Re-wire after character swap or menu rebuild.</summary>
    public static void RefreshAllFrom(CharacterSelector selector)
    {
        if (selector == null)
        {
            return;
        }

        selector.RefreshMenuBindings();
        ClearHoverPreview();
        ClearClickedPreview();
    }
}
