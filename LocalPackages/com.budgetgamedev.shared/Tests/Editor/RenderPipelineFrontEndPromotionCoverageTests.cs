using System;
using System.Reflection;
using BudgetGameDev.Shared.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Shared.Tests
{
    public sealed class RenderPipelineFrontEndPromotionCoverageTests
    {
        [Test]
        public void NullRegistrationsAreIgnoredAndOverridesAreObservable()
        {
            RenderPipelineFrontEnd.Register((IHdrGradeFrontEnd)null);
            RenderPipelineFrontEnd.Register((ILightingFrontEnd)null);
            var grade = new TestGrade(RenderPipelineKind.Universal);
            var lighting = new TestLighting(RenderPipelineKind.Universal);

            RenderPipelineFrontEnd.OverrideForTests(grade);
            RenderPipelineFrontEnd.OverrideForTests(lighting);
            try
            {
                Assert.That(RenderPipelineFrontEnd.HdrGrade, Is.SameAs(grade));
                Assert.That(RenderPipelineFrontEnd.Lighting, Is.SameAs(lighting));
                Assert.That(RenderPipelineFrontEnd.RegisteredSummary, Does.Contain("grade"));
            }
            finally
            {
                RenderPipelineFrontEnd.OverrideForTests((IHdrGradeFrontEnd)null);
                RenderPipelineFrontEnd.OverrideForTests((ILightingFrontEnd)null);
            }
        }

        [Test]
        public void RegistrationsCanBeResolvedForTheActivePipeline()
        {
            RenderPipelineKind active = RenderPipelineProbe.Current;
            if (active == RenderPipelineKind.Unknown)
                active = RenderPipelineKind.Universal;
            var grade = new TestGrade(active);
            var lighting = new TestLighting(active);

            RenderPipelineFrontEnd.Register(grade);
            RenderPipelineFrontEnd.Register(lighting);

            Assert.That(RenderPipelineFrontEnd.RegisteredSummary, Does.Contain(active.ToString()));
        }

        [Test]
        public void DiscoveryHandlesUnknownAndFindsLoadedFrontEnds()
        {
            RenderPipelineFrontEnd.ResetRegistrationsForTests();
            _ = RenderPipelineFrontEnd.HdrGrade;
            _ = RenderPipelineFrontEnd.Lighting;
            MethodInfo discover = typeof(RenderPipelineFrontEnd).GetMethod(
                "Discover",
                BindingFlags.Static | BindingFlags.NonPublic
            );
            MethodInfo gradeDiscovery = discover.MakeGenericMethod(typeof(IHdrGradeFrontEnd));
            var pipelineOf = new Func<IHdrGradeFrontEnd, RenderPipelineKind>(front =>
                front.Pipeline
            );

            Assert.That(
                gradeDiscovery.Invoke(
                    null,
                    new object[] { pipelineOf, RenderPipelineKind.Unknown }
                ),
                Is.Null
            );
            Assert.That(
                gradeDiscovery.Invoke(
                    null,
                    new object[] { pipelineOf, RenderPipelineKind.Universal }
                ),
                Is.Not.Null
            );

            MethodInfo missingDiscovery = discover.MakeGenericMethod(typeof(INeverFrontEnd));
            var missingPipeline = new Func<INeverFrontEnd, RenderPipelineKind>(_ =>
                RenderPipelineKind.Universal
            );
            Assert.That(
                missingDiscovery.Invoke(
                    null,
                    new object[] { missingPipeline, RenderPipelineKind.Universal }
                ),
                Is.Null
            );
        }

        [Test]
        public void PartiallyLoadableAssembliesKeepTheirUsableTypes()
        {
            Type[] types = RenderPipelineFrontEnd.LoadTypes(new PartiallyLoadableAssembly());

            Assert.That(types, Is.EqualTo(new[] { typeof(TestGrade) }));
        }

        private sealed class PartiallyLoadableAssembly : Assembly
        {
            public override Type[] GetTypes() =>
                throw new ReflectionTypeLoadException(
                    new[] { typeof(TestGrade), null },
                    new Exception[] { null, new TypeLoadException("deliberate test gap") }
                );
        }

        private interface INeverFrontEnd { }

        private sealed class NeverFrontEndWithoutDefault : INeverFrontEnd
        {
            public NeverFrontEndWithoutDefault(int value) { }
        }

        private sealed class TestGrade : IHdrGradeFrontEnd
        {
            public TestGrade(RenderPipelineKind pipeline) => Pipeline = pipeline;

            public RenderPipelineKind Pipeline { get; }

            public void Attach(GameObject host) { }

            public void Apply(in HdrGradeRequest request) { }

            public void Detach(
                bool isPlaying,
                Action<UnityEngine.Object> destroyDeferred,
                Action<UnityEngine.Object> destroyImmediate
            ) { }
        }

        private sealed class TestLighting : ILightingFrontEnd
        {
            public TestLighting(RenderPipelineKind pipeline) => Pipeline = pipeline;

            public RenderPipelineKind Pipeline { get; }

            public void ConfigurePunctual(
                Light light,
                in PunctualLightSpec spec,
                float paperWhiteNits
            ) { }
        }
    }
}
