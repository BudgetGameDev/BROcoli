using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// One thing the camera may be asked to lower - a wall run, an archway, a
    /// barrel, or whatever prop is added to the game next year. An occluder is a
    /// visibility group: everything under it fades together, and which of its
    /// pieces take part is settled by
    /// <see cref="WallVisibilityResolver.IsPieceInTheWay"/>.
    ///
    /// Architecture is grouped deliberately by <see cref="DungeonOcclusionSection"/>,
    /// which derives from this, because a run has to fade as one wall. Everything
    /// else becomes its own group the first time a sight line reaches it, so a prop
    /// occludes without being registered, tagged, named, or listed anywhere. What
    /// an occluder is never enters the decision; only what it measures does.
    /// </summary>
    [DisallowMultipleComponent]
    public class DungeonOccluder : MonoBehaviour
    {
        private static readonly Dictionary<int, DungeonOccluder> Registry = new();

        /// <summary>
        /// A renderer paired with the solid extent it is judged by. A wall mesh
        /// carries floor moulding that reaches well past the slab, and a prop mesh
        /// can carry a wide decorative base; deciding on either would lower
        /// geometry the player has not reached, so the decision uses the colliders
        /// and the fade is applied to the mesh.
        /// </summary>
        private readonly struct FadeCandidate
        {
            public readonly Renderer Renderer;
            public readonly Bounds Structure;

            /// <summary>The piece identity the resolver settles fades under.</summary>
            public readonly int PieceId;

            public FadeCandidate(Renderer renderer, Bounds structure)
            {
                Renderer = renderer;
                Structure = structure;
                PieceId = renderer.GetInstanceID();
            }
        }

        private FadeCandidate[] fadeCandidates;

        /// <summary>
        /// The visibility group this occluder is. Stable for its life and unique
        /// across occluders, so the decision layer can reason in plain ints.
        /// </summary>
        public int GroupId => GetInstanceID();

        /// <summary>The occluder owning a group id, or null once it is gone.</summary>
        public static DungeonOccluder ForGroup(int groupId)
        {
            if (!Registry.TryGetValue(groupId, out DungeonOccluder occluder))
                return null;
            if (occluder != null)
                return occluder;

            Registry.Remove(groupId);
            return null;
        }

        /// <summary>
        /// The occluder a physics hit belongs to, adopting geometry that has never
        /// been asked about before.
        ///
        /// This is what keeps the system open to props that did not exist when it
        /// was written: anything the camera can see and physics can hit becomes an
        /// occluder on the spot, and whether it actually lowers is then decided by
        /// how much of a character it covers rather than by what it is. Adopting a
        /// piece costs one component, once, for the life of the object.
        /// </summary>
        public static DungeonOccluder Owning(Component candidate)
        {
            if (candidate == null)
                return null;

            DungeonOccluder owner = candidate.GetComponentInParent<DungeonOccluder>();
            if (owner != null)
            {
                if (owner.IsExcluded(candidate.transform))
                    return null;
                owner.Register();
                return owner;
            }

            Transform root = RootOf(candidate.transform);
            DungeonOccluder adopted =
                root.GetComponent<DungeonOccluder>()
                ?? root.gameObject.AddComponent<DungeonOccluder>();
            adopted.Register();
            return adopted;
        }

        /// <summary>
        /// How much of the hierarchy around a hit counts as one object. The climb
        /// stops below the transform holding a room's contents, so a prop is the
        /// prefab it was instantiated from and never the whole room it stands in.
        /// </summary>
        private static Transform RootOf(Transform candidate)
        {
            Transform root = candidate;
            while (root.parent != null && root.parent.GetComponent<DungeonContentRoot>() == null)
                root = root.parent;
            return root;
        }

        /// <summary>
        /// Whether this occluder belongs to the room the player occupies. A prop
        /// answers from where it stands, which is all a freestanding object can
        /// know and all it needs to: architecture that spans two rooms overrides
        /// this with the ownership it was built with.
        /// </summary>
        public virtual bool BelongsToRoom(Vector2Int room, DungeonLayout layout)
        {
            Vector3 position = transform.position;
            return RoomsMatch(
                room,
                DungeonLayout.RoomAt(new Vector2(position.x, position.z)),
                layout
            );
        }

        /// <summary>
        /// The base and height a renderer's fade cutoff is measured against. What
        /// a piece measures is what it is judged by, so a prop of any size gets a
        /// correct cutoff without anyone recording its dimensions.
        /// </summary>
        public bool TryGetFadeReference(Renderer renderer, out float minimumY, out float height)
        {
            if (TryGetOverrideFadeReference(renderer, out minimumY, out height))
                return true;
            if (renderer == null)
            {
                minimumY = 0f;
                height = 0f;
                return false;
            }

            Bounds bounds = renderer.bounds;
            minimumY = bounds.min.y;
            height = bounds.size.y;
            return height > 0f;
        }

        /// <summary>
        /// The renderers of this occluder that should fade while it is lowered.
        /// The group decides when the transition happens; each piece still has to
        /// be standing in the way to take part.
        /// </summary>
        public void CollectFadeRenderers(
            WallVisibilityResolver resolver,
            Plane[] frustumPlanes,
            HashSet<Renderer> results,
            List<Renderer> rendererBuffer
        )
        {
            fadeCandidates ??= BuildFadeCandidates(rendererBuffer);
            foreach (FadeCandidate candidate in fadeCandidates)
            {
                if (
                    candidate.Renderer != null
                    && candidate.Renderer.enabled
                    && GeometryUtility.TestPlanesAABB(frustumPlanes, candidate.Renderer.bounds)
                    && resolver.IsPieceInTheWay(candidate.PieceId, candidate.Structure)
                )
                    results.Add(candidate.Renderer);
            }
        }

        /// <summary>A cutoff reference this occluder imposes instead of measuring
        /// the renderer, for pieces that have to line up with a neighbour.</summary>
        protected virtual bool TryGetOverrideFadeReference(
            Renderer renderer,
            out float minimumY,
            out float height
        )
        {
            minimumY = 0f;
            height = 0f;
            return false;
        }

        /// <summary>Geometry under this occluder that never fades with it.</summary>
        protected virtual bool IsExcluded(Transform candidate)
        {
            return false;
        }

        protected static bool RoomsMatch(Vector2Int first, Vector2Int second, DungeonLayout layout)
        {
            return layout != null ? layout.AreInSameRoom(first, second) : first == second;
        }

        /// <summary>Discards a cached piece list after the hierarchy changes.</summary>
        protected void InvalidateFadeCandidates()
        {
            fadeCandidates = null;
        }

        /// <summary>
        /// Pairs each renderer with the solid extent of the object it belongs to.
        /// Dungeon geometry does not move once built, so this is worked out once.
        /// </summary>
        private FadeCandidate[] BuildFadeCandidates(List<Renderer> rendererBuffer)
        {
            rendererBuffer.Clear();
            transform.GetComponentsInChildren(false, rendererBuffer);
            var candidates = new List<FadeCandidate>(rendererBuffer.Count);
            foreach (Renderer candidate in rendererBuffer)
            {
                if (candidate == null || IsExcluded(candidate.transform))
                    continue;
                candidates.Add(new FadeCandidate(candidate, StructureOf(candidate)));
            }
            return candidates.ToArray();
        }

        /// <summary>
        /// What is solid about the object a renderer belongs to, or the mesh itself
        /// when nothing about it is solid. Every collider shape counts, so a prop
        /// authored with a capsule or a mesh collider is measured like any other.
        /// </summary>
        private static Bounds StructureOf(Renderer renderer)
        {
            Transform prefabRoot =
                renderer.transform.parent != null ? renderer.transform.parent : renderer.transform;
            Bounds structure = default;
            bool any = false;
            foreach (Collider collider in prefabRoot.GetComponentsInChildren<Collider>())
            {
                if (collider == null || collider.isTrigger)
                    continue;
                if (!any)
                {
                    structure = collider.bounds;
                    any = true;
                }
                else
                    structure.Encapsulate(collider.bounds);
            }
            return any ? structure : renderer.bounds;
        }

        /// <summary>
        /// Publishes this occluder under its group id.
        ///
        /// Every occluder the resolver can name reaches it through
        /// <see cref="Owning"/>, so registering there is what actually keeps the
        /// registry complete. OnEnable alone would not: the Editor does not run it
        /// outside play mode, which would leave the whole registry empty for the
        /// tests and make them agree with a runtime they were not exercising.
        /// </summary>
        private void Register()
        {
            Registry[GroupId] = this;
        }

        private void OnEnable()
        {
            Register();
        }

        private void OnDisable()
        {
            Registry.Remove(GroupId);
        }
    }
}
