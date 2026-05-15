
using UnityEngine;
using GLTFast;
using System.Linq;
using System.Xml.Serialization;
using System;

public class CatchFruitHeroAvatar : MonoBehaviour
{

    private UPose server;
    public GameObject catchBox;

    private Transform Hips;
    private Transform Spine;
    private Transform LeftUpLeg;
    private Transform LeftLeg;
    private Transform LeftFoot;
    private Transform RightUpLeg;
    private Transform RightLeg;
    private Transform RightFoot;
    private Transform LeftShoulder;
    private Transform LeftArm;
    private Transform LeftForeArm;
    private Transform LeftHand;
    private Transform LeftPalm;
    private Transform RightShoulder;
    private Transform RightArm;
    private Transform RightForeArm;
    private Transform RightHand;
    private Transform RightPalm;

    private bool AVATAR_LOADED = false;

    private void Start()
    {

        server = FindFirstObjectByType<UPose>();
        if (server == null)
        {
            Debug.LogError("You must have a Pipeserver in the scene!");
        }

        InitializeAvatar();

    }

    private async void InitializeAvatar()
    {
        var gltfImport = new GltfImport();
        String avatar_url = "";
        avatar_url = "67e21d1a79ac9bcf81a46385.glb";
        await gltfImport.Load("https://digitalworlds.github.io/UPose/UPose/Assets/StreamingAssets/" + avatar_url);
        var instantiator = new GameObjectInstantiator(gltfImport, transform);
        var success = await gltfImport.InstantiateMainSceneAsync(instantiator);
        if (success)
        {
            Debug.Log("GLTF file is loaded.");


            Hips = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Hips");
            Spine = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Spine");
            Transform Spine1 = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Spine1");
            Spine1.localRotation = Quaternion.Euler(0, 0, 0);
            Transform Spine2 = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Spine2");
            Spine2.localRotation = Quaternion.Euler(0, 0, 0);

            LeftUpLeg = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "LeftUpLeg");
            LeftLeg = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "LeftLeg");

