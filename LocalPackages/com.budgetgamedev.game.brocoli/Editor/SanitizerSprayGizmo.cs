using UnityEditor;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Editor
{
    internal static class SanitizerSprayGizmo
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        private static void Draw(SanitizerSpray spray, GizmoType _)
        {
            Transform parent = spray.transform.parent;
            Vector3 origin = parent != null ? parent.position : spray.transform.position;
            float range = Application.isPlaying ? spray.SprayRange : SpraySettings.BaseSprayRange;
            float width = Application.isPlaying ? spray.SprayWidth : SpraySettings.BaseSprayAngle;

            Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.3f);
            Gizmos.DrawWireSphere(origin, range);

            Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.5f);
            Vector3 direction = spray.transform.right;
            Vector3 left = GroundPlane.YawRotation(width * 0.5f) * direction;
            Vector3 right = GroundPlane.YawRotation(-width * 0.5f) * direction;
            Gizmos.DrawLine(origin, origin + left * range);
            Gizmos.DrawLine(origin, origin + right * range);
        }
    }
}
