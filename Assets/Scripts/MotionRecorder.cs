using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

[Serializable]
public class MotionSample
{
    public float time;
    public Quaternion hips;
    public Quaternion spine;
    public Quaternion rightArm;
    public Quaternion leftArm;
    public Quaternion leftForeArm;
    public Quaternion rightForeArm;
    public Quaternion rightUpLeg;
    public Quaternion leftUpLeg;
    public Quaternion leftLeg;
    public Quaternion rightLeg; 
}

[Serializable]
public class MotionRecording
{
    public List<MotionSample> samples = new List<MotionSample>();
}

public class MotionRecorder : MonoBehaviour
{

    private MotionRecording recording = new MotionRecording();
    private MotionTrackingPose server;

    private float startTime;
    public bool isRecording;
    private long lastFrameRecorded;
    public long framesRecorded;

    public long mediapipeFrame;

    private void Start()
    {
        server = FindFirstObjectByType<UPose>();
        if (server == null)
        {
            Debug.LogError("You must have a MotionTracking server in the scene!");
            return;
        }
    }

    private void FixedUpdate()
    {
        mediapipeFrame= server.getFrameCounter();

        if (!isRecording) return;
        if(mediapipeFrame == lastFrameRecorded) return;

        MotionSample sample = new MotionSample
        {
            time = Time.time - startTime,
            hips = server.GetRotation(Landmark.PELVIS),
            spine = server.GetRotation(Landmark.SHOULDER_CENTER),
            rightArm = server.GetRotation(Landmark.RIGHT_SHOULDER),
            leftArm = server.GetRotation(Landmark.LEFT_SHOULDER),
            leftForeArm = server.GetRotation(Landmark.LEFT_ELBOW),
            rightForeArm = server.GetRotation(Landmark.RIGHT_ELBOW),
            rightUpLeg = server.GetRotation(Landmark.RIGHT_HIP),
            leftUpLeg = server.GetRotation(Landmark.LEFT_HIP),
            leftLeg = server.GetRotation(Landmark.LEFT_KNEE),
            rightLeg = server.GetRotation(Landmark.RIGHT_KNEE)
        };

        recording.samples.Add(sample);
        framesRecorded++;
        lastFrameRecorded = mediapipeFrame;
    }

    public void StartRecording()
    {
        recording = new MotionRecording();
        startTime = Time.time;
        framesRecorded = 0;
        isRecording = true;
        Debug.Log("Recording started");
    }

    public void StopRecording()
    {
        isRecording = false;
        Debug.Log("Recording stopped");
        SaveToJson();
    }

    public void SaveToJson()
    {
        string json = JsonUtility.ToJson(recording, true); // pretty print

        string path = Path.Combine(Application.persistentDataPath, "motion.json");
        File.WriteAllText(path, json);

        Debug.Log("Saved to: " + path);
    }
}