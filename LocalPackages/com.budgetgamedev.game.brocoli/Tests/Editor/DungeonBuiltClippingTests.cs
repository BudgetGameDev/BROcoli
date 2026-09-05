using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BudgetGameDev.Games.Brocoli;
using BudgetGameDev.Games.Brocoli.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// Regression guard against pieces clipping into a shared, fighting surface.
    /// The kit assembles by interpenetration: crossing slabs bury each other's
    /// end caps, gates swallow wall ends inside their own posts, seam caps hide
    /// inside butt joints. That is fine, because none of it is ever seen. What flickers is
    /// two different pieces drawing the same plane - the shimmer at the base of
    /// crossing wall runs was their identical floor decals fighting - which is
    /// why walls and gates are seated at slightly different heights. These tests
    /// pin that invariant from two directions: no shared up-facing area at all,
    /// and nothing coplanar anywhere else unless the joint is sealed or buried.
    /// </summary>
    public sealed partial class DungeonBuiltClippingTests
    {
        private const string WallPrefabPath =
            "Packages/com.budgetgamedev.game.brocoli/Prefabs/Dungeon/DungeonWall.prefab";
        private const string GatePrefabPath =
            "Packages/com.budgetgamedev.game.brocoli/Prefabs/Dungeon/DungeonGateOpen.prefab";
        private const string FloorPrefabPath =
            "Packages/com.budgetgamedev.game.brocoli/Prefabs/Dungeon/DungeonFloor.prefab";

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
                            + "surface, which z-fights:\n"
                            + string.Join("\n", clashes)
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
                            + "surface, which z-fights:\n"
                            + string.Join("\n", clashes)
                    );
                }
            );
        }

        [Test]
        public void RegularWallPrefabDoesNotCarryTheSourceFloorApron()
        {
            var wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallPrefabPath);
            MeshFilter filter = wallPrefab.GetComponentInChildren<MeshFilter>();
            Matrix4x4 modelToWall =
                wallPrefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            float minimumDepth = float.PositiveInfinity;
            foreach (Vector3 vertex in filter.sharedMesh.vertices)
                minimumDepth = Mathf.Min(minimumDepth, modelToWall.MultiplyPoint3x4(vertex).z);

            Assert.That(
                minimumDepth,
                Is.GreaterThan(0.3f),
                "the regular wall still projects an orange floor apron behind its slab"
            );
        }

        [Test]
        public void FloorAndWallUseOneCompleteStylizedPbrSet()
        {
            var wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallPrefabPath);
            var floorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FloorPrefabPath);
            MeshFilter filter = wallPrefab.GetComponentInChildren<MeshFilter>();
            Material wallMaterial = wallPrefab
                .GetComponentInChildren<MeshRenderer>()
                .sharedMaterial;
            Material floorMaterial = floorPrefab
                .GetComponentInChildren<MeshRenderer>()
                .sharedMaterial;

            Assert.That(filter.sharedMesh.name, Is.EqualTo("DungeonWallMasonry"));
            AssertCompletePbrMaterial(wallMaterial, "DungeonStylizedBrickWall");
            AssertCompletePbrMaterial(floorMaterial, "DungeonStylizedStoneFloor");
            AssertPbrGraphWiring(wallMaterial.shader);

            Vector2[] uv = filter.sharedMesh.uv;
            Assert.That(uv, Has.Length.EqualTo(filter.sharedMesh.vertexCount));
            float horizontalSpan = 0f;
            foreach (Vector2 point in uv)
                horizontalSpan = Mathf.Max(horizontalSpan, Mathf.Abs(point.x));
            Assert.That(
                horizontalSpan,
                Is.GreaterThan(0.5f),
                "the masonry UVs collapsed back into the source model's color atlas"
            );
        }

        private static void AssertCompletePbrMaterial(Material material, string expectedName)
        {
            Assert.That(material, Is.Not.Null);
            Assert.That(material.name, Is.EqualTo(expectedName));
            Assert.That(material.shader.name, Is.EqualTo(BrocoliShaders.Surface));
            Assert.That(material.GetTexture("_BaseMap"), Is.Not.Null);
            Assert.That(material.GetTexture("_BumpMap"), Is.Not.Null);
            Assert.That(material.GetTexture("_OcclusionMap"), Is.Not.Null);
            Assert.That(material.GetTexture("_ParallaxMap"), Is.Not.Null);
            Assert.That(material.GetTexture("_MetallicGlossMap"), Is.Not.Null);
        }

        private static void AssertPbrGraphWiring(Shader shader)
        {
            // Surface is a Shader Graph, which samples its maps unconditionally.
            // Stock URP/Lit material keywords do not enable these graph branches.
            string path = AssetDatabase.GetAssetPath(shader);
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
            string physicalPath = Path.Combine(
                package.resolvedPath,
                path.Substring(package.assetPath.Length + 1)
            );
            var objects = Regex
                .Split(File.ReadAllText(physicalPath), @"(?m)(?=^\{)")
                .Where(json => !string.IsNullOrWhiteSpace(json))
                .Select(JsonUtility.FromJson<PbrGraphObject>)
                .ToDictionary(node => node.m_ObjectId);
            PbrGraphEdge[] edges = objects.Values.Single(node => node.m_Edges != null).m_Edges;
            foreach (
                var contract in new[]
                {
                    ("_BaseMap", "BaseColor"),
                    ("_BumpMap", "NormalTS"),
                    ("_OcclusionMap", "Occlusion"),
                    ("_MetallicGlossMap", "Metallic"),
                    ("_MetallicGlossMap", "Smoothness"),
                }
            )
            {
                string property = objects
                    .Values.Single(node => node.m_OverrideReferenceName == contract.Item1)
                    .m_ObjectId;
                PbrGraphObject source = objects.Values.Single(node =>
                    node.m_Property?.m_Id == property
                );
                var pending = new Queue<(string id, bool sampled)>();
                var visited = new HashSet<(string id, bool sampled)>();
                pending.Enqueue((source.m_ObjectId, false));
                bool connected = false;
                while (pending.Count > 0)
                {
                    var current = pending.Dequeue();
                    if (!visited.Add(current))
                        continue;
                    PbrGraphObject node = objects[current.id];
                    bool sampled =
                        current.sampled
                        || node.m_Type == "UnityEditor.ShaderGraph.SampleTexture2DNode";
                    if (
                        sampled
                        && node.m_SerializedDescriptor == "SurfaceDescription." + contract.Item2
                    )
                        connected = true;
                    foreach (
                        PbrGraphEdge edge in edges.Where(edge =>
                            edge.m_OutputSlot.m_Node.m_Id == current.id
                        )
                    )
                        pending.Enqueue((edge.m_InputSlot.m_Node.m_Id, sampled));
                }
                Assert.That(
                    connected,
                    Is.True,
                    $"{contract.Item1} must be sampled into {contract.Item2}"
                );
            }
        }

        [System.Serializable]
        private sealed class PbrGraphObject
        {
            public string m_ObjectId = null;
            public string m_Type = null;
            public string m_OverrideReferenceName = null;
            public string m_SerializedDescriptor = null;
            public PbrGraphReference m_Property = null;
            public PbrGraphEdge[] m_Edges = null;
        }

        [System.Serializable]
        private sealed class PbrGraphReference
        {
            public string m_Id = null;
        }

        [System.Serializable]
        private sealed class PbrGraphSlot
        {
            public PbrGraphReference m_Node = null;
        }

        [System.Serializable]
        private sealed class PbrGraphEdge
        {
            public PbrGraphSlot m_OutputSlot = null;
            public PbrGraphSlot m_InputSlot = null;
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
}
