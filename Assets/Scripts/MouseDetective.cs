using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class MouseDetective : MonoBehaviour
{
    void Update()
    {
        // 1. Check if the EventSystem even exists
        if (EventSystem.current == null)
        {
            Debug.LogError("CRITICAL: No EventSystem found in Hierarchy! Right-click > UI > Event System.");
            return;
        }

        // 2. Create a pointer event at the mouse position
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        // 3. Raycast to see what the mouse is hitting
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        if (results.Count > 0)
        {
            // This tells you exactly what object is "blocking" the view
            Debug.Log("<color=yellow>Mouse is hitting:</color> " + results[0].gameObject.name);
        }
        else
        {
            Debug.Log("<color=red>Mouse is hitting NOTHING.</color> Check your Canvas Graphic Raycaster!");
        }
    }
}