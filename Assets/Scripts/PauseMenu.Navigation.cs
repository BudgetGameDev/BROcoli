using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class PauseMenu
{
    private bool navigationInitialized;

    private void SetupMenuNavigation()
    {
        if (pauseMenuUI == null)
            return;
        if (navigationInitialized)
        {
            ResetMenuNavigation();
            return;
        }

        Button[] allButtons = pauseMenuUI.GetComponentsInChildren<Button>(true);
        var buttonList = new List<Button>();
        foreach (Button button in allButtons)
        {
            if (button != null && button.interactable)
                buttonList.Add(button);
        }

        menuButtons = buttonList.ToArray();
        buttonOutlines = new Outline[menuButtons.Length];
        originalScales = new Vector3[menuButtons.Length];

        for (int i = 0; i < menuButtons.Length; i++)
        {
            Button button = menuButtons[i];
            if (button == null)
                continue;

            originalScales[i] = button.transform.localScale;
            Outline outline = button.GetComponent<Outline>();
            if (outline == null)
                outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.9f, 0.2f, 1f);
            outline.effectDistance = new Vector2(6f, 6f);
            outline.enabled = false;
            buttonOutlines[i] = outline;

            int index = i;
            EventTrigger trigger = button.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = button.gameObject.AddComponent<EventTrigger>();
            var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener((data) => SelectMenuButton(index));
            trigger.triggers.Add(enterEntry);
        }

        navigationInitialized = menuButtons.Length > 0;
    }

    private void ResetMenuNavigation()
    {
        if (menuButtons == null)
            return;

        for (int i = 0; i < menuButtons.Length; i++)
        {
            if (menuButtons[i] == null)
                continue;
            if (originalScales != null && i < originalScales.Length)
                menuButtons[i].transform.localScale = originalScales[i];
            if (buttonOutlines != null && i < buttonOutlines.Length && buttonOutlines[i] != null)
                buttonOutlines[i].enabled = false;
        }

        GameObject selected = EventSystem.current?.currentSelectedGameObject;
        if (
            selected != null
            && pauseMenuUI != null
            && selected.transform.IsChildOf(pauseMenuUI.transform)
        )
            EventSystem.current.SetSelectedGameObject(null);
    }
}
