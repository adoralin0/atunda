using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class DanceMenuGenerator : MonoBehaviour
{
    [Header("Setup")]
    public GameObject prefab; // Drag your AvatarMenuItem prefab here
    public AvatarAnimationPlayer previewDancer;
    public AvatarAnimationPlayer realDancer;

    void Start()
    {
        GenerateMenu();
    }

    public void GenerateMenu()
    {
        // 1. Clear any old manual boxes
        foreach (Transform child in transform) {
            Destroy(child.gameObject);
        }

        // 2. Load all TextAssets (JSONs) from Resources/Dances
        TextAsset[] danceFiles = Resources.LoadAll<TextAsset>("Dances");

        foreach (TextAsset file in danceFiles)
        {
            // 3. Spawn the prefab
            GameObject newBox = Instantiate(prefab, transform);
            
            // 4. Get the script on the box and set the data
            AvatarUIItem itemScript = newBox.GetComponent<AvatarUIItem>();
            if (itemScript != null)
            {
                itemScript.jsonFileName = file.name; // Automatically sets "Afefe_0", etc.
                itemScript.previewDancer = previewDancer;
                itemScript.realDancer = realDancer;
            }

            // 5. Update the text label on the box
            TMPro.TextMeshProUGUI label = newBox.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (label != null) {
                label.text = file.name;
            }
        }
    }
}
