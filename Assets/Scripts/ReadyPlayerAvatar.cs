
using UnityEngine;
using GLTFast;
using System.Linq;
using System.Xml.Serialization;
using System;
using System.Threading;
using UnityEngine.UIElements;
using Unity.VisualScripting;

[DefaultExecutionOrder(200)]
public class ReadyPlayerAvatar : MonoBehaviour
{

    private MotionTrackingPose server;

    public int Delay=0;

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

    private bool AVATAR_LOADED=false;
    private bool avatarLoadInFlight;
    private CancellationTokenSource avatarLoadCts;
    private GltfImport activeGltfImport;
    private bool loggedDanceBoneWarning;
    private bool loggedDanceSkipWarning;
    private bool loggedStickRetargetFailWarning;
    private string lastLoggedAppliedDanceId;
    private Vector3 danceStartPosition;
    private Vector3 initialStagePosition;
    private bool hasInitialStagePosition;
    private Vector3 danceHipsLocalOffset;
    private bool hasDanceHipsLocalOffset;
    private string activeDanceId;
    private bool hasSmoothedStickPose;
    private StickBoneLocals smoothedStickLocals;
    private Vector3 smoothedStickRootPos;
    private bool hasSmoothedStickRootPos;

    [SerializeField] bool applyStickWorldRoot = true;
    [SerializeField] bool useAutoTranslationScale = true;
    [SerializeField] float manualTranslationScale = 1f;
    [SerializeField] float stickRootVerticalOffset = 0f;
    [SerializeField] bool logProportionsOnDanceStart = true;
    [SerializeField] float stickPoseSmoothing = 20f;
    [SerializeField] float stickRootSmoothing = 20f;
    [SerializeField] float stickJointSmoothing = 20f;

    private bool hasSmoothedStickJoints;
    private UposeStickPoseSolver.HipLocalJoints smoothedStickJoints;
    private Vector3 danceAnchorStickHip;
    private Vector3 danceAnchorRootPosition;
    private bool hasDanceTranslationAnchor;
    private float computedLegHeightRatio = 1f;
    private float lastEffectiveTranslationScale = 1f;

    public bool ApplyStickWorldRoot => applyStickWorldRoot;
    public float ComputedLegHeightRatio => computedLegHeightRatio;
    public float ManualTranslationScale => manualTranslationScale;
    public float LastEffectiveTranslationScale => lastEffectiveTranslationScale;

    struct StickBoneLocals
    {
        public Quaternion hips;
        public Quaternion spine;
        public Quaternion leftArm;
        public Quaternion rightArm;
        public Quaternion leftForeArm;
        public Quaternion rightForeArm;
        public Quaternion leftUpLeg;
        public Quaternion rightUpLeg;
        public Quaternion leftLeg;
        public Quaternion rightLeg;
    }

    public struct LimbBindAxes
    {
        public Vector3 leftUpperArm;
        public Vector3 leftForeArm;
        public Vector3 rightUpperArm;
        public Vector3 rightForeArm;
        public Vector3 leftUpperLeg;
        public Vector3 leftLowerLeg;
        public Vector3 rightUpperLeg;
        public Vector3 rightLowerLeg;
        public float leftArmLength;
        public float rightArmLength;
        public float leftLegLength;
        public float rightLegLength;
        public float leftUpperArmLength;
        public float leftForeArmLength;
        public float rightUpperArmLength;
        public float rightForeArmLength;
        public float leftUpperLegLength;
        public float leftLowerLegLength;
        public float rightUpperLegLength;
        public float rightLowerLegLength;
        public Vector3 leftFootToeAxis;
        public Vector3 rightFootToeAxis;
    }

    LimbBindAxes bindAxes;
    bool bindAxesCaptured;

    public enum AvatarChoice { UseLocalFile, FemaleGymClothing, FemaleDress,FemaleCasual, MaleCasual, MaleTshirt, MaleArmored, FemaleYogaOutfit}
    public AvatarChoice onlineAvatar;

    //avatar filename inside the StreamingAssets folder
    public String localFilename = "67e21d1a79ac9bcf81a46385.glb";

    [Header("Floor Constraint")]
    [Tooltip("When enabled, feet cannot go below Floor Level.")]
    public bool moveToFloor = false;
    [Tooltip("World-space Y height treated as the floor.")]
    public float floorLevel = -1f;
    [Tooltip("Fine-tune sole height relative to foot bones. Positive = feet sit higher above Floor Level.")]
    public float floorContactOffset = 0f;

    private void Start()
    {
        initialStagePosition = transform.position;
        hasInitialStagePosition = true;

        if (gameObject.name.StartsWith("Preview", System.StringComparison.OrdinalIgnoreCase))
        {
            applyStickWorldRoot = false;
        }

        server = FindFirstObjectByType<PoseMemory>();
        if (server == null)
        {
            server = FindFirstObjectByType<UPose>();
            if (server == null)
            {
                Debug.LogError("You must have a MotionTracking server in the scene!");
                return;
            }
        }
    }

