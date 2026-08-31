using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static void ExerciseCameraOcclusionFader()
        {
            CameraOcclusionFader fader = Object.FindAnyObjectByType<CameraOcclusionFader>();
            Assert.That(fader, Is.Not.Null);
            SetHierarchyField(fader, "occluderMask", (LayerMask)0);
            InvokeHierarchy(fader, "Awake");
            CameraController cameraController = fader.GetComponent<CameraController>();
            Transform savedTarget = GetHierarchyField<Transform>(fader, "target");
            SetHierarchyField(fader, "target", null);
            if (cameraController != null)
                cameraController.target = savedTarget;
            InvokeHierarchy(fader, "ResolveTarget");
            InvokeHierarchy(fader, "LateUpdate");

            var current = GetHierarchyField<HashSet<Renderer>>(fader, "currentOccluders");
            Shader originalFadeShader = GetHierarchyField<Shader>(fader, "fadeShader");
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
                lit = Shader.Find("Sprites/Default");
            Assert.That(lit, Is.Not.Null);

            GameObject fallbackObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallbackObject.name = "Coverage Fallback Occluder";
            Renderer fallback = fallbackObject.GetComponent<Renderer>();
            Material fallbackMaterial = new(lit) { color = Color.red };
            fallback.sharedMaterials = new[] { fallbackMaterial, null };
            SetHierarchyField(fader, "fadeShader", null);
            current.Add(fallback);
            InvokeHierarchy(fader, "UpdateFades");

            current.Clear();
            SetHierarchyField(fader, "fadeSpeed", 10000f);
            InvokeHierarchy(fader, "UpdateFades");

            GameObject shaderObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shaderObject.name = "Coverage Shader Occluder";
            Renderer shaderRenderer = shaderObject.GetComponent<Renderer>();
            Material shaderMaterial = new(lit) { color = Color.blue };
            shaderRenderer.sharedMaterial = shaderMaterial;
            SetHierarchyField(fader, "fadeShader", originalFadeShader);
            current.Add(shaderRenderer);
            InvokeHierarchy(fader, "UpdateFades");
            InvokeHierarchy(fader, "OnDisable");

            GameObject doomedObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Renderer doomed = doomedObject.GetComponent<Renderer>();
            doomed.sharedMaterial = new Material(lit);
            current.Add(doomed);
            InvokeHierarchy(fader, "UpdateFades");
            Object.Destroy(doomedObject);

            Object.Destroy(fallbackMaterial);
            Object.Destroy(shaderMaterial);
            Object.Destroy(fallbackObject);
            Object.Destroy(shaderObject);
        }
    }
}
