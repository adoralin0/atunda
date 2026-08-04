using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Converts npz-json keypoints into 19-float RPM dance frames
/// using the same IK rules as UPose.cs (ReadyPlayerAvatar playback layout).
/// </summary>
public static class RpmDanceConverter
{
    public const int RotationCount = 19;
    public const int RootOffsetStart = 19;
    public const int FrameWithRootCount = 22;

    static readonly Vector3 DefaultAxisFlip = new Vector3(1f, -1f, 1f);

    public static float[][] ToLegacyFrames(NpzMotionClip clip, Vector3 axisFlip = default)
    {
        if (clip?.frames == null || clip.frames.Length == 0)
        {
            return Array.Empty<float[]>();
        }

        if (axisFlip == default)
        {
            axisFlip = DefaultAxisFlip;
        }

        float[][] output = new float[clip.frames.Length][];
        for (int i = 0; i < clip.frames.Length; i++)
        {
            output[i] = ConvertFrame(clip.frames[i], axisFlip);
        }

        return output;
    }

    static float[] LerpFrame(float[] a, float[] b, float blend)
    {
        int n = Mathf.Min(a?.Length ?? 0, b?.Length ?? 0);
        float[] o = new float[n];
        for (int i = 0; i < n; i++)
        {
            o[i] = Mathf.Lerp(a[i], b[i], blend);
        }

        return o;
    }

    public static float[][] ResampleToFrameCount(float[][] frames, int outCount)
    {
        if (frames == null || frames.Length == 0 || outCount <= 0)
        {
            return frames;
        }

        if (outCount == frames.Length)
        {
            return frames;
        }

        float[][] output = new float[outCount][];
        for (int i = 0; i < outCount; i++)
        {
            float t = i * (frames.Length - 1) / Mathf.Max(1f, outCount - 1f);
            int i0 = Mathf.FloorToInt(t);
            int i1 = Mathf.Min(i0 + 1, frames.Length - 1);
            float a = t - i0;
            output[i] = LerpFrame(frames[i0], frames[i1], a);
        }

        return output;
    }

