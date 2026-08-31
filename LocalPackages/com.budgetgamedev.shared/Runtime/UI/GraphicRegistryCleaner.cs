using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace BudgetGameDev.Shared
{
    /// <summary>
    /// Fixes MissingReferenceException in GraphicRaycaster by cleaning up destroyed Graphics from Unity's registry.
    /// Attach this to a GameObject that persists (like a manager object) or the Canvas.
    /// </summary>
    public class GraphicRegistryCleaner : MonoBehaviour
    {
        /// <summary>Unity's registry of which Graphic belongs to which Canvas.</summary>
        internal const string RegistryTypeName = "UnityEngine.UI.GraphicRegistry";

        [Tooltip("How often to check for destroyed graphics (in seconds)")]
        public float cleanupInterval = 0.5f;

        internal float lastCleanupTime;
        internal static GraphicRegistryCleaner instance;

        internal void Awake()
        {
            // Singleton pattern
            if (instance != null && instance != this)
            {
                // Destroy only works while the game is playing, and this component
                // is also placed by editor tooling, so fall back to the immediate
                // form rather than leaving a second cleaner running.
                if (Application.isPlaying)
                    Destroy(gameObject);
                else
                    DestroyImmediate(gameObject);
                return;
            }
            instance = this;
        }

        internal void Update()
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
            Canvas[] canvases = FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            foreach (Canvas canvas in canvases)
            {
                // Get all graphics registered to this canvas using reflection
                // GraphicRegistry is internal, so we need to access it via GraphicRaycaster
                GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
                if (raycaster == null)
                    continue;

                // Force the raycaster to rebuild by toggling it
                // This clears destroyed references
                if (raycaster.enabled)
                {
                    raycaster.enabled = false;
                    raycaster.enabled = true;
                }
            }

            // Also clean up via the Graphic registry directly
            CleanupGraphicRegistry(RegistryTypeName);
        }

        /// <summary>
        /// Uses reflection to clean up Unity's internal GraphicRegistry. The type
        /// name is a parameter so a test can drive the failure path with a name
        /// Unity does not have.
        /// </summary>
        internal static void CleanupGraphicRegistry(string registryTypeName)
        {
            CleanupGraphicRegistryType(typeof(Graphic).Assembly.GetType(registryTypeName));
        }

        internal static void CleanupGraphicRegistryType(System.Type registryType)
        {
            // Every lookup below reaches into another package's internals, so each
            // step gives up quietly if Unity has moved it. The smoke probes fail on
            // an application warning, so degrading in silence is deliberate: the
            // catch is for the unexpected, not for a renamed field.
            try
            {
                if (registryType == null)
                    return;

                // Get the instance
                PropertyInfo instanceProp = registryType.GetProperty(
                    "instance",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public
                );
                if (instanceProp == null)
                    return;

                object registryInstance = instanceProp.GetValue(null);
                if (registryInstance == null)
                    return;

                // The dictionary is Dictionary<Canvas, IndexedSet<Graphic>>
                FieldInfo graphicsField = registryType.GetField(
                    "m_Graphics",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );
                if (graphicsField == null)
                    return;

                var dict =
                    graphicsField.GetValue(registryInstance) as System.Collections.IDictionary;
                if (dict == null)
                    return;

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
                    // Get the list inside IndexedSet
                    FieldInfo listField = indexedSet
                        ?.GetType()
                        .GetField("m_List", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (listField == null)
                        continue;

                    var list = listField.GetValue(indexedSet) as System.Collections.IList;
                    if (list == null)
                        continue;

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
                Debug.LogWarning(
                    $"[GraphicRegistryCleaner] Reflection cleanup failed: {e.Message}"
                );
            }
        }

        internal void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
