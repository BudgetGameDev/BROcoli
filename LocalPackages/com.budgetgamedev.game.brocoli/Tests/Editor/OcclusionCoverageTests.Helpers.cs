using System.Collections.Generic;
using BudgetGameDev.Games.Brocoli;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class OcclusionCoverageTests
    {
        private static OcclusionCameraModel Camera(Vector3 playerPosition)
        {
            return OcclusionCameraModel.Perspective(
                playerPosition + CameraOffset,
                Quaternion.LookRotation(-CameraOffset.normalized, Vector3.up),
                35f,
                16f / 9f,
                0.3f,
                1000f
            );
        }

        /// <summary>
        /// A character to be kept readable, with no threshold of its own, so the
        /// measured fraction comes back rather than a yes or no.
        /// </summary>
        private static OcclusionTarget Target(
            in OcclusionCameraModel camera,
            Vector3 position,
            float characterHeight
        )
        {
            var bounds = new Bounds(
                position + Vector3.up * (characterHeight / 2f),
                new Vector3(PlayerWidth, characterHeight, PlayerWidth)
            );
            Assert.That(
                OcclusionTarget.TryCreate(
                    camera,
                    OcclusionTargetKind.Player,
                    position,
                    bounds,
                    0f,
                    out OcclusionTarget target
                ),
                "the character is not on screen"
            );
            return target;
        }
    }
}
