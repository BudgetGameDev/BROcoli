using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot migration for the world flip (issue #29): gameplay moves from the
/// XY plane with -Z as "up" to the XZ ground plane with +Y up, and physics moves
/// from 2D to 3D. Converts Rigidbody2D/Collider2D components, re-frames the
/// visual child transforms that compensated for the old axes, and updates the
/// Game scene's camera, lights, and background.
/// </summary>
public static class WorldFlipMigration
{
    // Rotates the old world frame (-Z up) into the new one (+Y up).
    private static readonly Quaternion Frame = Quaternion.Euler(90f, 0f, 0f);

    private const string ContentRoot = "Assets/Resources/CursedDevolpmentStudioAss Assets";

    [MenuItem("Tools/World Flip/Migrate Prefabs")]
    public static void MigratePrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { ContentRoot });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = MigratePrefabRoot(root);
                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log($"[WorldFlip] migrated {path}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        AssetDatabase.SaveAssets();
    }

    private static bool MigratePrefabRoot(GameObject root)
    {
        bool changed = false;
        changed |= ConvertPhysics(root);

        // Sprite-only visuals sat flat in the old ground plane; move them onto a
        // rotated child so the quad lies flat on the new ground plane.
        SpriteRenderer rootSprite = root.GetComponent<SpriteRenderer>();
        bool hasModelChild = root.GetComponentInChildren<MeshRenderer>(true) != null;
        if (rootSprite != null && !hasModelChild && rootSprite.sprite != null)
        {
            MoveSpriteToGroundChild(rootSprite);
            changed = true;
        }

        // Re-frame visual wrapper children (e.g. "enemy-0"): the wrapper keeps its
        // rotation (scripts drive it in the new frame) but its offset rotates, and
        // every static node below it carries the frame rotation.
        foreach (Transform child in root.transform)
        {
            if (child.GetComponent<RectTransform>() != null)
            {
                // World-space canvases (health bars) rotate rigidly with the world.
                child.localPosition = Frame * child.localPosition;
                child.localRotation = Frame * child.localRotation;
                changed = true;
                continue;
            }

            if (child.GetComponentInChildren<MeshRenderer>(true) != null)
            {
                child.localPosition = Frame * child.localPosition;
                foreach (Transform grandChild in child)
                {
                    grandChild.localPosition = Frame * grandChild.localPosition;
                    grandChild.localRotation = Frame * grandChild.localRotation;
                }
                changed = true;
            }
        }

        return changed;
    }

    private static void MoveSpriteToGroundChild(SpriteRenderer source)
    {
        GameObject holder = new GameObject("Sprite");
        holder.layer = source.gameObject.layer;
        holder.transform.SetParent(source.transform, false);
        holder.transform.localRotation = Frame;

        SpriteRenderer target = holder.AddComponent<SpriteRenderer>();
        target.sprite = source.sprite;
        target.color = source.color;
        target.flipX = source.flipX;
        target.flipY = source.flipY;
        target.drawMode = source.drawMode;
        target.sortingLayerID = source.sortingLayerID;
        target.sortingOrder = source.sortingOrder;
        target.sharedMaterial = source.sharedMaterial;
        target.maskInteraction = source.maskInteraction;
        target.enabled = source.enabled;

        Object.DestroyImmediate(source);
    }

    /// <summary>Converts 2D physics components on the object to 3D equivalents.
    /// Colliders occupy the vertical band y in [-0.5, 1.5]. Unity forbids mixing
    /// 2D and 3D physics components, so all 2D data is captured and removed
    /// before any 3D component is added.</summary>
    public static bool ConvertPhysics(GameObject go)
    {
        var circles = new List<(float radius, Vector2 offset, bool trigger)>();
        foreach (CircleCollider2D circle in go.GetComponents<CircleCollider2D>())
        {
            circles.Add((circle.radius, circle.offset, circle.isTrigger));
            Object.DestroyImmediate(circle, true);
        }

        var boxes = new List<(Vector2 size, Vector2 offset, bool trigger)>();
        foreach (BoxCollider2D box in go.GetComponents<BoxCollider2D>())
        {
            boxes.Add((box.size, box.offset, box.isTrigger));
            Object.DestroyImmediate(box, true);
        }

        Rigidbody2D body2D = go.GetComponent<Rigidbody2D>();
        bool hadBody = body2D != null;
        bool kinematic = false;
        float mass = 1f;
        float damping = 0f;
        bool interpolate = false;
        bool continuous = false;
        if (hadBody)
        {
            kinematic = body2D.bodyType != RigidbodyType2D.Dynamic;
            mass = body2D.mass;
            damping = body2D.linearDamping;
            interpolate = body2D.interpolation == RigidbodyInterpolation2D.Interpolate;
            continuous = body2D.collisionDetectionMode == CollisionDetectionMode2D.Continuous;
            Object.DestroyImmediate(body2D, true);
        }

        float band = GroundPlane.ColliderTop - GroundPlane.ColliderBottom;
        float bandCenter = (GroundPlane.ColliderTop + GroundPlane.ColliderBottom) * 0.5f;

        foreach (var circle in circles)
        {
            CapsuleCollider capsule = go.AddComponent<CapsuleCollider>();
            capsule.direction = 1; // Y
            capsule.radius = circle.radius;
            capsule.height = band;
            capsule.center = new Vector3(circle.offset.x, bandCenter, circle.offset.y);
            capsule.isTrigger = circle.trigger;
        }

        foreach (var box in boxes)
        {
            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(box.size.x, band, box.size.y);
            collider.center = new Vector3(box.offset.x, bandCenter, box.offset.y);
            collider.isTrigger = box.trigger;
        }

        if (hadBody)
        {
            Rigidbody body = go.AddComponent<Rigidbody>();
            body.isKinematic = kinematic;
            body.useGravity = false;
            body.mass = mass;
            body.linearDamping = damping;
            body.interpolation = interpolate
                ? RigidbodyInterpolation.Interpolate
                : RigidbodyInterpolation.None;
            body.collisionDetectionMode = continuous
                ? CollisionDetectionMode.ContinuousSpeculative
                : CollisionDetectionMode.Discrete;
            body.constraints = kinematic
                ? RigidbodyConstraints.FreezeRotation
                : RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        }

        return circles.Count > 0 || boxes.Count > 0 || hadBody;
    }

    [MenuItem("Tools/World Flip/Migrate Game Scene")]
    public static void MigrateGameScene()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            "Assets/Scenes/Game.unity"
        );

        // Camera: same view of the world, rotated with it.
        GameObject camera = GameObject.Find("Main Camera");
        camera.transform.position = new Vector3(0f, 8f, -13.5f);
        camera.transform.rotation = Quaternion.Euler(30f, 0f, 0f);

        // Player physics body and collider.
        GameObject player = GameObject.Find("Player");
        ConvertPhysics(player);
        Object.DestroyImmediate(
            player.GetComponent<UnityEngine.Rendering.Universal.ShadowCaster2D>()
        );

        // Player children authored against the old axes.
        ReframeLocal(player.transform.Find("DayLight2D-3"));
        ReframeLocal(player.transform.Find("Light"));
        ReframeLocal(player.transform.Find("UpperAreaLight"));

        Transform playerModel = player.transform.Find("YawPivot/HopPivot/PitchPivot/player-0");
        playerModel.localPosition = Frame * playerModel.localPosition;
        playerModel.localRotation = Frame * playerModel.localRotation;

        // Background lies flat on the new ground plane.
        GameObject background = GameObject.Find("Mock-up dirt bg");
        background.transform.rotation = Frame * background.transform.rotation;

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        Debug.Log("[WorldFlip] Game scene migrated");
    }

    private static void ReframeLocal(Transform transform)
    {
        if (transform == null)
            return;
        transform.localPosition = Frame * transform.localPosition;
        transform.localRotation = Frame * transform.localRotation;
    }
}
