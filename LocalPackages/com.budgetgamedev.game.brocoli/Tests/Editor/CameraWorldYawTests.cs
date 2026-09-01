using BudgetGameDev.Games.Brocoli;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    /// <summary>
    /// The diagonal view is one constant applied in two places: the camera rig
    /// turns by it and screen input turns by it. These pin the input half, so
    /// pressing up always walks toward the top of the screen.
    /// </summary>
    public sealed class CameraWorldYawTests
    {
        [Test]
        public void ScreenUpBecomesTheCamerasGroundForward()
        {
            Vector2 world = Vector2.up.RotatedByYaw(CameraController.WorldYawDegrees);

            // The camera is yawed 45 degrees, looking north-east; walking "up"
            // must head north-east too.
            Vector2 cameraGroundForward = (
                Quaternion.Euler(0f, CameraController.WorldYawDegrees, 0f) * Vector3.forward
            ).ToGround();
            Assert.That(world.x, Is.EqualTo(cameraGroundForward.x).Within(0.0001f));
            Assert.That(world.y, Is.EqualTo(cameraGroundForward.y).Within(0.0001f));
        }

        [Test]
        public void YawRotationPreservesMagnitudeAndZeroYawIsIdentity()
        {
            var input = new Vector2(0.6f, -0.4f);
            Assert.That(
                input.RotatedByYaw(CameraController.WorldYawDegrees).magnitude,
                Is.EqualTo(input.magnitude).Within(0.0001f)
            );

            Vector2 unrotated = input.RotatedByYaw(0f);
            Assert.That(unrotated.x, Is.EqualTo(input.x).Within(0.0001f));
            Assert.That(unrotated.y, Is.EqualTo(input.y).Within(0.0001f));
        }
    }
}
