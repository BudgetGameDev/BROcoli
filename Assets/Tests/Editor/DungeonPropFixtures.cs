using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds real rooms out of whatever props the project actually contains, and
/// invents props it does not.
///
/// Nothing here names a prefab. The prop set is read off disk, so swapping the
/// art or adding a prop changes what these tests cover without anyone editing
/// them; and <see cref="NovelProp"/> makes geometry that matches no asset at
/// all, which is the only honest way to check that a prop added after the
/// system was written is handled by it.
/// </summary>
public static class DungeonPropFixtures
{
    public const string PrefabFolder = "Assets/Prefabs/Dungeon";
    public const string WallPrefabPath = PrefabFolder + "/DungeonWall.prefab";
    public const string FloorPrefabPath = PrefabFolder + "/DungeonFloor.prefab";

    /// <summary>Every prop prefab the project ships, whatever they are.</summary>
    public static List<GameObject> AllPrefabs()
    {
        var prefabs = new List<GameObject>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                AssetDatabase.GUIDToAssetPath(guid)
            );
            if (prefab != null)
                prefabs.Add(prefab);
        }
        prefabs.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        Assert.That(prefabs, Is.Not.Empty, $"no prefabs found under {PrefabFolder}");
        return prefabs;
    }

    /// <summary>
    /// A room root that occlusion reads as holding contents rather than as one
    /// object, the way <see cref="DungeonManager"/> builds them.
    /// </summary>
    public static GameObject RoomRoot(string name = "BuiltRoom")
    {
        var root = new GameObject(name);
        root.AddComponent<DungeonContentRoot>();
        return root;
    }

    /// <summary>
    /// A prop of no particular kind: a box of the given size on the wall layer,
    /// named after nothing in the project. This stands in for the prop somebody
    /// adds next year - if the system handles this, it handles that.
    /// </summary>
    public static GameObject NovelProp(Transform parent, Vector3 size, Vector3 position)
    {
        var prop = new GameObject("Utterly Unknown Contraption");
        prop.transform.SetParent(parent, false);
        prop.transform.position = position;

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(prop.transform, false);
        body.transform.localScale = size;
        body.transform.localPosition = new Vector3(0f, size.y / 2f, 0f);
        body.layer = LayerMask.NameToLayer("Wall");
        return prop;
    }

    /// <summary>A prop placer holding every prop the project ships.</summary>
    public static DungeonPropPlacer Placer(GameObject host, IReadOnlyList<GameObject> prefabs)
    {
        DungeonPropPlacer placer = host.AddComponent<DungeonPropPlacer>();
        var serialized = new SerializedObject(placer);
        SerializedProperty array = serialized.FindProperty("propPrefabs");
        array.arraySize = prefabs.Count;
        for (int i = 0; i < prefabs.Count; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
        serialized.ApplyModifiedProperties();
        return placer;
    }

    /// <summary>A room builder that can raise walls and floors.</summary>
    public static DungeonRoomBuilder Builder(GameObject host)
    {
        DungeonRoomBuilder builder = host.AddComponent<DungeonRoomBuilder>();
        var serialized = new SerializedObject(builder);
        Assign(serialized, "wallPrefab", WallPrefabPath);
        Assign(serialized, "floorPrefab", FloorPrefabPath);
        serialized.ApplyModifiedProperties();
        return builder;
    }

    /// <summary>
    /// Every renderer under a root that stands on something solid - the pieces
    /// that can hide a character and so have to fade correctly. Found by
    /// looking for colliders, never by matching names, so it picks up whatever
    /// the generator produced.
    /// </summary>
    public static List<Renderer> OccludingRenderers(GameObject root)
    {
        var found = new List<Renderer>();
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(false))
        {
            if (renderer is ParticleSystemRenderer or TrailRenderer or LineRenderer)
                continue;
            if (HasSolidCollider(renderer.transform) && renderer.bounds.size.y > 0.01f)
                found.Add(renderer);
        }
        return found;
    }

    private static bool HasSolidCollider(Transform piece)
    {
        Transform body = piece.parent != null ? piece.parent : piece;
        foreach (Collider collider in body.GetComponentsInChildren<Collider>())
        {
            if (!collider.isTrigger)
                return true;
        }
        return false;
    }

    private static void Assign(SerializedObject serialized, string field, string assetPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        Assert.That(prefab, Is.Not.Null, assetPath);
        serialized.FindProperty(field).objectReferenceValue = prefab;
    }
}
