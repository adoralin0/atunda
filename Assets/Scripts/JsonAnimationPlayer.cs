using UnityEngine;
using System.Collections.Generic;

public class JsonAnimationPlayer : MonoBehaviour
{
    public Transform[] bones; // Ensure you dragged your avatar's bones here!
    private bool isPlaying = false;

    public void PlayDance(string fileName)
    {
        // 1. Load the file
        TextAsset jsonFile = Resources.Load<TextAsset>("Dances/" + fileName);
        
        if (jsonFile != null)
        {
            Debug.Log("Playing real data from: " + fileName);
            isPlaying = true;
            
            // For now, let's just prove we can read the first few numbers
            // In a full version, we'd loop through these every frame
            ParseAndMove(jsonFile.text);
        }
        else
        {
            Debug.LogError("Could not find file: " + fileName + " in Resources/Dances/");
        }
    }

    void ParseAndMove(string rawData)
    {
        // This is a simplified way to strip the brackets and get the numbers
        string cleanData = rawData.Replace("[", "").Replace("]", "").Replace(" ", "");
        string[] numbers = cleanData.Split(',');

        // Move the FIRST bone to the FIRST x,y,z in the file as a test
        if (bones.Length > 0 && numbers.Length >= 3)
        {
            float x = float.Parse(numbers[0]);
            float y = float.Parse(numbers[1]);
            float z = float.Parse(numbers[2]);
            bones[0].localPosition = new Vector3(x, y, z);
        }
    }

    public void StopDance()
    {
        isPlaying = false;
        Debug.Log("Dance Stopped");
    }
}