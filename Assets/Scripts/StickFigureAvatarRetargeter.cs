using UnityEngine;

/// <summary>
/// Drives ReadyPlayerAvatar from the stick figure's displayed joint positions
/// using the same UPose IK rules as live tracking.
/// </summary>
public static class StickFigureAvatarRetargeter
{
    public static bool TryApply(
        ReadyPlayerAvatar avatar,
        NpzStickFigureVisualizer stick,
        string danceId,
        ref string activeDanceId,
        ref Vector3 hipsLocalOffset,
        ref bool hasHipsLocalOffset)
    {
        if (avatar == null || stick == null || !avatar.isLoaded())
        {
            return false;
        }

        if (!TryReadStickJoints(stick, useInterpolatedJoints: true, out StickJoints joints))
        {
            return false;
        }

        UposeStickPoseSolver.HipLocalJoints hipLocal = ToAvatarHipLocalJoints(avatar.transform, joints);
        hipLocal = avatar.SmoothStickJoints(hipLocal, Time.deltaTime);
        if (!UposeStickPoseSolver.TrySolve(hipLocal, out UposeStickPoseSolver.SolvedPose pose))
        {
            return false;
        }

        Transform root = avatar.transform;
        Transform hips = avatar.GetPelvisTransform();
        if (root == null || hips == null)
        {
            return false;
        }

        if (danceId != activeDanceId)
        {
            activeDanceId = danceId;
            hipsLocalOffset = root.InverseTransformPoint(hips.position);
            hasHipsLocalOffset = true;
            avatar.ResetStickPoseSmoothing();

            if (stick.TryGetPlaybackSample(out NpzMotionFrame measureFrame, out _, out _))
            {
                avatar.BeginDanceTranslationAnchor(danceId, joints.hipMid, stick, measureFrame);
            }
        }

        if (avatar.ApplyStickWorldRoot && hasHipsLocalOffset)
        {
            Vector3 targetRootPos = avatar.ComputeScaledStickRootTarget(
                joints.hipMid,
                stick,
                hipsLocalOffset);
            avatar.ApplySmoothedStickRoot(targetRootPos, Time.deltaTime);
        }

        avatar.ApplySmoothedStickPose(pose, Time.deltaTime);
        avatar.ApplyFloorIfEnabled();
        return true;
    }

    struct StickJoints
    {
        public Vector3 hipMid;
        public Vector3 neck;
        public Vector3 leftShoulder;
        public Vector3 rightShoulder;
        public Vector3 leftElbow;
        public Vector3 rightElbow;
        public Vector3 leftWrist;
        public Vector3 rightWrist;
        public Vector3 leftHip;
        public Vector3 rightHip;
        public Vector3 leftKnee;
        public Vector3 rightKnee;
        public Vector3 leftAnkle;
        public Vector3 rightAnkle;
    }

    static bool TryReadStickJoints(NpzStickFigureVisualizer stick, bool useInterpolatedJoints, out StickJoints joints)
    {
        joints = default;
        if (!TryGetJoint(stick, useInterpolatedJoints, "left-hip", out joints.leftHip)
            || !TryGetJoint(stick, useInterpolatedJoints, "right-hip", out joints.rightHip)
            || !TryGetJoint(stick, useInterpolatedJoints, "neck", out joints.neck)
            || !TryGetJoint(stick, useInterpolatedJoints, "left-shoulder", out joints.leftShoulder)
            || !TryGetJoint(stick, useInterpolatedJoints, "right-shoulder", out joints.rightShoulder)
            || !TryGetJoint(stick, useInterpolatedJoints, "left-elbow", out joints.leftElbow)
            || !TryGetJoint(stick, useInterpolatedJoints, "right-elbow", out joints.rightElbow)
            || !TryGetJoint(stick, useInterpolatedJoints, "left-wrist", out joints.leftWrist)
            || !TryGetJoint(stick, useInterpolatedJoints, "right-wrist", out joints.rightWrist)
            || !TryGetJoint(stick, useInterpolatedJoints, "left-knee", out joints.leftKnee)
            || !TryGetJoint(stick, useInterpolatedJoints, "right-knee", out joints.rightKnee)
            || !TryGetJoint(stick, useInterpolatedJoints, "left-ankle", out joints.leftAnkle)
            || !TryGetJoint(stick, useInterpolatedJoints, "right-ankle", out joints.rightAnkle))
        {
            return false;
        }

        joints.hipMid = (joints.leftHip + joints.rightHip) * 0.5f;
        return true;
    }

    static bool TryGetJoint(
        NpzStickFigureVisualizer stick,
        bool useInterpolatedJoints,
        string jointName,
        out Vector3 worldPos)
    {
        return useInterpolatedJoints
            ? stick.TryGetInterpolatedJointWorldPosition(jointName, out worldPos)
            : stick.TryGetJointWorldPosition(jointName, out worldPos);
    }

    static UposeStickPoseSolver.HipLocalJoints ToAvatarHipLocalJoints(Transform avatarRoot, StickJoints joints)
    {
        Vector3 HipRel(Vector3 worldPos)
        {
            Vector3 worldOffset = worldPos - joints.hipMid;
            return avatarRoot != null
                ? avatarRoot.InverseTransformDirection(worldOffset)
                : worldOffset;
        }

        Vector3 shoulderCenter = (joints.leftShoulder + joints.rightShoulder) * 0.5f;

        return new UposeStickPoseSolver.HipLocalJoints
        {
            leftHip = HipRel(joints.leftHip),
            rightHip = HipRel(joints.rightHip),
            shoulderCenter = HipRel(shoulderCenter),
            leftShoulder = HipRel(joints.leftShoulder),
            rightShoulder = HipRel(joints.rightShoulder),
            leftElbow = HipRel(joints.leftElbow),
            rightElbow = HipRel(joints.rightElbow),
            leftWrist = HipRel(joints.leftWrist),
            rightWrist = HipRel(joints.rightWrist),
            leftKnee = HipRel(joints.leftKnee),
            rightKnee = HipRel(joints.rightKnee),
            leftAnkle = HipRel(joints.leftAnkle),
            rightAnkle = HipRel(joints.rightAnkle),
        };
    }
}
