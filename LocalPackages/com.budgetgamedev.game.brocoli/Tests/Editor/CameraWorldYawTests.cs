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

        /// <summary>
        /// The hop is a presentation cheat that displaces the model up the screen.
        /// It must follow the same yaw the rig and the input mapping do, or hopping
        /// while walking up the screen reads as a lurch along the walk direction.
        /// </summary>
        [Test]
        public void ScreenUpGroundMatchesTheYawedInputMapping()
        {
            Vector2 screenUp = CameraController.ScreenUpGround;
            Vector2 walkingUp = Vector2.up.RotatedByYaw(CameraController.WorldYawDegrees);

            Assert.That(screenUp.x, Is.EqualTo(walkingUp.x).Within(0.0001f));
            Assert.That(screenUp.y, Is.EqualTo(walkingUp.y).Within(0.0001f));
            Assert.That(screenUp.magnitude, Is.EqualTo(1f).Within(0.0001f));

            // Ground north is 45 degrees off screen-up, which is exactly the
            // regression: it leaves a component along the walk direction.
            Assert.That(Vector2.Dot(screenUp, Vector2.up), Is.LessThan(0.999f));
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
