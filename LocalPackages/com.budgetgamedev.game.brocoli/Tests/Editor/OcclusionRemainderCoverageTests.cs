using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class OcclusionRemainderCoverageTests
    {
        private const BindingFlags Hidden =
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;

        [Test]
        public void FaderUsesPlayerFallbackBoundsAndColorOnlyMaterials()
        {
            GameObject cameraObject = new(
                "Coverage fader camera",
                typeof(Camera),
                typeof(CameraOcclusionFader)
            );
            GameObject player = new("Coverage fader player");
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material colorMaterial = new(Shader.Find("Sprites/Default"));
            try
            {
                CameraOcclusionFader fader = cameraObject.GetComponent<CameraOcclusionFader>();
                Set(fader, "target", player.transform);
                object[] boundsArguments = { player.transform, default(Bounds) };
                Assert.That((bool)Invoke(fader, "TryGetTargetBounds", boundsArguments), Is.True);
                Assert.That(((Bounds)boundsArguments[1]).size.y, Is.GreaterThan(0f));

                cube.GetComponent<Renderer>().sharedMaterial = colorMaterial;
                Type stateType = typeof(CameraOcclusionFader).GetNestedType(
                    "FadeState",
                    BindingFlags.NonPublic
                );
                object state = Activator.CreateInstance(
                    stateType,
                    Hidden | BindingFlags.Public,
                    null,
                    new object[] { cube.GetComponent<Renderer>(), null, 0.1f, 0.2f, 2f },
                    null
                );
                stateType.GetField("Visibility").SetValue(state, 0.5f);
                InvokeStatic(typeof(CameraOcclusionFader), "ApplyVisibility", state);

                player.tag = "Player";
                Set(fader, "target", null);
                Invoke(fader, "ResolveTarget");
                Assert.That(Get<Transform>(fader, "target"), Is.SameAs(player.transform));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(colorMaterial);
                UnityEngine.Object.DestroyImmediate(cube);
                UnityEngine.Object.DestroyImmediate(player);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void VolumeQueriesCoverInactiveAndOwnedCandidates()
        {
            GameObject cameraObject = new(
                "Coverage volume camera",
                typeof(Camera),
                typeof(CameraOcclusionFader)
            );
            GameObject ownerObject = new("Coverage volume owner", typeof(DungeonOccluder));
            GameObject volumeObject = new("Coverage active volume", typeof(DungeonOcclusionVolume));
            GameObject inactiveObject = new(
                "Coverage inactive volume",
                typeof(DungeonOcclusionVolume)
            );
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                cameraObject.transform.position = Vector3.zero;
                cameraObject.transform.rotation = Quaternion.identity;
                volumeObject.transform.SetParent(ownerObject.transform, false);
                volumeObject.transform.position = Vector3.forward * 5f;
                volumeObject
                    .GetComponent<DungeonOcclusionVolume>()
                    .Configure(Vector3.zero, Vector3.one * 2f);

                CameraOcclusionFader fader = cameraObject.GetComponent<CameraOcclusionFader>();
                Set(fader, "gameplayCamera", camera);
                Plane[] planes = Get<Plane[]>(fader, "frustumPlanes");
                for (int index = 0; index < planes.Length; index++)
                    planes[index] = new Plane(Vector3.forward, 100f);

                inactiveObject.SetActive(false);
                var activeSet =
                    (HashSet<DungeonOcclusionVolume>)
                        typeof(DungeonOcclusionVolume)
                            .GetField("ActiveSet", BindingFlags.Static | BindingFlags.NonPublic)
                            .GetValue(null);
                activeSet.Add(volumeObject.GetComponent<DungeonOcclusionVolume>());
                activeSet.Add(inactiveObject.GetComponent<DungeonOcclusionVolume>());

                var rayResults = new List<OcclusionCandidate>();
                Ray ray = new(Vector3.zero, Vector3.forward);
                Bounds bounds = volumeObject.GetComponent<DungeonOcclusionVolume>().WorldBounds;
                Assert.That((bool)Invoke(fader, "IsVisibleGeometry", 0, bounds), Is.True);
                Assert.That(bounds.IntersectRay(ray, out float distance), Is.True);
                Assert.That(distance, Is.LessThanOrEqualTo(10f));
                Invoke(fader, "CollectAlongRay", ray, 10f, rayResults);
                Assert.That(rayResults, Is.Not.Empty);
                var enclosing = new List<OcclusionCandidate>();
                Invoke(
                    fader,
                    "CollectEnclosingVolumes",
                    volumeObject.transform.position,
                    enclosing
                );
                Assert.That(enclosing, Is.Not.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(inactiveObject);
                UnityEngine.Object.DestroyImmediate(volumeObject);
                UnityEngine.Object.DestroyImmediate(ownerObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void ProjectionOwnershipAndBehindTargetCoverFalseAndTruePolicies()
        {
            OcclusionCameraModel camera = OcclusionCameraModel.Perspective(
                Vector3.zero,
                Quaternion.identity,
                60f,
                1f,
                0.1f,
                100f
            );
            Assert.That(
                OcclusionTarget.TryCreate(
                    camera,
                    OcclusionTargetKind.Player,
                    Vector3.back,
                    new Bounds(Vector3.back, Vector3.one),
                    0.5f,
                    out _
                ),
                Is.False
            );

            GameObject sectionObject = new(
                "Coverage unowned section",
                typeof(DungeonOcclusionSection)
            );
            GameObject excluded = GameObject.CreatePrimitive(PrimitiveType.Cube);
            excluded.transform.SetParent(sectionObject.transform, false);
            try
            {
                DungeonOcclusionSection section =
                    sectionObject.GetComponent<DungeonOcclusionSection>();
                section.Exclude(excluded.transform);
                Assert.That(DungeonOccluder.Owning(excluded.GetComponent<Collider>()), Is.Null);
                _ = section.BelongsToRoom(Vector2Int.zero, new DungeonLayout(1));

                var resolver = new WallVisibilityResolver();
                resolver.BeginFrame();
                resolver.AddTarget(
                    new OcclusionTarget(
                        OcclusionTargetKind.Player,
                        Vector3.forward * 10f,
                        new Bounds(Vector3.forward * 10f, Vector3.one),
                        new Rect(0f, 0f, 1f, 1f),
                        0f
                    )
                );
                resolver.Resolve(camera, new EmptyWorld(), 0f);
                Assert.That(
                    (bool)Invoke(
                        resolver,
                        "AnyTargetIsBehind",
                        new Bounds(Vector3.forward * 5f, Vector3.one)
                    ),
                    Is.True
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sectionObject);
            }
        }

        private sealed class EmptyWorld : IOcclusionCandidateSource
        {
            public void Collect(
                Ray ray,
                float maximumDistance,
                List<OcclusionCandidate> results
            ) { }

            public void CollectEnclosing(
                Vector3 targetPosition,
                List<OcclusionCandidate> results
            ) { }
        }

        private static object Invoke(object target, string name, params object[] arguments)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
                foreach (MethodInfo method in type.GetMethods(Hidden))
                    if (method.Name == name && method.GetParameters().Length == arguments.Length)
                        return method.Invoke(target, arguments);
            throw new MissingMethodException(target.GetType().Name, name);
        }

        private static object InvokeStatic(Type type, string name, params object[] arguments)
        {
            foreach (MethodInfo method in type.GetMethods(Hidden))
                if (method.Name == name && method.GetParameters().Length == arguments.Length)
                    return method.Invoke(null, arguments);
            throw new MissingMethodException(type.Name, name);
        }

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, Hidden).SetValue(target, value);

        private static T Get<T>(object target, string name) =>
            (T)target.GetType().GetField(name, Hidden).GetValue(target);
    }
}
