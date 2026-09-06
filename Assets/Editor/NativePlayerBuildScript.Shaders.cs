#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class NativePlayerBuildScript
{
    private static Shader[] authoredAlwaysIncluded;

    private static SerializedObject GraphicsObject() =>
        new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]
        );

    private static void ConfigureAlwaysIncluded(RenderPipelineAsset pipeline)
    {
        var settings = GraphicsObject();
        var shaders = settings.FindProperty("m_AlwaysIncludedShaders");
        authoredAlwaysIncluded ??= Enumerable
            .Range(0, shaders.arraySize)
            .Select(index => shaders.GetArrayElementAtIndex(index).objectReferenceValue as Shader)
            .ToArray();
        // URP's Unlit has no HDRP subshader; always-including it bypasses normal
        // usage selection and leaves a stripped FallbackError in the HDRP build.
        var selected = authoredAlwaysIncluded
            .Where(shader =>
                pipeline.GetType().Name != "HDRenderPipelineAsset"
                || shader == null
                || !shader.name.StartsWith(
                    "Universal Render Pipeline/",
                    System.StringComparison.Ordinal
                )
            )
            .ToArray();
        shaders.arraySize = selected.Length;
        for (int i = 0; i < selected.Length; i++)
            shaders.GetArrayElementAtIndex(i).objectReferenceValue = selected[i];
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RestoreAlwaysIncluded()
    {
        if (authoredAlwaysIncluded == null)
            return;
        var settings = GraphicsObject();
        var shaders = settings.FindProperty("m_AlwaysIncludedShaders");
        shaders.arraySize = authoredAlwaysIncluded.Length;
        for (int i = 0; i < authoredAlwaysIncluded.Length; i++)
            shaders.GetArrayElementAtIndex(i).objectReferenceValue = authoredAlwaysIncluded[i];
        settings.ApplyModifiedPropertiesWithoutUndo();
        authoredAlwaysIncluded = null;
    }
}
#endif
