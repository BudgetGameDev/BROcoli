using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace BudgetGameDev.Shared
{
    /// <summary>
    /// Removes destroyed Graphics from Unity's registry without cycling live UI components.
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
                // A cleaner is hosted on the pause controller in BROcoli. Removing
                // that whole GameObject here can strand the dimmer and make the UI
                // appear black when two cleaners overlap during a scene transition.
                if (Application.isPlaying)
                    Destroy(this);
                else
                    DestroyImmediate(this);
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

                PruneDictionary(registryType, registryInstance, "m_Graphics");
                PruneDictionary(registryType, registryInstance, "m_RaycastableGraphics");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning(
                    $"[GraphicRegistryCleaner] Reflection cleanup failed: {e.Message}"
                );
            }
        }

        private static void PruneDictionary(
            System.Type registryType,
            object registryInstance,
            string fieldName
        )
        {
            FieldInfo graphicsField = registryType.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            if (graphicsField?.GetValue(registryInstance) is not IDictionary dictionary)
                return;

            var emptyCanvases = new List<object>();
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Value == null)
                    continue;

                FieldInfo listField = entry.Value
                    .GetType()
                    .GetField("m_List", BindingFlags.Instance | BindingFlags.NonPublic);
                if (listField?.GetValue(entry.Value) is not IList list)
                    continue;

                MethodInfo remove = entry.Value.GetType().GetMethod(
                    "Remove",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                );
                if (remove == null)
                    continue;

                var destroyed = new List<object>();
                foreach (object item in list)
                {
                    if (item is Graphic graphic && graphic == null)
                        destroyed.Add(item);
                }
                foreach (object item in destroyed)
                    remove.Invoke(entry.Value, new[] { item });

                if (list.Count == 0 || entry.Key is Canvas canvas && canvas == null)
                    emptyCanvases.Add(entry.Key);
            }
            foreach (object canvas in emptyCanvases)
                dictionary.Remove(canvas);
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
