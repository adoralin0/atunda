using UnityEngine;
using Newtonsoft.Json;

public class AvatarAnimationPlayer : MonoBehaviour
{
    public float fps = 30f;
    public bool loop = true;
    public bool isPlaying = false;
    
    private float[][] frames;
    private float startTime;
    private float[] currentRotations = new float[0];

    public void PlayDance(string fileName)
    {
        string cleanPath = fileName.Replace(".json", "");
        TextAsset jsonFile = Resources.Load<TextAsset>("Dances/" + cleanPath);

        if (jsonFile != null)
        {
            frames = JsonConvert.DeserializeObject<float[][]>(jsonFile.text);
            startTime = Time.time; // Reset clock so it starts at Frame 0
            isPlaying = true;
        }
    }

    public void StopDance()
    {
        isPlaying = false;
        // We stop updating, but don't force a "T-Pose" to avoid disorientation
    }

    public float[] getRotations() {
        return currentRotations;
    }

    void Update()
    {
        if (!isPlaying || frames == null || frames.Length == 0) return;

        int frameIndex = Mathf.FloorToInt((Time.time - startTime) * fps);
        if (loop) frameIndex %= frames.Length;
        else frameIndex = Mathf.Min(frameIndex, frames.Length - 1);

        currentRotations = frames[frameIndex];
    }
}
