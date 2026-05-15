using UnityEngine;
using UnityEngine.UI; // Required for the Image component

public class ImageSwapper : MonoBehaviour
{
    public Image targetImage;    // The UI Image you want to change
    public Sprite secondSprite;  // The new PNG/Sprite you want to show
    private Sprite originalSprite;
    private bool isSwapped = false;

    void Start()
    {
        // Save the first image so we can toggle back and forth
        if (targetImage != null)
            originalSprite = targetImage.sprite;
    }

    public void SwapImage()
    {
        if (targetImage == null || secondSprite == null) return;

        if (!isSwapped)
            targetImage.sprite = secondSprite;
        else
            targetImage.sprite = originalSprite;

        isSwapped = !isSwapped;
    }
}