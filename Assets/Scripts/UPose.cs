using System;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.ProBuilder.MeshOperations;


[DefaultExecutionOrder(-1)]
public class UPose : MonoBehaviour, MotionTrackingPose
{
    public string host = "127.0.0.1"; // This machines host.
    public int port = 52733; // Must match the Python side.
    private Transform bodyParent;
    private GameObject linePrefab;
    public float multiplier = 10f;
    public float landmarkScale = 1f;

    public bool RestrictMotionRange=true;

    public bool MMPose;

    private ServerUDP server;

    private Body body;

    private long frame_counter=0;

    public Transform GetLandmark(Landmark mark)
    {
        return body.instances[(int)mark].transform ;
    }

    private void Start()
    {
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

        GameObject child = new GameObject("Landmarks");
        child.transform.SetParent(transform);
        child.transform.localPosition=new Vector3(0,-5,20);
        child.SetActive(false);
        bodyParent=child.transform;

        GameObject linePrefab = new GameObject("linePrefab");
        LineRenderer lineRenderer = linePrefab.AddComponent<LineRenderer>();
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.cyan;
        lineRenderer.endColor = Color.cyan;

        body = new Body(bodyParent,linePrefab,landmarkScale);


        Thread t = new Thread(new ThreadStart(Run));
        t.Start();
    }
    private void Update()
    {
        UpdateBody(body);
    }


    private void CalculatePelvisRotation(Body b)
    {
        Vector3 p1=b.instances[(int)Landmark.LEFT_HIP].transform.localPosition;
        Vector3 p2=b.instances[(int)Landmark.RIGHT_HIP].transform.localPosition;
        
        Vector3 direction = (p2 - p1).normalized;
        
        Vector3 directionXZ = new Vector3(direction.x, 0, direction.z).normalized;
        float signedAngle = Vector3.SignedAngle(directionXZ, Vector3.right, Vector3.up);

        b.instances[(int)Landmark.PELVIS].transform.localRotation=Quaternion.Euler(0,-signedAngle,0);
    }
    

    private void CalculateTorsoRotation(Body b)
    {
        Vector3 p1=b.instances[(int)Landmark.PELVIS].transform.localPosition;
        Vector3 p2=b.instances[(int)Landmark.SHOULDER_CENTER].transform.localPosition;
        Quaternion base_rotation=b.instances[(int)Landmark.PELVIS].transform.localRotation;

        Vector3 direction = (p2 - p1).normalized;
        Vector3 localDirection = Quaternion.Inverse(base_rotation) * direction;
        float rotZ = Mathf.Asin(-localDirection.x) * Mathf.Rad2Deg;
        float rotX = Mathf.Atan2(localDirection.z, localDirection.y) * Mathf.Rad2Deg;
    
        b.instances[(int)Landmark.SHOULDER_CENTER].transform.localRotation = base_rotation*Quaternion.Euler(rotX,0,rotZ);
    }


    private void CalculateRotationShoulder(Body b, int bone1, int bone2, int base_bone,bool left)
    {
        Vector3 p1=b.instances[bone1].transform.localPosition;
        Vector3 p2=b.instances[bone2].transform.localPosition;
        Quaternion base_rotation=b.instances[base_bone].transform.localRotation;

        Vector3 direction = (p2 - p1).normalized;
        
        /*Vector3 localDirection = Quaternion.Inverse(base_rotation) * direction;

        float rotZ=0;
        float rotY=0;
        if(left){
            rotZ = -Mathf.Asin(localDirection.y) * Mathf.Rad2Deg+90;
            rotY = Mathf.Atan2(localDirection.z, -localDirection.x) * Mathf.Rad2Deg;
        }
        else{
            rotZ = Mathf.Asin(localDirection.y) * Mathf.Rad2Deg-90;
            rotY = -Mathf.Atan2(localDirection.z, localDirection.x) * Mathf.Rad2Deg;
        }
        
        
        if(rotZ<-180)rotZ+=360;
        
        if(RestrictMotionRange){

            if(left){
                if(rotZ<20)rotZ=20;
                else if(rotZ>160)rotZ=160;
            }else{
                if(rotZ>-20)rotZ=-20;
                else if(rotZ<-160)rotZ=-160;
            }
            
        }

        b.instances[bone1].transform.localRotation = base_rotation*Quaternion.Euler(0,rotY,rotZ);*/
 
        if(left){
            Vector3 localDirection = Quaternion.Inverse(base_rotation*Quaternion.Euler(0,0,90)) * direction;
            float rotZ = Mathf.Asin(-localDirection.x) * Mathf.Rad2Deg;
            float rotX = Mathf.Atan2(localDirection.z, localDirection.y) * Mathf.Rad2Deg;
            b.instances[bone1].transform.localRotation = base_rotation*Quaternion.Euler(0,0,90)*Quaternion.Euler(rotX,0,rotZ);
        }else{
            Vector3 localDirection = Quaternion.Inverse(base_rotation*Quaternion.Euler(0,0,-90)) * direction;
            float rotZ = Mathf.Asin(-localDirection.x) * Mathf.Rad2Deg;
            float rotX = Mathf.Atan2(localDirection.z, localDirection.y) * Mathf.Rad2Deg;
            b.instances[bone1].transform.localRotation = base_rotation*Quaternion.Euler(0,0,-90)*Quaternion.Euler(rotX,0,rotZ);
        }
    }

