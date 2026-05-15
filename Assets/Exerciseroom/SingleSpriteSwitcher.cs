using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SingleSpriteSwitcher : MonoBehaviour
{
    public string folderPath = "YogaPoses";
    public float switchInterval = 10f;

    private Sprite[] sprites;
    private Image imageComponent;
    private int currentIndex = 0;

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
        StartCoroutine(SwitchSpriteRoutine());
    }

    IEnumerator SwitchSpriteRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(switchInterval);
            currentIndex = (currentIndex + 1) % sprites.Length;
            imageComponent.sprite = sprites[currentIndex];
        }
    }
}