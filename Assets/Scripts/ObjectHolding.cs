using UnityEngine;

public class ObjectHolding : MonoBehaviour
{
    ReadyPlayerAvatar avatar;
    bool initialized=false;
    LineRenderer line;
    GameObject topBox;
    GameObject bottomBox;

    public GameObject objectHeld;
    public Vector3 objectTranslation=new Vector3(0,0,0);
    public Quaternion objectRotation=Quaternion.identity;
    public Vector3 objectScale=new Vector3(1,1,1);

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        avatar = GetComponent<ReadyPlayerAvatar>();

        if(objectHeld!=null){
            Rigidbody rb = objectHeld.GetComponentInChildren<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if(!initialized && avatar.isLoaded()){
            Debug.Log("objectHeld added");
            GameObject left_hand=avatar.getLeftPalm();
         
            //Attach the bow object (her it is shown as objectHeld)
            //GameObject objectHeld = GameObject.CreatePrimitive(PrimitiveType.objectHeld);
            objectHeld.transform.SetParent(left_hand.transform);
            

            //make an anchor object for the top of the bow
            topBox = new GameObject();
            topBox.transform.SetParent(objectHeld.transform);
            topBox.transform.localPosition = new Vector3(0,-1f,0f); 
            topBox.transform.localScale = new Vector3(0.2f, 1f, 0.2f);

            //make an anchor object for the bottom of the bow
            bottomBox = new GameObject();
            bottomBox.transform.SetParent(objectHeld.transform);
            bottomBox.transform.localPosition = new Vector3(0,1f,0f); 
            bottomBox.transform.localScale = new Vector3(0.2f, 1f, 0.2f);

            //make a line object
            GameObject linePrefab = new GameObject("linePrefab");
            LineRenderer lineRenderer = linePrefab.AddComponent<LineRenderer>();
            lineRenderer.startWidth = 0.01f;
            lineRenderer.endWidth = 0.01f;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = Color.black;
            lineRenderer.endColor = Color.black;

            line = Instantiate(linePrefab).GetComponent<LineRenderer>();
            line.positionCount = 3;
            

            initialized=true;
        }  

        if(!initialized)return;


        objectHeld.transform.localPosition = objectTranslation; 
        objectHeld.transform.localRotation = objectRotation;
        objectHeld.transform.localScale = objectScale; 

        //On every frame update the position of the line
        line.SetPosition(0, topBox.transform.position);
        line.SetPosition(1, avatar.getRightPalm().transform.position);
        line.SetPosition(2, bottomBox.transform.position);

    }
}
