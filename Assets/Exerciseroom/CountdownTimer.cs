using System.Collections;
using UnityEngine;
using TMPro;

public class CountdownTimer : MonoBehaviour
{
    public TextMeshProUGUI countdownText; // 拖入你的 TMP 文本组件
    public float countdownTime = 30f;

    private float timeLeft;

    void Start()
    {
        timeLeft = countdownTime;
        StartCoroutine(CountdownRoutine());
    }

    IEnumerator CountdownRoutine()
    {
        while (timeLeft > 0)
        {
            countdownText.text = $"倒计时：{Mathf.Ceil(timeLeft)} 秒";
            yield return new WaitForSeconds(1f);
            timeLeft -= 1f;
        }

        countdownText.text = "倒计时结束！";
        // 可在此处添加结束逻辑
    }
}