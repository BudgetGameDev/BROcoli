using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class DungeonOccluder
    {
        /// <summary>
        /// Freestanding geometry below this height stays fully visible. Both chest
        /// variants fit beneath it; walls and explicitly grouped architecture do not
        /// use this automatic-adoption threshold.
        /// </summary>
        public const float MinimumAutomaticFadeHeight = 1.5f;

        /// <summary>
        /// The occluder a physics hit belongs to, adopting previously unknown tall
        /// geometry while leaving low-profile props such as chests untouched.
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
            if (!IsTallEnoughToFade(root))
                return null;

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

        private static bool IsTallEnoughToFade(Transform root)
        {
            DungeonPropMeasurement measurement = DungeonPropMeasurement.Of(root.gameObject);
            float worldHeight = measurement.Height * Mathf.Abs(root.lossyScale.y);
            return worldHeight >= MinimumAutomaticFadeHeight;
        }
    }
}
