using UnityEngine;

public class CharacterSelector : MonoBehaviour
{
    [Header("Setup")]
    public Transform menuContent;   // Drag 'Content' object here
    public Transform previewParent; // Drag 'PreviewBuddy' (parent of previews) here
    
    private int currentCharacterIndex = 0;

    void Start()
    {
        UpdateCharacterVisibility();
    }

    public void NextCharacter()
    {
        StopCurrentDancer();
        currentCharacterIndex = (currentCharacterIndex + 1) % transform.childCount;
        UpdateCharacterVisibility();
    }

    public void PreviousCharacter()
    {
        StopCurrentDancer();
        currentCharacterIndex--;
        if (currentCharacterIndex < 0) currentCharacterIndex = transform.childCount - 1;
        UpdateCharacterVisibility();
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

        // 3. CRITICAL: Update every box in the menu to point to the new preview character
        if (menuContent != null && newMainDancer != null && newPreviewDancer != null)
        {
            foreach (Transform box in menuContent)
            {
                AvatarUIItem item = box.GetComponent<AvatarUIItem>();
                if (item != null)
                {
                    item.realDancer = newMainDancer;
                    item.previewDancer = newPreviewDancer;
                }
            }
            Debug.Log("<color=yellow>Menu updated to: </color>" + newPreviewDancer.gameObject.name);
        }
    }
}