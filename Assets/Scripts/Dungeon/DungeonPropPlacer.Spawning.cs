using System.Collections.Generic;
using UnityEngine;

public partial class DungeonPropPlacer
{
    private static readonly List<Collider> SpawnColliders = new();
    private static int cachedWallLayer = -2;

    private readonly Dictionary<GameObject, DungeonPropMeasurement> measurements = new();

    /// <summary>
    /// What a prop measures, worked out once per prefab. Every placement rule
    /// reads its numbers from here, so none of them has to know which prop it
    /// is holding.
    /// </summary>
    private DungeonPropMeasurement Measure(GameObject prefab)
    {
        if (prefab == null)
            return DungeonPropMeasurement.Of(null);
        if (!measurements.TryGetValue(prefab, out DungeonPropMeasurement measurement))
        {
            measurement = DungeonPropMeasurement.Of(prefab);
            measurements[prefab] = measurement;
        }
        return measurement;
    }

    private float FootprintRadius(GameObject prefab)
    {
        return Measure(prefab).Radius;
    }

    /// <summary>
    /// Places one prop and leaves it able to take part in everything the game
    /// does to props. It stands on the floor rather than on its pivot, and
    /// whatever is solid about it is on the layer the camera and the
    /// projectiles search - so a prop added later occludes, stops bullets, and
    /// sits at the right height without anyone remembering to arrange it.
    /// </summary>
    private GameObject SpawnProp(
        Transform parent,
        GameObject prefab,
        Vector2 ground,
        Quaternion rotation,
        float scale = 1f,
        float lift = 0f
    )
    {
        if (prefab == null)
            return null;

        DungeonPropMeasurement measurement = Measure(prefab);
        GameObject prop = Instantiate(
            prefab,
            ground.ToWorld(lift - measurement.BaseOffset * scale),
            rotation,
            parent
        );
        if (!Mathf.Approximately(scale, 1f))
            prop.transform.localScale *= scale;
        EnrolAsOccluder(prop);
        return prop;
    }

    /// <summary>
    /// Puts a prop's solid parts on the wall layer when nobody has said
    /// otherwise. A prop left on the default layer looks right and behaves
    /// wrongly in three separate systems at once, and none of them complains;
    /// an authored layer is left alone, so scenery and triggers keep theirs.
    /// </summary>
    private static void EnrolAsOccluder(GameObject prop)
    {
        if (cachedWallLayer == -2)
            cachedWallLayer = LayerMask.NameToLayer("Wall");
        if (cachedWallLayer < 0)
            return;

        prop.GetComponentsInChildren(true, SpawnColliders);
        foreach (Collider collider in SpawnColliders)
        {
            if (!collider.isTrigger && collider.gameObject.layer == 0)
                collider.gameObject.layer = cachedWallLayer;
        }
        SpawnColliders.Clear();
    }
}
