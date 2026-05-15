using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;

public class YogaInteraction : MonoBehaviour
{
    ReadyPlayerAvatar avatar;
    bool initialized = false;
    public Image exerciseImage;
    public Sprite leftHipAbductionSprite;
    public Sprite rightHipAbductionSprite;
    public Sprite leftElbowFlexionSprite;
    public Sprite rightElbowFlexionSprite;

    public enum ExerciseType { LeftHipAbduction, RightHipAbduction, LeftElbowFlexion, RightElbowFlexion };

    public ExerciseType currentExercise = ExerciseType.RightHipAbduction;

    bool aboveLimit = false;
    int counter = 0;

    // 👇 引入 UI 控件
    public TextMeshProUGUI exerciseText;
    public TextMeshProUGUI counterText;

    // 动作切换时使用的原始颜色（用于闪烁）
    private Color originalColor;

    void Start()
    {
        avatar = GetComponent<ReadyPlayerAvatar>();
        if (exerciseText != null)
        {
            originalColor = exerciseText.color;
            UpdateUI();
        }
    }

    void Update()
    {
        if (!initialized && avatar.isLoaded())
        {
            initialized = true;
        }
        if (!initialized) return;

        // 动作检测和切换逻辑
        if (currentExercise == ExerciseType.RightHipAbduction)
        {
            Quaternion rotation = avatar.getRightHipRotation();
            float angle = Mathf.Abs(rotation.eulerAngles.z);
            if (!aboveLimit && angle > 210) { aboveLimit = true; counter++; }
            else if (aboveLimit && angle < 190) { aboveLimit = false; }

            if (counter >= 10) setExercise(ExerciseType.LeftHipAbduction);
        }
        else if (currentExercise == ExerciseType.LeftHipAbduction)
        {
            Quaternion rotation = avatar.getLeftHipRotation();
            float angle = Mathf.Abs(rotation.eulerAngles.z);
            if (!aboveLimit && angle < 150) { aboveLimit = true; counter++; }
            else if (aboveLimit && angle > 170) { aboveLimit = false; }

            if (counter >= 10) setExercise(ExerciseType.RightElbowFlexion);
        }
        else if (currentExercise == ExerciseType.RightElbowFlexion)
        {
            Quaternion rotation = avatar.getRightElbowRotation();
            float angle = Mathf.Abs(rotation.eulerAngles.z);
            
            if (!aboveLimit && angle > 110) { aboveLimit = true; counter++; }
            else if (aboveLimit && angle < 40) { aboveLimit = false; }

            if (counter >= 10) setExercise(ExerciseType.LeftElbowFlexion);
        }
        else if (currentExercise == ExerciseType.LeftElbowFlexion)
        {
            Quaternion rotation = avatar.getLeftElbowRotation();
            float angle = Mathf.Abs(rotation.eulerAngles.z);
            
            if (!aboveLimit && angle < 270) { aboveLimit = true; counter++;  }
            else if (aboveLimit && angle > 300) { aboveLimit = false; }

            if (counter >= 10) setExercise(ExerciseType.RightHipAbduction);
        }

        // 每帧更新 UI
        UpdateUI();
    }

    public void setExercise(ExerciseType exercise)
    {
        currentExercise = exercise;
        counter = 0;
        aboveLimit = false;

        // 播放闪烁提示动画
        if (exerciseText != null && exerciseImage != null)
        {
            switch (exercise)
            {
                case ExerciseType.LeftHipAbduction:
                    exerciseImage.sprite = leftHipAbductionSprite;
                    break;
                case ExerciseType.RightHipAbduction:
                    exerciseImage.sprite = rightHipAbductionSprite;
                    break;
                case ExerciseType.LeftElbowFlexion:
                    exerciseImage.sprite = leftElbowFlexionSprite;
                    break;
                case ExerciseType.RightElbowFlexion:
                    exerciseImage.sprite = rightElbowFlexionSprite;
                    break;
            }

            StopAllCoroutines(); // 防止多次叠加
            StartCoroutine(FlashExerciseText());
        }
    }

    public int getCounter() { return counter; }

    private void UpdateUI()
    {
        if (exerciseText != null)
        {
            // 更新练习名称
        exerciseText.text = $"Current Exercise";//\n{FormatExerciseName(yogaInteraction.currentExercise)}

        }
        if (counterText != null)
        {
            counterText.text = $"Reps Completed:{counter}/10";
        }
    }

    private string FormatExerciseName(ExerciseType type)
    {
        switch (type)
        {
            case ExerciseType.LeftHipAbduction: return "左侧髋外展";
            case ExerciseType.RightHipAbduction: return "右侧髋外展";
            case ExerciseType.LeftElbowFlexion: return "左手肘弯曲";
            case ExerciseType.RightElbowFlexion: return "右手肘弯曲";
            default: return "未知动作";
        }
    }

    private IEnumerator FlashExerciseText()
    {
        for (int i = 0; i < 6; i++) // 闪烁3次
        {
            exerciseText.color = Color.yellow;
            yield return new WaitForSeconds(0.15f);
            exerciseText.color = originalColor;
            yield return new WaitForSeconds(0.15f);
        }
    }

}