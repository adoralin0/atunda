using UnityEngine;

public class AvatarDanceDemoLauncher : MonoBehaviour
{
    public string danceName = "1";
    public bool playOnStart = true;

    void Start()
    {
        if (!playOnStart) return;
        var player = GetComponent<AvatarAnimationPlayer>();
        if (player == null)
        {
            Debug.LogWarning("AvatarDanceDemoLauncher: no AvatarAnimationPlayer found on this GameObject.");
            return;
        }

        player.PlayDance(danceName + ".json");
    }
}