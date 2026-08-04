using UnityEngine;

/// <summary>
/// Mirrors UPose.cs IK on hip-centered joint positions (same math as live tracking).
/// </summary>
public static class UposeStickPoseSolver
{
    public struct HipLocalJoints
    {
        public Vector3 leftHip;
        public Vector3 rightHip;
        public Vector3 shoulderCenter;
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

    public struct SolvedPose
    {
        public Quaternion pelvis;
        public Quaternion spine;
        public Quaternion leftShoulder;
        public Quaternion leftElbow;
        public Quaternion rightShoulder;
        public Quaternion rightElbow;
        public Quaternion leftHip;
        public Quaternion leftKnee;
        public Quaternion rightHip;
        public Quaternion rightKnee;
    }

    public static bool TrySolve(HipLocalJoints joints, out SolvedPose pose)
    {
        pose = default;

        if ((joints.rightHip - joints.leftHip).sqrMagnitude < 1e-8f)
        {
            return false;
        }

        Vector3 pelvisPos = (joints.leftHip + joints.rightHip) * 0.5f;

        pose.pelvis = SolvePelvis(joints.leftHip, joints.rightHip);
        pose.spine = SolveTorso(pose.pelvis, pelvisPos, joints.shoulderCenter);

        pose.leftShoulder = SolveShoulder(joints.leftShoulder, joints.leftElbow, pose.spine, true);
        pose.rightShoulder = SolveShoulder(joints.rightShoulder, joints.rightElbow, pose.spine, false);

        SolveElbow(
            joints.leftElbow,
            joints.leftWrist,
            pose.leftShoulder,
            true,
            out pose.leftShoulder,
            out pose.leftElbow);
        SolveElbow(
            joints.rightElbow,
            joints.rightWrist,
            pose.rightShoulder,
            false,
            out pose.rightShoulder,
            out pose.rightElbow);

        pose.leftHip = SolveThigh(joints.leftHip, joints.leftKnee, pose.pelvis, true);
        pose.rightHip = SolveThigh(joints.rightHip, joints.rightKnee, pose.pelvis, false);

        pose.leftKnee = SolveKnee(joints.leftKnee, joints.leftAnkle, pose.leftHip);
        pose.rightKnee = SolveKnee(joints.rightKnee, joints.rightAnkle, pose.rightHip);

        return true;
    }

    static Quaternion SolvePelvis(Vector3 leftHip, Vector3 rightHip)
    {
        Vector3 direction = (rightHip - leftHip).normalized;
        Vector3 directionXZ = new Vector3(direction.x, 0f, direction.z).normalized;
        float signedAngle = Vector3.SignedAngle(directionXZ, Vector3.right, Vector3.up);
        return Quaternion.Euler(0f, -signedAngle, 0f);
    }

    static Quaternion SolveTorso(Quaternion pelvisQ, Vector3 pelvisPos, Vector3 shoulderCenter)
    {
        Vector3 direction = (shoulderCenter - pelvisPos).normalized;
        if (direction.sqrMagnitude < 1e-8f)
        {
            return pelvisQ;
        }

        Vector3 localDirection = Quaternion.Inverse(pelvisQ) * direction;
        float rotZ = Mathf.Asin(-localDirection.x) * Mathf.Rad2Deg;
        float rotX = Mathf.Atan2(localDirection.z, localDirection.y) * Mathf.Rad2Deg;
        return pelvisQ * Quaternion.Euler(rotX, 0f, rotZ);
    }

