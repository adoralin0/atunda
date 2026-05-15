using UnityEngine;

public class KickForce : MonoBehaviour
{
    public float kickForce = 10f; 
    private Rigidbody footRb; 

    void Start()
    {
        
        footRb = GetComponent<Rigidbody>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Box"))
        {
            Rigidbody boxRb = collision.gameObject.GetComponent<Rigidbody>();
            if (boxRb != null && footRb != null)
            {
                float forceAmount = footRb.linearVelocity.magnitude * kickForce;

                Vector3 kickDirection = collision.contacts[0].point - transform.position;
                kickDirection = kickDirection.normalized;

                boxRb.AddForce(kickDirection * forceAmount, ForceMode.Impulse);
            }
        }
    }
}