    private void CalculateRotationElbow(Body b, int bone1, int bone2, int base_bone, bool left)
    {
        Vector3 p1=b.instances[bone1].transform.localPosition;
        Vector3 p2=b.instances[bone2].transform.localPosition;
        Quaternion base_rotation=b.instances[base_bone].transform.localRotation;

        Vector3 direction = (p2 - p1).normalized;
        
        Vector3 localDirection = Quaternion.Inverse(base_rotation) * direction;
        
        //float rotZ = Mathf.Asin(-localDirection.x) * Mathf.Rad2Deg;
        //float rotX = Mathf.Atan2(localDirection.z, localDirection.y) * Mathf.Rad2Deg;
        
        float rotZ;
        float rotY;
        if(left){
            rotZ = -Mathf.Acos(localDirection.y) * Mathf.Rad2Deg;
            rotY = Mathf.Atan2(-localDirection.z, localDirection.x) * Mathf.Rad2Deg;
        }
        else{
            rotZ = Mathf.Acos(localDirection.y) * Mathf.Rad2Deg;
            rotY = Mathf.Atan2(localDirection.z,-localDirection.x) * Mathf.Rad2Deg;
        }

        float w=Mathf.Abs(rotZ);
        if(w<20){
            if(w<10){//between 10-0 rotY=0
                rotY=0;
            }else{//between 20-10 linear interpolation from rotY to 0
                rotY=rotY*(w-10)/10;
            }
        }

        b.instances[base_bone].transform.localRotation=base_rotation*Quaternion.Euler(0,rotY,0);

        b.instances[bone1].transform.localRotation = base_rotation*Quaternion.Euler(0,rotY,rotZ);
    }

    private void CalculateRotationThigh(Body b, int bone1, int bone2, int base_bone, bool left)
    {
        Vector3 p1=b.instances[bone1].transform.localPosition;
        Vector3 p2=b.instances[bone2].transform.localPosition;
        Quaternion base_rotation=b.instances[base_bone].transform.localRotation;

        Vector3 direction = (p2 - p1).normalized;
        
        Vector3 localDirection = Quaternion.Inverse(base_rotation) * direction;
        
        float rotZ = Mathf.Asin(localDirection.x) * Mathf.Rad2Deg;
        float rotX = Mathf.Atan2(-localDirection.z, -localDirection.y) * Mathf.Rad2Deg;

        if(rotX==-180)rotX=0;

        if(RestrictMotionRange)
        {
            if(left)
            {
                if(rotZ>0)rotZ=0;
            }else{
                if(rotZ<0)rotZ=0;
            }
        }
        b.instances[bone1].transform.localRotation = base_rotation*Quaternion.Euler(rotX,0,rotZ+180);
 
    }

     private void CalculateRotationKnee(Body b, int bone1, int bone2, int base_bone)
    {
        Vector3 p1=b.instances[bone1].transform.localPosition;
        Vector3 p2=b.instances[bone2].transform.localPosition;
        Quaternion base_rotation=b.instances[base_bone].transform.localRotation;

        Vector3 direction = (p2 - p1).normalized;
        
        Vector3 localDirection = Quaternion.Inverse(base_rotation) * direction;
        
        float rotZ = Mathf.Asin(-localDirection.x) * Mathf.Rad2Deg;
        float rotX = Mathf.Atan2(localDirection.z, localDirection.y) * Mathf.Rad2Deg;
        b.instances[bone1].transform.localRotation = base_rotation*Quaternion.Euler(rotX,0,rotZ);
 

    }


    

