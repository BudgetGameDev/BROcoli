using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// The gameplay camera reduced to the two matrices an occlusion decision
    /// needs. A real <see cref="Camera"/> and a camera described by nothing but
    /// numbers produce the same model, so the wall-visibility rules can be
    /// evaluated in a test without a scene, a render, or a frame of play.
    /// </summary>
    public readonly struct OcclusionCameraModel
    {
        private readonly Matrix4x4 inverseViewProjection;

        /// <summary>The view matrix: world space into camera space, -Z forward.</summary>
        public readonly Matrix4x4 WorldToCamera;

        /// <summary>The OpenGL-convention projection matrix, clip Z in -1..1.</summary>
        public readonly Matrix4x4 Projection;

        public readonly Vector3 Position;
        public readonly Vector3 Forward;
        public readonly float NearClip;

        public OcclusionCameraModel(Matrix4x4 worldToCamera, Matrix4x4 projection, float nearClip)
        {
            WorldToCamera = worldToCamera;
            Projection = projection;
            NearClip = nearClip;
            Matrix4x4 cameraToWorld = worldToCamera.inverse;
            Position = cameraToWorld.MultiplyPoint3x4(Vector3.zero);
            Forward = cameraToWorld.MultiplyVector(new Vector3(0f, 0f, -1f)).normalized;
            inverseViewProjection = (projection * worldToCamera).inverse;
        }

        public static OcclusionCameraModel FromCamera(Camera camera)
        {
            return new OcclusionCameraModel(
                camera.worldToCameraMatrix,
                camera.projectionMatrix,
                camera.nearClipPlane
            );
        }

        /// <summary>A perspective camera built from the values an inspector shows.</summary>
        public static OcclusionCameraModel Perspective(
            Vector3 position,
            Quaternion rotation,
            float verticalFieldOfView,
            float aspect,
            float nearClip,
            float farClip
        )
        {
            Matrix4x4 worldToCamera =
                Matrix4x4.Scale(new Vector3(1f, 1f, -1f))
                * Matrix4x4.TRS(position, rotation, Vector3.one).inverse;
            return new OcclusionCameraModel(
                worldToCamera,
                Matrix4x4.Perspective(verticalFieldOfView, aspect, nearClip, farClip),
                nearClip
            );
        }

        /// <summary>
        /// Viewport coordinates, matching <see cref="Camera.WorldToViewportPoint"/>:
        /// X and Y in 0..1 across the frame, Z the distance along the view axis.
        /// </summary>
        public Vector3 WorldToViewportPoint(Vector3 world)
        {
            Vector3 view = WorldToCamera.MultiplyPoint3x4(world);
            Vector4 clip = Projection * new Vector4(view.x, view.y, view.z, 1f);
            float w = Mathf.Approximately(clip.w, 0f) ? 1e-7f : clip.w;
            return new Vector3(clip.x / w * 0.5f + 0.5f, clip.y / w * 0.5f + 0.5f, -view.z);
        }

        /// <summary>The ray leaving the near plane through a viewport point.</summary>
        public Ray ViewportPointToRay(Vector2 viewport)
        {
            Vector3 near = Unproject(viewport, -1f);
            Vector3 far = Unproject(viewport, 1f);
            return new Ray(near, (far - near).normalized);
        }

        public void CalculateFrustumPlanes(Plane[] planes)
        {
            GeometryUtility.CalculateFrustumPlanes(Projection * WorldToCamera, planes);
        }

        private Vector3 Unproject(Vector2 viewport, float clipZ)
        {
            Vector4 point =
                inverseViewProjection
                * new Vector4(viewport.x * 2f - 1f, viewport.y * 2f - 1f, clipZ, 1f);
            float w = Mathf.Approximately(point.w, 0f) ? 1e-7f : point.w;
            return new Vector3(point.x / w, point.y / w, point.z / w);
        }
    }
}