            RightUpLeg = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "RightUpLeg");
            RightLeg = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "RightLeg");

            LeftFoot = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "LeftFoot");

            GameObject colliderHolder = new GameObject("LeftFootCollider");
            colliderHolder.transform.SetParent(LeftFoot);
            colliderHolder.transform.localPosition = new Vector3(0, 0.125f, 0);
            colliderHolder.transform.localRotation = Quaternion.Euler(-55, 0, 0);
            Rigidbody rb = colliderHolder.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            BoxCollider footCollider = colliderHolder.AddComponent<BoxCollider>();
            footCollider.size = new Vector3(0.15f, 0.1f, 0.3f);
            colliderHolder.AddComponent<KickForce>();


            RightFoot = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "RightFoot");

            colliderHolder = new GameObject("RightFootCollider");
            colliderHolder.transform.SetParent(RightFoot);
            colliderHolder.transform.localPosition = new Vector3(0, 0.125f, 0);
            colliderHolder.transform.localRotation = Quaternion.Euler(-55, 0, 0);
            rb = colliderHolder.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            footCollider = colliderHolder.AddComponent<BoxCollider>();
            footCollider.size = new Vector3(0.15f, 0.1f, 0.3f);
            colliderHolder.AddComponent<KickForce>();

            LeftShoulder = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "LeftShoulder");
            LeftShoulder.localRotation=Quaternion.Euler(0,0,90);
            LeftArm = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "LeftArm");
            
            LeftForeArm = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "LeftForeArm");

            LeftHand = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "LeftHand");

            GameObject leftPalm = new GameObject("LeftPalm");
            leftPalm.transform.parent = LeftHand;
            leftPalm.transform.localPosition = new Vector3(0, 0.07f, 0.04f);
            leftPalm.transform.localRotation = Quaternion.Euler(0, 0, 0);
            LeftPalm = leftPalm.transform;

            colliderHolder = new GameObject("LeftHandCollider");
            colliderHolder.transform.SetParent(LeftHand);
            colliderHolder.transform.localPosition = new Vector3(0, 0.1f, 0);
            colliderHolder.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            rb = colliderHolder.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            footCollider = colliderHolder.AddComponent<BoxCollider>();
            footCollider.size = new Vector3(0.15f, 0.1f, 0.2f);
            colliderHolder.AddComponent<KickForce>();

            RightShoulder = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "RightShoulder");
            RightShoulder.localRotation=Quaternion.Euler(0,0,-90);
            RightArm = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "RightArm");
            
            RightForeArm = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "RightForeArm");

            RightHand = GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "RightHand");

            GameObject rightPalm = new GameObject("RightPalm");
            rightPalm.transform.parent = RightHand;
            rightPalm.transform.localPosition = new Vector3(0, 0.07f, 0.04f);
            rightPalm.transform.localRotation = Quaternion.Euler(0, 0, 0);
            RightPalm = rightPalm.transform;

            colliderHolder = new GameObject("RightHandCollider");
            colliderHolder.transform.SetParent(RightHand);
            colliderHolder.transform.localPosition = new Vector3(0, 0.1f, 0);
            colliderHolder.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            rb = colliderHolder.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            footCollider = colliderHolder.AddComponent<BoxCollider>();
            footCollider.size = new Vector3(0.15f, 0.1f, 0.2f);
            colliderHolder.AddComponent<KickForce>();

            AVATAR_LOADED = true;

            DynamicPlatformBetweenPoints points =
                Instantiate(catchBox, transform).GetComponent<DynamicPlatformBetweenPoints>();
            points.pointB = LeftPalm;
            points.pointA = RightPalm;

        }
        else
        {
            Debug.Log("ERROR: GLTF file is NOT loaded!");
        }
    }

    public bool isLoaded()
    {
        return AVATAR_LOADED;
    }

    public GameObject getLeftHand()
    {
        return LeftHand.gameObject;
    }

    public GameObject getRightHand()
    {
        return RightHand.gameObject;
    }

    public GameObject getLeftFoot()
    {
        return LeftFoot.gameObject;
    }

    public GameObject getRightFoot()
    {
        return RightFoot.gameObject;
    }

    public GameObject getLeftForeArm()
    {
        return LeftForeArm.gameObject;
    }

    public GameObject getRightForeArm()
    {
        return RightForeArm.gameObject;
    }

    public GameObject getLeftLeg()
    {
        return LeftLeg.gameObject;
    }

    public GameObject getRightLeg()
    {
        return RightLeg.gameObject;
    }

    public GameObject getLeftShoulder()
    {
        return LeftShoulder.gameObject;
    }

    public GameObject getRightShoulder()
    {
        return RightShoulder.gameObject;
    }

    public GameObject getLeftUpLeg()
    {
        return LeftUpLeg.gameObject;
    }

    public GameObject getRightUpLeg()
    {
        return RightUpLeg.gameObject;
    }

    public GameObject getLeftPalm()
    {
        return LeftPalm.gameObject;
    }

    public GameObject getRightPalm()
    {
        return RightPalm.gameObject;
    }

    private void Update()
    {
        if (!AVATAR_LOADED) return;

        //Get pelvis local rotation and apply it to the avatar
        Hips.localRotation = server.GetRotation(Landmark.PELVIS);
        //Get torso local rotation and apply it to the avatar
        Spine.localRotation = server.GetRotation(Landmark.SHOULDER_CENTER);
        //Get right upper arm rotation and apply it to the avatar
        RightArm.localRotation = Quaternion.Euler(0, 0, 90) * server.GetRotation(Landmark.RIGHT_SHOULDER);
        //Get left upper arm rotation and apply it to the avatar
        LeftArm.localRotation = Quaternion.Euler(0, 0, -90) * server.GetRotation(Landmark.LEFT_SHOULDER);
        //Get left fore arm rotation and apply it to the avatar
        LeftForeArm.localRotation = server.GetRotation(Landmark.LEFT_ELBOW);
        //Get right fore arm rotation and apply it to the avatar
        RightForeArm.localRotation = server.GetRotation(Landmark.RIGHT_ELBOW);
        //Get right thigh arm rotation and apply it to the avatar  
        RightUpLeg.localRotation = server.GetRotation(Landmark.RIGHT_HIP);
        //Get left thigh rotation and apply it to the avatar
        LeftUpLeg.localRotation = server.GetRotation(Landmark.LEFT_HIP);
        //Get left leg rotation and apply it to the avatar
        LeftLeg.localRotation = server.GetRotation(Landmark.LEFT_KNEE);
        //Get right leg rotation and apply it to the avatar
        RightLeg.localRotation = server.GetRotation(Landmark.RIGHT_KNEE);


        //server.MoveToFloor(this,-1);
    }
}