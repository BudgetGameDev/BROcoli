using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Keeps one soft moonlight shaft entering the gameplay camera from the upper
/// left. The dungeon streams forever, so the shaft follows the player while
/// remaining composed consistently in the camera view.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class MoonlightGodrays : MonoBehaviour
{
    private static readonly Vector2 FloorOffset = new(3f, -1f);

    [Header("Appearance")]
    [SerializeField]
    private Material beamMaterial;

    [SerializeField, Min(1f)]
    private float beamLength = 11f;

    [SerializeField, Min(0.05f)]
    private float topRadius = 0.8f;

    [SerializeField, Min(0.1f)]
    private float floorRadius = 3.2f;

    [SerializeField]
    private Vector3 rayDirection = new(-1.8f, 1f, 0.12f);

    [SerializeField]
    private float floorHeight = 0.22f;

    private Transform followTarget;
    private GameObject ray;
    private Mesh beamMesh;

    private void OnEnable()
    {
        Rebuild();
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall -= RebuildAfterValidation;
#endif
        ClearGeneratedObjects();
    }

    private void OnValidate()
    {
        beamLength = Mathf.Max(1f, beamLength);
        topRadius = Mathf.Max(0.05f, topRadius);
        floorRadius = Mathf.Max(topRadius, floorRadius);

        if (!isActiveAndEnabled)
            return;

#if UNITY_EDITOR
        // Unity forbids DestroyImmediate inside OnValidate. Rebuild on the
        // next editor update instead so live inspector changes remain safe.
        UnityEditor.EditorApplication.delayCall -= RebuildAfterValidation;
        UnityEditor.EditorApplication.delayCall += RebuildAfterValidation;
#endif
    }

#if UNITY_EDITOR
    private void RebuildAfterValidation()
    {
        if (this != null && isActiveAndEnabled)
            Rebuild();
    }
#endif

    private void LateUpdate()
    {
        if (!Application.isPlaying)
            return;

        if (followTarget == null)
            followTarget = ResolveFollowTarget();
        if (followTarget == null)
            return;

        Vector3 target = followTarget.position;
        transform.position = new Vector3(target.x, 0f, target.z);
        AlignRayToLevel();
    }

    private void Rebuild()
    {
        ClearGeneratedObjects();
        if (beamMaterial == null)
            return;

        beamMesh = BuildSoftBeamQuad(beamLength, topRadius, floorRadius);

        Vector3 direction = rayDirection.sqrMagnitude > 0.0001f
            ? rayDirection.normalized
            : Vector3.up;
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, direction);
        Vector3 floorPoint = new(FloorOffset.x, floorHeight, FloorOffset.y);

        ray = new GameObject("__MoonlightRay")
        {
            hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave,
        };
        ray.transform.SetParent(transform, false);
        ray.transform.localPosition = floorPoint + direction * (beamLength * 0.5f);
        ray.transform.localRotation = rotation;

        MeshFilter filter = ray.AddComponent<MeshFilter>();
        filter.sharedMesh = beamMesh;

        MeshRenderer renderer = ray.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = beamMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

        AlignRayToLevel();
    }

    private void AlignRayToLevel()
    {
        if (ray == null)
            return;

        Vector3 direction = rayDirection.sqrMagnitude > 0.0001f
            ? rayDirection.normalized
            : Vector3.up;
        Vector3 side = Vector3.Cross(Vector3.up, direction);
        if (side.sqrMagnitude < 0.0001f)
            side = Vector3.right;
        side.Normalize();

        // A horizontal width axis keeps both lower corners above the floor,
        // avoiding the sharp diagonal clipping produced by a full billboard.
        Vector3 forward = Vector3.Cross(side, direction).normalized;
        ray.transform.rotation = Quaternion.LookRotation(forward, direction);
    }

    private static Mesh BuildSoftBeamQuad(float length, float upperRadius, float lowerRadius)
    {
        float halfLength = length * 0.5f;
        var vertices = new[]
        {
            new Vector3(-lowerRadius, -halfLength, 0f),
            new Vector3(lowerRadius, -halfLength, 0f),
            new Vector3(-upperRadius, halfLength, 0f),
            new Vector3(upperRadius, halfLength, 0f),
        };
        var uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
        };
        int[] triangles = { 0, 2, 1, 2, 3, 1 };

        var mesh = new Mesh
        {
            name = "Moonlight Godray Billboard",
            hideFlags = HideFlags.HideAndDontSave,
            vertices = vertices,
            uv = uv,
            triangles = triangles,
        };
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Transform ResolveFollowTarget()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            return player.transform;

        Camera camera = Camera.main;
        return camera != null ? camera.transform : null;
    }

    private void ClearGeneratedObjects()
    {
        if (ray != null)
            DestroyGenerated(ray);

        ray = null;
        if (beamMesh != null)
            DestroyGenerated(beamMesh);
        beamMesh = null;
    }

    private static void DestroyGenerated(Object generated)
    {
        if (Application.isPlaying)
            Destroy(generated);
        else
            DestroyImmediate(generated);
    }
}
