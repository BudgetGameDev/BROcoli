using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class DungeonBuiltClippingTests
    {
        /// <summary>Faces this close to one plane count as coplanar. Well under
        /// the 0.002 seating steps that keep separated planes apart.</summary>
        private const float PlaneStep = 0.0005f;

        /// <summary>
        /// Overlap smaller than this is a shared boundary edge, not shared area:
        /// abutting floor tiles and wall pieces meet exactly at zero.
        /// </summary>
        private const float OverlapAreaLimit = 1e-3f;

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
        ///   gate post;
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
                float distance = Vector3.Dot(normal, pieces[firstPiece].Corners[firstTriangle * 3]);
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
                        if (
                            !upFacingOnly
                            && SealedOrBuried(pieces[pieceA], pieces[pieceB], normal, distance)
                        )
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
    }
}
