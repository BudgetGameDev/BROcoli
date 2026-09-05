using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class DungeonBuiltClippingTests
    {
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
    }
}