    private void OnEnable()
    {
        // CharacterSelector toggles avatars active/inactive. Only load while enabled,
        // and retry if a previous load was cancelled mid-flight.
        if (!AVATAR_LOADED)
        {
            InitializeAvatar();
        }
    }

    private void OnDisable()
    {
        // Abort in-flight GLTFast loads so texture decode can't run after teardown.
        CancelAvatarLoadToken();
    }

    private void OnDestroy()
    {
        CancelAvatarLoadToken();
        DisposeActiveGltfImport();
    }

    private void CancelAvatarLoadToken()
    {
        if (avatarLoadCts == null)
        {
            return;
        }

        try
        {
            avatarLoadCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        avatarLoadCts.Dispose();
        avatarLoadCts = null;
    }

    private void DisposeActiveGltfImport()
    {
        if (activeGltfImport == null)
        {
            return;
        }

        try
        {
            activeGltfImport.Dispose();
        }
        catch (Exception)
        {
        }

        activeGltfImport = null;
    }

    private bool CanContinueAvatarLoad(CancellationToken token)
    {
        return this != null && isActiveAndEnabled && !token.IsCancellationRequested;
    }

    private async void InitializeAvatar()
    {
        if (AVATAR_LOADED || avatarLoadInFlight || !isActiveAndEnabled)
        {
            return;
        }

        avatarLoadInFlight = true;
        CancelAvatarLoadToken();
        avatarLoadCts = new CancellationTokenSource();
        CancellationToken token = avatarLoadCts.Token;

        // Replace any previous failed import before starting a new one.
        if (!AVATAR_LOADED)
        {
            DisposeActiveGltfImport();
        }

        var gltfImport = new GltfImport();
        activeGltfImport = gltfImport;

        string avatar_url = "";
        switch (onlineAvatar)
        {
            case AvatarChoice.UseLocalFile:
                avatar_url = "";
                break;
            case AvatarChoice.FemaleGymClothing:
                avatar_url = "avatar.glb";
                break;
            case AvatarChoice.FemaleDress:
                avatar_url = "avatar1.glb";
                break;
            case AvatarChoice.FemaleCasual:
                avatar_url = "67e20a7fc5f8c4a77988b853.glb";
                break;
            case AvatarChoice.MaleCasual:
                avatar_url = "67d411b30787acbf58ce58ac.glb";
                break;
            case AvatarChoice.MaleTshirt:
                avatar_url = "67e21d1a79ac9bcf81a46385.glb";
                break;
            case AvatarChoice.MaleArmored:
                avatar_url = "67e21f3db6349f1f57421ba0.glb";
                break;
            case AvatarChoice.FemaleYogaOutfit:
                avatar_url = "67f433b69dc08cf26d2cf585.glb";
                break;
            default:
                avatar_url = "avatar.glb";
                break;
        }

        try
        {
            string path = avatar_url.Length == 0
                ? System.IO.Path.Combine(Application.streamingAssetsPath, localFilename)
                : System.IO.Path.Combine(Application.streamingAssetsPath, avatar_url);

            bool loaded = await gltfImport.Load(path, cancellationToken: token);
            if (!loaded || !CanContinueAvatarLoad(token))
            {
                return;
            }

            var instantiator = new GameObjectInstantiator(gltfImport, transform);
            bool success = await gltfImport.InstantiateMainSceneAsync(instantiator, token);
            if (!success || !CanContinueAvatarLoad(token))
            {
                return;
            }

            Debug.Log("GLTF file is loaded.");

            foreach (Animator animator in GetComponentsInChildren<Animator>(true))
            {
                animator.enabled = false;
            }

            Hips = FindBone("Hips");
            Spine = FindBone("Spine");
            Transform Spine1 = FindBone("Spine1");
            if (Spine1 != null) Spine1.localRotation = Quaternion.Euler(0, 0, 0);
            Transform Spine2 = FindBone("Spine2");
            if (Spine2 != null) Spine2.localRotation = Quaternion.Euler(0, 0, 0);

            LeftUpLeg = FindBone("LeftUpLeg");
            LeftLeg = FindBone("LeftLeg");

            RightUpLeg = FindBone("RightUpLeg");
            RightLeg = FindBone("RightLeg");

            LeftFoot = FindBone("LeftFoot");

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

            RightFoot = FindBone("RightFoot");

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

            LeftShoulder = FindBone("LeftShoulder");
            if (LeftShoulder != null) LeftShoulder.localRotation = Quaternion.Euler(0, 0, 90);
            LeftArm = FindBone("LeftArm");

            LeftForeArm = FindBone("LeftForeArm");

            LeftHand = FindBone("LeftHand");

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

            RightShoulder = FindBone("RightShoulder");
            if (RightShoulder != null) RightShoulder.localRotation = Quaternion.Euler(0, 0, -90);
            RightArm = FindBone("RightArm");

            RightForeArm = FindBone("RightForeArm");

            RightHand = FindBone("RightHand");

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

            CaptureBindAxes();
            AVATAR_LOADED = true;
        }
        catch (OperationCanceledException)
        {
            // Expected when CharacterSelector disables this avatar mid-load.
        }
        catch (MissingReferenceException)
        {
            // GLTFast texture decode raced with teardown; ignore.
        }
        catch (ArgumentNullException)
        {
            // LoadImage(tex, ...) after Texture2D was destroyed mid-import.
        }
        catch (Exception ex)
        {
            if (this != null)
            {
                Debug.LogWarning("ReadyPlayerAvatar: GLTF load failed on '" + gameObject.name + "': " + ex.Message);
            }
        }
        finally
        {
            avatarLoadInFlight = false;

            // Drop imports that never finished instantiating so textures aren't left half-alive.
            if (!AVATAR_LOADED && activeGltfImport == gltfImport)
            {
                DisposeActiveGltfImport();
            }
        }
    }

    public bool isLoaded(){return AVATAR_LOADED;}
    public bool HasBindAxes => bindAxesCaptured;
    public LimbBindAxes GetBindAxes() => bindAxes;

    void CaptureBindAxes()
    {
        if (LeftArm == null || LeftForeArm == null || LeftHand == null
            || RightArm == null || RightForeArm == null || RightHand == null
            || LeftUpLeg == null || LeftLeg == null || LeftFoot == null
            || RightUpLeg == null || RightLeg == null || RightFoot == null)
        {
            return;
        }

        bindAxes.leftUpperArm = LeftArm.InverseTransformPoint(LeftForeArm.position).normalized;
        bindAxes.leftForeArm = LeftForeArm.InverseTransformPoint(LeftHand.position).normalized;
        bindAxes.rightUpperArm = RightArm.InverseTransformPoint(RightForeArm.position).normalized;
        bindAxes.rightForeArm = RightForeArm.InverseTransformPoint(RightHand.position).normalized;
        bindAxes.leftUpperLeg = LeftUpLeg.InverseTransformPoint(LeftLeg.position).normalized;
        bindAxes.leftLowerLeg = LeftLeg.InverseTransformPoint(LeftFoot.position).normalized;
        bindAxes.rightUpperLeg = RightUpLeg.InverseTransformPoint(RightLeg.position).normalized;
        bindAxes.rightLowerLeg = RightLeg.InverseTransformPoint(RightFoot.position).normalized;
        bindAxes.leftArmLength = Vector3.Distance(LeftArm.position, LeftHand.position);
        bindAxes.rightArmLength = Vector3.Distance(RightArm.position, RightHand.position);
        bindAxes.leftLegLength = Vector3.Distance(LeftUpLeg.position, LeftFoot.position);
        bindAxes.rightLegLength = Vector3.Distance(RightUpLeg.position, RightFoot.position);
        bindAxes.leftUpperArmLength = LeftArm.InverseTransformPoint(LeftForeArm.position).magnitude;
        bindAxes.leftForeArmLength = LeftForeArm.InverseTransformPoint(LeftHand.position).magnitude;
        bindAxes.rightUpperArmLength = RightArm.InverseTransformPoint(RightForeArm.position).magnitude;
        bindAxes.rightForeArmLength = RightForeArm.InverseTransformPoint(RightHand.position).magnitude;
        bindAxes.leftUpperLegLength = LeftUpLeg.InverseTransformPoint(LeftLeg.position).magnitude;
        bindAxes.leftLowerLegLength = LeftLeg.InverseTransformPoint(LeftFoot.position).magnitude;
        bindAxes.rightUpperLegLength = RightUpLeg.InverseTransformPoint(RightLeg.position).magnitude;
        bindAxes.rightLowerLegLength = RightLeg.InverseTransformPoint(RightFoot.position).magnitude;
        Vector3 leftToeGuess = LeftFoot.position + LeftFoot.rotation * Vector3.forward * 0.08f;
        Vector3 rightToeGuess = RightFoot.position + RightFoot.rotation * Vector3.forward * 0.08f;
        bindAxes.leftFootToeAxis = LeftFoot.InverseTransformPoint(leftToeGuess).normalized;
        bindAxes.rightFootToeAxis = RightFoot.InverseTransformPoint(rightToeGuess).normalized;
        bindAxesCaptured = true;
    }

    public Transform GetPelvisTransform() { return Hips; }
    public Transform GetSpineTransform() { return Spine; }
    public Transform GetLeftShoulderTransform() { return LeftShoulder; }
    public Transform GetRightShoulderTransform() { return RightShoulder; }
    public Transform GetLeftArmTransform() { return LeftArm; }
    public Transform GetRightArmTransform() { return RightArm; }
    public Transform GetLeftHandTransform() { return LeftHand; }
    public Transform GetRightHandTransform() { return RightHand; }
    public Transform GetLeftForeArmTransform() { return LeftForeArm; }
    public Transform GetRightForeArmTransform() { return RightForeArm; }
    public Transform GetLeftUpLegTransform() { return LeftUpLeg; }
    public Transform GetRightUpLegTransform() { return RightUpLeg; }
    public Transform GetLeftLegTransform() { return LeftLeg; }
    public Transform GetRightLegTransform() { return RightLeg; }
    public Transform GetLeftFootTransform() { return LeftFoot; }
    public Transform GetRightFootTransform() { return RightFoot; }
    public GameObject getLeftHand(){return LeftHand.gameObject;}
    public GameObject getRightHand(){return RightHand.gameObject;}
    public GameObject getLeftFoot(){return LeftFoot.gameObject;}
    public GameObject getRightFoot(){return RightFoot.gameObject;}
    public GameObject getLeftForeArm(){return LeftForeArm.gameObject;}
    public GameObject getRightForeArm(){return RightForeArm.gameObject;}
    public GameObject getLeftLeg(){return LeftLeg.gameObject;}
    public GameObject getRightLeg(){return RightLeg.gameObject;}
    public GameObject getLeftShoulder(){return LeftShoulder.gameObject;}
    public GameObject getRightShoulder(){return RightShoulder.gameObject;}
    public GameObject getLeftUpLeg(){return LeftUpLeg.gameObject;}
    public GameObject getRightUpLeg(){return RightUpLeg.gameObject;}
    public GameObject getLeftPalm(){return LeftPalm.gameObject;}
    public GameObject getRightPalm(){return RightPalm.gameObject;}

    public Quaternion getRightHipRotation() { return server.GetRotation(Landmark.RIGHT_HIP); }
    public Quaternion getLeftHipRotation() { return server.GetRotation(Landmark.LEFT_HIP); }
    public Quaternion getRightElbowRotation() { return server.GetRotation(Landmark.RIGHT_ELBOW); }
    public Quaternion getLeftElbowRotation() { return server.GetRotation(Landmark.LEFT_ELBOW); }

    public void MoveToFloor(float floorY)
    {
        EnforceFloorMinimum(floorY);
    }

    public void EnforceFloorMinimum(float floorY)
    {
        float lowestY = GetLowestFootContactY();
        if (float.IsPositiveInfinity(lowestY))
        {
            return;
        }

        float lift = floorY - lowestY;
        if (lift <= 0f)
        {
            return;
        }

        Vector3 pos = transform.position;
        transform.position = new Vector3(pos.x, pos.y + lift, pos.z);
    }

    public void ApplyFloorIfEnabled()
    {
        if (!moveToFloor)
        {
            return;
        }

        EnforceFloorMinimum(floorLevel);
        if (hasSmoothedStickRootPos)
        {
            smoothedStickRootPos = transform.position;
        }
    }

    public float GetLowestFootContactY()
    {
        float minY = float.PositiveInfinity;

        if (LeftFoot != null)
        {
            minY = Mathf.Min(minY, GetFootContactY(LeftFoot));
        }

        if (RightFoot != null)
        {
            minY = Mathf.Min(minY, GetFootContactY(RightFoot));
        }

        if (float.IsPositiveInfinity(minY))
        {
            return float.PositiveInfinity;
        }

        return minY + floorContactOffset;
    }

    float GetFootContactY(Transform footBone)
    {
        float minY = footBone.position.y;

        Collider[] colliders = footBone.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            minY = Mathf.Min(minY, collider.bounds.min.y);
        }

        Renderer[] renderers = footBone.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            minY = Mathf.Min(minY, renderer.bounds.min.y);
        }

