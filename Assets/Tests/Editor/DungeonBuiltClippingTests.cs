using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Regression guard against pieces clipping into a shared, fighting surface.
/// The kit assembles by interpenetration: slabs cross inside junction posts,
/// gates swallow wall ends inside their own posts, seam caps hide inside butt
/// joints. That is fine, because none of it is ever seen. What flickers is
/// two different pieces drawing the same plane - the shimmer at the base of
/// crossing wall runs was their identical floor decals fighting - which is
/// why walls and gates are seated at slightly different heights. These tests
/// pin that invariant from two directions: no shared up-facing area at all,
/// and nothing coplanar anywhere else unless the joint is sealed or buried.
/// </summary>
public sealed class DungeonBuiltClippingTests
{
    private const string WallPrefabPath = "Assets/Prefabs/Dungeon/DungeonWall.prefab";
    private const string GatePrefabPath = "Assets/Prefabs/Dungeon/DungeonGateOpen.prefab";
    private const string ColumnPrefabPath = "Assets/Prefabs/Dungeon/DungeonColumn.prefab";
    private const string FloorPrefabPath = "Assets/Prefabs/Dungeon/DungeonFloor.prefab";

    /// <summary>Faces this close to one plane count as coplanar. Well under
    /// the 0.002 seating steps that keep separated planes apart.</summary>
    private const float PlaneStep = 0.0005f;

    /// <summary>
    /// Overlap smaller than this is a shared boundary edge, not shared area:
    /// abutting floor tiles and wall pieces meet exactly at zero.
    /// </summary>
    private const float OverlapAreaLimit = 1e-3f;

