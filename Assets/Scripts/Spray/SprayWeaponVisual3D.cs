using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds the player's stylized low-poly hand and surface-disinfectant bottle.
/// The model is visual only; aiming, particles, and damage remain 2D.
/// </summary>
[DisallowMultipleComponent]
public sealed class SprayWeaponVisual3D : MonoBehaviour
{
    private const string ModelRootName = "SanitizerModel3D";
    private const int RadialSegments = 10;
    private static readonly Vector3 ModelScale = new Vector3(1f, 1.5f, 1.3f);

    private static readonly Dictionary<Color32, Material> Materials =
        new Dictionary<Color32, Material>();

    private static Mesh boxMesh;
    private static Mesh cylinderMesh;
    private static Mesh bottleMesh;

    private Transform modelRoot;
    private bool initialized;

    public Transform ModelRoot => modelRoot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSharedResources()
    {
        Materials.Clear();
        boxMesh = null;
        cylinderMesh = null;
        bottleMesh = null;
    }

    public static SprayWeaponVisual3D Attach(GameObject handObject)
    {
        SprayWeaponVisual3D visual = handObject.GetComponent<SprayWeaponVisual3D>();
        if (visual == null)
            visual = handObject.AddComponent<SprayWeaponVisual3D>();

        visual.Initialize();
        return visual;
    }

    public void SetVisible(bool visible)
    {
        if (modelRoot != null)
            modelRoot.gameObject.SetActive(visible);
    }

    public void SetFacingLeft(bool facingLeft)
    {
        if (modelRoot != null)
        {
            modelRoot.localScale = new Vector3(
                ModelScale.x,
                facingLeft ? -ModelScale.y : ModelScale.y,
                ModelScale.z);
        }
    }

    private void Initialize()
    {
        if (initialized)
            return;

        initialized = true;
        Transform existingRoot = transform.Find(ModelRootName);
        if (existingRoot != null)
        {
            modelRoot = existingRoot;
            modelRoot.localScale = ModelScale;
            return;
        }

        GameObject rootObject = new GameObject(ModelRootName);
        rootObject.layer = gameObject.layer;
        modelRoot = rootObject.transform;
        modelRoot.SetParent(transform, false);
        modelRoot.localPosition = Vector3.zero;
        modelRoot.localRotation = Quaternion.identity;
        modelRoot.localScale = ModelScale;

        BuildBottle();
        BuildHand();
    }

    private void BuildBottle()
    {
        Color bottleWhite = new Color(0.88f, 0.94f, 0.98f);
        Color bottleHighlight = new Color(0.98f, 1f, 1f);
        Color sanitizerBlue = new Color(0.035f, 0.22f, 0.67f);
        Color sanitizerBlueLight = new Color(0.08f, 0.48f, 0.92f);
        Color labelWhite = new Color(0.92f, 0.97f, 1f);

        CreatePart(
            modelRoot,
            "White Bottle Body",
            GetBottleMesh(),
            Vector3.zero,
            Quaternion.identity,
            Vector3.one,
            bottleWhite);

        CreatePart(
            modelRoot,
            "Bottle Highlight",
            GetBoxMesh(),
            new Vector3(-0.072f, -0.14f, -0.067f),
            Quaternion.Euler(0f, 0f, -5f),
            new Vector3(0.018f, 0.19f, 0.008f),
            bottleHighlight);

        CreatePart(
            modelRoot,
            "Bottle Neck",
            GetCylinderMesh(),
            new Vector3(0.012f, 0.022f, 0f),
            Quaternion.Euler(90f, 0f, 0f),
            new Vector3(0.056f, 0.056f, 0.07f),
            bottleWhite);

        CreatePart(
            modelRoot,
            "Blue Collar",
            GetCylinderMesh(),
            new Vector3(0.018f, 0.052f, 0f),
            Quaternion.Euler(90f, 0f, 0f),
            new Vector3(0.082f, 0.082f, 0.042f),
            sanitizerBlue);

        CreatePart(
            modelRoot,
            "Blue Sprayer Head",
            GetBoxMesh(),
            new Vector3(0.085f, 0.074f, 0f),
            Quaternion.Euler(0f, 0f, -4f),
            new Vector3(0.17f, 0.078f, 0.12f),
            sanitizerBlue);

        CreatePart(
            modelRoot,
            "Blue Nozzle Barrel",
            GetBoxMesh(),
            new Vector3(0.176f, 0.064f, 0f),
            Quaternion.identity,
            new Vector3(0.095f, 0.047f, 0.095f),
            sanitizerBlueLight);

        CreatePart(
            modelRoot,
            "White Nozzle Tip",
            GetCylinderMesh(),
            new Vector3(0.228f, 0.064f, 0f),
            Quaternion.Euler(0f, 90f, 0f),
            new Vector3(0.06f, 0.06f, 0.035f),
            labelWhite);

        CreatePart(
            modelRoot,
            "Trigger",
            GetBoxMesh(),
            new Vector3(0.075f, 0.008f, -0.005f),
            Quaternion.Euler(0f, 0f, -24f),
            new Vector3(0.038f, 0.105f, 0.052f),
            labelWhite);

        CreatePart(
            modelRoot,
            "Blue Label",
            GetBoxMesh(),
            new Vector3(0f, -0.18f, -0.066f),
            Quaternion.identity,
            new Vector3(0.174f, 0.105f, 0.01f),
            sanitizerBlue);

        CreatePart(
            modelRoot,
            "Label Stripe",
            GetBoxMesh(),
            new Vector3(0f, -0.154f, -0.073f),
            Quaternion.identity,
            new Vector3(0.14f, 0.018f, 0.008f),
            labelWhite);

        CreatePart(
            modelRoot,
            "Label Mark Vertical",
            GetBoxMesh(),
            new Vector3(0f, -0.205f, -0.073f),
            Quaternion.identity,
            new Vector3(0.018f, 0.046f, 0.008f),
            labelWhite);

        CreatePart(
            modelRoot,
            "Label Mark Horizontal",
            GetBoxMesh(),
            new Vector3(0f, -0.205f, -0.074f),
            Quaternion.identity,
            new Vector3(0.052f, 0.016f, 0.008f),
            labelWhite);
    }