    static Quaternion SolveShoulder(Vector3 shoulderPos, Vector3 elbowPos, Quaternion spineQ, bool left)
    {
        Vector3 direction = (elbowPos - shoulderPos).normalized;
        if (direction.sqrMagnitude < 1e-8f)
        {
            return spineQ;
        }

        if (left)
        {
            Vector3 localDirection = Quaternion.Inverse(spineQ * Quaternion.Euler(0f, 0f, 90f)) * direction;
            float rotZ = Mathf.Asin(-localDirection.x) * Mathf.Rad2Deg;
            float rotX = Mathf.Atan2(localDirection.z, localDirection.y) * Mathf.Rad2Deg;
            return spineQ * Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(rotX, 0f, rotZ);
        }

        Vector3 rightLocal = Quaternion.Inverse(spineQ * Quaternion.Euler(0f, 0f, -90f)) * direction;
        float rightRotZ = Mathf.Asin(-rightLocal.x) * Mathf.Rad2Deg;
        float rightRotX = Mathf.Atan2(rightLocal.z, rightLocal.y) * Mathf.Rad2Deg;
        return spineQ * Quaternion.Euler(0f, 0f, -90f) * Quaternion.Euler(rightRotX, 0f, rightRotZ);
    }

    static void SolveElbow(
        Vector3 elbowPos,
        Vector3 wristPos,
        Quaternion shoulderQ,
        bool left,
        out Quaternion shoulderOut,
        out Quaternion elbowOut)
    {
        shoulderOut = shoulderQ;
        elbowOut = shoulderQ;

        Vector3 direction = (wristPos - elbowPos).normalized;
        if (direction.sqrMagnitude < 1e-8f)
        {
            return;
        }

        Vector3 localDirection = Quaternion.Inverse(shoulderQ) * direction;

        float rotZ;
        float rotY;
        if (left)
        {
            rotZ = -Mathf.Acos(Mathf.Clamp(localDirection.y, -1f, 1f)) * Mathf.Rad2Deg;
            rotY = Mathf.Atan2(-localDirection.z, localDirection.x) * Mathf.Rad2Deg;
        }
        else
        {
            rotZ = Mathf.Acos(Mathf.Clamp(localDirection.y, -1f, 1f)) * Mathf.Rad2Deg;
            rotY = Mathf.Atan2(localDirection.z, -localDirection.x) * Mathf.Rad2Deg;
        }

        float w = Mathf.Abs(rotZ);
        if (w < 20f)
        {
            if (w < 10f)
            {
                rotY = 0f;
            }
            else
            {
                rotY = rotY * (w - 10f) / 10f;
            }
        }

        shoulderOut = shoulderQ * Quaternion.Euler(0f, rotY, 0f);
        elbowOut = shoulderQ * Quaternion.Euler(0f, rotY, rotZ);
    }

    static Quaternion SolveThigh(Vector3 hipPos, Vector3 kneePos, Quaternion pelvisQ, bool left)
    {
        Vector3 direction = (kneePos - hipPos).normalized;
        if (direction.sqrMagnitude < 1e-8f)
        {
            return pelvisQ;
        }

        Vector3 localDirection = Quaternion.Inverse(pelvisQ) * direction;
        float rotZ = Mathf.Asin(localDirection.x) * Mathf.Rad2Deg;
        float rotX = Mathf.Atan2(-localDirection.z, -localDirection.y) * Mathf.Rad2Deg;
        if (Mathf.Approximately(rotX, -180f))
        {
            rotX = 0f;
        }

        return pelvisQ * Quaternion.Euler(rotX, 0f, rotZ + 180f);
    }

    static Quaternion SolveKnee(Vector3 kneePos, Vector3 anklePos, Quaternion hipQ)
    {
        Vector3 direction = (anklePos - kneePos).normalized;
        if (direction.sqrMagnitude < 1e-8f)
        {
            return hipQ;
        }

        Vector3 localDirection = Quaternion.Inverse(hipQ) * direction;
        float rotZ = Mathf.Asin(-localDirection.x) * Mathf.Rad2Deg;
        float rotX = Mathf.Atan2(localDirection.z, localDirection.y) * Mathf.Rad2Deg;
        return hipQ * Quaternion.Euler(rotX, 0f, rotZ);
    }
}
