using UnityEngine;

[DefaultExecutionOrder(-150)]
public class CharacterSelector : MonoBehaviour
{
    [Header("Setup")]
    public Transform menuContent;   // Drag 'Content' object here
    public Transform previewParent; // Drag 'PreviewBuddy' (parent of previews) here
    
    private int currentCharacterIndex = 0;

    void Awake()
    {
        if (previewParent != null && previewParent.GetComponent<PreviewAvatarStage>() == null)
        {
            previewParent.gameObject.AddComponent<PreviewAvatarStage>();
        }

        PreviewAvatarStage.TryInitializeFrom(previewParent);
        PreviewDanceStickFigure.TryInitializeFrom(previewParent);
    }

    void Start()
    {
        UpdateCharacterVisibility();
        RefreshMenuBindings();
    }

    public void NextCharacter()
    {
        StopCurrentDancer();
        AvatarUIItem.ClearHoverPreview();
        currentCharacterIndex = (currentCharacterIndex + 1) % transform.childCount;
        UpdateCharacterVisibility();
        RefreshMenuBindings();
        AvatarUIItem.RestoreSelectionAfterCharacterSwap();
    }

    public void PreviousCharacter()
    {
        StopCurrentDancer();
        AvatarUIItem.ClearHoverPreview();
        currentCharacterIndex--;
        if (currentCharacterIndex < 0) currentCharacterIndex = transform.childCount - 1;
        UpdateCharacterVisibility();
        RefreshMenuBindings();
        AvatarUIItem.RestoreSelectionAfterCharacterSwap();
    }

    private void StopCurrentDancer()
    {
        // Stop the main character
        var mainDancer = transform.GetChild(currentCharacterIndex).GetComponent<AvatarAnimationPlayer>();
        if (mainDancer != null) mainDancer.StopDance();

        // Stop the preview character
        if (previewParent != null && previewParent.childCount > currentCharacterIndex)
        {
            var previewDancer = previewParent.GetChild(currentCharacterIndex).GetComponent<AvatarAnimationPlayer>();
            if (previewDancer != null) previewDancer.StopDance();
        }
    }

    private void UpdateCharacterVisibility()
    {
        AvatarAnimationPlayer newMainDancer = null;
        AvatarAnimationPlayer newPreviewDancer = null;

        // 1. Activate Main Avatar
        for (int i = 0; i < transform.childCount; i++)
        {
            bool isActive = (i == currentCharacterIndex);
            transform.GetChild(i).gameObject.SetActive(isActive);
            if (isActive) newMainDancer = transform.GetChild(i).GetComponent<AvatarAnimationPlayer>();
        }

        // 2. Activate Preview Avatar
        if (previewParent != null)
        {
            for (int i = 0; i < previewParent.childCount; i++)
            {
                bool isActive = (i == currentCharacterIndex);
                previewParent.GetChild(i).gameObject.SetActive(isActive);
                if (isActive) newPreviewDancer = previewParent.GetChild(i).GetComponent<AvatarAnimationPlayer>();
            }
        }

        RefreshMenuBindings();
    }

    /// <summary>Call after DanceMenuGenerator builds the menu so each item uses the active avatars.</summary>
    public void RefreshMenuBindings()
    {
        if (menuContent == null)
        {
            return;
        }

        AvatarAnimationPlayer mainDancer = null;

        for (int i = 0; i < transform.childCount; i++)
        {
            if (i == currentCharacterIndex)
            {
                mainDancer = transform.GetChild(i).GetComponent<AvatarAnimationPlayer>();
                break;
            }
        }

        if (mainDancer == null)
        {
            return;
        }

        foreach (Transform box in menuContent)
        {
            AvatarUIItem item = box.GetComponent<AvatarUIItem>();
            if (item != null)
            {
                item.realDancer = mainDancer;
                item.RefreshPreviewImage();
            }
        }

        if (previewParent != null && previewParent.childCount > currentCharacterIndex)
        {
            AvatarAnimationPlayer previewDancer = previewParent.GetChild(currentCharacterIndex).GetComponent<AvatarAnimationPlayer>();
            if (previewDancer != null)
            {
                PreviewDanceStickFigure.AlignToPreviewAvatar(previewDancer.transform);
            }
        }

        Debug.Log("CharacterSelector: menu wired to main '" + mainDancer.gameObject.name + "'.");
    }
}