using UnityEngine;

public interface MotionTrackingPose
{
    public Quaternion GetRotation(Landmark i);
    public Quaternion GetRotation(Landmark i,int Delay);
    public long getFrameCounter();
}
