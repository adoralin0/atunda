// using System;
// using System.Linq;
// using UnityEditor.SpeedTree.Importer;
// using UnityEngine;

// public class ArcherInteraction : MonoBehaviour
// {
//     ReadyPlayerAvatar avatar;
//     bool initialized = false;
//     LineRenderer line;
//     GameObject topBox;
//     GameObject bottomBox;

//     public GameObject objectHeld;
//     public Vector3 objectTranslation = new Vector3(0, 0, 0);
//     public Quaternion objectRotation = Quaternion.identity;
//     public Vector3 objectScale = new Vector3(1, 1, 1);

//     public GameObject object2Held;
//     public Vector3 object2Translation = new Vector3(0, 0, 0);
//     public Quaternion object2Rotation = Quaternion.identity;
//     public Vector3 object2Scale = new Vector3(1, 1, 1);

//     Transform stringPosition;
//     float animationY = 0;
//     bool isAnimated = false;
//     Vector3 arrow_direction;
//     Vector3 arrow_position;
//     float speed = 8;

//     GameObject left_hand;
//     GameObject arrow_container;


//     // Start is called once before the first execution of Update after the MonoBehaviour is created
//     void Start()
//     {
//         avatar = GetComponent<ReadyPlayerAvatar>();

//         arrow_container = new GameObject("ArrowContainer");

//         if (objectHeld != null)
//         {

//             stringPosition = objectHeld.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "WB.string");

//             Rigidbody rb = objectHeld.GetComponentInChildren<Rigidbody>();
//             if (rb != null)
//             {
//                 rb.isKinematic = true;
//             }
//         }

//         if (object2Held != null)
//         {
//             Rigidbody rb = object2Held.GetComponentInChildren<Rigidbody>();
//             if (rb != null)
//             {
//                 rb.isKinematic = true;
//             }
//         }
//     }

//     // Update is called once per frame
//     void Update()
//     {
//         if (!initialized && avatar.isLoaded())
//         {
//             Debug.Log("objectHeld added");
//             left_hand = avatar.getLeftPalm();

//             //Attach the bow object (her it is shown as objectHeld)
//             //GameObject objectHeld = GameObject.CreatePrimitive(PrimitiveType.objectHeld);
//             objectHeld.transform.SetParent(left_hand.transform);
//             object2Held.transform.SetParent(arrow_container.transform);
            

//             /*
//             //make an anchor object for the top of the bow
//             topBox = new GameObject();
//             topBox.transform.SetParent(objectHeld.transform);
//             topBox.transform.localPosition = new Vector3(0,-1f,0f); 
//             topBox.transform.localScale = new Vector3(0.2f, 1f, 0.2f);

//             //make an anchor object for the bottom of the bow
//             bottomBox = new GameObject();
//             bottomBox.transform.SetParent(objectHeld.transform);
//             bottomBox.transform.localPosition = new Vector3(0,1f,0f); 
//             bottomBox.transform.localScale = new Vector3(0.2f, 1f, 0.2f);

//             //make a line object
//             GameObject linePrefab = new GameObject("linePrefab");
//             LineRenderer lineRenderer = linePrefab.AddComponent<LineRenderer>();
//             lineRenderer.startWidth = 0.01f;
//             lineRenderer.endWidth = 0.01f;
//             lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
//             lineRenderer.startColor = Color.black;
//             lineRenderer.endColor = Color.black;

//             line = Instantiate(linePrefab).GetComponent<LineRenderer>();
//             line.positionCount = 3;
//             */

//             initialized = true;
//         }

//         if (!initialized) return;


//         objectHeld.transform.localPosition = objectTranslation;
//         objectHeld.transform.localRotation = objectRotation;
//         objectHeld.transform.localScale = objectScale;


//         if (isAnimated)
//         {
//             arrow_container.transform.position+= arrow_direction * Time.deltaTime * speed;
//             arrow_container.transform.rotation=Quaternion.LookRotation(arrow_direction)*Quaternion.Euler(90,0,0);
            
//             Vector3 diff = object2Held.transform.position - avatar.getLeftPalm().transform.position;
//             if (diff.magnitude > 20)//how far it can fly
//             {
//                 isAnimated = false;
//             }
//         }
//         else
//         {
//             Vector3 diff = avatar.getLeftPalm().transform.position - avatar.getRightPalm().transform.position;

