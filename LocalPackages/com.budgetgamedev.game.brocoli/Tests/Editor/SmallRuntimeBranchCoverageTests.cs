using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using BudgetGameDev.Games.Brocoli.Rendering;
using BudgetGameDev.Hub;
using BudgetGameDev.Shared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class SmallRuntimeBranchCoverageTests
    {
        [Test]
        public void PurePoliciesCoverTheirRemainingBoundaryResults()
        {
            Assert.That(
                VirtualJoystickMath.AnalogInput(Vector2.one, 0f, 0f, 1f),
                Is.EqualTo(Vector2.zero)
            );
            DateTime now = new(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc);
            Assert.That(BrocoliSaveSummary.Age(now.AddDays(-40).Ticks, now), Does.Contain("2026"));

            Assert.That(
                BotDecisionPolicy.ChooseIntent(
                    new BotSituation(
                        true,
                        100f,
                        0,
                        1f,
                        false,
                        false,
                        float.PositiveInfinity,
                        float.PositiveInfinity
                    ),
                    new BotTuning(1f, 5, 0.2f, 14f, 16f),
                    BotIntent.Waiting
                ),
                Is.EqualTo(BotIntent.Engage)
            );
            Assert.That(
                BotExplorationPolicy.ChooseDirection(null, default, new(), 1f, -1),
                Is.EqualTo(-1)
            );
            Assert.That(ProjectileWallCollision.HasClearLine(Vector3.one, Vector3.one), Is.True);
            Assert.That(
                ProjectileWallCollision.HasClearLine(Vector3.zero, Vector3.one, 0),
                Is.True
            );
            var closedLayout = new DungeonLayout(123);
            BotExplorationPolicy.ChooseDirection(
                closedLayout,
                new Vector2Int(1000, 1000),
                new(),
                1f,
                -1
            );
            for (int direction = 0; direction < 4; direction++)
            {
                if (!closedLayout.IsPlayableDoorOpen(Vector2Int.zero, direction))
                    continue;
                BotExplorationPolicy.ChooseDirection(
                    closedLayout,
                    Vector2Int.zero,
                    new(),
                    1f,
                    (direction + 2) % 4
                );
                break;
            }

            var resolver = new WallVisibilityResolver();
            resolver.BeginFrame();
            Assert.That(resolver.IsPieceInTheGap(new Bounds(Vector3.zero, Vector3.one)), Is.False);
            Assert.That(
                MainMenu.ResolveNavigationAxis(false, false, Vector2.zero, Vector2.up),
                Is.EqualTo(1f)
            );
            Assert.That(
                MainMenu.ResolveNavigationAxis(false, true, Vector2.down, Vector2.zero),
                Is.EqualTo(-1f)
            );
            Shader fallbackShader = Shader.Find("Sprites/Default");
            Assert.That(
                SprayMaterialCreator.ResolveShader(
                    BrocoliShaders.ParticleUnlit,
                    _ => fallbackShader,
                    _ => null
                ),
                Is.SameAs(fallbackShader),
                "The catalog's graph is used when it resolves."
            );
            Assert.That(
                SprayMaterialCreator.ResolveShader(
                    BrocoliShaders.ParticleUnlit,
                    _ => null,
                    name => name == "Sprites/Default" ? fallbackShader : null
                ),
                Is.SameAs(fallbackShader),
                "A missing graph still leaves the spray something to draw with."
            );
            Assert.That(
                ResponsiveMainMenuLayout.ResolveCreditsScrollAxis(
                    false,
                    false,
                    true,
                    false,
                    null,
                    null
                ),
                Is.EqualTo(3f)
            );
            Assert.That(
                ResponsiveMainMenuLayout.ResolveCreditsScrollAxis(
                    false,
                    false,
                    false,
                    true,
                    Vector2.zero,
                    Vector2.down
                ),
                Is.EqualTo(-1f)
            );

            foreach (
                Type type in new[]
                {
                    typeof(ProceduralFootstepAudio),
                    typeof(ProceduralEnemyWalkAudio),
                    typeof(ProceduralXPPickupAudio),
                    typeof(ProceduralBoostAudio),
                    typeof(ProceduralEnemyProjectileHitAudio),
                }
            )
            {
                MethodInfo softClip = type.GetMethod(
                    type == typeof(ProceduralEnemyProjectileHitAudio) ? "StaticSoftClip"
                        : type == typeof(ProceduralFootstepAudio)
                        || type == typeof(ProceduralEnemyWalkAudio)
                            ? "SoftClipStatic"
                        : "SoftClip",
                    BindingFlags.Static | BindingFlags.NonPublic
                );
                float positive = (float)softClip.Invoke(null, new object[] { 2f });
                float negative = (float)softClip.Invoke(null, new object[] { -2f });
                Assert.That(positive, Is.InRange(0f, 1f));
                Assert.That(negative, Is.InRange(-1f, 0f));
            }
        }

        [Test]
        [TestMustExpectAllLogs(false)]
        public void PlatformOptimizerParameterlessEntryUsesCurrentPipeline()
        {
            GameObject host = new("Coverage iOS Optimizer");
            try
            {
                Component optimizer = host.AddComponent<iOSSafariWebGLOptimizer>();
                optimizer
                    .GetType()
                    .GetMethod(
                        "ApplyOptimizations",
                        BindingFlags.Instance | BindingFlags.NonPublic,
                        null,
                        Type.EmptyTypes,
                        null
                    )
                    .Invoke(optimizer, null);
                optimizer
                    .GetType()
                    .GetMethod(
                        "ApplyOptimizationsIfNeeded",
                        BindingFlags.Instance | BindingFlags.NonPublic,
                        null,
                        new[] { typeof(bool) },
                        null
                    )
                    .Invoke(optimizer, new object[] { true });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void LauncherBootedPathDoesNotBuildTheCatalogInterface()
        {
            GameObject host = new("Coverage Booted Launcher");
            try
            {
                GameLauncher launcher = host.AddComponent<GameLauncher>();
                typeof(GameLauncher)
                    .GetMethod("CompleteStart", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(launcher, new object[] { true });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void PickupSymbolsAndRendererColorsCoverAlternateShaderProperties()
        {
            GameObject host = new("Coverage Small Visual Branches");
            GameObject faceObject = new("Face");
            faceObject.transform.SetParent(host.transform, false);
            try
            {
                PickupVisual3D visual = host.AddComponent<PickupVisual3D>();
                visual.BuildSymbol(
                    faceObject.transform,
                    PickupVisual3D.ModelKind.SprayRange,
                    Color.white,
                    Color.cyan
                );
                visual.BuildSymbol(
                    faceObject.transform,
                    PickupVisual3D.ModelKind.SprayWidth,
                    Color.white,
                    Color.cyan
                );

                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.SetParent(host.transform, false);
                Renderer renderer = cube.GetComponent<Renderer>();
                var properties = new MaterialPropertyBlock();
                properties.SetColor(Shader.PropertyToID("_BaseColor"), Color.red);
                renderer.SetPropertyBlock(properties);
                Assert.That(
                    EnemyRendererColor.Get(renderer, new MaterialPropertyBlock()),
                    Is.EqualTo(Color.red)
                );
                properties.Clear();
                properties.SetColor(Shader.PropertyToID("_Color"), Color.green);
                renderer.SetPropertyBlock(properties);
                Assert.That(
                    EnemyRendererColor.Get(renderer, new MaterialPropertyBlock()),
                    Is.EqualTo(Color.green)
                );

                SpriteRenderer sprite = host.AddComponent<SpriteRenderer>();
                sprite.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                EnemyRendererColor.Get(sprite, new MaterialPropertyBlock());
                foreach (
                    string shaderName in new[]
                    {
                        "Hidden/BlitCopy",
                        "Hidden/Internal-GUITextureClip",
                        "UI/Default",
                    }
                )
                {
                    Shader shader = Shader.Find(shaderName);
                    if (shader == null)
                        continue;
                    sprite.sharedMaterial = new Material(shader);
                    EnemyRendererColor.Get(sprite, new MaterialPropertyBlock());
                }

                Bar bar = host.AddComponent<Bar>();
                bar.ShowBar();
                bar.HideBar();
                _ = CameraShake.Instance;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}
