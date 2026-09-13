using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Fixes MissingReferenceException in GraphicRaycaster by cleaning up destroyed Graphics from Unity's registry.
/// Attach this to a GameObject that persists (like a manager object) or the Canvas.
/// </summary>
public class GraphicRegistryCleaner : MonoBehaviour
{
    [Tooltip("How often to check for destroyed graphics (in seconds)")]
    public float cleanupInterval = 0.5f;
    
    private float lastCleanupTime;
    private static GraphicRegistryCleaner instance;

    void Awake()
    {
        // Singleton pattern
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    void Update()
    {
        // Periodically clean up
        if (Time.unscaledTime - lastCleanupTime > cleanupInterval)
        {
            lastCleanupTime = Time.unscaledTime;
            CleanupDestroyedGraphics();
        }
    }

    /// <summary>
    /// Removes destroyed Graphics from all Canvas graphic lists.
    /// This prevents MissingReferenceException in GraphicRaycaster.Raycast.
    /// </summary>
    public static void CleanupDestroyedGraphics()
    {
        // Find all canvases
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        
        foreach (Canvas canvas in canvases)
        {
            if (canvas == null) continue;
            
            // Get all graphics registered to this canvas using reflection
            // GraphicRegistry is internal, so we need to access it via GraphicRaycaster
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null) continue;
            
            // Force the raycaster to rebuild by toggling it
            // This clears destroyed references
            if (raycaster.enabled)
            {
                raycaster.enabled = false;
                raycaster.enabled = true;
            }
        }
        
        // Also clean up via the Graphic registry directly
        CleanupGraphicRegistry();
    }

    /// <summary>
    /// Uses reflection to clean up Unity's internal GraphicRegistry.
    /// </summary>
    private static void CleanupGraphicRegistry()
    {
        try
        {
            // Get the GraphicRegistry type
            System.Type registryType = typeof(Graphic).Assembly.GetType("UnityEngine.UI.GraphicRegistry");
            if (registryType == null) return;

            // Get the instance
            PropertyInfo instanceProp = registryType.GetProperty("instance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (instanceProp == null) return;
            
            object registryInstance = instanceProp.GetValue(null);
            if (registryInstance == null) return;

            // Get the m_Graphics dictionary field
            FieldInfo graphicsField = registryType.GetField("m_Graphics", BindingFlags.Instance | BindingFlags.NonPublic);
            if (graphicsField == null) return;

            // The dictionary is Dictionary<Canvas, IndexedSet<Graphic>>
            object graphicsDict = graphicsField.GetValue(registryInstance);
            if (graphicsDict == null) return;

            // Get the dictionary as IDictionary to iterate
            var dict = graphicsDict as System.Collections.IDictionary;
            if (dict == null) return;

            // Collect canvases with null graphics to clean
            List<Canvas> canvasesToClean = new List<Canvas>();
            
            foreach (System.Collections.DictionaryEntry entry in dict)
            {
                Canvas canvas = entry.Key as Canvas;
                if (canvas == null)
                {
                    continue; // Canvas itself is destroyed
                }
                
                // Check if any graphics in this canvas's list are destroyed
                var indexedSet = entry.Value;
                if (indexedSet == null) continue;

                // Get the list inside IndexedSet
                FieldInfo listField = indexedSet.GetType().GetField("m_List", BindingFlags.Instance | BindingFlags.NonPublic);
                if (listField == null) continue;

                var list = listField.GetValue(indexedSet) as System.Collections.IList;
                if (list == null) continue;

                foreach (var item in list)
                {
                    Graphic graphic = item as Graphic;
                    // Check if graphic is destroyed (Unity overloads == for null check on destroyed objects)
                    if (graphic == null)
                    {
                        canvasesToClean.Add(canvas);
                        break;
                    }
                }
            }

            // For each canvas with destroyed graphics, force re-registration
            foreach (Canvas canvas in canvasesToClean)
            {
                if (canvas == null) continue;
                
                // Disable and re-enable all graphics on this canvas to force re-registration
                Graphic[] graphics = canvas.GetComponentsInChildren<Graphic>(true);
                foreach (Graphic g in graphics)
                {
                    if (g != null && g.enabled)
                    {
                        g.enabled = false;
                        g.enabled = true;
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[GraphicRegistryCleaner] Reflection cleanup failed: {e.Message}");
        }
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
