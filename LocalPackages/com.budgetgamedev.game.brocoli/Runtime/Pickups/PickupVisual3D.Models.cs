using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BudgetGameDev.Games.Brocoli
{
    public sealed partial class PickupVisual3D
    {
        private void BuildExperienceCrystal()
        {
            Color deepBlue = new Color(0.02f, 0.22f, 0.58f);
            Color electricBlue = new Color(0.02f, 0.72f, 1f);
            Color iceBlue = new Color(0.62f, 0.96f, 1f);

            CreatePart(
                modelRoot,
                "XP Crystal",
                GetGemMesh(),
                Vector3.zero,
                Quaternion.Euler(0f, 0f, 22.5f),
                new Vector3(0.58f, 0.58f, 0.82f),
                deepBlue,
                electricBlue,
                iceBlue
            );

            CreatePart(
                modelRoot,
                "Crystal Orbit",
                GetRingMesh(),
                Vector3.zero,
                Quaternion.Euler(68f, 0f, 18f),
                new Vector3(0.98f, 0.98f, 0.05f),
                iceBlue
            );
        }

        private void BuildBoostToken(ModelKind kind)
        {
            (Color baseColor, Color rimColor, Color symbolColor) = GetPalette(kind);

            GameObject faceObject = new GameObject("Token Face");
            faceObject.layer = gameObject.layer;
            Transform face = faceObject.transform;
            face.SetParent(modelRoot, false);
            face.localRotation = Quaternion.Euler(-60f, 0f, 0f);

            CreatePart(
                face,
                "Token Core",
                GetCylinderMesh(),
                Vector3.zero,
                Quaternion.identity,
                new Vector3(0.84f, 0.84f, 0.16f),
                baseColor
            );

            CreatePart(
                face,
                "Token Rim",
                GetRingMesh(),
                Vector3.zero,
                Quaternion.identity,
                new Vector3(0.98f, 0.98f, 0.21f),
                rimColor
            );

            BuildSymbol(face, kind, symbolColor, rimColor);
        }

        private void AddChevron(Transform parent, float yOffset, float depth, Color color)
        {
            AddBox(
                parent,
                "Chevron Left",
                new Vector3(-0.105f, yOffset, depth),
                new Vector3(0.1f, 0.34f, 0.075f),
                -48f,
                color
            );
            AddBox(
                parent,
                "Chevron Right",
                new Vector3(0.105f, yOffset, depth),
                new Vector3(0.1f, 0.34f, 0.075f),
                48f,
                color
            );
        }

        private void AddBox(
            Transform parent,
            string partName,
            Vector3 position,
            Vector3 scale,
            float zRotation,
            Color color
        )
        {
            CreatePart(
                parent,
                partName,
                GetBoxMesh(),
                position,
                Quaternion.Euler(0f, 0f, zRotation),
                scale,
                color
            );
        }

        private GameObject CreatePart(
            Transform parent,
            string partName,
            Mesh mesh,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            params Color[] colors
        )
        {
            GameObject part = new GameObject(partName, typeof(MeshFilter), typeof(MeshRenderer));
            part.layer = gameObject.layer;
            Transform partTransform = part.transform;
            partTransform.SetParent(parent, false);
            partTransform.localPosition = position;
            partTransform.localRotation = rotation;
            partTransform.localScale = scale;

            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = CreateMaterialArray(colors);
            renderer.shadowCastingMode =
                Kind == ModelKind.Experience ? ShadowCastingMode.Off : ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            return part;
        }

        private static Material[] CreateMaterialArray(Color[] colors)
        {
            Material[] result = new Material[colors.Length];
            for (int i = 0; i < colors.Length; i++)
                result[i] = GetMaterial(colors[i]);
            return result;
        }

        private static Material GetMaterial(Color color)
        {
            Color32 key = color;
            if (Materials.TryGetValue(key, out Material material) && material != null)
                return material;

            Shader shader = FindPickupShader(Resources.Load<Shader>, Shader.Find);
            material = new Material(shader)
            {
                name = $"Pickup3D {ColorUtility.ToHtmlStringRGB(color)}",
                color = color,
                enableInstancing = true,
            };

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.5f);

            Materials[key] = material;
            return material;
        }

        internal static (Color baseColor, Color rimColor, Color symbolColor) GetPalette(
            ModelKind kind
        )
        {
            return kind switch
            {
                ModelKind.Health => (
                    new Color(0.34f, 0.035f, 0.06f),
                    new Color(1f, 0.18f, 0.24f),
                    new Color(1f, 0.88f, 0.88f)
                ),
                ModelKind.Damage => (
                    new Color(0.28f, 0.055f, 0.015f),
                    new Color(1f, 0.32f, 0.05f),
                    new Color(1f, 0.86f, 0.16f)
                ),
                ModelKind.AttackSpeed => (
                    new Color(0.23f, 0.025f, 0.36f),
                    new Color(0.84f, 0.17f, 1f),
                    new Color(1f, 0.72f, 1f)
                ),
                ModelKind.MovementSpeed => (
                    new Color(0.015f, 0.22f, 0.3f),
                    new Color(0.05f, 0.9f, 1f),
                    new Color(0.78f, 1f, 1f)
                ),
                ModelKind.ExperienceBoost => (
                    new Color(0.015f, 0.16f, 0.44f),
                    new Color(0.05f, 0.55f, 1f),
                    new Color(0.55f, 0.92f, 1f)
                ),
                ModelKind.DetectionRadius => (
                    new Color(0.02f, 0.25f, 0.13f),
                    new Color(0.08f, 0.95f, 0.42f),
                    new Color(0.72f, 1f, 0.8f)
                ),
                ModelKind.Magnet => (
                    new Color(0.14f, 0.07f, 0.2f),
                    new Color(0.28f, 0.63f, 1f),
                    new Color(1f, 0.22f, 0.22f)
                ),
                ModelKind.Hourglass => (
                    new Color(0.04f, 0.1f, 0.3f),
                    new Color(0.26f, 0.64f, 1f),
                    new Color(0.95f, 0.96f, 1f)
                ),
                ModelKind.SprayRange => (
                    new Color(0.05f, 0.2f, 0.16f),
                    new Color(0.18f, 0.9f, 0.68f),
                    new Color(0.8f, 1f, 0.9f)
                ),
                ModelKind.SprayWidth => (
                    new Color(0.13f, 0.16f, 0.28f),
                    new Color(0.4f, 0.65f, 1f),
                    new Color(0.82f, 0.9f, 1f)
                ),
                _ => (Color.black, Color.white, Color.white),
            };
        }
    }
}
