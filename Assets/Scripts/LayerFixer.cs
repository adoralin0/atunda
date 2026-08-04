using UnityEngine;

public class LayerFixer : MonoBehaviour
{
    public string targetLayer = "UI_Avatar";

    void Update()
    {
        ReadyPlayerAvatar avatar = GetComponent<ReadyPlayerAvatar>();
        if (avatar != null && avatar.isLoaded())
        {
            int layer = LayerMask.NameToLayer(targetLayer);
            if (layer >= 0)
            {
                SetLayerRecursive(gameObject, layer);
            }

            this.enabled = false;
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