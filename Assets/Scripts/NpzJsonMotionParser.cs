using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Reads npz-json (SAM-body4d / ATUNDA) for conversion pipelines.
/// </summary>
public static class NpzJsonMotionParser
{
    public static NpzMotionClip Parse(string jsonText, bool preferCameraSpace = true)
    {
        JObject root = JObject.Parse(jsonText);
        JObject arrays = root["arrays"] as JObject;
        if (arrays == null)
        {
            throw new Exception("Missing 'arrays' in npz-json JSON.");
        }

        JArray kp = preferCameraSpace ? ReadArray(arrays["pred_keypoints_3d_cam"]) : ReadArray(arrays["pred_keypoints_3d"]);
        if (kp == null || kp.Count == 0)
        {
            kp = preferCameraSpace ? ReadArray(arrays["pred_keypoints_3d"]) : ReadArray(arrays["pred_keypoints_3d_cam"]);
        }
        JArray names = ReadArray(arrays["frame_names"]);
        JArray bodyPose = ReadArray(arrays["body_pose_params"]);
        JArray cam = ReadArray(arrays["pred_cam_t"]);

        int frameCount = kp?.Count ?? 0;
        if (frameCount == 0)
        {
            throw new Exception("No frames in pred_keypoints_3d.");
        }

        string[] boneNames = ReadStringArray(arrays["keypoint_names"]);
        float fps = ReadScalarFloat(arrays["video_fps"], 60f);

        Vector3 camMean = ComputeCamMean(cam, frameCount);
        NpzMotionFrame[] frames = new NpzMotionFrame[frameCount];

        for (int fi = 0; fi < frameCount; fi++)
        {
            frames[fi] = new NpzMotionFrame
            {
                frameIndex = fi,
                frameName = names != null && fi < names.Count ? names[fi].ToString() : "frame_" + fi,
                keypoints = ReadFrameKeypoints(kp[fi], boneNames),
                bodyPoseParams = ReadFloatRow(bodyPose, fi),
                predCamCentered = ReadCamCentered(cam, fi, camMean)
            };
        }

        return new NpzMotionClip
        {
            source = root.Value<string>("source"),
            fps = fps,
            boneNames = boneNames,
            frames = frames,
            centeredCamMean = camMean
        };
    }

    public static bool TryGetPoint(NpzMotionFrame frame, string name, out Vector3 p)
    {
        p = default;
        if (frame?.keypoints == null || string.IsNullOrEmpty(name))
        {
            return false;
        }

        for (int i = 0; i < frame.keypoints.Length; i++)
        {
            if (string.Equals(frame.keypoints[i].name, name, StringComparison.OrdinalIgnoreCase))
            {
                p = frame.keypoints[i].position;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Mean-centered SAM camera translation (pred_cam_t) for root motion.
    /// </summary>
    public static bool TryGetRootOffset(NpzMotionFrame frame, Vector3 axisFlip, out Vector3 offset)
    {
        offset = default;
        if (frame?.predCamCentered == null || frame.predCamCentered.Length < 3)
        {
            return false;
        }

        offset = new Vector3(frame.predCamCentered[0], frame.predCamCentered[1], frame.predCamCentered[2]);
        if (axisFlip != default)
        {
            offset = Vector3.Scale(offset, axisFlip);
        }

        return true;
    }

    static Vector3 ComputeCamMean(JArray cam, int frameCount)
    {
        if (cam == null)
        {
            return Vector3.zero;
        }

        Vector3 sum = Vector3.zero;
        int n = 0;
        for (int i = 0; i < frameCount && i < cam.Count; i++)
        {
            if (TryReadVec3(cam[i], out Vector3 v))
            {
                sum += v;
                n++;
            }
        }

        return n > 0 ? sum / n : Vector3.zero;
    }

    static float[] ReadCamCentered(JArray cam, int fi, Vector3 mean)
    {
        if (cam == null || fi >= cam.Count || !TryReadVec3(cam[fi], out Vector3 v))
        {
            return null;
        }

        Vector3 c = v - mean;
        return new[] { c.x, c.y, c.z };
    }

    static float[] ReadFloatRow(JArray bodyPose, int fi)
    {
        if (bodyPose == null || fi >= bodyPose.Count || !(bodyPose[fi] is JArray row))
        {
            return null;
        }

        JArray r = (JArray)bodyPose[fi];
        float[] o = new float[r.Count];
        for (int i = 0; i < r.Count; i++)
        {
            o[i] = r[i].Value<float>();
        }

        return o;
    }

    static NpzKeypoint[] ReadFrameKeypoints(JToken frameToken, string[] names)
    {
        JArray frame = frameToken as JArray;
        if (frame == null)
        {
            return Array.Empty<NpzKeypoint>();
        }

        int n = frame.Count;
        NpzKeypoint[] pts = new NpzKeypoint[n];
        for (int i = 0; i < n; i++)
        {
            pts[i].name = names != null && i < names.Length ? names[i] : "kp_" + i;
            if (frame[i] is JArray xyz && xyz.Count >= 3)
            {
                pts[i].position = new Vector3(xyz[0].Value<float>(), xyz[1].Value<float>(), xyz[2].Value<float>());
            }
        }

        return pts;
    }

    static string[] ReadStringArray(JToken entry)
    {
        JArray data = ReadArray(entry);
        if (data == null)
        {
            return Array.Empty<string>();
        }

        string[] s = new string[data.Count];
        for (int i = 0; i < data.Count; i++)
        {
            s[i] = data[i].ToString();
        }

        return s;
    }

    static float ReadScalarFloat(JToken entry, float fallback)
    {
        JToken d = entry?["data"] ?? entry;
        if (d is JArray a && a.Count > 0)
        {
            return a[0].Value<float>();
        }

        return d != null ? d.Value<float>() : fallback;
    }

    static JArray ReadArray(JToken entry)
    {
        if (entry == null)
        {
            return null;
        }

        return entry["data"] as JArray ?? entry as JArray;
    }

    static bool TryReadVec3(JToken t, out Vector3 v)
    {
        if (t is JArray a && a.Count >= 3)
        {
            v = new Vector3(a[0].Value<float>(), a[1].Value<float>(), a[2].Value<float>());
            return true;
        }

        v = default;
        return false;
    }
}

[Serializable]
public class NpzMotionClip
{
    public string source;
    public float fps;
    public string[] boneNames;
    public NpzMotionFrame[] frames;
    public Vector3 centeredCamMean;
}

[Serializable]
public class NpzMotionFrame
{
    public int frameIndex;
    public string frameName;
    public NpzKeypoint[] keypoints;
    public float[] bodyPoseParams;
    public float[] predCamCentered;
}

[Serializable]
public struct NpzKeypoint
{
    public string name;
    public Vector3 position;
}
