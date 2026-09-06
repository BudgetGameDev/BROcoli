using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class DungeonOccluderCoverageTests
    {
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

        [Test]
        public void SectionBuildsAndFiltersEveryFadeCandidateShape()
        {
            GameObject root = new("Coverage occlusion section");
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject gateway = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject excluded = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material material = new(Shader.Find("Sprites/Default"));
            try
            {
                DungeonOcclusionSection section = root.AddComponent<DungeonOcclusionSection>();
                wall.transform.SetParent(root.transform);
                wall.transform.position = new Vector3(0f, 1f, 0f);
                wall.transform.localScale = new Vector3(2f, 2f, 1f);
                wall.GetComponent<Renderer>().sharedMaterial = material;
                gateway.transform.SetParent(root.transform);
                gateway.transform.position = new Vector3(0f, 1f, 0.1f);
                gateway.GetComponent<Renderer>().sharedMaterial = material;
                excluded.transform.SetParent(root.transform);
                excluded.transform.position = new Vector3(0f, 1f, 0.2f);
                excluded.GetComponent<Renderer>().sharedMaterial = material;
                excluded.AddComponent<SphereCollider>();
                excluded.AddComponent<CapsuleCollider>().isTrigger = true;

                section.Exclude(excluded.transform);
                section.ConfigureGateway(gateway.transform);
                Assert.That(
                    section.TryGetFadeReference(
                        gateway.GetComponent<Renderer>(),
                        out _,
                        out float height
                    ),
                    Is.True
                );
                Assert.That(height, Is.GreaterThan(0f));

                OcclusionCameraModel camera = OcclusionCameraModel.Perspective(
                    new Vector3(0f, 1f, -10f),
                    Quaternion.identity,
                    60f,
                    1f,
                    0.1f,
                    100f
                );
                var resolver = new WallVisibilityResolver();
                resolver.BeginFrame();
                resolver.AddTarget(
                    new OcclusionTarget(
                        OcclusionTargetKind.Player,
                        new Vector3(0f, 1f, 10f),
                        new Bounds(new Vector3(0f, 1f, 10f), Vector3.one),
                        new Rect(0f, 0f, 1f, 1f),
                        0f
                    )
                );
                resolver.Resolve(camera, new EmptyWorld(), 1f);

                var results = new HashSet<Renderer>();
                var buffer = new List<Renderer> { excluded.GetComponent<Renderer>() };
                section.CollectFadeRenderers(resolver, new Plane[0], results, buffer);
                Assert.That(results.Contains(wall.GetComponent<Renderer>()), Is.True);
                Assert.That(results.Contains(gateway.GetComponent<Renderer>()), Is.True);
                Assert.That(results.Contains(excluded.GetComponent<Renderer>()), Is.False);

                wall.GetComponent<Renderer>().enabled = false;
                results.Clear();
                section.CollectFadeRenderers(resolver, new Plane[0], results, buffer);
                Assert.That(results.Contains(wall.GetComponent<Renderer>()), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void RegistryPrunesDestroyedEntries()
        {
            GameObject host = new("Coverage stale occluder");
            DungeonOccluder occluder = host.AddComponent<DungeonOccluder>();
            EntityId id = occluder.GroupId;
            Object.DestroyImmediate(host);

            FieldInfo registryField = typeof(DungeonOccluder).GetField(
                "Registry",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            var registry = (Dictionary<EntityId, DungeonOccluder>)registryField.GetValue(null);
            registry[id] = occluder;

            Assert.That(DungeonOccluder.ForGroup(id), Is.Null);
            Assert.That(registry.ContainsKey(id), Is.False);
        }
    }
}
