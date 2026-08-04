using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AvatarUIItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("References")]
    public AvatarAnimationPlayer previewDancer; 
    public AvatarAnimationPlayer realDancer; 
    public string jsonFileName = "Afefe_0";
    
    private RawImage myImage;

    void Start() {
        myImage = GetComponent<RawImage>();
        // Ensure the box starts invisible
        SetAlpha(0f);
    }

    public void OnPointerEnter(PointerEventData eventData) {
        SetAlpha(1f); // Make the character visible
        if (previewDancer != null) previewDancer.PlayDance(jsonFileName);
    }

    public void OnPointerExit(PointerEventData eventData) {
        SetAlpha(0f); // Make the character invisible
        if (previewDancer != null) previewDancer.StopDance();
    }

    public void OnPointerClick(PointerEventData eventData) {
        if (realDancer != null) realDancer.PlayDance(jsonFileName);
    }

    // Helper function to change transparency without disabling the component
    private void SetAlpha(float alpha) {
        if (myImage != null) {
            Color c = myImage.color;
            c.a = alpha;
            myImage.color = c;
        }
    }
}

