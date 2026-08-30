using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Handles elite enemy visual effects (glow, tint, scale).
    /// Separated from EnemyBase to keep files under 300 LOC.
    /// </summary>
    public class EliteEnemyEffects : MonoBehaviour
    {
        private GameObject glowEffect;
        private SpriteRenderer mainSpriteRenderer;
        private Color originalColor;
        private readonly List<RendererState> rendererStates = new();

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private sealed class RendererState
        {
            public Renderer Renderer;
            public MaterialPropertyBlock Properties;
        }

        [Header("Elite Visual Settings")]
        public Color glowColor = new Color(1f, 0.85f, 0.2f, 0.4f);
        public Color tintColor = new Color(1f, 0.9f, 0.5f, 1f);
        public float glowScale = 1.3f;

        public void ApplyEliteVisuals()
        {
            RemoveEliteVisuals();

            // Disabled SpriteRenderers on the enemy prefabs are legacy 2D artwork.
            // GetComponentInChildren<SpriteRenderer>() still returns disabled
            // renderers, so the old implementation cloned that sprite into a new,
            // enabled EliteGlow object. Only consider a sprite that is actually in
            // use; otherwise apply the elite tint to the current 3D model.
            foreach (var spriteRenderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (
                    !spriteRenderer.enabled
                    || !spriteRenderer.gameObject.activeInHierarchy
                    || spriteRenderer.sprite == null
                )
                {
                    continue;
                }

                mainSpriteRenderer = spriteRenderer;
                break;
            }

            if (mainSpriteRenderer != null)
            {
                originalColor = mainSpriteRenderer.color;
                CreateGlowEffect();
                mainSpriteRenderer.color = tintColor;
                return;
            }

            ApplyMeshTint();
        }

        private void CreateGlowEffect()
        {
            glowEffect = new GameObject("EliteGlow");
            glowEffect.transform.SetParent(transform, false);
            glowEffect.transform.localPosition = Vector3.zero;

            SpriteRenderer glowSr = glowEffect.AddComponent<SpriteRenderer>();
            glowSr.sprite = mainSpriteRenderer.sprite;
            glowSr.sortingOrder = mainSpriteRenderer.sortingOrder - 1;
            glowSr.color = glowColor;
            glowEffect.transform.localScale = Vector3.one * glowScale;
        }

        private void ApplyMeshTint()
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (
                    !renderer.enabled
                    || !renderer.gameObject.activeInHierarchy
                    || renderer is SpriteRenderer
                )
                {
                    continue;
                }

                var originalProperties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(originalProperties);
                rendererStates.Add(
                    new RendererState { Renderer = renderer, Properties = originalProperties }
                );

                var eliteProperties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(eliteProperties);
                SetTintedColor(renderer, originalProperties, eliteProperties, BaseColorId);
                SetTintedColor(renderer, originalProperties, eliteProperties, ColorId);
                renderer.SetPropertyBlock(eliteProperties);
            }
        }

        private void SetTintedColor(
            Renderer renderer,
            MaterialPropertyBlock originalProperties,
            MaterialPropertyBlock eliteProperties,
            int propertyId
        )
        {
            Color sourceColor;

            if (originalProperties.HasColor(propertyId))
            {
                sourceColor = originalProperties.GetColor(propertyId);
            }
            else if (
                renderer.sharedMaterial != null
                && renderer.sharedMaterial.HasProperty(propertyId)
            )
            {
                sourceColor = renderer.sharedMaterial.GetColor(propertyId);
            }
            else
            {
                return;
            }

            eliteProperties.SetColor(propertyId, sourceColor * tintColor);
        }

        public void RemoveEliteVisuals()
        {
            if (glowEffect != null)
            {
                Destroy(glowEffect);
                glowEffect = null;
            }

            if (mainSpriteRenderer != null)
            {
                mainSpriteRenderer.color = originalColor;
                mainSpriteRenderer = null;
            }

            foreach (var state in rendererStates)
            {
                if (state.Renderer != null)
                {
                    state.Renderer.SetPropertyBlock(state.Properties);
                }
            }

            rendererStates.Clear();
        }

        void OnDestroy()
        {
            RemoveEliteVisuals();
        }
    }
}