    public float getLeftElbowAngle(){
        return Quaternion.Angle(Quaternion.identity, GetRotation(Landmark.LEFT_ELBOW));
     }

    public float getRightElbowAngle(){
        return Quaternion.Angle(Quaternion.identity, GetRotation(Landmark.RIGHT_ELBOW));
    }

    public float getLeftKneeAngle(){
        return Quaternion.Angle(Quaternion.identity, GetRotation(Landmark.LEFT_KNEE));
    }

    public float getRightKneeAngle(){
        return Quaternion.Angle(Quaternion.identity, GetRotation(Landmark.RIGHT_KNEE));
    }





    private void UpdateBody(Body b)
    {
        if(b.format==1){
            for (int i = 0; i < LANDMARK_COUNT; ++i)
            {
                b.positions[i] = b.positionsBuffer[i].value *multiplier;
                b.instances[i].transform.localPosition=b.positions[i];
                if(b.positionsBuffer[i].visible)
                b.instances[i].GetComponent<Renderer>().enabled = true;
                else b.instances[i].GetComponent<Renderer>().enabled = false;
            }

            CalculatePelvisRotation(b);
            CalculateTorsoRotation(b);        
            
            CalculateRotationShoulder(b,(int)Landmark.RIGHT_SHOULDER,(int)Landmark.RIGHT_ELBOW,(int)Landmark.SHOULDER_CENTER,false);
            CalculateRotationShoulder(b,(int)Landmark.LEFT_SHOULDER,(int)Landmark.LEFT_ELBOW,(int)Landmark.SHOULDER_CENTER,true);
            
            CalculateRotationElbow(b,(int)Landmark.RIGHT_ELBOW,(int)Landmark.RIGHT_WRIST,(int)Landmark.RIGHT_SHOULDER,false);        
            CalculateRotationElbow(b,(int)Landmark.LEFT_ELBOW,(int)Landmark.LEFT_WRIST,(int)Landmark.LEFT_SHOULDER,true); 

            CalculateRotationThigh(b,(int)Landmark.LEFT_HIP,(int)Landmark.LEFT_KNEE,(int)Landmark.PELVIS,true);        
            CalculateRotationThigh(b,(int)Landmark.RIGHT_HIP,(int)Landmark.RIGHT_KNEE,(int)Landmark.PELVIS,false); 

            CalculateRotationKnee(b,(int)Landmark.RIGHT_KNEE,(int)Landmark.RIGHT_ANKLE,(int)Landmark.RIGHT_HIP);        
            CalculateRotationKnee(b,(int)Landmark.LEFT_KNEE,(int)Landmark.LEFT_ANKLE,(int)Landmark.LEFT_HIP); 

            
            b.UpdateLines();

            b.rotations[(int)Landmark.PELVIS]=GetLandmark(Landmark.PELVIS).localRotation;
            b.rotations[(int)Landmark.SHOULDER_CENTER]=Quaternion.Inverse(GetLandmark(Landmark.PELVIS).localRotation)*GetLandmark(Landmark.SHOULDER_CENTER).localRotation;
            b.rotations[(int)Landmark.LEFT_SHOULDER]=Quaternion.Inverse(GetLandmark(Landmark.SHOULDER_CENTER).localRotation)*GetLandmark(Landmark.LEFT_SHOULDER).localRotation;
            b.rotations[(int)Landmark.RIGHT_SHOULDER]=Quaternion.Inverse(GetLandmark(Landmark.SHOULDER_CENTER).localRotation)*GetLandmark(Landmark.RIGHT_SHOULDER).localRotation;
            b.rotations[(int)Landmark.LEFT_ELBOW]=Quaternion.Inverse(GetLandmark(Landmark.LEFT_SHOULDER).localRotation)*GetLandmark(Landmark.LEFT_ELBOW).localRotation;
            b.rotations[(int)Landmark.RIGHT_ELBOW]=Quaternion.Inverse(GetLandmark(Landmark.RIGHT_SHOULDER).localRotation)*GetLandmark(Landmark.RIGHT_ELBOW).localRotation;
            b.rotations[(int)Landmark.LEFT_HIP]=Quaternion.Inverse(GetLandmark(Landmark.PELVIS).localRotation)*GetLandmark(Landmark.LEFT_HIP).localRotation;
            b.rotations[(int)Landmark.RIGHT_HIP]=Quaternion.Inverse(GetLandmark(Landmark.PELVIS).localRotation)*GetLandmark(Landmark.RIGHT_HIP).localRotation;
            b.rotations[(int)Landmark.LEFT_KNEE]=Quaternion.Inverse(GetLandmark(Landmark.LEFT_HIP).localRotation)*GetLandmark(Landmark.LEFT_KNEE).localRotation;
            b.rotations[(int)Landmark.RIGHT_KNEE]=Quaternion.Inverse(GetLandmark(Landmark.RIGHT_HIP).localRotation)*GetLandmark(Landmark.RIGHT_KNEE).localRotation;
        }
        else if(b.format==0){//mprot

        }

    }

