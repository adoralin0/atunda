using UnityEngine;

/// <summary>
/// Compares Euclidean segment lengths from npz-json keypoints vs RPM avatar bones.
/// Ratio avatar/tracked is used to scale world translations during stick retarget.
/// </summary>
public static class BodyProportionScaler
{
    public struct SegmentLengths
    {
        public float leftHipToKnee;
        public float rightHipToKnee;
        public float leftKneeToAnkle;
        public float rightKneeToAnkle;
        public float leftLegHeight;
        public float rightLegHeight;
        public float averageThigh;
        public float averageShin;
        public float averageLegHeight;
        public float neckToHip;
        public float hipToShoulderCenter;
    }

    public struct SegmentReport
    {
        public SegmentLengths tracked;
        public SegmentLengths avatar;
        public float legHeightRatio;
        public float thighRatio;
        public float shinRatio;
        public float torsoRatio;

        public void Log(string avatarName)
        {
            Debug.Log(
                "BodyProportionScaler on '" + avatarName + "':\n" +
                "  tracked leg avg=" + tracked.averageLegHeight.ToString("F3") + "m" +
                " (thigh " + tracked.averageThigh.ToString("F3") +
                ", shin " + tracked.averageShin.ToString("F3") +
                ", neck-hip " + tracked.neckToHip.ToString("F3") + ")\n" +
                "  avatar leg avg=" + avatar.averageLegHeight.ToString("F3") + "m" +
                " (thigh " + avatar.averageThigh.ToString("F3") +
                ", shin " + avatar.averageShin.ToString("F3") +
                ", neck-hip " + avatar.neckToHip.ToString("F3") + ")\n" +
                "  ratio avatar/tracked leg=" + legHeightRatio.ToString("F3") +
                ", thigh=" + thighRatio.ToString("F3") +
                ", shin=" + shinRatio.ToString("F3") +
                ", torso=" + torsoRatio.ToString("F3"));
        }
    }

    public static bool TryMeasureTrackedPerson(NpzMotionFrame frame, Vector3 axisFlip, out SegmentLengths lengths)
    {
        lengths = default;

        if (!TryDistance(frame, "left-hip", "left-knee", axisFlip, out lengths.leftHipToKnee)
            || !TryDistance(frame, "right-hip", "right-knee", axisFlip, out lengths.rightHipToKnee)
            || !TryDistance(frame, "left-knee", "left-ankle", axisFlip, out lengths.leftKneeToAnkle)
            || !TryDistance(frame, "right-knee", "right-ankle", axisFlip, out lengths.rightKneeToAnkle))
        {
            return false;
        }

        lengths.leftLegHeight = lengths.leftHipToKnee + lengths.leftKneeToAnkle;
        lengths.rightLegHeight = lengths.rightHipToKnee + lengths.rightKneeToAnkle;
        lengths.averageThigh = (lengths.leftHipToKnee + lengths.rightHipToKnee) * 0.5f;
        lengths.averageShin = (lengths.leftKneeToAnkle + lengths.rightKneeToAnkle) * 0.5f;
        lengths.averageLegHeight = (lengths.leftLegHeight + lengths.rightLegHeight) * 0.5f;

        if (TryPoint(frame, "left-hip", axisFlip, out Vector3 leftHip)
            && TryPoint(frame, "right-hip", axisFlip, out Vector3 rightHip))
        {
            Vector3 hipCenter = (leftHip + rightHip) * 0.5f;

            if (TryPoint(frame, "neck", axisFlip, out Vector3 neck))
            {
                lengths.neckToHip = Vector3.Distance(hipCenter, neck);
            }

            if (TryPoint(frame, "left-shoulder", axisFlip, out Vector3 leftShoulder)
                && TryPoint(frame, "right-shoulder", axisFlip, out Vector3 rightShoulder))
            {
                Vector3 shoulderCenter = (leftShoulder + rightShoulder) * 0.5f;
                lengths.hipToShoulderCenter = Vector3.Distance(hipCenter, shoulderCenter);
            }
        }

        return lengths.averageLegHeight > 1e-4f;
    }

