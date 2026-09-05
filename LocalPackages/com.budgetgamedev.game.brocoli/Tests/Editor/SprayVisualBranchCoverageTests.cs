using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class SprayVisualBranchCoverageTests
    {
        private const BindingFlags Hidden =
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;

        [Test]
        public void HandVisualCoversAlternateHierarchyAndFallbackProperties()
        {
            GameObject player = new("Coverage Spray Player");
            GameObject spray = new("Coverage Spray Root");
            GameObject sprite = new("Legacy Sprite");
            spray.transform.SetParent(player.transform, false);
            sprite.transform.SetParent(spray.transform, false);
            SpriteRenderer renderer = sprite.AddComponent<SpriteRenderer>();
            try
            {
                var visuals = new SprayHandVisuals(spray.transform);
                visuals.CreateHandVisuals();
                Assert.That(renderer.enabled, Is.False);
                Assert.That(visuals.HandTransform, Is.Not.Null);
                Assert.That(visuals.HasHand, Is.True);
                Set(visuals, "hasPreviousPlayerPosition", false);
                Assert.That(visuals.ResolveMovementAmount(1f), Is.Zero);

                GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
                target.transform.position = Vector3.right;
                visuals.SetTarget(target.transform, Vector2.up);
                Assert.That(visuals.HasTarget, Is.True);
                _ = visuals.IsTargetInRange;
                visuals.ClearTarget();
                Invoke(visuals, "GetTargetCenter");

                Set(visuals, "weaponVisual", null);
                _ = visuals.GetNozzleWorldPosition();
                Set(visuals, "playerTransform", null);
                _ = visuals.IsTargetInRange;
                _ = visuals.GetNozzleWorldPosition();
                visuals.SetVisible(false);

                // The hand angle wraps at both ends. Leaving this to whichever way a
                // playtest bot happened to walk makes the coverage of two real lines
                // depend on the weather, so drive them here.
                Set(visuals, "currentHandAngle", 200f);
                visuals.Update();
                Assert.That(Get<float>(visuals, "currentHandAngle"), Is.LessThanOrEqualTo(180f));
                Set(visuals, "currentHandAngle", -200f);
                visuals.Update();
                Assert.That(
                    Get<float>(visuals, "currentHandAngle"),
                    Is.GreaterThanOrEqualTo(-180f)
                );

                UnityEngine.Object.DestroyImmediate(target);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        [Test]
        [TestMustExpectAllLogs(false)]
        public void WeaponVisualCoversExistingRootAndImportedComponentPreparation()
        {
            GameObject host = new("Coverage Spray Weapon");
            GameObject existing = new("SanitizerModel3D");
            existing.transform.SetParent(host.transform, false);
            try
            {
                SprayWeaponVisual3D weapon = SprayWeaponVisual3D.Attach(host);
                Assert.That(weapon.NozzleTransform, Is.Not.Null);
                weapon.SetPresentation(Vector3.one);
                LogAssert.Expect(
                    LogType.Error,
                    "Missing sanitizer bottle resource: Brocoli/ThirdParty/SprayBottle/SprayBottle"
                );
                weapon.BuildBottle(null);
                LogAssert.Expect(
                    LogType.Error,
                    "Missing licensed hand resource: Brocoli/Generated/Licensed/theHand"
                );
                weapon.BuildHand(null, null);

                GameObject imported = GameObject.CreatePrimitive(PrimitiveType.Cube);
                imported.transform.SetParent(existing.transform, false);
                imported.AddComponent<Animator>();
                imported.AddComponent<Animation>();
                InvokeStatic(typeof(SprayWeaponVisual3D), "PrepareImportedModel", imported);
                Assert.That(imported.GetComponent<Collider>().enabled, Is.False);
                GameObject handPrefab = new("Coverage Hand Prefab");
                LogAssert.Expect(
                    LogType.Warning,
                    "Licensed hand is missing its GrabHold animation clip."
                );
                weapon.BuildHand(handPrefab, null);
                UnityEngine.Object.DestroyImmediate(handPrefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ParticleHelpersCoverNullFallbacksAndLicensedShaderProperties()
        {
            GameObject host = new("Coverage spray particle remainder");
            Texture2D texture = new(2, 2);
            Material material = null;
            Material tintMaterial = null;
            try
            {
                var layers = new SprayParticleLayers(host.transform);
                layers.PlayBurst(10);
                layers.SetDirectionAndPosition(Vector2.right, Vector3.one);
                layers.UpdateForStats(5f, 30f);
                Assert.That(layers.GetParticleSpeed(), Is.GreaterThan(0f));

                var splash = new SprayHitSplash(host.transform);
                splash.Emit(null, 0);

                ParticleSystem particles = host.AddComponent<ParticleSystem>();
                var main = particles.main;
                main.startSpeed = 7f;
                var controller = new SprayParticleController(host.transform);
                controller.SetParticleSystem(particles);
                Assert.That(controller.GetParticleSpeed(), Is.EqualTo(7f));

                Material template = Resources.Load<Material>(
                    "Brocoli/Integration/LicensedWaterSpray"
                );
                Assert.That(template, Is.Not.Null);
                material = new Material(template);
                SprayLayerFactory.SetupBillboardRenderer(particles, texture, material);
                InvokeStatic(
                    typeof(SprayMaterialCreator),
                    "ConfigureParticleBlending",
                    material,
                    SprayMaterialCreator.BlendMode.Multiply
                );
                InvokeStatic(
                    typeof(SprayMaterialCreator),
                    "SetMaterialColor",
                    material,
                    Color.cyan
                );
                tintMaterial = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
                InvokeStatic(
                    typeof(SprayMaterialCreator),
                    "SetMaterialColor",
                    tintMaterial,
                    Color.magenta
                );
                Material coverageMaterial = new Material(
                    Shader.Find("Hidden/Brocoli/CoverageProperties")
                );
                SprayLayerFactory.SetupBillboardRenderer(particles, texture, coverageMaterial);
                InvokeStatic(
                    typeof(SprayMaterialCreator),
                    "ConfigureParticleBlending",
                    coverageMaterial,
                    SprayMaterialCreator.BlendMode.Alpha
                );
                InvokeStatic(
                    typeof(SprayMaterialCreator),
                    "EnableSoftParticles",
                    coverageMaterial,
                    0.3f
                );
                SprayMaterialCreator.ConfigureDropletSurface(coverageMaterial);
                UnityEngine.Object.DestroyImmediate(coverageMaterial);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tintMaterial);
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static object Invoke(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, Hidden).Invoke(target, args);

        private static object InvokeStatic(Type type, string name, params object[] args) =>
            type.GetMethod(name, Hidden).Invoke(null, args);

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, Hidden).SetValue(target, value);

        private static T Get<T>(object target, string name) =>
            (T)target.GetType().GetField(name, Hidden).GetValue(target);
    }
}
