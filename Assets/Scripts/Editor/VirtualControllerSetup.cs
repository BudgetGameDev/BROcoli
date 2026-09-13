using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

public class VirtualControllerSetup
{
    [MenuItem("GameObject/UI/Virtual Controller", false, 10)]
    static void CreateVirtualController()
    {
        // Find or create Canvas
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Create main container
        GameObject controllerObj = new GameObject("VirtualController");
        controllerObj.transform.SetParent(canvas.transform, false);
        RectTransform controllerRect = controllerObj.AddComponent<RectTransform>();
        controllerRect.anchorMin = Vector2.zero;
        controllerRect.anchorMax = Vector2.one;
        controllerRect.offsetMin = Vector2.zero;
        controllerRect.offsetMax = Vector2.zero;

        VirtualController controller = controllerObj.AddComponent<VirtualController>();

        // Get built-in UI sprite for visibility
        Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        Sprite bgSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");

        // Create Joystick Background
        GameObject joystickBg = new GameObject("JoystickBackground");
        joystickBg.transform.SetParent(controllerObj.transform, false);
        RectTransform bgRect = joystickBg.AddComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(150, 150);
        Image bgImage = joystickBg.AddComponent<Image>();
        bgImage.sprite = bgSprite;
        bgImage.color = new Color(1, 1, 1, 0.5f);
        bgImage.type = Image.Type.Sliced;
        
        // Create Joystick Handle
        GameObject joystickHandle = new GameObject("JoystickHandle");
        joystickHandle.transform.SetParent(joystickBg.transform, false);
        RectTransform handleRect = joystickHandle.AddComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(60, 60);
        handleRect.anchoredPosition = Vector2.zero;
        Image handleImage = joystickHandle.AddComponent<Image>();
        handleImage.sprite = uiSprite;
        handleImage.color = new Color(1, 1, 1, 0.9f);

        // Create Action Button
        GameObject buttonObj = new GameObject("ActionButton");
        buttonObj.transform.SetParent(controllerObj.transform, false);
        RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(80, 80);
        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.sprite = uiSprite;
        buttonImage.color = new Color(0.2f, 0.6f, 1f, 0.8f);
        Button button = buttonObj.AddComponent<Button>();
        
        // Add button label
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(buttonObj.transform, false);
        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        Text label = labelObj.AddComponent<Text>();
        label.text = "A";
        label.alignment = TextAnchor.MiddleCenter;
        label.fontSize = 32;
        label.color = Color.white;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Wire up serialized fields via reflection
        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("joystickBackground").objectReferenceValue = bgRect;
        serializedController.FindProperty("joystickHandle").objectReferenceValue = handleRect;
        serializedController.FindProperty("actionButton").objectReferenceValue = button;
        serializedController.FindProperty("canvas").objectReferenceValue = canvas;
        serializedController.ApplyModifiedProperties();

        Selection.activeGameObject = controllerObj;
        Undo.RegisterCreatedObjectUndo(controllerObj, "Create Virtual Controller");

        Debug.Log("Virtual Controller created! Assign sprites in the Inspector for better visuals.");
    }
}