    public static bool TryMeasureAvatar(ReadyPlayerAvatar avatar, out SegmentLengths lengths)
    {
        lengths = default;
        if (avatar == null || !avatar.isLoaded() || !avatar.HasBindAxes)
        {
            return false;
        }

        ReadyPlayerAvatar.LimbBindAxes axes = avatar.GetBindAxes();
        lengths.leftHipToKnee = axes.leftUpperLegLength;
        lengths.rightHipToKnee = axes.rightUpperLegLength;
        lengths.leftKneeToAnkle = axes.leftLowerLegLength;
        lengths.rightKneeToAnkle = axes.rightLowerLegLength;
        lengths.leftLegHeight = axes.leftLegLength;
        lengths.rightLegHeight = axes.rightLegLength;
        lengths.averageThigh = (lengths.leftHipToKnee + lengths.rightHipToKnee) * 0.5f;
        lengths.averageShin = (lengths.leftKneeToAnkle + lengths.rightKneeToAnkle) * 0.5f;
        lengths.averageLegHeight = (lengths.leftLegHeight + lengths.rightLegHeight) * 0.5f;

        Transform hips = avatar.GetPelvisTransform();
        Transform leftShoulder = avatar.GetLeftShoulderTransform();
        Transform rightShoulder = avatar.GetRightShoulderTransform();
        if (hips != null && leftShoulder != null && rightShoulder != null)
        {
            Vector3 shoulderCenter = (leftShoulder.position + rightShoulder.position) * 0.5f;
            lengths.hipToShoulderCenter = Vector3.Distance(hips.position, shoulderCenter);
            lengths.neckToHip = lengths.hipToShoulderCenter;
        }
        else
        {
            Transform spine = avatar.GetSpineTransform();
            if (hips != null && spine != null)
            {
                lengths.neckToHip = Vector3.Distance(hips.position, spine.position);
                lengths.hipToShoulderCenter = lengths.neckToHip;
            }
        }

        return lengths.averageLegHeight > 1e-4f;
    }

    public static bool TryBuildReport(
        NpzMotionFrame frame,
        Vector3 axisFlip,
        ReadyPlayerAvatar avatar,
        out SegmentReport report)
    {
        report = default;
        if (!TryMeasureTrackedPerson(frame, axisFlip, out report.tracked)
            || !TryMeasureAvatar(avatar, out report.avatar))
        {
            return false;
        }

        report.legHeightRatio = SafeRatio(report.avatar.averageLegHeight, report.tracked.averageLegHeight);
        report.thighRatio = SafeRatio(report.avatar.averageThigh, report.tracked.averageThigh);
        report.shinRatio = SafeRatio(report.avatar.averageShin, report.tracked.averageShin);
        report.torsoRatio = SafeRatio(report.avatar.hipToShoulderCenter, report.tracked.hipToShoulderCenter);
        return true;
    }

    public static float ComputeWorldTranslationScale(
        float legHeightRatio,
        float stickPoseScale,
        float manualMultiplier,
        bool useAutoRatio)
    {
        float baseScale = useAutoRatio ? legHeightRatio : 1f;
        if (stickPoseScale > 1e-4f)
        {
            baseScale /= stickPoseScale;
        }

        return baseScale * manualMultiplier;
    }

    static float SafeRatio(float avatarLength, float trackedLength)
    {
        if (trackedLength <= 1e-4f)
        {
            return 1f;
        }

        return avatarLength / trackedLength;
    }

    static bool TryDistance(NpzMotionFrame frame, string jointA, string jointB, Vector3 axisFlip, out float distance)
    {
        distance = 0f;
        if (!TryPoint(frame, jointA, axisFlip, out Vector3 pointA)
            || !TryPoint(frame, jointB, axisFlip, out Vector3 pointB))
        {
            return false;
        }

        distance = Vector3.Distance(pointA, pointB);
        return distance > 1e-6f;
    }

    static bool TryPoint(NpzMotionFrame frame, string jointName, Vector3 axisFlip, out Vector3 point)
    {
        point = default;
        if (!NpzJsonMotionParser.TryGetPoint(frame, jointName, out Vector3 raw))
        {
            return false;
        }

        point = Vector3.Scale(raw, axisFlip);
        return true;
    }
}