    private GameObject host;
    private GameObject root;
    private DungeonRoomBuilder builder;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("BuilderHost");
        root = new GameObject("BuiltRoom");
        builder = host.AddComponent<DungeonRoomBuilder>();
        var serialized = new SerializedObject(builder);
        SetPrefab(serialized, "wallPrefab", WallPrefabPath);
        SetPrefab(serialized, "gateOpenPrefab", GatePrefabPath);
        SetPrefab(serialized, "junctionPostPrefab", ColumnPrefabPath);
        SetPrefab(serialized, "floorPrefab", FloorPrefabPath);
        serialized.ApplyModifiedProperties();
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null)
            Object.DestroyImmediate(root);
        if (host != null)
            Object.DestroyImmediate(host);
    }

    /// <summary>
    /// The strict guard for what the top-down camera looks at: no two pieces
    /// may share up-facing area on one plane, full stop. Up-facing surfaces
    /// have nothing above them to hide behind, so there is no sealed-joint
    /// allowance here - this is the test that fails when a seating height
    /// regresses and floor decals or ledges start fighting again.
    /// </summary>
    [Test]
    public void BuiltPiecesNeverShareCoplanarUpFacingSurface()
    {
        ForEachBuiltRoom(
            (seed, room) =>
            {
                List<string> clashes = CoplanarUpFacingOverlaps(root.transform);
                Assert.That(
                    clashes,
                    Is.Empty,
                    $"seed {seed}: room {room} built pieces sharing coplanar up-facing "
                        + "surface, which z-fights:\n" + string.Join("\n", clashes)
                );
            }
        );
    }

    /// <summary>
    /// The broad guard for every other orientation: pieces may interpenetrate,
    /// but every coplanar contact must be a sealed butt joint or buried inside
    /// a piece's body. Anything else is exposed and fights - a piece built
    /// twice, a run misplaced onto another's face, a frame whose shell lands
    /// exactly on a neighbour's cap.
    /// </summary>
    [Test]
    public void BuiltPiecesOnlyShareCoplanarSurfaceInsideSealedJoints()
    {
        ForEachBuiltRoom(
            (seed, room) =>
            {
                List<string> clashes = ExposedCoplanarOverlaps(root.transform);
                Assert.That(
                    clashes,
                    Is.Empty,
                    $"seed {seed}: room {room} built pieces sharing exposed coplanar "
                        + "surface, which z-fights:\n" + string.Join("\n", clashes)
                );
            }
        );
    }

    private void ForEachBuiltRoom(System.Action<int, Vector2Int> check)
    {
        foreach (int seed in DungeonGeometryModel.Seeds)
        {
            var layout = new DungeonLayout(seed);
            foreach (Vector2Int room in Rooms())
            {
                DungeonLayout.RoomArchetype archetype = layout.Archetype(room);
                builder.BuildFloor(root.transform, room, archetype, new System.Random(seed));
                builder.BuildInterior(root.transform, room, archetype);
                for (int direction = 0; direction < 4; direction++)
                {
                    DungeonEdge edge = DungeonLayout.EdgeBetween(room, direction);
                    builder.BuildEdge(
                        root.transform,
                        edge,
                        layout.Passage(edge, layout.IsDoorOpen(room, direction))
                    );
                }

                check(seed, room);

                // Collect first: destroying while enumerating a Transform skips
                // siblings and would leave the next room dirty.
                var spent = new List<GameObject>();
                foreach (Transform child in root.transform)
                    spent.Add(child.gameObject);
                foreach (GameObject child in spent)
                    Object.DestroyImmediate(child);
            }
        }
    }

    /// <summary>
    /// Every pair of prefab instances under <paramref name="parent"/> whose
    /// rendered meshes share up-facing area on one horizontal plane.
    /// </summary>
    public static List<string> CoplanarUpFacingOverlaps(Transform parent)
    {
        return CoplanarOverlaps(parent, upFacingOnly: true);
    }

    /// <summary>
    /// Every pair of prefab instances under <paramref name="parent"/> whose
    /// rendered meshes share same-facing area on one plane of any orientation,
    /// except contact no camera can see:
    /// - a sealed butt joint, where the two bodies lie on opposite sides of
    ///   the plane and their caps close the seam between them;
    /// - a patch buried inside a piece whose body continues past both sides
    ///   of the plane, such as a wall cap swallowed by a crossing slab or a
    ///   gate post, or the moulding contact inside a junction's column;
    /// - down-facing faces at floor level, which nothing is ever under.
    /// </summary>
    public static List<string> ExposedCoplanarOverlaps(Transform parent)
    {
        return CoplanarOverlaps(parent, upFacingOnly: false);
    }

    private static List<string> CoplanarOverlaps(Transform parent, bool upFacingOnly)
    {
        var pieces = new List<BuiltPiece>();
        CollectPieces(parent, pieces);

        var byPlane = new Dictionary<(int, int, int, int), List<(int piece, int triangle)>>();
        for (int piece = 0; piece < pieces.Count; piece++)
        {
            IReadOnlyList<Vector3> corners = pieces[piece].Corners;
            for (int triangle = 0; triangle * 3 < corners.Count; triangle++)
            {
                Vector3 normal = pieces[piece].Normals[triangle];
                if (upFacingOnly && normal.y < 0.999f)
                    continue;

                float distance = Vector3.Dot(normal, corners[triangle * 3]);
                var plane = (
                    Mathf.RoundToInt(normal.x * 200f),
                    Mathf.RoundToInt(normal.y * 200f),
                    Mathf.RoundToInt(normal.z * 200f),
                    Mathf.RoundToInt(distance / PlaneStep)
                );
                if (!byPlane.TryGetValue(plane, out var triangles))
                    byPlane[plane] = triangles = new List<(int, int)>();
                triangles.Add((piece, triangle));
            }
        }

        var clashes = new SortedDictionary<string, float>();
        foreach (var triangles in byPlane.Values)
        {
            if (triangles.Count < 2)
                continue;

            (int firstPiece, int firstTriangle) = triangles[0];
            Vector3 normal = pieces[firstPiece].Normals[firstTriangle];
            float distance = Vector3.Dot(
                normal,
                pieces[firstPiece].Corners[firstTriangle * 3]
            );
            if (!upFacingOnly && normal.y < -0.95f && Mathf.Abs(distance) <= 0.011f)
                continue;

            Vector3 right = Vector3.Normalize(
                Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right)
            );
            Vector3 up = Vector3.Cross(normal, right);

            for (int i = 0; i < triangles.Count; i++)
            {
                for (int j = i + 1; j < triangles.Count; j++)
                {
                    (int pieceA, int triangleA) = triangles[i];
                    (int pieceB, int triangleB) = triangles[j];
                    if (pieceA == pieceB)
                        continue;
                    if (!upFacingOnly && SealedOrBuried(pieces[pieceA], pieces[pieceB], normal, distance))
                        continue;

                    float area = OverlapArea(
                        Project(pieces[pieceA].Corners, triangleA, right, up),
                        Project(pieces[pieceB].Corners, triangleB, right, up)
                    );
                    if (area <= OverlapAreaLimit)
                        continue;

                    Vector3 at = pieces[pieceA].Corners[triangleA * 3];
                    string clash =
                        $"{pieces[pieceA].Name} and {pieces[pieceB].Name} on plane "
                        + $"n={normal:F1} near ({at.x:F1}, {at.y:F1}, {at.z:F1})";
                    clashes.TryGetValue(clash, out float total);
                    clashes[clash] = total + area;
                }
            }
        }

        var report = new List<string>();
        foreach (KeyValuePair<string, float> clash in clashes)
            report.Add($"{clash.Key}, overlapping {clash.Value:F3} m^2");
        return report;
    }

    /// <summary>
    /// Whether coplanar contact between these two pieces on this plane is a
    /// sealed butt joint or buried inside one piece's body, and so never seen.
    /// </summary>
    private static bool SealedOrBuried(
        BuiltPiece a,
        BuiltPiece b,
        Vector3 normal,
        float distance
    )
    {
        (float minA, float maxA) = a.SideRange(normal, distance);
        (float minB, float maxB) = b.SideRange(normal, distance);

        const float seam = 0.005f;
        const float body = 0.01f;
        bool sealedJoint =
            (maxA <= seam && minB >= -seam && minA < -body && maxB > body)
            || (maxB <= seam && minA >= -seam && minB < -body && maxA > body);
        bool buried = (minA < -body && maxA > body) || (minB < -body && maxB > body);
        return sealedJoint || buried;
    }

    /// <summary>One prefab instance's rendered triangles in world space.</summary>
    private sealed class BuiltPiece
    {
        public string Name;
        public readonly List<Vector3> Corners = new();
        public readonly List<Vector3> Normals = new();
        private Bounds bounds;

        public void Add(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude < 1e-12f)
                return;
            if (Corners.Count == 0)
                bounds = new Bounds(a, Vector3.zero);
            bounds.Encapsulate(a);
            bounds.Encapsulate(b);
            bounds.Encapsulate(c);
            Corners.Add(a);
            Corners.Add(b);
            Corners.Add(c);
            Normals.Add(normal.normalized);
        }

        /// <summary>How far this piece's mesh reaches to either side of a
        /// plane, as signed distances along its normal.</summary>
        public (float min, float max) SideRange(Vector3 normal, float distance)
        {
            float center = Vector3.Dot(normal, bounds.center) - distance;
            float reach =
                Mathf.Abs(normal.x) * bounds.extents.x
                + Mathf.Abs(normal.y) * bounds.extents.y
                + Mathf.Abs(normal.z) * bounds.extents.z;
            return (center - reach, center + reach);
        }
    }

    private static void CollectPieces(Transform parent, List<BuiltPiece> pieces)
    {
        var byInstance = new Dictionary<Transform, BuiltPiece>();
        foreach (MeshFilter filter in parent.GetComponentsInChildren<MeshFilter>())
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null)
                continue;

            Transform meshTransform = filter.transform;
            Transform instance = meshTransform;
            while (instance.parent != null && !instance.name.EndsWith("(Clone)"))
                instance = instance.parent;
            if (!byInstance.TryGetValue(instance, out BuiltPiece piece))
            {
                byInstance[instance] = piece = new BuiltPiece
                {
                    Name = $"{instance.name} at {instance.position:F2}",
                };
                pieces.Add(piece);
            }

            Vector3[] vertices = mesh.vertices;
            int[] indices = mesh.triangles;
            for (int i = 0; i < indices.Length; i += 3)
            {
                piece.Add(
                    meshTransform.TransformPoint(vertices[indices[i]]),
                    meshTransform.TransformPoint(vertices[indices[i + 1]]),
                    meshTransform.TransformPoint(vertices[indices[i + 2]])
                );
            }
        }
    }

    private static Vector2[] Project(
        IReadOnlyList<Vector3> corners,
        int triangle,
        Vector3 right,
        Vector3 up
    )
    {
        var projected = new Vector2[3];
        for (int i = 0; i < 3; i++)
        {
            Vector3 corner = corners[triangle * 3 + i];
            projected[i] = new Vector2(Vector3.Dot(corner, right), Vector3.Dot(corner, up));
        }
        // Same-facing triangles project with one winding; normalize it so the
        // clipper's inside test is consistent.
        if (SignedArea(projected) < 0f)
            (projected[0], projected[2]) = (projected[2], projected[0]);
        return projected;
    }

    /// <summary>
    /// The in-plane area two triangles cover together, by clipping one
    /// against the other's edges.
    /// </summary>
    private static float OverlapArea(Vector2[] first, Vector2[] second)
    {
        var polygon = new List<Vector2>(first);
        var clipped = new List<Vector2>(6);
        for (int edge = 0; edge < 3 && polygon.Count > 2; edge++)
        {
            Vector2 from = second[edge];
            Vector2 to = second[(edge + 1) % 3];
            clipped.Clear();
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 current = polygon[i];
                Vector2 previous = polygon[(i + polygon.Count - 1) % polygon.Count];
                float currentSide = Cross(from, to, current);
                float previousSide = Cross(from, to, previous);
                if (currentSide >= 0f)
                {
                    if (previousSide < 0f)
                        clipped.Add(Intersect(previous, current, previousSide, currentSide));
                    clipped.Add(current);
                }
                else if (previousSide >= 0f)
                {
                    clipped.Add(Intersect(previous, current, previousSide, currentSide));
                }
            }
            (polygon, clipped) = (clipped, polygon);
        }
        return polygon.Count > 2 ? Mathf.Abs(SignedArea(polygon)) : 0f;
    }

    private static float Cross(Vector2 from, Vector2 to, Vector2 point)
    {
        return (to.x - from.x) * (point.y - from.y) - (to.y - from.y) * (point.x - from.x);
    }

    private static Vector2 Intersect(Vector2 from, Vector2 to, float fromSide, float toSide)
    {
        return from + (to - from) * (fromSide / (fromSide - toSide));
    }

    private static float SignedArea(IReadOnlyList<Vector2> polygon)
    {
        float doubled = 0f;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 current = polygon[i];
            Vector2 next = polygon[(i + 1) % polygon.Count];
            doubled += current.x * next.y - next.x * current.y;
        }
        return doubled / 2f;
    }

    private static void SetPrefab(SerializedObject serialized, string field, string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null, path);
        serialized.FindProperty(field).objectReferenceValue = prefab;
    }

    private static IEnumerable<Vector2Int> Rooms()
    {
        yield return Vector2Int.zero;
        yield return new Vector2Int(1, 0);
        yield return new Vector2Int(0, 1);
        yield return new Vector2Int(-2, 3);
        yield return new Vector2Int(5, -4);
    }
}
