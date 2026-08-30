using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// What a prop prefab actually measures, worked out from its meshes and its
    /// colliders rather than from its name.
    ///
    /// Placement needs three numbers from every prop: how much floor it takes up,
    /// how tall it stands, and where its base sits relative to its pivot. Reading
    /// them off the asset is what lets the placer treat a prop nobody has seen
    /// before exactly like the ones it was written against.
    /// </summary>
    public readonly struct DungeonPropMeasurement
    {
        /// <summary>The floor a prop with no measurable geometry is given.</summary>
        public const float FallbackRadius = 0.5f;

        /// <summary>
        /// The footprint radius at which a prop is spaced as a large obstacle.
        /// Measured, so a new large prop is spaced correctly the first time it is
        /// placed instead of when someone remembers to classify it.
        /// </summary>
        public const float LargeRadius = 1.1f;

        /// <summary>
        /// How tall a wide prop has to stand before it is spaced as an obstacle.
        /// A floor plate or a puddle covers a lot of ground without being anything
        /// to walk around, so width alone would space them as though they were.
        /// Roughly knee height on this game's scale.
        /// </summary>
        public const float LargeHeight = 0.5f;

        /// <summary>How far from the pivot the prop reaches across the ground.</summary>
        public readonly float Radius;

        /// <summary>How tall the prop stands.</summary>
        public readonly float Height;

        /// <summary>Where the prop's base sits relative to its pivot.</summary>
        public readonly float BaseOffset;

        public DungeonPropMeasurement(float radius, float height, float baseOffset)
        {
            Radius = radius;
            Height = height;
            BaseOffset = baseOffset;
        }

        /// <summary>
        /// Whether this prop is spaced as a large obstacle: broad enough to block
        /// a lane and tall enough that a player would go around it rather than
        /// over it.
        /// </summary>
        public bool IsLarge => Radius >= LargeRadius && Height >= LargeHeight;

        /// <summary>
        /// Measures a prefab. Every mesh contributes, and so does every collider
        /// shape - a prop authored with a capsule, sphere, or mesh collider is
        /// measured like one authored with a box.
        /// </summary>
        public static DungeonPropMeasurement Of(GameObject prefab)
        {
            if (prefab == null)
                return new DungeonPropMeasurement(FallbackRadius, 0f, 0f);

            Transform root = prefab.transform;
            float radiusSquared = FallbackRadius * FallbackRadius;
            float minimumY = float.PositiveInfinity;
            float maximumY = float.NegativeInfinity;

            foreach (MeshFilter meshFilter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter.sharedMesh == null)
                    continue;
                Include(
                    root,
                    meshFilter.transform,
                    meshFilter.sharedMesh.bounds,
                    ref radiusSquared,
                    ref minimumY,
                    ref maximumY
                );
            }

            foreach (Collider collider in prefab.GetComponentsInChildren<Collider>(true))
            {
                if (collider.isTrigger || !TryLocalBounds(collider, out Bounds local))
                    continue;
                Include(
                    root,
                    collider.transform,
                    local,
                    ref radiusSquared,
                    ref minimumY,
                    ref maximumY
                );
            }

            bool measured = maximumY > minimumY;
            return new DungeonPropMeasurement(
                Mathf.Sqrt(radiusSquared),
                measured ? maximumY - minimumY : 0f,
                measured ? minimumY : 0f
            );
        }

        /// <summary>
        /// A collider's extent in its own local space. Unity gives world bounds
        /// only for colliders in a scene, and these are read off prefab assets.
        /// </summary>
        private static bool TryLocalBounds(Collider collider, out Bounds bounds)
        {
            switch (collider)
            {
                case BoxCollider box:
                    bounds = new Bounds(box.center, box.size);
                    return true;
                case SphereCollider sphere:
                    bounds = new Bounds(sphere.center, Vector3.one * (sphere.radius * 2f));
                    return true;
                case CapsuleCollider capsule:
                    float diameter = capsule.radius * 2f;
                    var size = new Vector3(diameter, diameter, diameter);
                    size[Mathf.Clamp(capsule.direction, 0, 2)] = Mathf.Max(
                        capsule.height,
                        diameter
                    );
                    bounds = new Bounds(capsule.center, size);
                    return true;
                case MeshCollider mesh when mesh.sharedMesh != null:
                    bounds = mesh.sharedMesh.bounds;
                    return true;
                default:
                    bounds = default;
                    return false;
            }
        }

        private static void Include(
            Transform root,
            Transform owner,
            Bounds local,
            ref float radiusSquared,
            ref float minimumY,
            ref float maximumY
        )
        {
            Matrix4x4 toRoot = root.worldToLocalMatrix * owner.localToWorldMatrix;
            Vector3 min = local.min;
            Vector3 max = local.max;
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
            {
                Vector3 point = toRoot.MultiplyPoint3x4(
                    new Vector3(
                        x == 0 ? min.x : max.x,
                        y == 0 ? min.y : max.y,
                        z == 0 ? min.z : max.z
                    )
                );
                radiusSquared = Mathf.Max(radiusSquared, point.x * point.x + point.z * point.z);
                minimumY = Mathf.Min(minimumY, point.y);
                maximumY = Mathf.Max(maximumY, point.y);
            }
        }
    }
}
