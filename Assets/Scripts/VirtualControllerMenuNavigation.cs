using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// Handles menu navigation using virtual controller joystick.
/// Attach to a Canvas or UI root object.
/// </summary>
public class VirtualControllerMenuNavigation : MonoBehaviour
{
    [SerializeField] private float navigationDelay = 0.3f;
    [SerializeField] private float joystickThreshold = 0.5f;
    [SerializeField] private Selectable firstSelected;

    private float lastNavigationTime;
    private Vector2 lastJoystickDirection;
    private EventSystem eventSystem;
    private List<Selectable> selectables = new List<Selectable>();

    private void Start()
    {
        eventSystem = EventSystem.current;
        
        // Find all selectables in scene
        RefreshSelectables();
        
        // Select first item if nothing selected
        if (firstSelected != null && eventSystem.currentSelectedGameObject == null)
        {
            eventSystem.SetSelectedGameObject(firstSelected.gameObject);
        }
        else if (selectables.Count > 0 && eventSystem.currentSelectedGameObject == null)
        {
            eventSystem.SetSelectedGameObject(selectables[0].gameObject);
        }
    }

    private void RefreshSelectables()
    {
        selectables.Clear();
        selectables.AddRange(FindObjectsOfType<Selectable>());
        
        // Filter to only interactable ones
        selectables.RemoveAll(s => !s.interactable || !s.gameObject.activeInHierarchy);
    }

    private void Update()
    {
        if (VirtualController.Instance == null) return;

        Vector2 input = VirtualController.Instance.JoystickInput;
        
        // Check if joystick moved past threshold
        if (input.magnitude > joystickThreshold && Time.time - lastNavigationTime > navigationDelay)
        {
            // Determine primary direction
            if (Mathf.Abs(input.y) > Mathf.Abs(input.x))
            {
                // Vertical navigation
                if (input.y > joystickThreshold)
                {
                    NavigateUp();
                    lastNavigationTime = Time.time;
                }
                else if (input.y < -joystickThreshold)
                {
                    NavigateDown();
                    lastNavigationTime = Time.time;
                }
            }
            else
            {
                // Horizontal navigation
                if (input.x > joystickThreshold)
                {
                    NavigateRight();
                    lastNavigationTime = Time.time;
                }
                else if (input.x < -joystickThreshold)
                {
                    NavigateLeft();
                    lastNavigationTime = Time.time;
                }
            }
        }
        
        // Reset navigation delay when joystick returns to center
        if (input.magnitude < 0.2f)
        {
            lastNavigationTime = 0f;
        }
    }

    private void NavigateUp()
    {
        Navigate(MoveDirection.Up);
    }

    private void NavigateDown()
    {
        Navigate(MoveDirection.Down);
    }

    private void NavigateLeft()
    {
        Navigate(MoveDirection.Left);
    }

    private void NavigateRight()
    {
        Navigate(MoveDirection.Right);
    }

    private void Navigate(MoveDirection direction)
    {
        if (eventSystem == null) return;

        GameObject current = eventSystem.currentSelectedGameObject;
        if (current == null)
        {
            // Select first available
            if (selectables.Count > 0)
            {
                eventSystem.SetSelectedGameObject(selectables[0].gameObject);
            }
            return;
        }

        Selectable currentSelectable = current.GetComponent<Selectable>();
        if (currentSelectable == null) return;

        Selectable next = null;
        switch (direction)
        {
            case MoveDirection.Up:
                next = currentSelectable.FindSelectableOnUp();
                break;
            case MoveDirection.Down:
                next = currentSelectable.FindSelectableOnDown();
                break;
            case MoveDirection.Left:
                next = currentSelectable.FindSelectableOnLeft();
                break;
            case MoveDirection.Right:
                next = currentSelectable.FindSelectableOnRight();
                break;
        }

        if (next != null && next.interactable)
        {
            eventSystem.SetSelectedGameObject(next.gameObject);
        }
    }
}