    public Quaternion GetRotation(Landmark i)
    {
        return body.rotations[(int)i];
    }
    public Quaternion GetRotation(Landmark i,int Delay)
    {
        return GetRotation(i);
    }
    public void SetVisible(bool visible)
    {
        bodyParent.gameObject.SetActive(visible);
    }

    public void MoveToFloor(Component component, float floorY){
        Renderer[] renderers = component.GetComponentsInChildren<Renderer>();
        float minY = float.MaxValue;

        foreach (Renderer renderer in renderers)
        {
            minY = Mathf.Min(minY, renderer.bounds.min.y);
        }
        component.transform.localPosition+=new Vector3(0,floorY-minY,0);
    }

    private void Run()
    {
        Debug.Log("Started");
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

        server = new ServerUDP(host, port);
        server.Connect();
        server.StartListeningAsync();
        print("Listening @"+host+":"+port);        

        Landmark[] mr=new Landmark[10];
        mr[0]=Landmark.PELVIS;
        mr[1]=Landmark.SHOULDER_CENTER;
        mr[2]=Landmark.LEFT_SHOULDER;
        mr[3]=Landmark.RIGHT_SHOULDER;
        mr[4]=Landmark.LEFT_ELBOW;
        mr[5]=Landmark.RIGHT_ELBOW;
        mr[6]=Landmark.LEFT_HIP;
        mr[7]=Landmark.RIGHT_HIP;
        mr[8]=Landmark.LEFT_KNEE;
        mr[9]=Landmark.RIGHT_KNEE;

        Landmark[] m=new Landmark[17];
        m[0]=Landmark.PELVIS;
        m[1]=Landmark.LEFT_HIP;
        m[2]=Landmark.LEFT_KNEE;
        m[3]=Landmark.LEFT_ANKLE;
        m[4]=Landmark.RIGHT_HIP;
        m[5]=Landmark.RIGHT_KNEE;
        m[6]=Landmark.RIGHT_ANKLE;
        m[7]=Landmark.SPINE;
        m[8]=Landmark.SHOULDER_CENTER;
        m[9]=Landmark.NECK;
        m[10]=Landmark.HEAD;
        m[11]=Landmark.RIGHT_SHOULDER;
        m[12]=Landmark.RIGHT_ELBOW;
        m[13]=Landmark.RIGHT_WRIST;
        m[14]=Landmark.LEFT_SHOULDER;
        m[15]=Landmark.LEFT_ELBOW;
        m[16]=Landmark.LEFT_WRIST;

        while (true)
        {
            try
            {
                Body h = body;
                var len = 0;
                var str = "";

                
                if(server.HasMessage())
                    str = server.GetMessage();
                else continue;
                len = str.Length;
                

                string[] lines = str.Split('\n');
                if(lines[0].CompareTo("mprot")==0){
                    h.format=0;
                    for(int j=1;j<lines.Length;j++){
                        string[] s=lines[j].Split('|');
                        if (s.Length < 4) continue;
                        int i;
                        if (!int.TryParse(s[0], out i)) continue;
                        h.rotations[(int)mr[i]]=new Quaternion(float.Parse(s[1]),float.Parse(s[2]),float.Parse(s[3]),float.Parse(s[4]));
                        if(s.Length==6 && float.Parse(s[5])>0.5) h.positionsBuffer[(int)mr[i]].visible=true;
                    }
               }
                else if(lines[0].CompareTo("mpxyz")==0){
                    h.format=1;
                    foreach (string l in lines)
                    {
                        if (string.IsNullOrWhiteSpace(l))
                            continue;
                        string[] s = l.Split('|');
                        if (s.Length < 4) continue;
                        int i;
                        if (!int.TryParse(s[0], out i)) continue;
                        
                        if(MMPose)
                        {
                            i=(int)m[i];
                            h.positionsBuffer[i].value = new Vector3(float.Parse(s[1]), -float.Parse(s[2]), float.Parse(s[3]));
                            if(s.Length==5 && float.Parse(s[4])>0.5) h.positionsBuffer[i].visible=true;
                            else h.positionsBuffer[i].visible=false;
                        }
                        else {
                            h.positionsBuffer[i].value = new Vector3(float.Parse(s[1]), -float.Parse(s[2]), -float.Parse(s[3]));
                            if(s.Length==5 && float.Parse(s[4])>0.5) h.positionsBuffer[i].visible=true;
                            else h.positionsBuffer[i].visible=false;
                        }
                        
                        h.positionsBuffer[i].accumulatedValuesCount = 1;
                        
                        h.active = true;
                    }

                    if(!MMPose){
                        if(h.positionsBuffer[(int)Landmark.LEFT_HIP].visible && h.positionsBuffer[(int)Landmark.RIGHT_HIP].visible)
                        {
                            h.positionsBuffer[(int)Landmark.PELVIS].value=(h.positionsBuffer[(int)Landmark.LEFT_HIP].value+h.positionsBuffer[(int)Landmark.RIGHT_HIP].value)/2;
                            h.positionsBuffer[(int)Landmark.PELVIS].visible=true;
                        }else h.positionsBuffer[(int)Landmark.PELVIS].visible=false;

                        if(h.positionsBuffer[(int)Landmark.LEFT_SHOULDER].visible && h.positionsBuffer[(int)Landmark.RIGHT_SHOULDER].visible)
                        {
                            h.positionsBuffer[(int)Landmark.SHOULDER_CENTER].value=(h.positionsBuffer[(int)Landmark.LEFT_SHOULDER].value+h.positionsBuffer[(int)Landmark.RIGHT_SHOULDER].value)/2;
                            h.positionsBuffer[(int)Landmark.SHOULDER_CENTER].visible=true;
                        }else h.positionsBuffer[(int)Landmark.SHOULDER_CENTER].visible=false;

                    }
                }
                //Debug.Log("NEW DATA");
                frame_counter+=1;
            }
            catch (EndOfStreamException)
            {
                print("server Disconnected");
                break;
            }
        }

    }

