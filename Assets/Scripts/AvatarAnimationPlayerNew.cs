using UnityEngine;
using System.IO;
using Newtonsoft.Json;

public class AvatarAnimationPlayerNew : MonoBehaviour
{
    public string jsonFileName;
    public float fps = 30f;
    public bool loop = true;

    MotionRecording recording;
    float startTime;

    MotionSample rotValues;

    void Start()
    {
        LoadData();
        startTime = Time.time;
    }

    void LoadData()
    {
        string path = Path.Combine(Application.persistentDataPath, "motion.json");

        if (!File.Exists(path))
        {
            Debug.LogError("File not found: " + path);
            return;
        }

        string json = File.ReadAllText(path);
        Debug.Log("Loaded json: " + json);
        recording=JsonConvert.DeserializeObject<MotionRecording>(json);


        Debug.Log("Loaded frames: " + recording.samples.Count);

    }

    void Update()
    {
        if (recording == null || recording.samples.Count == 0)
            return;

        int frame = Mathf.FloorToInt((Time.time - startTime) * fps);

        if (loop)
            frame %= recording.samples.Count;
        else
            frame = Mathf.Min(frame, recording.samples.Count - 1);

        rotValues = recording.samples[frame];

    }

    public MotionSample getRotations()
    {
        return rotValues;
    }
}
