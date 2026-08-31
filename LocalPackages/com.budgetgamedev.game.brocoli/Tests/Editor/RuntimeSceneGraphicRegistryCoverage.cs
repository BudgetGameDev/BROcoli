using System.Collections;
using BudgetGameDev.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static void ExerciseGraphicRegistryCleaner()
        {
            GameObject canvasObject = new("Coverage Graphic Registry Canvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            GraphicRaycaster raycaster = canvasObject.AddComponent<GraphicRaycaster>();
            GraphicRegistryCleaner.CleanupDestroyedGraphics();
            raycaster.enabled = false;
            GraphicRegistryCleaner.CleanupDestroyedGraphics();

            GameObject cleanerObject = new("Coverage Graphic Cleaner");
            GraphicRegistryCleaner cleaner = cleanerObject.AddComponent<GraphicRegistryCleaner>();
            SetHierarchyField(cleaner, "lastCleanupTime", -10f);
            InvokeHierarchy(cleaner, "Update");
            GameObject duplicateObject = new("Coverage Graphic Cleaner Duplicate");
            GraphicRegistryCleaner duplicate =
                duplicateObject.AddComponent<GraphicRegistryCleaner>();
            InvokeHierarchy(duplicate, "Awake");

            InvokeHierarchy(cleaner, "CleanupGraphicRegistryType", (object)null);
            InvokeHierarchy(cleaner, "CleanupGraphicRegistryType", typeof(NoInstanceRegistry));
            InvokeHierarchy(cleaner, "CleanupGraphicRegistryType", typeof(NullInstanceRegistry));
            InvokeHierarchy(cleaner, "CleanupGraphicRegistryType", typeof(NoDictionaryRegistry));
            InvokeHierarchy(cleaner, "CleanupGraphicRegistryType", typeof(NullDictionaryRegistry));
            InvokeHierarchy(cleaner, "CleanupGraphicRegistryType", typeof(RegistryWithMissingList));
            InvokeHierarchy(cleaner, "CleanupGraphicRegistryType", typeof(RegistryWithNullList));
            InvokeHierarchy(cleaner, "CleanupGraphicRegistryType", typeof(RegistryWithNullGraphic));
            InvokeHierarchy(cleaner, "OnDestroy");
            canvasObject.SetActive(false);
            Object.Destroy(canvasObject);
            Object.Destroy(cleanerObject);
        }

        private sealed class NoInstanceRegistry { }

        private sealed class NullInstanceRegistry
        {
            public static object instance => null;
        }

        private sealed class NoDictionaryRegistry
        {
            public static object instance { get; } = new NoDictionaryRegistry();
        }

        private sealed class NullDictionaryRegistry
        {
            public static NullDictionaryRegistry instance { get; } = new();
            private IDictionary m_Graphics;
        }

        private sealed class RegistryWithMissingList
        {
            public static RegistryWithMissingList instance { get; } = new();
            private IDictionary m_Graphics = RegistryDictionary(new object());
        }

        private sealed class RegistryWithNullList
        {
            public static RegistryWithNullList instance { get; } = new();
            private IDictionary m_Graphics = RegistryDictionary(new NullListSet());
        }

        private sealed class RegistryWithNullGraphic
        {
            public static RegistryWithNullGraphic instance { get; } = new();
            private IDictionary m_Graphics = RegistryDictionary(new NullGraphicSet());
        }

        private sealed class NullListSet
        {
            private IList m_List;
        }

        private sealed class NullGraphicSet
        {
            private IList m_List = new ArrayList { null };
        }

        private static IDictionary RegistryDictionary(object value)
        {
            var canvasObject = new GameObject("Coverage Registry Entry Canvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvasObject.SetActive(false);
            return new Hashtable { [canvas] = value };
        }
    }
}
