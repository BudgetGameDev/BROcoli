using System.Collections.Generic;
using BudgetGameDev.Games.Brocoli.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// A compact, layered fire simulation. Authored torch transforms remain the source of the
    /// flame position; presentation is built at runtime so both render pipelines use the same fire.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed partial class TorchFireVfx : MonoBehaviour
    {
        internal const int ParticleBudget = 40;
        private readonly List<Material> ownedMaterials = new();
        private bool initialized;
        private Mesh flameMesh;

        private void Awake() => Initialize();

        internal void Initialize()
        {
            if (initialized)
                return;
            Shader shader = BrocoliShaders.Resolve(BrocoliShaders.TorchFire);
            if (shader == null)
                return;

            // Do not replace unrelated fireballs or emitters. Only a real torch's primary
            // fire material authorizes replacing its legacy particle stack.
            ParticleSystemRenderer primary = null;
            var legacy = GetComponentsInChildren<ParticleSystemRenderer>(true);
            foreach (var renderer in legacy)
                if (HdrTorchFlamePresentation.IsPrimaryFlame(renderer.sharedMaterial))
                    primary = renderer;
            if (primary == null)
                return;

            initialized = true;
            // The imported particle stack has its own offset inside the fuel mesh. Use
            // the torch's authored wick anchor so the blue ignition zone remains visible.
            Transform anchor = transform.Find("Flame");
            Vector3 origin = transform.InverseTransformPoint(
                anchor != null ? anchor.position : primary.transform.position
            );
            foreach (var renderer in legacy)
            {
                renderer
                    .GetComponent<ParticleSystem>()
                    ?.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                renderer.enabled = false;
            }

            // Long-lived sheets stay seated at the wick. The shader supplies the slow
            // convection; particles no longer throw fresh flame silhouettes into the air.
            CreateLayer(
                shader,
                "Fire Core",
                origin,
                0,
                8,
                1.4f,
                2.8f,
                3.2f,
                0.70f,
                0.76f,
                0.86f,
                1.0f,
                0.6f,
                0.7f,
                0.004f,
                new Color(5.2f, 2.4f, 0.45f),
                HdrTorchFlamePresentation.PrimaryMaterialName
            );
            CreateLayer(
                shader,
                "Fire Tongues",
                origin,
                0,
                8,
                0.8f,
                2.2f,
                2.8f,
                0.64f,
                0.72f,
                0.94f,
                1.12f,
                0.16f,
                0.23f,
                0.015f,
                new Color(2f, 0.65f, 0.035f),
                "DungeonTorchFireSecondary"
            );
            CreateLayer(
                shader,
                "Fire Smoke",
                origin + Vector3.up * 0.56f,
                1,
                10,
                0.9f,
                2f,
                2.8f,
                0.22f,
                0.32f,
                0.3f,
                0.42f,
                0.035f,
                0.07f,
                0.16f,
                new Color(0.045f, 0.04f, 0.035f),
                "DungeonTorchSmoke"
            );
            CreateLayer(
                shader,
                "Fire Embers",
                origin,
                2,
                12,
                0f,
                0.8f,
                1.5f,
                0.012f,
                0.022f,
                0.018f,
                0.03f,
                0.4f,
                0.65f,
                0f,
                new Color(5f, 1.2f, 0.06f),
                "DungeonTorchEmbers"
            );
            CreateLayer(
                shader,
                "Fire Heat",
                origin + Vector3.up * 0.18f,
                3,
                2,
                0.7f,
                2f,
                2.4f,
                0.38f,
                0.4f,
                0.48f,
                0.52f,
                0.22f,
                0.28f,
                0.012f,
                Color.white,
                "DungeonTorchHeat"
            );
        }

        private void OnDestroy()
        {
            if (flameMesh != null)
                if (Application.isPlaying)
                    Destroy(flameMesh);
                else
                    DestroyImmediate(flameMesh);
            foreach (Material material in ownedMaterials)
                if (Application.isPlaying)
                    Destroy(material);
                else
                    DestroyImmediate(material);
        }
    }
}
