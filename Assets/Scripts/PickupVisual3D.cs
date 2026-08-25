using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds lightweight, shared low-poly meshes for XP and boost pickups.
/// The gameplay remains 2D: these objects are visual children only and add no
/// physics colliders, so collection and magnet movement keep using Rigidbody2D.
/// </summary>
[DisallowMultipleComponent]
public sealed class PickupVisual3D : MonoBehaviour
{
    public enum ModelKind
    {
        Experience,
        Health,
        Damage,
        AttackSpeed,
        MovementSpeed,
        ExperienceBoost,
        DetectionRadius,
        Magnet,
        Hourglass,
        SprayRange,
        SprayWidth
    }

    private const string ModelRootName = "PickupModel3D";
    private const int RadialSegments = 16;
    private const float ExperienceVisualScale = 0.3f;
    private const float BoostVisualScale = 0.8f;
    private const float ExperienceBaseDepth = -0.28f;
    private const float BoostBaseDepth = -0.62f;

    private static readonly Dictionary<Color32, Material> Materials =
        new Dictionary<Color32, Material>();

    private static Mesh boxMesh;
    private static Mesh cylinderMesh;
    private static Mesh ringMesh;
    private static Mesh gemMesh;

    private Transform modelRoot;
    private Transform spinTarget;
    private Vector3 modelBasePosition;
    private Vector3 modelBaseScale;
    private Quaternion modelBaseRotation;
    private Quaternion spinBaseRotation;
    private Vector3 rotationAxis = Vector3.forward;
    private float animationPhase;
    private float spinSpeed;
    private float attractionBlend;
    private bool attractionRequested;
    private bool initialized;

    public ModelKind Kind { get; private set; }
    public Transform ModelRoot => modelRoot;
    public bool IsAttracted => attractionRequested;
    public float AttractionBlend => attractionBlend;

    public void SetAttracted(bool attracted)
    {
        attractionRequested = attracted;
    }

