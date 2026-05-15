using UnityEngine;
using TMPro;

public class ArcherPoint : MonoBehaviour
{
    public int score = 0;
    public TextMeshProUGUI scoreText;
    public string targetTag = "Target";

    void Start()
    {
        UpdateScoreText();
    }

    /*void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag(targetTag))
        {
            score++;
            UpdateScoreText();
        }
    }*/

    private void OnTriggerEnter(Collider collider)
    {
        Debug.Log("OnTriggerEnter");
        if(collider.gameObject.CompareTag(targetTag))
        {
            score++;
            UpdateScoreText();
        }
    }

    void UpdateScoreText()
    {
        scoreText.text = "Score: " + score;
    }
}
