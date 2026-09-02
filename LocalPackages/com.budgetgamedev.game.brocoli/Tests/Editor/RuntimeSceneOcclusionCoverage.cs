using System.Collections.Generic;
using BudgetGameDev.Games.Brocoli.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        /// <summary>
        /// Drives a fader against a wall it has to lower. The bot walk used to
        /// reach the activation and lowering branches by chance, which made this
        /// coverage depend on where the dungeon happened to send it, so the
        /// scenario is built explicitly here instead.
        /// </summary>
        /// <remarks>
        /// Runs after <see cref="ExerciseCameraOcclusionFader"/> and tears its rig
        /// down at once, because that one reaches for the fader by type: a second
        /// one left in the scene would be exercised in the scene camera's place,
        /// and it carries neither the CameraController sibling nor the frames of
        /// life that the rest of the fader's paths need.
        /// </remarks>
        private static void ExerciseOcclusionActivation()
        {
            // Far from the generated dungeon, so only the geometry below is in
            // shot. Both objects still land in one room cell, which is what
            // BelongsToPlayerRoom asks about.
            Vector3 origin = new(0f, 0f, 4000f);
            GameObject playerObject = new("Coverage Occlusion Player");
            playerObject.transform.position = origin;

            Vector3 cameraOffset = new(0f, 10.5f, -11.7f);
            GameObject cameraObject = new(
                "Coverage Occlusion Camera",
                typeof(Camera),
                typeof(CameraOcclusionFader)
            );
            cameraObject.transform.SetPositionAndRotation(
                origin + cameraOffset,
                Quaternion.LookRotation(-cameraOffset.normalized, Vector3.up)
            );
            Camera occlusionCamera = cameraObject.GetComponent<Camera>();
            occlusionCamera.fieldOfView = 35f;
            occlusionCamera.nearClipPlane = 0.3f;
            occlusionCamera.farClipPlane = 1000f;
            // Nothing should render from this rig; it only supplies the model.
            occlusionCamera.enabled = false;

            CameraOcclusionFader fader = cameraObject.GetComponent<CameraOcclusionFader>();
            InvokeHierarchy(fader, "Awake");
            SetHierarchyField(fader, "target", playerObject.transform);

            // Wide and tall enough to block every sight line to the player, which
            // is what clears the 0.8 coverage a player target demands.
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Coverage Occluding Wall";
            wall.layer = LayerMask.NameToLayer("Wall");
            wall.transform.position = origin + new Vector3(0f, 3f, -3f);
            wall.transform.localScale = new Vector3(12f, 6f, 1f);
            wall.AddComponent<DungeonOccluder>();
            Physics.SyncTransforms();

            InvokeHierarchy(fader, "LateUpdate");

            var resolver = GetHierarchyField<WallVisibilityResolver>(fader, "resolver");
            Assert.That(resolver.Activations, Is.Not.Empty);
            Assert.That(resolver.LoweredGroups, Is.Not.Empty);
            Assert.That(fader.MaximumDetectedCoverage, Is.GreaterThan(0f));
            Assert.That(fader.QualifyingGroupCount, Is.GreaterThan(0));

            // The wall renderer sits under the occluder, so fading it reads the
            // occluder's fade reference rather than the plain-renderer fallback.
            var collected = GetHierarchyField<HashSet<Renderer>>(fader, "currentOccluders");
            Assert.That(collected, Is.Not.Empty);
            InvokeHierarchy(fader, "LateUpdate");

            InvokeHierarchy(fader, "OnDisable");
            Object.DestroyImmediate(wall);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(playerObject);
        }

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
            Shader lit = BrocoliShaders.Resolve(BrocoliShaders.Surface);
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
