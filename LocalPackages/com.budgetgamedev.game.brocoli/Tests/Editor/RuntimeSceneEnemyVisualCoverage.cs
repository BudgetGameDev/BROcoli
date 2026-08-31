using UnityEngine;
using UnityEngine.UI;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static void ExerciseEnemyVisualEffects()
        {
            ExerciseEliteEffects();
            ExerciseWalkAnimation();
        }

        private static void ExerciseEliteEffects()
        {
            GameObject root = new("Coverage Elite Effects");
            GameObject inactive = new("Inactive Sprite");
            inactive.transform.SetParent(root.transform, false);
            inactive.AddComponent<SpriteRenderer>().enabled = false;

            GameObject spriteObject = new("Active Sprite");
            spriteObject.transform.SetParent(root.transform, false);
            SpriteRenderer spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
            Texture2D texture = new(2, 2);
            spriteRenderer.sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 2f, 2f),
                new Vector2(0.5f, 0.5f)
            );
            spriteRenderer.color = Color.cyan;

            EliteEnemyEffects effects = root.AddComponent<EliteEnemyEffects>();
            effects.ApplyEliteVisuals();
            effects.ApplyEliteVisuals();
            effects.RemoveEliteVisuals();

            spriteRenderer.enabled = false;
            GameObject meshObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            meshObject.transform.SetParent(root.transform, false);
            MeshRenderer mesh = meshObject.GetComponent<MeshRenderer>();
            var properties = new MaterialPropertyBlock();
            properties.SetColor(Shader.PropertyToID("_BaseColor"), Color.magenta);
            mesh.SetPropertyBlock(properties);
            effects.ApplyEliteVisuals();
            mesh.SetPropertyBlock(null);
            effects.ApplyEliteVisuals();
            Object.Destroy(mesh);
            effects.RemoveEliteVisuals();
            InvokeHierarchy(effects, "OnDestroy");
            Object.Destroy(root);
            Object.Destroy(texture);
        }

        private static void ExerciseWalkAnimation()
        {
            GameObject root = new("Coverage Walk Parent");
            Rigidbody body = root.AddComponent<Rigidbody>();
            GameObject animated = new("Animated Enemy");
            animated.transform.SetParent(root.transform, false);

            GameObject canvasObject = new("Ignored Canvas");
            canvasObject.transform.SetParent(animated.transform, false);
            canvasObject.AddComponent<Canvas>();
            GameObject visual = new("Walk Visual");
            visual.transform.SetParent(animated.transform, false);
            visual.transform.localScale = Vector3.zero;
            visual.transform.localPosition = Vector3.zero;

            EnemyWalkAnimation animation = animated.AddComponent<EnemyWalkAnimation>();
            InvokeHierarchy(animation, "Start");
            body.linearVelocity = new Vector3(3f, 0f, 0f);
            InvokeHierarchy(animation, "Update");
            body.linearVelocity = Vector3.zero;
            InvokeHierarchy(animation, "Update");
            animation.SetAttackOverride(true);
            InvokeHierarchy(animation, "Update");
            animation.SetAttackOverride(false);
            InvokeHierarchy(animation, "OnDisable");

            SetHierarchyField(animation, "isInitialized", false);
            animation.SetAttackOverride(false);
            SetHierarchyField(animation, "visualTransform", null);
            InvokeHierarchy(animation, "Update");
            animation.SetAttackOverride(false);

            GameObject selfAnimated = new("Self Animated Enemy");
            EnemyWalkAnimation selfAnimation = selfAnimated.AddComponent<EnemyWalkAnimation>();
            SetHierarchyField(selfAnimation, "isInitialized", false);
            InvokeHierarchy(selfAnimation, "Start");
            InvokeHierarchy(selfAnimation, "Update");
            InvokeHierarchy(selfAnimation, "OnDisable");
            Object.Destroy(root);
            Object.Destroy(selfAnimated);
        }
    }
}
