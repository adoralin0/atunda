using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SpriteSwitcherWithPoseDelay : MonoBehaviour
{
    public string folderPath = "YogaPoses";
    public float countdownDuration = 30f;
    public float changePoseDuration = 5f;

    public TextMeshProUGUI countdownText;   // 倒计时文本（TextMeshPro）
    public GameObject changePoseUI;         // “Change Pose” 提示 UI（Text/Image等）

    public Color normalColor = Color.white; // 正常状态字体颜色
    public Color warningColor = Color.red;  // 最后 5 秒字体颜色

    private Sprite[] sprites;
    private Image imageComponent;
    private int currentIndex = 0;

    private Coroutine flashCoroutine;

    void Start()
    {
        imageComponent = GetComponent<Image>();

        if (imageComponent == null)
        {
            Debug.LogError("找不到 Image 组件，请将脚本挂在 UI > Image 对象上！");
            return;
        }

        sprites = Resources.LoadAll<Sprite>(folderPath);

        if (sprites == null || sprites.Length == 0)
        {
            Debug.LogError($"没有找到任何图片资源，请确保 Resources/{folderPath} 下有导入为 Sprite 的图片！");
            return;
        }

        imageComponent.sprite = sprites[0];

        if (changePoseUI != null)
            changePoseUI.SetActive(false);

        StartCoroutine(MainCycle());
    }

    IEnumerator MainCycle()
    {
        while (true)
        {
            // -------- 倒计时阶段 --------
            float timeLeft = countdownDuration;
            countdownText.color = normalColor;
            countdownText.alpha = 1f;

            while (timeLeft > 0)
            {
                countdownText.text = $"Timer Left: {Mathf.Ceil(timeLeft)} s";
                countdownText.fontSize = 48;


                if (timeLeft <= 5)
                {
                    countdownText.color = warningColor;

                    if (flashCoroutine == null)
                        flashCoroutine = StartCoroutine(FlashCountdownText());
                }

                yield return new WaitForSeconds(1f);
                timeLeft -= 1f;
            }

            // 停止闪烁
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
                flashCoroutine = null;
            }

            countdownText.text = "";
            countdownText.alpha = 1f;
            countdownText.color = normalColor;

            // -------- 换动作提示阶段 --------
            if (changePoseUI != null)
                changePoseUI.SetActive(true);

            currentIndex = (currentIndex + 1) % sprites.Length;
            imageComponent.sprite = sprites[currentIndex];

            yield return new WaitForSeconds(changePoseDuration);

            if (changePoseUI != null)
                changePoseUI.SetActive(false);
        }
    }

    IEnumerator FlashCountdownText()
    {
        while (true)
        {
            countdownText.alpha = 0f;
            yield return new WaitForSeconds(0.25f);
            countdownText.alpha = 1f;
            yield return new WaitForSeconds(0.25f);
        }
    }
}