using UnityEngine;
using UnityEngine.Events;

public class WaterfallCollision : MonoBehaviour
{
    public UnityEvent uEvent;
    public GameObject TriggerObject;


    void Start()
    {

    }

    public void OnTriggerEnter(Collider collider)
    {
        if (collider.gameObject == TriggerObject)
        {
            uEvent.Invoke();
        }
    }
}
