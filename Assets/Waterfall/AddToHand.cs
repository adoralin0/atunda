using UnityEngine;

public class NewMonoBehaviourScript : MonoBehaviour
{
    ReadyPlayerAvatar avatar;
    bool initialized = false;

    public GameObject objectToHold_L;
    public GameObject objectToHold_R;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        avatar = GetComponent<ReadyPlayerAvatar>();
    }


    // Update is called once per frame
    void Update()
    {
        if (!initialized && avatar.isLoaded())
        {
            
            GameObject left_hand = avatar.getLeftPalm();
            objectToHold_L.transform.SetParent(left_hand.transform);
            objectToHold_L.transform.localPosition = new Vector3(0, 0, 0);

            GameObject right_hand = avatar.getRightPalm();
            objectToHold_R.transform.SetParent(right_hand.transform);
            objectToHold_R.transform.localPosition = new Vector3(0, 0, 0);
            initialized = true;
        }
        if (!initialized) return;
    }
}