//             arrow_container.transform.position=avatar.getLeftPalm().transform.position;
//             arrow_container.transform.rotation=Quaternion.LookRotation(diff.normalized)*Quaternion.Euler(90,0,0);

//             object2Held.transform.localPosition = object2Translation;
//             object2Held.transform.localRotation = object2Rotation;
//             object2Held.transform.localScale = object2Scale;

            
//             if (diff.magnitude > 1f)
//             { //how far the hands should be to trigger
//                 isAnimated = true;
//                 arrow_direction = diff.normalized;
//                 arrow_position=object2Held.transform.position;
//             }
//         }






//         stringPosition.position = avatar.getRightPalm().transform.position;

//         //On every frame update the position of the line
//         //line.SetPosition(0, topBox.transform.position);
//         //line.SetPosition(1, avatar.getRightPalm().transform.position);
//         //line.SetPosition(2, bottomBox.transform.position);

//     }
// }
using System;
using System.Linq;
// Removed the SpeedTree importer line that was causing the build crash
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ArcherInteraction : MonoBehaviour
{
    ReadyPlayerAvatar avatar;
    bool initialized = false;
    LineRenderer line;
    GameObject topBox;
    GameObject bottomBox;

    public GameObject objectHeld;
    public Vector3 objectTranslation = new Vector3(0, 0, 0);
    public Quaternion objectRotation = Quaternion.identity;
    public Vector3 objectScale = new Vector3(1, 1, 1);

    public GameObject object2Held;
    public Vector3 object2Translation = new Vector3(0, 0, 0);
    public Quaternion object2Rotation = Quaternion.identity;
    public Vector3 object2Scale = new Vector3(1, 1, 1);

    Transform stringPosition;
    float animationY = 0;
    bool isAnimated = false;
    Vector3 arrow_direction;
    Vector3 arrow_position;
    float speed = 8;

    GameObject left_hand;
    GameObject arrow_container;

    void Start()
    {
        avatar = GetComponent<ReadyPlayerAvatar>();
        arrow_container = new GameObject("ArrowContainer");

        if (objectHeld != null)
        {
            stringPosition = objectHeld.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "WB.string");

            Rigidbody rb = objectHeld.GetComponentInChildren<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }
        }

        if (object2Held != null)
        {
            Rigidbody rb = object2Held.GetComponentInChildren<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }
        }
    }

    void Update()
    {
        if (!initialized && avatar.isLoaded())
        {
            Debug.Log("objectHeld added");
            left_hand = avatar.getLeftPalm();

            objectHeld.transform.SetParent(left_hand.transform);
            object2Held.transform.SetParent(arrow_container.transform);

            initialized = true;
        }

        if (!initialized) return;

        objectHeld.transform.localPosition = objectTranslation;
        objectHeld.transform.localRotation = objectRotation;
        objectHeld.transform.localScale = objectScale;

        if (isAnimated)
        {
            arrow_container.transform.position += arrow_direction * Time.deltaTime * speed;
            arrow_container.transform.rotation = Quaternion.LookRotation(arrow_direction) * Quaternion.Euler(90, 0, 0);
            
            Vector3 diff = object2Held.transform.position - avatar.getLeftPalm().transform.position;
            if (diff.magnitude > 20)
            {
                isAnimated = false;
            }
        }
        else
        {
            Vector3 diff = avatar.getLeftPalm().transform.position - avatar.getRightPalm().transform.position;

            arrow_container.transform.position = avatar.getLeftPalm().transform.position;
            arrow_container.transform.rotation = Quaternion.LookRotation(diff.normalized) * Quaternion.Euler(90, 0, 0);

            object2Held.transform.localPosition = object2Translation;
            object2Held.transform.localRotation = object2Rotation;
            object2Held.transform.localScale = object2Scale;
            
            if (diff.magnitude > 1f)
            {
                isAnimated = true;
                arrow_direction = diff.normalized;
                arrow_position = object2Held.transform.position;
            }
        }

        if (stringPosition != null)
        {
            stringPosition.position = avatar.getRightPalm().transform.position;
        }
    }
}