    public long getFrameCounter(){return frame_counter;}

    private void OnDisable()
    {
        print("server disconnected.");    
        server.Disconnect();
    
    }

    const int LANDMARK_COUNT = 38;
    const int LINES_COUNT = 11;

    public struct AccumulatedBuffer
    {
        public Vector3 value;
        public int accumulatedValuesCount;
        public bool visible;
        public AccumulatedBuffer(Vector3 v, int ac, bool vis)
        {
            value = v;
            accumulatedValuesCount = ac;
            visible=vis;
        }
    }

    public class Body
    {
        public int format=0;
        public Transform parent;
        public AccumulatedBuffer[] positionsBuffer = new AccumulatedBuffer[LANDMARK_COUNT];
        public Vector3[] positions = new Vector3[LANDMARK_COUNT];
        public Quaternion[] rotations = new Quaternion[LANDMARK_COUNT];
        public GameObject[] instances = new GameObject[LANDMARK_COUNT];
        public LineRenderer[] lines = new LineRenderer[LINES_COUNT];

        public bool active;

        private void MakeXYZ(GameObject o)
        {
            GameObject zAxis=GameObject.CreatePrimitive(PrimitiveType.Cube);
                zAxis.GetComponent<Renderer>().material.color = Color.blue; // Change to red
                zAxis.transform.localScale=new Vector3(0.1f,0.1f,2f);
                zAxis.transform.localPosition=new Vector3(0,0,1);
                zAxis.transform.parent=o.transform;
                zAxis.name =o.name+" z";

                GameObject yAxis=GameObject.CreatePrimitive(PrimitiveType.Cube);
                yAxis.GetComponent<Renderer>().material.color = Color.green; // Change to red
                yAxis.transform.localScale=new Vector3(0.1f,2f,0.1f);
                yAxis.transform.localPosition=new Vector3(0,1,0);
                yAxis.transform.parent=o.transform;
                yAxis.name =o.name+" y";

                GameObject xAxis=GameObject.CreatePrimitive(PrimitiveType.Cube);
                xAxis.GetComponent<Renderer>().material.color = Color.red; // Change to red
                xAxis.transform.localScale=new Vector3(2f,0.1f,0.1f);
                xAxis.transform.localPosition=new Vector3(1,0,0);
                xAxis.transform.parent=o.transform;
                xAxis.name =o.name+" x";
        }

