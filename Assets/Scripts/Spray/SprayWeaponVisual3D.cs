using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Loads the floating hand and upright spray bottle used by the 2D sanitizer weapon.
/// The imported models are presentation-only; aiming, particles, and damage remain 2D.
/// </summary>
[DisallowMultipleComponent]
public sealed class SprayWeaponVisual3D : MonoBehaviour
{
    private const string ModelRootName = "SanitizerModel3D";
    private const string BottleResourcePath = "ThirdParty/SprayBottle/SprayBottle";
    private const string HandResourcePath = "Generated/Licensed/theHand";
    private const float GripPoseNormalized = 0.55f;

    private static readonly Vector3 BottleScale = Vector3.one * 0.42f;
    private static readonly Vector3 HandScale = Vector3.one * 0.26f;
    private static readonly Vector3 HandPosition = new Vector3(-0.03f, 0.12f, -0.12f);
    private static readonly Dictionary<Color32, Material> Materials = new();

    private Transform modelRoot;
    private bool initialized;

    public Transform ModelRoot => modelRoot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSharedResources()
    {
        Materials.Clear();
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

    /// <summary>
    /// Cancels the parent's aim rotation so the bottle remains upright, then adds
    /// a small presentation tilt. Mirroring on X keeps the same grip on either side.
    /// </summary>
    public void SetPresentation(float aimAngleDegrees, bool facingLeft, float tiltDegrees)
    {
        if (modelRoot == null)
            return;

        modelRoot.localScale = new Vector3(facingLeft ? -1f : 1f, 1f, 1f);
        modelRoot.localRotation = Quaternion.Euler(0f, 0f, -aimAngleDegrees + tiltDegrees);
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
            return;
        }

        GameObject rootObject = new GameObject(ModelRootName);
        rootObject.layer = gameObject.layer;
        modelRoot = rootObject.transform;
        modelRoot.SetParent(transform, false);
        BuildBottle();
        BuildHand();
    }

    private void BuildBottle()
    {
        GameObject bottlePrefab = Resources.Load<GameObject>(BottleResourcePath);
        if (bottlePrefab == null)
        {
            Debug.LogError($"Missing sanitizer bottle resource: {BottleResourcePath}");
            return;
        }

        GameObject bottle = Object.Instantiate(bottlePrefab, modelRoot, false);
        bottle.name = "Upright Sanitizer Bottle";
        bottle.transform.localPosition = Vector3.zero;
        bottle.transform.localRotation = Quaternion.identity;
        bottle.transform.localScale = BottleScale;
        PrepareImportedModel(bottle);

        foreach (Renderer renderer in bottle.GetComponentsInChildren<Renderer>(true))
        {
            Material[] source = renderer.sharedMaterials;
            Material[] styled = new Material[source.Length];
            for (int i = 0; i < source.Length; i++)
                styled[i] = GetBottleMaterial(source[i] != null ? source[i].name : string.Empty);
            renderer.sharedMaterials = styled;
        }
    }

    private void BuildHand()
    {
        GameObject handPrefab = Resources.Load<GameObject>(HandResourcePath);
        if (handPrefab == null)
        {
            Debug.LogError($"Missing licensed hand resource: {HandResourcePath}");
            return;
        }

        GameObject hand = Object.Instantiate(handPrefab, modelRoot, false);
        hand.name = "Floating Cartoon Hand";
        hand.transform.localPosition = HandPosition;
        hand.transform.localRotation = Quaternion.identity;
        hand.transform.localScale = HandScale;
        PrepareImportedModel(hand);

        Material skin = GetMaterial(new Color(0.86f, 0.57f, 0.42f), "Warm Light Skin");
        foreach (Renderer renderer in hand.GetComponentsInChildren<Renderer>(true))
        {
            Material[] styled = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < styled.Length; i++)
                styled[i] = skin;
            renderer.sharedMaterials = styled;
        }

        AnimationClip grip = FindClip("GrabHold");
        if (grip != null)
            grip.SampleAnimation(hand, grip.length * GripPoseNormalized);
        else
            Debug.LogWarning("Licensed hand is missing its GrabHold animation clip.");
    }

    private static void PrepareImportedModel(GameObject instance)
    {
        foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true))
            animator.enabled = false;
        foreach (Animation animation in instance.GetComponentsInChildren<Animation>(true))
            animation.enabled = false;
        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        SetLayerRecursively(instance.transform, instance.transform.parent.gameObject.layer);
        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private static AnimationClip FindClip(string clipName)
    {
        foreach (AnimationClip clip in Resources.LoadAll<AnimationClip>(HandResourcePath))
        {
            if (clip.name == clipName)
                return clip;
        }
        return null;
    }

    private static Material GetBottleMaterial(string sourceName)
    {
        if (sourceName.Contains(".001"))
            return GetMaterial(new Color(0.03f, 0.20f, 0.67f), "Sanitizer Blue");
        if (sourceName.Contains(".002"))
            return GetMaterial(new Color(0.06f, 0.42f, 0.92f), "Nozzle Blue");
        return GetMaterial(new Color(0.92f, 0.96f, 1f), "Bottle White");
    }

    private static Material GetMaterial(Color color, string name)
    {
        Color32 key = color;
        if (Materials.TryGetValue(key, out Material material) && material != null)
            return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
            Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
        material = new Material(shader)
        {
            name = name,
            color = color,
            enableInstancing = true
        };
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.25f);
        Materials[key] = material;
        return material;
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root)
            SetLayerRecursively(child, layer);
    }
}