        return minY;
    }

    float GetLowestContactY()
    {
        float footY = GetLowestFootContactY();
        if (!float.IsPositiveInfinity(footY))
        {
            return footY;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        float minY = float.PositiveInfinity;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            minY = Mathf.Min(minY, renderer.bounds.min.y);
        }

        return minY;
    }

    
    private void Update()
    {
        if (!AVATAR_LOADED) return;

        AvatarAnimationPlayer player = GetComponent<AvatarAnimationPlayer>();
        if (player != null && player.isPlaying)
        {
            return;
        }

        if (player != null && !player.isPlaying)
        {
            activeDanceId = null;
            hasDanceHipsLocalOffset = false;
            loggedDanceBoneWarning = false;
            loggedDanceSkipWarning = false;
            lastLoggedAppliedDanceId = null;
            ResetStickPoseSmoothing();
        }

        if(server == null) return;

        //Get pelvis local rotation and apply it to the avatar
        Hips.localRotation = server.GetRotation(Landmark.PELVIS, Delay);
        //Get torso local rotation and apply it to the avatar
        Spine.localRotation = server.GetRotation(Landmark.SHOULDER_CENTER, Delay);
        //Get right upper arm rotation and apply it to the avatar
        RightArm.localRotation = Quaternion.Euler(0, 0, 90) * server.GetRotation(Landmark.RIGHT_SHOULDER, Delay);
        //Get left upper arm rotation and apply it to the avatar
        LeftArm.localRotation = Quaternion.Euler(0, 0, -90) * server.GetRotation(Landmark.LEFT_SHOULDER, Delay);
        //Get left fore arm rotation and apply it to the avatar
        LeftForeArm.localRotation = server.GetRotation(Landmark.LEFT_ELBOW, Delay);
        //Get right fore arm rotation and apply it to the avatar
        RightForeArm.localRotation = server.GetRotation(Landmark.RIGHT_ELBOW, Delay);
        //Get right thigh arm rotation and apply it to the avatar  
        RightUpLeg.localRotation = server.GetRotation(Landmark.RIGHT_HIP, Delay);
        //Get left thigh rotation and apply it to the avatar
        LeftUpLeg.localRotation = server.GetRotation(Landmark.LEFT_HIP, Delay);
        //Get left leg rotation and apply it to the avatar
        LeftLeg.localRotation = server.GetRotation(Landmark.LEFT_KNEE, Delay);
        //Get right leg rotation and apply it to the avatar
        RightLeg.localRotation = server.GetRotation(Landmark.RIGHT_KNEE, Delay);

        if (moveToFloor) ApplyFloorIfEnabled();
    }

    public void ApplyDancePlayback(AvatarAnimationPlayer player)
    {
        if (player == null || !player.isPlaying)
        {
            return;
        }

        if (!AVATAR_LOADED)
        {
            if (!loggedDanceSkipWarning)
            {
                loggedDanceSkipWarning = true;
                Debug.LogWarning(
                    "ReadyPlayerAvatar on '" + name + "': dance '" + player.CurrentDanceId +
                    "' waiting for GLB load before applying bones.");
            }

            return;
        }

        float[] rotations = player.getRotations();
        if (!player.UseStickFigurePose && rotations.Length != RpmDanceConverter.RotationCount)
        {
            if (!loggedDanceSkipWarning)
            {
                loggedDanceSkipWarning = true;
                Debug.LogWarning(
                    "ReadyPlayerAvatar on '" + name + "': expected " + RpmDanceConverter.RotationCount +
                    " rotations but got " + rotations.Length + " for dance '" + player.CurrentDanceId + "'.");
            }

            return;
        }

        loggedDanceSkipWarning = false;
        DisableAnimators();

        if (player.UseStickFigurePose && player.SyncedStick != null)
        {
            if (StickFigureAvatarRetargeter.TryApply(
                    this,
                    player.SyncedStick,
                    player.CurrentDanceId,
                    ref activeDanceId,
                    ref danceHipsLocalOffset,
                    ref hasDanceHipsLocalOffset))
            {
                if (player.CurrentDanceId != lastLoggedAppliedDanceId)
                {
                    lastLoggedAppliedDanceId = player.CurrentDanceId;
                    Debug.Log(
                        "ReadyPlayerAvatar on '" + name + "': stick-retarget dance '" + player.CurrentDanceId +
                        "' frame=" + player.SyncedStick.CurrentFrameIndex);
                }

                return;
            }

            if (!loggedStickRetargetFailWarning)
            {
                loggedStickRetargetFailWarning = true;
                Debug.LogWarning(
                    "ReadyPlayerAvatar on '" + name + "': stick-retarget failed for dance '" +
                    player.CurrentDanceId + "'; check NpzStickFigure is loaded and joints are visible.");
            }
        }

        loggedStickRetargetFailWarning = false;
        ApplyDanceRootMotion(player);
        ApplyDanceRotations(rotations);

        if (player.CurrentDanceId != lastLoggedAppliedDanceId)
        {
            lastLoggedAppliedDanceId = player.CurrentDanceId;
            Debug.Log(
                "ReadyPlayerAvatar on '" + name + "': applying dance '" + player.CurrentDanceId +
                "' hipsY=" + rotations[0].ToString("F1") +
                " leftKneeX=" + rotations[15].ToString("F1"));
        }

        if (moveToFloor)
        {
            ApplyFloorIfEnabled();
        }

        if (player.driveStickOverlay && !player.UseStickFigurePose && player.IsStickSynced
            && player.SyncedStick != null && Hips != null && player.SyncedStick.alignToAvatarPelvis)
        {
            player.SyncedStick.AlignToPelvis(Hips, LeftFoot, RightFoot);
        }
    }

    private void LateUpdate()
    {
        AvatarAnimationPlayer player = GetComponent<AvatarAnimationPlayer>();
        if (player != null && player.isPlaying)
        {
            ApplyDancePlayback(player);
        }
    }

    void DisableAnimators()
    {
        Animator[] animators = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            animators[i].enabled = false;
        }
    }

    Transform FindBone(string boneName)
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name == boneName)
            {
                return transforms[i];
            }
        }

        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name.EndsWith(boneName, System.StringComparison.OrdinalIgnoreCase))
            {
                return transforms[i];
            }
        }

        return null;
    }

    public void ResetStickPoseSmoothing()
    {
        hasSmoothedStickPose = false;
        hasSmoothedStickRootPos = false;
        hasSmoothedStickJoints = false;
        hasDanceTranslationAnchor = false;
    }

    public void BeginDanceTranslationAnchor(
        string danceId,
        Vector3 stickHipWorld,
        NpzStickFigureVisualizer stick,
        NpzMotionFrame measureFrame)
    {
        if (danceId == activeDanceId && hasDanceTranslationAnchor)
        {
            return;
        }

        danceAnchorStickHip = stickHipWorld;
        danceAnchorRootPosition = hasInitialStagePosition ? initialStagePosition : transform.position;
        hasDanceTranslationAnchor = true;

        if (applyStickWorldRoot)
        {
            transform.position = danceAnchorRootPosition;
            smoothedStickRootPos = danceAnchorRootPosition;
            hasSmoothedStickRootPos = true;
        }

        if (BodyProportionScaler.TryBuildReport(measureFrame, stick.AxisFlip, this, out BodyProportionScaler.SegmentReport report))
        {
            computedLegHeightRatio = report.legHeightRatio;
            if (logProportionsOnDanceStart)
            {
                report.Log(name);
                lastEffectiveTranslationScale = GetEffectiveTranslationScale(stick);
                Debug.Log(
                    "ReadyPlayerAvatar on '" + name + "': translation scale=" +
                    lastEffectiveTranslationScale.ToString("F3") +
                    " (auto leg ratio " + computedLegHeightRatio.ToString("F3") +
                    " / stick poseScale " + stick.PoseScale.ToString("F3") +
                    " * manual " + manualTranslationScale.ToString("F3") +
                    "). Tune Manual Translation Scale on this component to reduce floating.");
            }
        }
    }

    public float GetEffectiveTranslationScale(NpzStickFigureVisualizer stick)
    {
        lastEffectiveTranslationScale = BodyProportionScaler.ComputeWorldTranslationScale(
            computedLegHeightRatio,
            stick != null ? stick.PoseScale : 1f,
            manualTranslationScale,
            useAutoTranslationScale);
        return lastEffectiveTranslationScale;
    }

    public Vector3 ComputeScaledStickRootTarget(
        Vector3 currentStickHipWorld,
        NpzStickFigureVisualizer stick,
        Vector3 hipsLocalOffset)
    {
        Vector3 scaledHip = currentStickHipWorld;
        if (hasDanceTranslationAnchor)
        {
            float scale = GetEffectiveTranslationScale(stick);
            scaledHip = danceAnchorStickHip + (currentStickHipWorld - danceAnchorStickHip) * scale;
        }

        Vector3 target = scaledHip - transform.rotation * hipsLocalOffset;
        target.y -= stickRootVerticalOffset;
        return target;
    }

    [ContextMenu("Log Foot Floor Clearance")]
    public void LogFootFloorClearance()
    {
        float lowest = GetLowestFootContactY();
        if (float.IsPositiveInfinity(lowest))
        {
            Debug.LogWarning("ReadyPlayerAvatar on '" + name + "': no foot contact found.");
            return;
        }

        Debug.Log(
            "ReadyPlayerAvatar on '" + name + "': lowest foot Y=" + lowest.ToString("F3") +
            ", floorLevel=" + floorLevel.ToString("F3") +
            ", clearance=" + (lowest - floorLevel).ToString("F3") +
            " (negative = clipping through floor)");
    }

    [ContextMenu("Log Body Proportions (current stick frame)")]
    public void LogBodyProportionsFromStick()
    {
        if (!isLoaded())
        {
            Debug.LogWarning("ReadyPlayerAvatar: avatar not loaded.");
            return;
        }

        NpzStickFigureVisualizer stick = FindFirstObjectByType<NpzStickFigureVisualizer>();
        if (stick == null || !stick.TryGetPlaybackSample(out NpzMotionFrame frame0, out _, out _))
        {
            Debug.LogWarning("ReadyPlayerAvatar: no loaded NpzStickFigureVisualizer clip.");
            return;
        }

        if (BodyProportionScaler.TryBuildReport(frame0, stick.AxisFlip, this, out BodyProportionScaler.SegmentReport report))
        {
            report.Log(name);
            computedLegHeightRatio = report.legHeightRatio;
            lastEffectiveTranslationScale = GetEffectiveTranslationScale(stick);
            Debug.Log(
                "ReadyPlayerAvatar on '" + name + "': effective translation scale would be " +
                lastEffectiveTranslationScale.ToString("F3"));
        }
    }

    public UposeStickPoseSolver.HipLocalJoints SmoothStickJoints(
        in UposeStickPoseSolver.HipLocalJoints target,
        float deltaTime)
    {
        if (!hasSmoothedStickJoints)
        {
            smoothedStickJoints = target;
            hasSmoothedStickJoints = true;
            return target;
        }

        float t = GetStickSmoothStep(stickJointSmoothing, deltaTime);
        smoothedStickJoints.leftHip = Vector3.Lerp(smoothedStickJoints.leftHip, target.leftHip, t);
        smoothedStickJoints.rightHip = Vector3.Lerp(smoothedStickJoints.rightHip, target.rightHip, t);
        smoothedStickJoints.shoulderCenter = Vector3.Lerp(smoothedStickJoints.shoulderCenter, target.shoulderCenter, t);
        smoothedStickJoints.leftShoulder = Vector3.Lerp(smoothedStickJoints.leftShoulder, target.leftShoulder, t);
        smoothedStickJoints.rightShoulder = Vector3.Lerp(smoothedStickJoints.rightShoulder, target.rightShoulder, t);
        smoothedStickJoints.leftElbow = Vector3.Lerp(smoothedStickJoints.leftElbow, target.leftElbow, t);
        smoothedStickJoints.rightElbow = Vector3.Lerp(smoothedStickJoints.rightElbow, target.rightElbow, t);
        smoothedStickJoints.leftWrist = Vector3.Lerp(smoothedStickJoints.leftWrist, target.leftWrist, t);
        smoothedStickJoints.rightWrist = Vector3.Lerp(smoothedStickJoints.rightWrist, target.rightWrist, t);
        smoothedStickJoints.leftKnee = Vector3.Lerp(smoothedStickJoints.leftKnee, target.leftKnee, t);
        smoothedStickJoints.rightKnee = Vector3.Lerp(smoothedStickJoints.rightKnee, target.rightKnee, t);
        smoothedStickJoints.leftAnkle = Vector3.Lerp(smoothedStickJoints.leftAnkle, target.leftAnkle, t);
        smoothedStickJoints.rightAnkle = Vector3.Lerp(smoothedStickJoints.rightAnkle, target.rightAnkle, t);
        return smoothedStickJoints;
    }

    public void ApplySmoothedStickRoot(Vector3 targetRootPos, float deltaTime)
    {
        if (!hasSmoothedStickRootPos)
        {
            smoothedStickRootPos = targetRootPos;
            hasSmoothedStickRootPos = true;
        }
        else
        {
            float t = GetStickSmoothStep(stickRootSmoothing, deltaTime);
            if (moveToFloor)
            {
                smoothedStickRootPos.x = Mathf.Lerp(smoothedStickRootPos.x, targetRootPos.x, t);
                smoothedStickRootPos.z = Mathf.Lerp(smoothedStickRootPos.z, targetRootPos.z, t);
            }
            else
            {
                smoothedStickRootPos = Vector3.Lerp(smoothedStickRootPos, targetRootPos, t);
            }
        }

        transform.position = smoothedStickRootPos;
    }

    public void ApplySmoothedStickPose(in UposeStickPoseSolver.SolvedPose targetPose, float deltaTime)
    {
        StickBoneLocals targetLocals = PoseToStickBoneLocals(targetPose);
        if (!hasSmoothedStickPose)
        {
            smoothedStickLocals = targetLocals;
            hasSmoothedStickPose = true;
            ApplyStickBoneLocals(smoothedStickLocals);
            return;
        }

        float t = GetStickSmoothStep(stickPoseSmoothing, deltaTime);
        smoothedStickLocals.hips = Quaternion.Slerp(smoothedStickLocals.hips, targetLocals.hips, t);
        smoothedStickLocals.spine = Quaternion.Slerp(smoothedStickLocals.spine, targetLocals.spine, t);
        smoothedStickLocals.leftArm = Quaternion.Slerp(smoothedStickLocals.leftArm, targetLocals.leftArm, t);
        smoothedStickLocals.rightArm = Quaternion.Slerp(smoothedStickLocals.rightArm, targetLocals.rightArm, t);
        smoothedStickLocals.leftForeArm = Quaternion.Slerp(smoothedStickLocals.leftForeArm, targetLocals.leftForeArm, t);
        smoothedStickLocals.rightForeArm = Quaternion.Slerp(smoothedStickLocals.rightForeArm, targetLocals.rightForeArm, t);
        smoothedStickLocals.leftUpLeg = Quaternion.Slerp(smoothedStickLocals.leftUpLeg, targetLocals.leftUpLeg, t);
        smoothedStickLocals.rightUpLeg = Quaternion.Slerp(smoothedStickLocals.rightUpLeg, targetLocals.rightUpLeg, t);
        smoothedStickLocals.leftLeg = Quaternion.Slerp(smoothedStickLocals.leftLeg, targetLocals.leftLeg, t);
        smoothedStickLocals.rightLeg = Quaternion.Slerp(smoothedStickLocals.rightLeg, targetLocals.rightLeg, t);
        ApplyStickBoneLocals(smoothedStickLocals);
    }

    static float GetStickSmoothStep(float smoothing, float deltaTime)
    {
        if (smoothing <= 0f)
        {
            return 1f;
        }

        return 1f - Mathf.Exp(-smoothing * Mathf.Max(deltaTime, 0f));
    }

    public void ApplyStickPose(in UposeStickPoseSolver.SolvedPose pose)
    {
        ApplyStickBoneLocals(PoseToStickBoneLocals(pose));
    }

    static StickBoneLocals PoseToStickBoneLocals(in UposeStickPoseSolver.SolvedPose pose)
    {
        return new StickBoneLocals
        {
            hips = pose.pelvis,
            spine = Quaternion.Inverse(pose.pelvis) * pose.spine,
            leftArm = Quaternion.Euler(0f, 0f, -90f) * Quaternion.Inverse(pose.spine) * pose.leftShoulder,
            rightArm = Quaternion.Euler(0f, 0f, 90f) * Quaternion.Inverse(pose.spine) * pose.rightShoulder,
            leftForeArm = Quaternion.Inverse(pose.leftShoulder) * pose.leftElbow,
            rightForeArm = Quaternion.Inverse(pose.rightShoulder) * pose.rightElbow,
            leftUpLeg = Quaternion.Inverse(pose.pelvis) * pose.leftHip,
            rightUpLeg = Quaternion.Inverse(pose.pelvis) * pose.rightHip,
            leftLeg = Quaternion.Inverse(pose.leftHip) * pose.leftKnee,
            rightLeg = Quaternion.Inverse(pose.rightHip) * pose.rightKnee,
        };
    }

    void ApplyStickBoneLocals(in StickBoneLocals locals)
    {
        if (Hips == null || Spine == null || LeftArm == null || RightArm == null
            || LeftForeArm == null || RightForeArm == null || LeftUpLeg == null || RightUpLeg == null
            || LeftLeg == null || RightLeg == null)
        {
            if (!loggedDanceBoneWarning)
            {
                loggedDanceBoneWarning = true;
                Debug.LogError("ReadyPlayerAvatar: Dance bones not found on '" + name + "'. Re-import the avatar GLB.");
            }

            return;
        }

        if (LeftShoulder != null)
        {
            LeftShoulder.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        if (RightShoulder != null)
        {
            RightShoulder.localRotation = Quaternion.Euler(0f, 0f, -90f);
        }

        Hips.localRotation = locals.hips;
        Spine.localRotation = locals.spine;
        LeftArm.localRotation = locals.leftArm;
        RightArm.localRotation = locals.rightArm;
        LeftForeArm.localRotation = locals.leftForeArm;
        RightForeArm.localRotation = locals.rightForeArm;
        LeftUpLeg.localRotation = locals.leftUpLeg;
        RightUpLeg.localRotation = locals.rightUpLeg;
        LeftLeg.localRotation = locals.leftLeg;
        RightLeg.localRotation = locals.rightLeg;
    }

    public void ApplyDanceRotations(float[] rotations)
    {
        if (Hips == null || Spine == null || LeftArm == null || RightArm == null
            || LeftForeArm == null || RightForeArm == null || LeftUpLeg == null || RightUpLeg == null
            || LeftLeg == null || RightLeg == null)
        {
            if (!loggedDanceBoneWarning)
            {
                loggedDanceBoneWarning = true;
                Debug.LogError("ReadyPlayerAvatar: Dance bones not found on '" + name + "'. Re-import the avatar GLB.");
            }

            return;
        }

        if (LeftShoulder != null)
        {
            LeftShoulder.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        if (RightShoulder != null)
        {
            RightShoulder.localRotation = Quaternion.Euler(0f, 0f, -90f);
        }

        //0:pelvis 1,2:torso, 3,4,5:left_shoulder, 6,7,8:right_shoulder, 9:left_elbow, 10:right_elbow, 11,12:left_hip, 13,14:right_hip, 15,16:left_knee, 17,18:right_knee
        Hips.localRotation = Quaternion.Euler(0, rotations[0], 0);
        Spine.localRotation = Quaternion.Euler(rotations[1], 0, rotations[2]);
        LeftArm.localRotation = Quaternion.Euler(0f, 0f, -90f) * Quaternion.Euler(rotations[3], rotations[4], rotations[5]);
        RightArm.localRotation = Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(rotations[6], rotations[7], rotations[8]);
        LeftForeArm.localRotation = Quaternion.Euler(0, 0, rotations[9]);
        RightForeArm.localRotation = Quaternion.Euler(0, 0, rotations[10]);
        LeftUpLeg.localRotation = Quaternion.Euler(rotations[11], 0, rotations[12]);
        RightUpLeg.localRotation = Quaternion.Euler(rotations[13], 0, rotations[14]);
        LeftLeg.localRotation = Quaternion.Euler(rotations[15], 0, rotations[16]);
        RightLeg.localRotation = Quaternion.Euler(rotations[17], 0, rotations[18]);
    }

    void ApplyDanceRootMotion(AvatarAnimationPlayer player)
    {
        if (player == null || !player.HasRootMotionData)
        {
            return;
        }

        if (player.CurrentDanceId != activeDanceId)
        {
            danceStartPosition = transform.position;
            activeDanceId = player.CurrentDanceId;
        }

        transform.position = danceStartPosition + player.GetRootOffset();
    }

}