        public Body(Transform parent, GameObject linePrefab, float s)
        {
            this.parent = parent;
            for (int i = 0; i < instances.Length; ++i)
            {
                string name=((Landmark)i).ToString();
                if(true&&(name.Contains("SHOULDER")||name.Contains("ELBOW")||name.Contains("HIP")||name.Contains("KNEE")||name.Contains("PELVIS")))
                {
                     instances[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);//Instantiate(landmarkPrefab);
                     instances[i].transform.localScale = Vector3.one * s;
                }
                else{
                    instances[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    instances[i].transform.localScale = Vector3.one * s*1.0f;
                }
                instances[i].transform.parent = parent;
                instances[i].name = name;
                instances[i].GetComponent<Renderer>().enabled = false;
            }

            MakeXYZ(instances[(int)Landmark.RIGHT_SHOULDER]);
            MakeXYZ(instances[(int)Landmark.LEFT_SHOULDER]);

            MakeXYZ(instances[(int)Landmark.RIGHT_ELBOW]);
            MakeXYZ(instances[(int)Landmark.LEFT_ELBOW]);

            MakeXYZ(instances[(int)Landmark.RIGHT_HIP]);
            MakeXYZ(instances[(int)Landmark.LEFT_HIP]);

            MakeXYZ(instances[(int)Landmark.RIGHT_KNEE]);
            MakeXYZ(instances[(int)Landmark.LEFT_KNEE]);

            MakeXYZ(instances[(int)Landmark.PELVIS]);
            MakeXYZ(instances[(int)Landmark.SHOULDER_CENTER]);

            for (int i = 0; i < lines.Length; ++i)
            {
                lines[i] = Instantiate(linePrefab).GetComponent<LineRenderer>();
                lines[i].transform.parent = parent;
            }
            rotations[(int)Landmark.RIGHT_HIP]=Quaternion.Euler(0,0,180);
            rotations[(int)Landmark.LEFT_HIP]=Quaternion.Euler(0,0,180);
        }
        public void UpdateLines()
        {
            

            //LEFT LEG
            lines[2].positionCount = 4;
            lines[2].SetPosition(0, Position(Landmark.LEFT_ANKLE));
            lines[2].SetPosition(1, Position(Landmark.LEFT_KNEE));
            lines[2].SetPosition(2, Position(Landmark.LEFT_HIP));
            lines[2].SetPosition(3, Position(Landmark.PELVIS));
            //RIGHT LEG
            lines[3].positionCount = 4;
            lines[3].SetPosition(0, Position(Landmark.RIGHT_ANKLE));
            lines[3].SetPosition(1, Position(Landmark.RIGHT_KNEE));
            lines[3].SetPosition(2, Position(Landmark.RIGHT_HIP));
            lines[3].SetPosition(3, Position(Landmark.PELVIS));

            //TORSO
            lines[4].positionCount = 5;
            lines[4].SetPosition(0, Position(Landmark.PELVIS));
            lines[4].SetPosition(1, Position(Landmark.SPINE));
            lines[4].SetPosition(2, Position(Landmark.SHOULDER_CENTER));
            lines[4].SetPosition(3, Position(Landmark.NECK));
            lines[4].SetPosition(4, Position(Landmark.HEAD));

            //LEFT ARM
            lines[5].positionCount = 4;
            lines[5].SetPosition(0, Position(Landmark.LEFT_WRIST));
            lines[5].SetPosition(1, Position(Landmark.LEFT_ELBOW));
            lines[5].SetPosition(2, Position(Landmark.LEFT_SHOULDER));
            lines[5].SetPosition(3, Position(Landmark.SHOULDER_CENTER));
           //RIGHT ARM
            lines[6].positionCount = 4;
            lines[6].SetPosition(0, Position(Landmark.RIGHT_WRIST));
            lines[6].SetPosition(1, Position(Landmark.RIGHT_ELBOW));
            lines[6].SetPosition(2, Position(Landmark.RIGHT_SHOULDER));
            lines[6].SetPosition(3, Position(Landmark.SHOULDER_CENTER));
        }

        public Vector3 Direction(Landmark from,Landmark to)
        {
            return (instances[(int)to].transform.position - instances[(int)from].transform.position).normalized;
        }
        public float Distance(Landmark from, Landmark to)
        {
            return (instances[(int)from].transform.position - instances[(int)to].transform.position).magnitude;
        }
        public Vector3 LocalPosition(Landmark Mark)
        {
            return instances[(int)Mark].transform.localPosition;
        }
        public Vector3 Position(Landmark Mark)
        {
            return instances[(int)Mark].transform.position;
        }

    }
}