    private void BuildHand()
    {
        Color skin = new Color(0.72f, 0.34f, 0.18f);
        Color skinHighlight = new Color(0.94f, 0.56f, 0.3f);

        CreatePart(
            modelRoot,
            "Floating Faceted Palm",
            GetCylinderMesh(),
            new Vector3(-0.058f, -0.09f, -0.045f),
            Quaternion.Euler(-6f, 10f, -9f),
            new Vector3(0.13f, 0.145f, 0.075f),
            skin);

        for (int i = 0; i < 3; i++)
        {
            CreatePart(
                modelRoot,
                $"Grip Finger {i + 1}",
                GetCylinderMesh(),
                new Vector3(0.012f, -0.095f - i * 0.047f, -0.09f),
                Quaternion.Euler(90f, 0f, 0f),
                new Vector3(0.052f, 0.052f, 0.082f),
                i == 0 ? skinHighlight : skin);
        }

        CreatePart(
            modelRoot,
            "Thumb",
            GetCylinderMesh(),
            new Vector3(-0.005f, -0.018f, -0.085f),
            Quaternion.Euler(0f, 90f, 24f),
            new Vector3(0.056f, 0.06f, 0.13f),
            skinHighlight);

        CreatePart(
            modelRoot,
            "Trigger Finger",
            GetCylinderMesh(),
            new Vector3(0.073f, 0.012f, -0.068f),
            Quaternion.Euler(90f, 0f, -18f),
            new Vector3(0.043f, 0.043f, 0.11f),
            skinHighlight);
    }

    private static Transform CreatePart(
        Transform parent,
        string partName,
        Mesh mesh,
        Vector3 localPosition,
        Quaternion localRotation,
        Vector3 localScale,
        Color color)
    {
        GameObject part = new GameObject(partName, typeof(MeshFilter), typeof(MeshRenderer));
        part.layer = parent.gameObject.layer;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = localRotation;
        part.transform.localScale = localScale;

        part.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = part.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = GetMaterial(color);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return part.transform;
    }

    private static Material GetMaterial(Color color)
    {
        Color32 key = color;
        if (Materials.TryGetValue(key, out Material material) && material != null)
            return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        material = new Material(shader)
        {
            name = $"Sanitizer 3D {ColorUtility.ToHtmlStringRGB(color)}",
            color = color,
            enableInstancing = true
        };

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.32f);

        Materials[key] = material;
        return material;
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

        boxMesh = FinalizeMesh("Sanitizer Box", vertices, triangles);
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

        cylinderMesh = FinalizeMesh(
            "Sanitizer Low-Poly Cylinder",
            vertices.ToArray(),
            triangles.ToArray());
        return cylinderMesh;
    }

    private static Mesh GetBottleMesh()
    {
        if (bottleMesh != null)
            return bottleMesh;

        Vector2[] outline =
        {
            new Vector2(-0.105f, -0.29f),
            new Vector2(0.105f, -0.29f),
            new Vector2(0.12f, -0.245f),
            new Vector2(0.112f, -0.09f),
            new Vector2(0.085f, -0.035f),
            new Vector2(0.04f, 0.002f),
            new Vector2(0.032f, 0.025f),
            new Vector2(-0.02f, 0.025f),
            new Vector2(-0.032f, -0.012f),
            new Vector2(-0.082f, -0.05f),
            new Vector2(-0.118f, -0.105f),
            new Vector2(-0.122f, -0.245f)
        };

        const float halfDepth = 0.06f;
        List<Vector3> vertices = new List<Vector3>(outline.Length * 2 + 2);
        for (int i = 0; i < outline.Length; i++)
            vertices.Add(new Vector3(outline[i].x, outline[i].y, -halfDepth));
        for (int i = 0; i < outline.Length; i++)
            vertices.Add(new Vector3(outline[i].x, outline[i].y, halfDepth));

        int nearCenter = vertices.Count;
        vertices.Add(new Vector3(0f, -0.14f, -halfDepth));
        int farCenter = vertices.Count;
        vertices.Add(new Vector3(0f, -0.14f, halfDepth));

        List<int> triangles = new List<int>(outline.Length * 12);
        for (int i = 0; i < outline.Length; i++)
        {
            int next = (i + 1) % outline.Length;
            int near = i;
            int nearNext = next;
            int far = outline.Length + i;
            int farNext = outline.Length + next;

            triangles.AddRange(new[] { nearCenter, nearNext, near });
            triangles.AddRange(new[] { farCenter, far, farNext });
            triangles.AddRange(new[] { near, nearNext, farNext, near, farNext, far });
        }

        bottleMesh = FinalizeMesh(
            "Classic Sanitizer Bottle",
            vertices.ToArray(),
            triangles.ToArray());
        return bottleMesh;
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
