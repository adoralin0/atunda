using UnityEngine;

public class PoseMemory : MonoBehaviour, MotionTrackingPose
{
    private UPose server;

    private long previous_frame=0;
    
    public int BufferSize=101;

    Quaternion[] Pelvis;
    Quaternion[] Spine;
    Quaternion[] RightShoulder;
    Quaternion[] LeftShoulder;
    Quaternion[] LeftForeArm;
    Quaternion[] RightForeArm;
    Quaternion[] RightUpLeg;
    Quaternion[] LeftUpLeg;
    Quaternion[] LeftLeg;
    Quaternion[] RightLeg;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        server = FindFirstObjectByType<UPose>();
        if (server == null)
        {
            Debug.LogError("You must have a MotionTracking server in the scene!");
            return;
        }
        Pelvis=new Quaternion[BufferSize];
        Spine=new Quaternion[BufferSize];
        RightShoulder=new Quaternion[BufferSize];
        LeftShoulder=new Quaternion[BufferSize];
        RightForeArm=new Quaternion[BufferSize];
        LeftForeArm=new Quaternion[BufferSize];
        RightUpLeg=new Quaternion[BufferSize];
        LeftUpLeg=new Quaternion[BufferSize];
        RightLeg=new Quaternion[BufferSize];
        LeftLeg=new Quaternion[BufferSize];
        for(int i=0;i<BufferSize;i++){
            RightUpLeg[i]=Quaternion.Euler(0,0,180);
            LeftUpLeg[i]=Quaternion.Euler(0,0,180);
        }
    }

    private int i=0;

    // Update is called once per frame
    void Update()
    {
        if(server==null)return;

        long current_frame=server.getFrameCounter();
        if(current_frame<=previous_frame)return;
       
        Pelvis[i]=server.GetRotation(Landmark.PELVIS);
        Spine[i]=server.GetRotation(Landmark.SHOULDER_CENTER);
        RightShoulder[i]=server.GetRotation(Landmark.RIGHT_SHOULDER);
        LeftShoulder[i]=server.GetRotation(Landmark.LEFT_SHOULDER);
        LeftForeArm[i]=server.GetRotation(Landmark.LEFT_ELBOW);
        RightForeArm[i]=server.GetRotation(Landmark.RIGHT_ELBOW);
        RightUpLeg[i]=server.GetRotation(Landmark.RIGHT_HIP);
        LeftUpLeg[i]=server.GetRotation(Landmark.LEFT_HIP);
        LeftLeg[i]=server.GetRotation(Landmark.LEFT_KNEE);
        RightLeg[i]=server.GetRotation(Landmark.RIGHT_KNEE);
        i+=1;
        if(i>=BufferSize)i=0;

        previous_frame=current_frame;
    }

    public Quaternion GetRotation(Landmark landmark, int back_in_time){

        int j=i-1+back_in_time;
        while(j<0)j+=BufferSize;
        
        j=(j)%(BufferSize);

        switch(landmark){
            case Landmark.PELVIS:
            return Pelvis[j]; 
            break;
            case Landmark.SHOULDER_CENTER:
            return Spine[j]; 
            break;
            case Landmark.RIGHT_SHOULDER:
            return RightShoulder[j]; 
            break;
            case Landmark.LEFT_SHOULDER:
            return LeftShoulder[j]; 
            break;
            case Landmark.RIGHT_ELBOW:
            return RightForeArm[j]; 
            break;
            case Landmark.LEFT_ELBOW:
            return LeftForeArm[j]; 
            break;
            case Landmark.RIGHT_HIP:
            return RightUpLeg[j]; 
            break;
            case Landmark.LEFT_HIP:
            return LeftUpLeg[j]; 
            break;
            case Landmark.RIGHT_KNEE:
            return RightLeg[j]; 
            break;
            case Landmark.LEFT_KNEE:
            return LeftLeg[j]; 
            break;
            default:
            return Quaternion.identity;
            break;
        }
    }

    public Quaternion GetRotation(Landmark landmark){
        return GetRotation(landmark,0);
    }

    public long getFrameCounter(){
        return previous_frame;
    }
}