    public static string ResolveLegacyReferencePath(string npzSource, string streamingAssetsDir)
    {
        if (string.IsNullOrEmpty(npzSource))
        {
            return null;
        }

        string normalized = npzSource.Replace('\\', '/');
        if (!normalized.EndsWith("feature_vectors/1.npz", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string refPath = Path.Combine(streamingAssetsDir, "atunda", "ETIGHI_reference.json");
        return File.Exists(refPath) ? refPath : null;
    }

    public struct PoseLocalJoints
    {
        public Vector3 leftHip;
        public Vector3 rightHip;
        public Vector3 neck;
        public Vector3 leftShoulder;
        public Vector3 rightShoulder;
        public Vector3 leftElbow;
        public Vector3 rightElbow;
        public Vector3 leftWrist;
        public Vector3 rightWrist;
        public Vector3 leftKnee;
        public Vector3 rightKnee;
        public Vector3 leftAnkle;
        public Vector3 rightAnkle;
    }

    /// <summary>
    /// Same IK as ConvertFrame, but from hip-relative pose-local positions
    /// (e.g. inverse-mapped from NpzStickFigureVisualizer world joints).
    /// </summary>
    public static float[] ConvertFromPoseLocal(PoseLocalJoints joints)
    {
        float hipsY = CalculatePelvisYaw(joints.leftHip, joints.rightHip, joints.neck);
        Quaternion pelvisQ = Quaternion.Euler(0f, hipsY, 0f);

        Vector3 hipMid = (joints.leftHip + joints.rightHip) * 0.5f;
        CalculateTorso(pelvisQ, hipMid, joints.neck, out float spineX, out float spineZ);
        Quaternion spineQ = pelvisQ * Quaternion.Euler(spineX, 0f, spineZ);

        ExtractArmDanceEulerFromPoints(
            joints.leftShoulder, joints.leftElbow, joints.leftWrist, spineQ, true,
            out float l0, out float l1, out float l2, out float le);
        ExtractArmDanceEulerFromPoints(
            joints.rightShoulder, joints.rightElbow, joints.rightWrist, spineQ, false,
            out float r0, out float r1, out float r2, out float re);
        ExtractLegFromPoints(
            joints.leftHip, joints.leftKnee, joints.leftAnkle, pelvisQ,
            out float lh0, out float lh1, out float lk0, out float lk1);
        ExtractLegFromPoints(
            joints.rightHip, joints.rightKnee, joints.rightAnkle, pelvisQ,
            out float rh0, out float rh1, out float rk0, out float rk1);

        return new[]
        {
            hipsY, spineX, spineZ,
            l0, l1, l2, r0, r1, r2,
            le, re,
            lh0, lh1, rh0, rh1,
            lk0, lk1, rk0, rk1
        };
    }

    public static float[] ConvertFrame(NpzMotionFrame frame, Vector3 axisFlip = default)
    {
        if (!TryPoint(frame, "left-hip", axisFlip, out Vector3 leftHip)
            || !TryPoint(frame, "right-hip", axisFlip, out Vector3 rightHip)
            || !TryPoint(frame, "neck", axisFlip, out Vector3 neck))
        {
            return AppendRootOffset(ZeroFrame(), frame, axisFlip);
        }

        Vector3 hipMid = (leftHip + rightHip) * 0.5f;
        PoseLocalJoints joints = new PoseLocalJoints
        {
            leftHip = leftHip,
            rightHip = rightHip,
            neck = neck,
            leftShoulder = TryPoint(frame, "left-shoulder", axisFlip, out Vector3 ls) ? ls : leftHip,
            rightShoulder = TryPoint(frame, "right-shoulder", axisFlip, out Vector3 rs) ? rs : rightHip,
            leftElbow = TryPoint(frame, "left-elbow", axisFlip, out Vector3 le) ? le : leftHip,
            rightElbow = TryPoint(frame, "right-elbow", axisFlip, out Vector3 re) ? re : rightHip,
            leftWrist = TryPoint(frame, "left-wrist", axisFlip, out Vector3 lw) ? lw : leftHip,
            rightWrist = TryPoint(frame, "right-wrist", axisFlip, out Vector3 rw) ? rw : rightHip,
            leftKnee = TryPoint(frame, "left-knee", axisFlip, out Vector3 lk) ? lk : leftHip,
            rightKnee = TryPoint(frame, "right-knee", axisFlip, out Vector3 rk) ? rk : rightHip,
            leftAnkle = TryPoint(frame, "left-ankle", axisFlip, out Vector3 la) ? la : leftHip,
            rightAnkle = TryPoint(frame, "right-ankle", axisFlip, out Vector3 ra) ? ra : rightHip,
        };

        float[] rotations = ConvertFromPoseLocal(joints);
        return AppendRootOffset(rotations, frame, axisFlip);
    }

    public static float[] AppendRootOffset(float[] rotations, NpzMotionFrame sourceFrame, Vector3 axisFlip = default)
    {
        if (rotations == null)
        {
            return ZeroFrameWithRoot();
        }

        if (axisFlip == default)
        {
            axisFlip = DefaultAxisFlip;
        }

        float[] output = new float[FrameWithRootCount];
        int copyCount = Mathf.Min(rotations.Length, RotationCount);
        for (int i = 0; i < copyCount; i++)
        {
            output[i] = rotations[i];
        }

        if (NpzJsonMotionParser.TryGetRootOffset(sourceFrame, axisFlip, out Vector3 root))
        {
            output[RootOffsetStart] = root.x;
            output[RootOffsetStart + 1] = root.y;
            output[RootOffsetStart + 2] = root.z;
        }

        return output;
    }

    public static bool HasRootOffset(float[] frame)
    {
        return frame != null && frame.Length >= FrameWithRootCount;
    }

    public static Vector3 ReadRootOffset(float[] frame)
    {
        if (!HasRootOffset(frame))
        {
            return Vector3.zero;
        }

        return new Vector3(frame[RootOffsetStart], frame[RootOffsetStart + 1], frame[RootOffsetStart + 2]);
    }

    static float CalculatePelvisYaw(Vector3 leftHip, Vector3 rightHip, Vector3 neck)
    {
        Vector3 hipLine = (rightHip - leftHip).normalized;
        Vector3 hipMid = (leftHip + rightHip) * 0.5f;
        Vector3 forward = (neck - hipMid).normalized;
        forward.y = 0f;
        hipLine.y = 0f;

        if (forward.sqrMagnitude < 1e-8f)
        {
            forward = Vector3.Cross(Vector3.up, hipLine).normalized;
        }

        return Mathf.Atan2(-forward.x, -forward.z) * Mathf.Rad2Deg;
    }

    static void CalculateTorso(Quaternion pelvisQ, Vector3 hipMid, Vector3 neck, out float spineX, out float spineZ)
    {
        Vector3 direction = (neck - hipMid).normalized;
        Vector3 localDirection = Quaternion.Inverse(pelvisQ) * direction;
        spineZ = Mathf.Asin(Mathf.Clamp(-localDirection.x, -1f, 1f)) * Mathf.Rad2Deg;
        spineX = Mathf.Atan2(localDirection.z, localDirection.y) * Mathf.Rad2Deg;
    }

    static void ExtractArmDanceEulerFromPoints(
        Vector3 shoulderPos,
        Vector3 elbowPos,
        Vector3 wristPos,
        Quaternion spineQ,
        bool left,
        out float e0,
        out float e1,
        out float e2,
        out float elbowY)
    {
        e0 = e1 = e2 = elbowY = 0f;

        if ((elbowPos - shoulderPos).sqrMagnitude < 1e-8f)
        {
            return;
        }

        Quaternion shoulderOffset = Quaternion.Euler(0f, 0f, left ? 90f : -90f);
        Quaternion rpmPreOffset = Quaternion.Euler(0f, 0f, left ? -90f : 90f);
        Vector3 upperDir = (elbowPos - shoulderPos).normalized;
        Vector3 localUpper = Quaternion.Inverse(spineQ * shoulderOffset) * upperDir;
        float rotZ = Mathf.Asin(Mathf.Clamp(-localUpper.x, -1f, 1f)) * Mathf.Rad2Deg;
        float rotX = Mathf.Atan2(localUpper.z, localUpper.y) * Mathf.Rad2Deg;

        // ReadyPlayerAvatar live layout: LeftArm = Euler(0,0,-90) * Euler(r3,r4,r5)
        Quaternion uposeShoulder = spineQ * shoulderOffset * Quaternion.Euler(rotX, 0f, rotZ);
        Quaternion rpmArm = Quaternion.Inverse(rpmPreOffset) * uposeShoulder;
        Vector3 euler = rpmArm.eulerAngles;
        e0 = NormalizeAngle(euler.x);
        e1 = NormalizeAngle(euler.y);
        e2 = NormalizeAngle(euler.z);

        if ((wristPos - elbowPos).sqrMagnitude < 1e-8f)
        {
            return;
        }

        Vector3 foreDir = (wristPos - elbowPos).normalized;
        Vector3 localFore = Quaternion.Inverse(uposeShoulder) * foreDir;

        float foreRotZ;
        float foreRotY;
        if (left)
        {
            foreRotZ = -Mathf.Acos(Mathf.Clamp(localFore.y, -1f, 1f)) * Mathf.Rad2Deg;
            foreRotY = Mathf.Atan2(-localFore.z, localFore.x) * Mathf.Rad2Deg;
        }
        else
        {
            foreRotZ = Mathf.Acos(Mathf.Clamp(localFore.y, -1f, 1f)) * Mathf.Rad2Deg;
            foreRotY = Mathf.Atan2(localFore.z, -localFore.x) * Mathf.Rad2Deg;
        }

        float w = Mathf.Abs(foreRotZ);
        if (w < 20f)
        {
            if (w < 10f)
            {
                foreRotY = 0f;
            }
            else
            {
                foreRotY = foreRotY * (w - 10f) / 10f;
            }
        }

        Quaternion uposeShoulderTwisted = uposeShoulder * Quaternion.Euler(0f, foreRotY, 0f);
        rpmArm = Quaternion.Inverse(rpmPreOffset) * uposeShoulderTwisted;
        Vector3 twistedEuler = rpmArm.eulerAngles;
        e0 = NormalizeAngle(twistedEuler.x);
        e1 = NormalizeAngle(twistedEuler.y);
        e2 = NormalizeAngle(twistedEuler.z);
        elbowY = foreRotZ;
    }

    static void ExtractLegFromPoints(
        Vector3 hipPos,
        Vector3 kneePos,
        Vector3 anklePos,
        Quaternion pelvisQ,
        out float hipX,
        out float hipZ,
        out float kneeX,
        out float kneeZ)
    {
        hipX = hipZ = kneeX = kneeZ = 0f;

        if ((kneePos - hipPos).sqrMagnitude >= 1e-8f)
        {
            Vector3 thighDir = (kneePos - hipPos).normalized;
            Vector3 localThigh = Quaternion.Inverse(pelvisQ) * thighDir;
            float rotZ = Mathf.Asin(Mathf.Clamp(localThigh.x, -1f, 1f)) * Mathf.Rad2Deg;
            float rotX = Mathf.Atan2(-localThigh.z, -localThigh.y) * Mathf.Rad2Deg;
            if (Mathf.Approximately(rotX, -180f))
            {
                rotX = 0f;
            }

            hipX = rotX;
            hipZ = rotZ + 180f;
        }

        if ((anklePos - kneePos).sqrMagnitude >= 1e-8f)
        {
            Quaternion hipQ = pelvisQ * Quaternion.Euler(hipX, 0f, hipZ);
            Vector3 shinDir = (anklePos - kneePos).normalized;
            Vector3 localShin = Quaternion.Inverse(hipQ) * shinDir;
            float rotZ = Mathf.Asin(Mathf.Clamp(-localShin.x, -1f, 1f)) * Mathf.Rad2Deg;
            float rotX = Mathf.Atan2(localShin.z, localShin.y) * Mathf.Rad2Deg;
            kneeX = rotX;
            kneeZ = rotZ;
        }
    }

    static bool TryPoint(NpzMotionFrame frame, string name, Vector3 axisFlip, out Vector3 p)
    {
        p = default;
        if (!NpzJsonMotionParser.TryGetPoint(frame, name, out Vector3 raw))
        {
            return false;
        }

        p = Vector3.Scale(raw, axisFlip);
        return true;
    }

    static float[] ZeroFrame()
    {
        return new float[RotationCount];
    }

    static float[] ZeroFrameWithRoot()
    {
        return new float[FrameWithRootCount];
    }

    static float NormalizeAngle(float a)
    {
        while (a > 180f) a -= 360f;
        while (a < -180f) a += 360f;
        return a;
    }
}
