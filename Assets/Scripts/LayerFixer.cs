using UnityEngine;

public class LayerFixer : MonoBehaviour
{
    public string targetLayer = "UI_Avatar";

    void Update()
    {
        // Only run this if the avatar has finished loading
        ReadyPlayerAvatar avatar = GetComponent<ReadyPlayerAvatar>();
        if (avatar != null && avatar.isLoaded())
        {
            SetLayerRecursive(gameObject, LayerMask.NameToLayer(targetLayer));
            this.enabled = false; // Turn off this script once fixed
        }
    }

    void SetLayerRecursive(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursive(child.gameObject, newLayer);
        }
    }
}