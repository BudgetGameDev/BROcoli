using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Tracks and draws the main menu's current selection. Every panel highlights
/// through here, so what is outlined is always what the confirm button runs.
/// </summary>
public partial class MainMenu
{
    private const float SelectedScale = 1.06f;
    private const float SelectionLerpSpeed = 12f;
    private static readonly Color SelectionColor = new(0.64f, 1f, 0.76f, 0.95f);
    private static readonly Vector2 SelectionThickness = new(5f, 5f);

    private sealed class ButtonVisual
    {
        public Outline Outline;
        public RectTransform Rect;
        public Vector3 BaseScale;
    }

    private readonly Dictionary<Button, ButtonVisual> buttonVisuals = new();

    private void RegisterButtonVisual(Button button)
    {
        // Wired once per button: re-adding the hover listener on every rescan
        // left stale listeners behind pointing at the previous panel's indices.
        if (buttonVisuals.ContainsKey(button))
            return;

        Outline outline = button.GetComponent<Outline>();
        if (outline == null)
            outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = SelectionColor;
        outline.effectDistance = SelectionThickness;
        outline.enabled = false;

        RectTransform rect = button.GetComponent<RectTransform>();
        buttonVisuals[button] = new ButtonVisual
        {
            Outline = outline,
            Rect = rect,
            BaseScale = rect != null ? rect.localScale : Vector3.one,
        };

        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();

        Button hovered = button;
        var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener(_ => SelectButton(hovered));
        trigger.triggers.Add(enterEntry);
    }

    /// <summary>Drops every highlight, so a hidden panel leaves nothing glowing.</summary>
    private void ClearSelectionVisuals()
    {
        foreach (KeyValuePair<Button, ButtonVisual> pair in buttonVisuals)
        {
            if (pair.Key == null)
                continue;

            if (pair.Value.Outline != null)
                pair.Value.Outline.enabled = false;
            if (pair.Value.Rect != null)
                pair.Value.Rect.localScale = pair.Value.BaseScale;
        }
    }

    private void SelectButton(Button button)
    {
        int index = menuButtons != null ? Array.IndexOf(menuButtons, button) : -1;
        if (index >= 0)
            SelectButton(index);
    }

    private void SelectButton(int index, bool playSound = true)
    {
        if (
            menuButtons == null
            || index < 0
            || index >= menuButtons.Length
            || index == selectedIndex
        )
        {
            return;
        }

        if (playSound)
            ProceduralUIAudio.PlayHover();

        selectedIndex = index;

        if (EventSystem.current != null && menuButtons[index] != null)
            EventSystem.current.SetSelectedGameObject(menuButtons[index].gameObject);
    }

    private void UpdateSelectionVisuals()
    {
        Button selected =
            menuButtons != null && selectedIndex >= 0 && selectedIndex < menuButtons.Length
                ? menuButtons[selectedIndex]
                : null;

        foreach (KeyValuePair<Button, ButtonVisual> pair in buttonVisuals)
        {
            ButtonVisual visual = pair.Value;
            if (pair.Key == null || visual.Rect == null)
                continue;

            bool isSelected = pair.Key == selected;
            if (visual.Outline != null)
                visual.Outline.enabled = isSelected;

            Vector3 target = visual.BaseScale * (isSelected ? SelectedScale : 1f);
            visual.Rect.localScale = Vector3.Lerp(
                visual.Rect.localScale,
                target,
                Time.unscaledDeltaTime * SelectionLerpSpeed
            );
        }
    }
}
