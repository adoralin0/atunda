using UnityEngine;

public class HandParticleController2 : MonoBehaviour
{
    public ParticleSystem particleEffect;
    public Transform handTransform;
    public Gradient colorGradient;
    private bool isFollowing = false;
    private ParticleSystem.MainModule mainModule;

    void Start()
    {
        if (particleEffect != null)
        {
            mainModule = particleEffect.main;
            mainModule.startColor = new ParticleSystem.MinMaxGradient(colorGradient);
            particleEffect.Stop();
        }
    }

    void Update()
    {
        if (isFollowing && particleEffect != null)
        {
            particleEffect.transform.position = handTransform.position;

            float t = Mathf.PingPong(Time.time * 0.5f, 1f);
            Color dynamicColor = colorGradient.Evaluate(t);
            mainModule.startColor = new ParticleSystem.MinMaxGradient(dynamicColor);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.transform == handTransform)
        {
            isFollowing = true;
            particleEffect.Play();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.transform == handTransform)
        {
            isFollowing = false;
            particleEffect.Stop();
        }
    }
}