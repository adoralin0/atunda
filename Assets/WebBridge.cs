using UnityEngine;

/// <summary>
/// Public entry points for JavaScript (WebGL) via
/// unityInstance.SendMessage("WebBridge", "MethodName").
/// </summary>
public class WebBridge : MonoBehaviour
{
    /// <summary>
    /// Same behavior as the in-game right arrow: advance to the next character.
    /// </summary>
    public void NextCharacter()
    {
        CharacterSelector selector = FindFirstObjectByType<CharacterSelector>();
        if (selector == null)
        {
            Debug.LogWarning("WebBridge.NextCharacter: no CharacterSelector found.");
            return;
        }

        selector.NextCharacter();
        Debug.Log("WebBridge.NextCharacter: advanced character.");
    }

    /// <summary>
    /// Same behavior as the in-game left arrow: go to the previous character.
    /// </summary>
    public void PreviousCharacter()
    {
        CharacterSelector selector = FindFirstObjectByType<CharacterSelector>();
        if (selector == null)
        {
            Debug.LogWarning("WebBridge.PreviousCharacter: no CharacterSelector found.");
            return;
        }

        selector.PreviousCharacter();
        Debug.Log("WebBridge.PreviousCharacter: previous character.");
    }

    /// <summary>
    /// Selects the next dance in the menu list.
    /// If nothing is selected, selects the first dance.
    /// </summary>
    public void NextDanceMove()
    {
        if (AvatarUIItem.SelectNextDance())
        {
            Debug.Log("WebBridge.NextDanceMove: selected next dance.");
            return;
        }

        Debug.LogWarning("WebBridge.NextDanceMove: no dance menu items found.");
    }

    /// <summary>
    /// Selects the previous dance in the menu list.
    /// If nothing is selected, selects the last dance.
    /// </summary>
    public void PreviousDanceMove()
    {
        if (AvatarUIItem.SelectPreviousDance())
        {
            Debug.Log("WebBridge.PreviousDanceMove: selected previous dance.");
            return;
        }

        Debug.LogWarning("WebBridge.PreviousDanceMove: no dance menu items found.");
    }
}