    public void ResetAttraction()
    {
        attractionRequested = false;
        attractionBlend = 0f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSharedResources()
    {
        Materials.Clear();
        boxMesh = null;
        cylinderMesh = null;
        ringMesh = null;
        gemMesh = null;
    }

    public static PickupVisual3D AttachExperience(GameObject pickup)
    {
        return Attach(pickup, ModelKind.Experience);
    }

    public static PickupVisual3D AttachBoost(BoostBase boost)
    {
        ModelKind kind = boost.BoostSoundType switch
        {
            ProceduralBoostAudio.BoostSoundType.Health => ModelKind.Health,
            ProceduralBoostAudio.BoostSoundType.Damage => ModelKind.Damage,
            ProceduralBoostAudio.BoostSoundType.AttackSpeed => ModelKind.AttackSpeed,
            ProceduralBoostAudio.BoostSoundType.MovementSpeed => ModelKind.MovementSpeed,
            ProceduralBoostAudio.BoostSoundType.Experience => ModelKind.ExperienceBoost,
            ProceduralBoostAudio.BoostSoundType.DetectionRadius => ModelKind.DetectionRadius,
            ProceduralBoostAudio.BoostSoundType.SprayRange => ModelKind.SprayRange,
            ProceduralBoostAudio.BoostSoundType.SprayWidth => ModelKind.SprayWidth,
            ProceduralBoostAudio.BoostSoundType.Magnet => ModelKind.Magnet,
            ProceduralBoostAudio.BoostSoundType.TimeSlow => ModelKind.Hourglass,
            _ => ModelKind.ExperienceBoost
        };

        return Attach(boost.gameObject, kind);
    }

    private static PickupVisual3D Attach(GameObject pickup, ModelKind kind)
    {
        PickupVisual3D visual = pickup.GetComponent<PickupVisual3D>();
        if (visual == null)
            visual = pickup.AddComponent<PickupVisual3D>();

        visual.Initialize(kind);
        return visual;
    }

    private void Initialize(ModelKind kind)
    {
        if (initialized)
            return;

        initialized = true;
        Kind = kind;
        animationPhase = Mathf.Abs(GetInstanceID() % 1000) * 0.013f;
        spinSpeed = kind == ModelKind.Experience ? 72f : 34f;
        rotationAxis = kind == ModelKind.Experience
            ? new Vector3(0.25f, 0.5f, 1f).normalized
            : Vector3.forward;
        modelBasePosition = new Vector3(
            0f,
            0f,
            kind == ModelKind.Experience ? ExperienceBaseDepth : BoostBaseDepth);
        modelBaseScale = Vector3.one *
            (kind == ModelKind.Experience ? ExperienceVisualScale : BoostVisualScale);

        foreach (SpriteRenderer spriteRenderer in GetComponentsInChildren<SpriteRenderer>(true))
            spriteRenderer.enabled = false;

        Transform existingRoot = transform.Find(ModelRootName);
        if (existingRoot != null)
        {
            modelRoot = existingRoot;
            modelRoot.localPosition = modelBasePosition;
            modelRoot.localScale = modelBaseScale;
            modelBaseRotation = Quaternion.identity;
            modelRoot.localRotation = modelBaseRotation;
            spinTarget = kind == ModelKind.Experience
                ? modelRoot
                : modelRoot.Find("Token Face");
            spinBaseRotation = kind == ModelKind.Experience
                ? Quaternion.identity
                : Quaternion.Euler(-60f, 0f, 0f);
            if (spinTarget != null)
                spinTarget.localRotation = spinBaseRotation;
            return;
        }

        GameObject rootObject = new GameObject(ModelRootName);
        rootObject.layer = gameObject.layer;
        modelRoot = rootObject.transform;
        modelRoot.SetParent(transform, false);
        modelBaseRotation = Quaternion.identity;
        modelRoot.localPosition = modelBasePosition;
        modelRoot.localRotation = modelBaseRotation;
        modelRoot.localScale = modelBaseScale;

        if (kind == ModelKind.Experience)
            BuildExperienceCrystal();
        else
            BuildBoostToken(kind);

        spinTarget = kind == ModelKind.Experience
            ? modelRoot
            : modelRoot.Find("Token Face");
        spinBaseRotation = spinTarget != null
            ? spinTarget.localRotation
            : Quaternion.identity;
    }

    private void Update()
    {
        if (!initialized || modelRoot == null)
            return;

        float time = Time.time + animationPhase;
        attractionBlend = Mathf.MoveTowards(
            attractionBlend,
            attractionRequested ? 1f : 0f,
            Time.deltaTime * 7f);
        float bobAmplitude = Mathf.Lerp(0.055f, 0.012f, attractionBlend);
        float bob = Mathf.Sin(time * 3.2f) * bobAmplitude;
        float pulse = 1f + Mathf.Sin(time * 4.1f) * 0.035f;
        float magneticWobble = Mathf.Sin(time * 16f) * 24f * attractionBlend;
        float angle = Mathf.Repeat(time * spinSpeed + magneticWobble, 360f);
        float pullScale = Mathf.Lerp(1f, 0.72f, attractionBlend);

        modelRoot.localPosition = modelBasePosition + Vector3.back * bob;
        modelRoot.localRotation = modelBaseRotation;
        modelRoot.localScale = modelBaseScale * pulse * pullScale;
        if (spinTarget != null)
            spinTarget.localRotation = spinBaseRotation * Quaternion.AngleAxis(angle, rotationAxis);
    }

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
            iceBlue);

