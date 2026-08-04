using System;
using UnityEngine;

[Serializable]
public class AvatarMotionExport
{
    public string source;
    public string source_title;
    public int object_id;
    public float video_fps = 60f;
    public string[] bone_names;
    public CenteringInfo centering;
    public FrameData[] frames;
}

[Serializable]
public class CenteringInfo
{
    public string method;
    public float[] mean_pred_cam_t;
}

[Serializable]
public class FrameData
{
    public int frame_index;
    public string frame_name;
    public float[] pred_cam_t_centered;
    public float[] body_pose_params;
    public BoneSample[] bones;
}

[Serializable]
public class BoneSample
{
    public string name;
    public int index;
    public float[] point_3d;
    public float[] point_3d_cam;
    public float[] point_2d;
    public float[] rotation;
    public float[] rotation_cam;
}

[Serializable]
public class BoneBinding
{
    public string jsonBoneName;
    public Transform target;
    public bool useLocalPosition = true;
    public Vector3 positionScale = Vector3.one;
    public Vector3 positionOffset = Vector3.zero;
    public bool useLocalRotation = true;
    // Optional: name of a child joint in the JSON to compute a look-rotation
    public string childJsonBoneName;
    // Rotation offset applied after computed or source quaternion (Euler degrees)
    public Vector3 rotationOffsetEuler = Vector3.zero;
    // If true, prefer the quaternion provided in JSON (if present). If false, compute look rotation from positions.
    public bool preferSourceQuaternion = true;
}
