using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Editor
{
    /// <summary>
    /// Rebuilds the Kenney dungeon props so they can wear the stylized PBR
    /// materials the floor and walls use.
    ///
    /// The source props are painted from one colour atlas: every prop is a
    /// single sub-mesh whose UVs point at a few pixels of a palette, so a
    /// tiling wood or steel texture cannot be applied to them as they ship, and
    /// the staves and the bands of a barrel cannot be told apart by material.
    ///
    /// This bake reads the palette colour behind each triangle, sorts the
    /// triangles into material groups from <see cref="Recipes"/>, and projects
    /// fresh object-space UVs measured in metres so one texel density holds
    /// across every prop. The result is written next to the other generated
    /// meshes and the prefab is repointed at it.
    /// </summary>
    public static partial class DungeonPropPbrBaker
    {
        public const string PrefabFolder =
            "Packages/com.budgetgamedev.game.brocoli/Prefabs/Dungeon";
        public const string MeshFolder =
            "Packages/com.budgetgamedev.game.brocoli/Models/Dungeon/Props";
        private const string MaterialFolder =
            "Packages/com.budgetgamedev.game.brocoli/Materials/Dungeon";
        private const string ThirdPartyFolder =
            "Packages/com.budgetgamedev.game.brocoli/Resources/Brocoli/ThirdParty";

        [MenuItem("Tools/BROcoli/Bake Dungeon Prop PBR Meshes")]
        public static void BakeAll()
        {
            int baked = 0;
            foreach (KeyValuePair<string, Recipe> entry in Recipes)
                baked += Bake(entry.Key, entry.Value);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Baked PBR meshes for {baked} dungeon prop renderers.");
        }

        /// <summary>
        /// Rebuilds one prefab's renderers. Returns how many were rebuilt, so a
        /// prop that lost its model shows up as a zero rather than silently
        /// keeping its palette material.
        /// </summary>
        public static int Bake(string prefabName, Recipe recipe)
        {
            string prefabPath = $"{PrefabFolder}/{prefabName}.prefab";
            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            if (contents == null)
                return 0;

            int baked = 0;
            try
            {
                foreach (MeshFilter filter in contents.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (renderer == null)
                        continue;
                    if (!recipe.Meshes.TryGetValue(filter.gameObject.name, out string sourceName))
                        continue;

                    // Always bake from the kit's own mesh, so re-running the
                    // tool re-reads the palette instead of chewing on the
                    // result of the last bake.
                    Mesh source = SourceMesh(recipe.Kit, sourceName);
                    Texture2D atlas = Palette(recipe.Kit);
                    if (source == null || atlas == null)
                        continue;

                    var materials = new List<string>();
                    Mesh mesh = BuildMesh(source, atlas, recipe, materials);
                    if (mesh == null)
                        continue;

                    string meshPath = $"{MeshFolder}/{prefabName}_{filter.gameObject.name}.asset";
                    filter.sharedMesh = WriteMesh(mesh, meshPath);
                    renderer.sharedMaterials = LoadMaterials(materials);
                    baked++;
                }

                if (baked > 0)
                    PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            return baked;
        }

        /// <summary>The kit mesh a baked prop mesh was grown from.</summary>
        public static Mesh SourceMesh(string kit, string meshName)
        {
            string[] folder = { $"{ThirdPartyFolder}/{kit}" };
            foreach (string guid in AssetDatabase.FindAssets("t:Mesh", folder))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is Mesh mesh && mesh.name == meshName)
                        return mesh;
                }
            }
            return null;
        }

        public static Texture2D Palette(string kit) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{ThirdPartyFolder}/{kit}/Textures/colormap.png"
            );

        private static Mesh WriteMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            // Overwriting in place keeps the GUID, so prefabs and scenes that
            // already reference the mesh survive a re-bake.
            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        private static Material[] LoadMaterials(List<string> names)
        {
            var materials = new Material[names.Count];
            for (int i = 0; i < names.Count; i++)
            {
                materials[i] = AssetDatabase.LoadAssetAtPath<Material>(
                    $"{MaterialFolder}/{names[i]}.mat"
                );
            }
            return materials;
        }
    }
}
