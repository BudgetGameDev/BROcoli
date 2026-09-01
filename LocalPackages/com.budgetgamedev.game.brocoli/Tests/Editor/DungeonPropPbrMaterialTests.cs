using System.Collections.Generic;
using System.Linq;
using BudgetGameDev.Games.Brocoli.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// Keeps the props wearing the stylized PBR materials the floor and walls
    /// use.
    ///
    /// The props ship painted from a colour atlas: one sub-mesh, UVs pointing at
    /// a few palette pixels. A prop in that state cannot carry a tiling wood or
    /// steel map, and re-importing a kit or re-saving a prefab from an older
    /// scene is enough to put one back that way - which looks like nothing more
    /// than a slightly flat barrel until someone stands next to the wall.
    /// <see cref="DungeonPropPbrBaker"/> rebuilds them; these fail when a prop
    /// misses the bake.
    /// </summary>
    public sealed class DungeonPropPbrMaterialTests
    {
        /// <summary>
        /// Props allowed to keep their palette material. The potion is glass and
        /// coloured liquid, which the flat palette reads better than any tiling
        /// map, and the water prop is no longer placed in the dungeon.
        /// </summary>
        private static readonly string[] PaletteProps = { "DungeonPotion", "DungeonWater" };

        private static readonly string[] PaletteMaterials =
        {
            "DungeonProps",
            "DungeonKit",
            "DungeonChestGold",
        };

        [Test]
        public void EveryBakedPropWearsAStylizedPbrMaterial()
        {
            foreach (
                KeyValuePair<
                    string,
                    DungeonPropPbrBaker.Recipe
                > entry in DungeonPropPbrBaker.Recipes
            )
            {
                foreach (MeshRenderer renderer in RenderersOf(entry.Key))
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        Assert.That(
                            material,
                            Is.Not.Null,
                            $"{entry.Key} has an empty material slot, so part of it renders magenta"
                        );
                        Assert.That(
                            PaletteMaterials,
                            Does.Not.Contain(material.name),
                            $"{entry.Key} is still painted from the flat colour atlas, so it "
                                + "sits in a PBR room looking like a different game"
                        );
                    }
                }
            }
        }

        /// <summary>
        /// No prop is left behind. A prop the bake does not know about keeps its
        /// palette material, and only the ones listed above may.
        /// </summary>
        [Test]
        public void OnlyTheAllowedPropsKeepTheColourAtlas()
        {
            foreach (
                string guid in AssetDatabase.FindAssets(
                    "t:Prefab",
                    new[] { DungeonPropPbrBaker.PrefabFolder }
                )
            )
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (PaletteProps.Contains(name))
                    continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (
                    MeshRenderer renderer in prefab.GetComponentsInChildren<MeshRenderer>(true)
                )
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        Assert.That(
                            PaletteMaterials,
                            Does.Not.Contain(material == null ? string.Empty : material.name),
                            $"{name} was never given a PBR material, so it stayed flat while "
                                + "the room around it did not"
                        );
                    }
                }
            }
        }

        /// <summary>
        /// A renderer paints one material per sub-mesh. A mismatch renders the
        /// spare sub-mesh with whatever material sits last in the array, which
        /// is how a barrel ends up with steel staves.
        /// </summary>
        [Test]
        public void EachSubMeshHasItsOwnMaterial()
        {
            foreach (string prop in DungeonPropPbrBaker.Recipes.Keys)
            {
                foreach (MeshRenderer renderer in RenderersOf(prop))
                {
                    Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    Assert.That(
                        renderer.sharedMaterials.Length,
                        Is.EqualTo(mesh.subMeshCount),
                        $"{prop}/{renderer.name} has {mesh.subMeshCount} sub-meshes and "
                            + $"{renderer.sharedMaterials.Length} materials"
                    );
                }
            }
        }

        /// <summary>
        /// The bake projects UVs from object-space position, so every vertex's V
        /// is one of its own coordinates in metres. Atlas UVs are palette
        /// look-ups that match nothing, so this is what tells the two apart.
        /// </summary>
        [Test]
        public void BakedMeshesCarryProjectedUvs()
        {
            foreach (string prop in DungeonPropPbrBaker.Recipes.Keys)
            {
                foreach (MeshRenderer renderer in RenderersOf(prop))
                {
                    Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    Vector3[] positions = mesh.vertices;
                    Vector2[] uvs = mesh.uv;
                    Assert.That(uvs.Length, Is.EqualTo(positions.Length), $"{prop} lost its UVs");

                    for (int i = 0; i < positions.Length; i++)
                    {
                        Assert.That(
                            IsCoordinate(uvs[i].y, positions[i]),
                            $"{prop}/{renderer.name} vertex {i} still carries an atlas UV "
                                + $"({uvs[i]}), so its material tiles across a palette swatch"
                        );
                    }
                }
            }
        }

        /// <summary>
        /// The headline case: a barrel is wooden staves inside steel bands, and
        /// it takes two materials on one mesh to say so.
        /// </summary>
        [Test]
        public void TheBarrelKeepsWoodStavesAndSteelBands()
        {
            MeshRenderer renderer = RenderersOf("DungeonBarrel").Single();
            string[] materials = renderer
                .sharedMaterials.Select(material => material.name)
                .ToArray();
            Assert.That(materials, Does.Contain("DungeonPropWood"));
            Assert.That(materials, Does.Contain("DungeonPropSteel"));
        }

        private static bool IsCoordinate(float value, Vector3 position)
        {
            const float tolerance = 1e-4f;
            return Mathf.Abs(value - position.x) < tolerance
                || Mathf.Abs(value - position.y) < tolerance
                || Mathf.Abs(value - position.z) < tolerance;
        }

        private static IEnumerable<MeshRenderer> RenderersOf(string prop)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{DungeonPropPbrBaker.PrefabFolder}/{prop}.prefab"
            );
            Assert.That(prefab, Is.Not.Null, $"{prop} is missing from the prop folder");
            return prefab
                .GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => renderer.GetComponent<MeshFilter>() != null);
        }
    }
}