        CreatePart(
            modelRoot,
            "Crystal Orbit",
            GetRingMesh(),
            Vector3.zero,
            Quaternion.Euler(68f, 0f, 18f),
            new Vector3(0.98f, 0.98f, 0.05f),
            iceBlue);
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
            baseColor);

        CreatePart(
            face,
            "Token Rim",
            GetRingMesh(),
            Vector3.zero,
            Quaternion.identity,
            new Vector3(0.98f, 0.98f, 0.21f),
            rimColor);

        BuildSymbol(face, kind, symbolColor, rimColor);
    }

    private void BuildSymbol(Transform face, ModelKind kind, Color symbolColor, Color accentColor)
    {
        const float faceDepth = -0.145f;

        switch (kind)
        {
            case ModelKind.Health:
                AddBox(face, "Health Vertical", new Vector3(0f, 0f, faceDepth),
                    new Vector3(0.14f, 0.54f, 0.075f), 0f, symbolColor);
                AddBox(face, "Health Horizontal", new Vector3(0f, 0f, faceDepth),
                    new Vector3(0.54f, 0.14f, 0.075f), 0f, symbolColor);
                break;

            case ModelKind.Damage:
                AddBox(face, "Blade", new Vector3(0.02f, 0.03f, faceDepth),
                    new Vector3(0.13f, 0.58f, 0.075f), -24f, symbolColor);
                AddBox(face, "Guard", new Vector3(-0.08f, -0.18f, faceDepth - 0.005f),
                    new Vector3(0.4f, 0.09f, 0.085f), -24f, accentColor);
                AddBox(face, "Pommel", new Vector3(-0.17f, -0.34f, faceDepth),
                    new Vector3(0.13f, 0.13f, 0.08f), -24f, symbolColor);
                break;

            case ModelKind.MovementSpeed:
                AddChevron(face, -0.11f, faceDepth, symbolColor);
                AddChevron(face, 0.16f, faceDepth, symbolColor);
                break;

            case ModelKind.AttackSpeed:
                CreatePart(face, "Attack Dial", GetRingMesh(),
                    new Vector3(0f, 0f, faceDepth), Quaternion.identity,
                    new Vector3(0.6f, 0.6f, 0.07f), symbolColor);
                AddBox(face, "Attack Hand", new Vector3(0.07f, 0.09f, faceDepth - 0.015f),
                    new Vector3(0.09f, 0.34f, 0.09f), -34f, accentColor);
                AddBox(face, "Attack Tick", new Vector3(-0.2f, 0.19f, faceDepth - 0.01f),
                    new Vector3(0.08f, 0.15f, 0.08f), -44f, symbolColor);
                break;

            case ModelKind.ExperienceBoost:
                AddBox(face, "XP Diamond", new Vector3(0f, 0f, faceDepth),
                    new Vector3(0.36f, 0.36f, 0.1f), 45f, symbolColor);
                AddBox(face, "XP Spark", new Vector3(0.22f, 0.22f, faceDepth - 0.015f),
                    new Vector3(0.1f, 0.1f, 0.08f), 45f, accentColor);
                break;

            case ModelKind.DetectionRadius:
                CreatePart(face, "Radar Outer", GetRingMesh(),
                    new Vector3(0f, 0f, faceDepth), Quaternion.identity,
                    new Vector3(0.65f, 0.65f, 0.065f), symbolColor);
                CreatePart(face, "Radar Inner", GetRingMesh(),
                    new Vector3(0f, 0f, faceDepth - 0.01f), Quaternion.identity,
                    new Vector3(0.34f, 0.34f, 0.08f), accentColor);
                AddBox(face, "Radar Sweep", new Vector3(0.08f, 0.09f, faceDepth - 0.02f),
                    new Vector3(0.07f, 0.34f, 0.09f), -42f, symbolColor);
                break;

            case ModelKind.Magnet:
                AddBox(face, "Magnet Bridge", new Vector3(0f, -0.2f, faceDepth),
                    new Vector3(0.5f, 0.13f, 0.085f), 0f, accentColor);
                AddBox(face, "Magnet Left", new Vector3(-0.19f, 0f, faceDepth),
                    new Vector3(0.13f, 0.42f, 0.085f), 0f, accentColor);
                AddBox(face, "Magnet Right", new Vector3(0.19f, 0f, faceDepth),
                    new Vector3(0.13f, 0.42f, 0.085f), 0f, symbolColor);
                AddBox(face, "Magnet Left Tip", new Vector3(-0.19f, 0.25f, faceDepth - 0.01f),
                    new Vector3(0.15f, 0.12f, 0.095f), 0f, symbolColor);
                AddBox(face, "Magnet Right Tip", new Vector3(0.19f, 0.25f, faceDepth - 0.01f),
                    new Vector3(0.15f, 0.12f, 0.095f), 0f, Color.white);
                break;

            case ModelKind.Hourglass:
                AddBox(face, "Hourglass Top", new Vector3(0f, 0.28f, faceDepth),
                    new Vector3(0.52f, 0.08f, 0.075f), 0f, accentColor);
                AddBox(face, "Hourglass Bottom", new Vector3(0f, -0.28f, faceDepth),
                    new Vector3(0.52f, 0.08f, 0.075f), 0f, accentColor);
                AddBox(face, "Hourglass Left", new Vector3(-0.11f, 0f, faceDepth),
                    new Vector3(0.08f, 0.5f, 0.065f), -24f, symbolColor);
                AddBox(face, "Hourglass Right", new Vector3(0.11f, 0f, faceDepth),
                    new Vector3(0.08f, 0.5f, 0.065f), 24f, symbolColor);
                AddBox(face, "Hourglass Sand", new Vector3(0f, -0.1f, faceDepth - 0.015f),
                    new Vector3(0.13f, 0.2f, 0.09f), 45f, accentColor);
                break;

            case ModelKind.SprayRange:
                AddBox(face, "Range Stem", new Vector3(-0.08f, -0.08f, faceDepth),
                    new Vector3(0.1f, 0.5f, 0.075f), -35f, symbolColor);
                AddChevron(face, 0.18f, faceDepth - 0.01f, accentColor);
                break;

            case ModelKind.SprayWidth:
                AddBox(face, "Width Left", new Vector3(-0.13f, 0f, faceDepth),
                    new Vector3(0.09f, 0.54f, 0.075f), -20f, symbolColor);
                AddBox(face, "Width Right", new Vector3(0.13f, 0f, faceDepth),
                    new Vector3(0.09f, 0.54f, 0.075f), 20f, symbolColor);
                AddBox(face, "Width Base", new Vector3(0f, -0.22f, faceDepth - 0.01f),
                    new Vector3(0.42f, 0.09f, 0.085f), 0f, accentColor);
                break;
        }
    }

    private void AddChevron(Transform parent, float yOffset, float depth, Color color)
    {
        AddBox(parent, "Chevron Left", new Vector3(-0.105f, yOffset, depth),
            new Vector3(0.1f, 0.34f, 0.075f), -48f, color);
        AddBox(parent, "Chevron Right", new Vector3(0.105f, yOffset, depth),
            new Vector3(0.1f, 0.34f, 0.075f), 48f, color);
    }

    private void AddBox(
        Transform parent,
        string partName,
        Vector3 position,
        Vector3 scale,
        float zRotation,
        Color color)
    {
        CreatePart(
            parent,
            partName,
            GetBoxMesh(),
            position,
            Quaternion.Euler(0f, 0f, zRotation),
            scale,
            color);
    }

    private GameObject CreatePart(
        Transform parent,
        string partName,
        Mesh mesh,
        Vector3 position,
        Quaternion rotation,
        Vector3 scale,
        params Color[] colors)
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
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
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

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        material = new Material(shader)
        {
            name = $"Pickup3D {ColorUtility.ToHtmlStringRGB(color)}",
            color = color,
            enableInstancing = true
        };

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        Materials[key] = material;
        return material;
    }

    private static (Color baseColor, Color rimColor, Color symbolColor) GetPalette(ModelKind kind)
    {
        return kind switch
        {
            ModelKind.Health => (
                new Color(0.34f, 0.035f, 0.06f),
                new Color(1f, 0.18f, 0.24f),
                new Color(1f, 0.88f, 0.88f)),
            ModelKind.Damage => (
                new Color(0.28f, 0.055f, 0.015f),
                new Color(1f, 0.32f, 0.05f),
                new Color(1f, 0.86f, 0.16f)),
            ModelKind.AttackSpeed => (
                new Color(0.23f, 0.025f, 0.36f),
                new Color(0.84f, 0.17f, 1f),
                new Color(1f, 0.72f, 1f)),
            ModelKind.MovementSpeed => (
                new Color(0.015f, 0.22f, 0.3f),
                new Color(0.05f, 0.9f, 1f),
                new Color(0.78f, 1f, 1f)),
            ModelKind.ExperienceBoost => (
                new Color(0.015f, 0.16f, 0.44f),
                new Color(0.05f, 0.55f, 1f),
                new Color(0.55f, 0.92f, 1f)),
            ModelKind.DetectionRadius => (
                new Color(0.02f, 0.25f, 0.13f),
                new Color(0.08f, 0.95f, 0.42f),
                new Color(0.72f, 1f, 0.8f)),
            ModelKind.Magnet => (
                new Color(0.14f, 0.07f, 0.2f),
                new Color(0.28f, 0.63f, 1f),
                new Color(1f, 0.22f, 0.22f)),
            ModelKind.Hourglass => (
                new Color(0.04f, 0.1f, 0.3f),
                new Color(0.26f, 0.64f, 1f),
                new Color(0.95f, 0.96f, 1f)),
            ModelKind.SprayRange => (
                new Color(0.05f, 0.2f, 0.16f),
                new Color(0.18f, 0.9f, 0.68f),
                new Color(0.8f, 1f, 0.9f)),
            ModelKind.SprayWidth => (
                new Color(0.13f, 0.16f, 0.28f),
                new Color(0.4f, 0.65f, 1f),
                new Color(0.82f, 0.9f, 1f)),
            _ => (Color.black, Color.white, Color.white)
        };
    }

    private static Mesh GetBoxMesh()
    {
        if (boxMesh != null)
            return boxMesh;

        Vector3[] vertices =
        {
            new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f)
        };
        int[] triangles =
        {
            0, 2, 1, 0, 3, 2,
            1, 2, 6, 1, 6, 5,
            5, 6, 7, 5, 7, 4,
            4, 7, 3, 4, 3, 0,
            3, 7, 6, 3, 6, 2,
            4, 0, 1, 4, 1, 5
        };

        boxMesh = FinalizeMesh("Pickup Box", vertices, triangles);
        return boxMesh;
    }

    private static Mesh GetCylinderMesh()
    {
        if (cylinderMesh != null)
            return cylinderMesh;

        List<Vector3> vertices = new List<Vector3>(RadialSegments * 2 + 2);
        List<int> triangles = new List<int>(RadialSegments * 12);
        for (int i = 0; i < RadialSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / RadialSegments;
            float x = Mathf.Cos(angle) * 0.5f;
            float y = Mathf.Sin(angle) * 0.5f;
            vertices.Add(new Vector3(x, y, -0.5f));
            vertices.Add(new Vector3(x, y, 0.5f));
        }

        int nearCenter = vertices.Count;
        vertices.Add(new Vector3(0f, 0f, -0.5f));
        int farCenter = vertices.Count;
        vertices.Add(new Vector3(0f, 0f, 0.5f));

        for (int i = 0; i < RadialSegments; i++)
        {
            int next = (i + 1) % RadialSegments;
            int near = i * 2;
            int far = near + 1;
            int nearNext = next * 2;
            int farNext = nearNext + 1;

            triangles.AddRange(new[] { nearCenter, nearNext, near });
            triangles.AddRange(new[] { farCenter, far, farNext });
            triangles.AddRange(new[] { near, nearNext, farNext, near, farNext, far });
        }

        cylinderMesh = FinalizeMesh("Pickup Cylinder", vertices.ToArray(), triangles.ToArray());
        return cylinderMesh;
    }

    private static Mesh GetRingMesh()
    {
        if (ringMesh != null)
            return ringMesh;

        List<Vector3> vertices = new List<Vector3>(RadialSegments * 4);
        List<int> triangles = new List<int>(RadialSegments * 24);
        for (int i = 0; i < RadialSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / RadialSegments;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            vertices.Add(new Vector3(direction.x * 0.5f, direction.y * 0.5f, -0.5f));
            vertices.Add(new Vector3(direction.x * 0.37f, direction.y * 0.37f, -0.5f));
            vertices.Add(new Vector3(direction.x * 0.5f, direction.y * 0.5f, 0.5f));
            vertices.Add(new Vector3(direction.x * 0.37f, direction.y * 0.37f, 0.5f));
        }

        for (int i = 0; i < RadialSegments; i++)
        {
            int current = i * 4;
            int next = ((i + 1) % RadialSegments) * 4;

            int nearOuter = current;
            int nearInner = current + 1;
            int farOuter = current + 2;
            int farInner = current + 3;
            int nextNearOuter = next;
            int nextNearInner = next + 1;
            int nextFarOuter = next + 2;
            int nextFarInner = next + 3;

            triangles.AddRange(new[]
            {
                nearOuter, nextNearInner, nextNearOuter,
                nearOuter, nearInner, nextNearInner,
                farOuter, nextFarOuter, nextFarInner,
                farOuter, nextFarInner, farInner,
                nearOuter, nextNearOuter, nextFarOuter,
                nearOuter, nextFarOuter, farOuter,
                nearInner, farInner, nextFarInner,
                nearInner, nextFarInner, nextNearInner
            });
        }

        ringMesh = FinalizeMesh("Pickup Ring", vertices.ToArray(), triangles.ToArray());
        return ringMesh;
    }

    private static Mesh GetGemMesh()
    {
        if (gemMesh != null)
            return gemMesh;

        const int sides = 8;
        List<Vector3> vertices = new List<Vector3>(sides + 2)
        {
            new Vector3(0f, 0f, -0.62f),
            new Vector3(0f, 0f, 0.46f)
        };
        for (int i = 0; i < sides; i++)
        {
            float angle = i * Mathf.PI * 2f / sides;
            vertices.Add(new Vector3(Mathf.Cos(angle) * 0.5f, Mathf.Sin(angle) * 0.5f, 0f));
        }

        List<int>[] facets =
        {
            new List<int>(),
            new List<int>(),
            new List<int>()
        };
        for (int i = 0; i < sides; i++)
        {
            int current = 2 + i;
            int next = 2 + ((i + 1) % sides);
            List<int> group = facets[i % facets.Length];
            group.AddRange(new[] { 0, next, current });
            group.AddRange(new[] { 1, current, next });
        }

        gemMesh = new Mesh { name = "Pickup Faceted Gem", subMeshCount = facets.Length };
        gemMesh.SetVertices(vertices);
        for (int i = 0; i < facets.Length; i++)
            gemMesh.SetTriangles(facets[i], i);
        gemMesh.RecalculateNormals();
        gemMesh.RecalculateBounds();
        gemMesh.UploadMeshData(true);
        return gemMesh;
    }

    private static Mesh FinalizeMesh(string meshName, Vector3[] vertices, int[] triangles)
    {
        Mesh mesh = new Mesh { name = meshName };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.UploadMeshData(true);
        return mesh;
    }
}
