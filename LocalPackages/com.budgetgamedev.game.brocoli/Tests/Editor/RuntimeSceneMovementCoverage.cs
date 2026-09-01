using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static void ExercisePlayerMovement(PlayerStats stats)
        {
            PlayerMovement movement = stats.GetComponent<PlayerMovement>();
            Assert.That(movement, Is.Not.Null);
            Rigidbody body = movement.Body;
            Assert.That(body, Is.Not.Null);
            Collider movementCollider = stats.GetComponent<Collider>();
            movementCollider.enabled = false;
            InvokeHierarchy(movement, "ResolveNavigationCollisions", Vector2.right);
            movementCollider.enabled = true;
            InvokeHierarchy(
                movement,
                "TryGetBlockingHit",
                Vector3.zero,
                Vector3.zero,
                0.5f,
                Vector2.right,
                1f,
                0,
                default(RaycastHit),
                Vector2.zero
            );

            SetHierarchyField(movement, "_knockbackVelocity", Vector2.one);
            Assert.That(movement.IsKnockedBack, Is.True);
            Assert.That(movement.KnockbackMagnitude, Is.GreaterThan(0f));

            object hop = GetHierarchyField<object>(movement, "_hopVisual");
            object playerStats = GetHierarchyField<object>(movement, "_playerStats");
            SetHierarchyField(movement, "_hopVisual", null);
            SetHierarchyField(movement, "_playerStats", null);
            movement.ProcessMovement(Vector2.one * 2f);
            movement.ApplyKnockbackImpulse(Vector2.right);
            movement.ApplyKnockbackImpulse(Vector2.right, 100f);
            movement.StopMovement();

            SetHierarchyField(movement, "_body", null);
            movement.ProcessMovement(Vector2.one);
            movement.ApplyKnockbackImpulse(Vector2.zero, 1f);
            movement.SetPosition(movement.Position + Vector2.one);
            movement.StopMovement();

            SetHierarchyField(movement, "_body", body);
            SetHierarchyField(movement, "_hopVisual", hop);
            SetHierarchyField(movement, "_playerStats", playerStats);
            movement.SetPosition(movement.Position);
            ExercisePlayerController(stats, movement);
        }

        private static void ExercisePlayerController(PlayerStats stats, PlayerMovement movement)
        {
            PlayerController controller = stats.GetComponent<PlayerController>();
            Assert.That(controller.DebugBaseDamage, Is.GreaterThanOrEqualTo(0f));
            _ = controller.RawInput;
            _ = controller.movement;
            _ = controller.getGameOver();
            controller.TakeMeleeDamage(0f);
            controller.TakeMeleeDamage(0f, Vector2.right);
            controller.ApplyKnockback(Vector2.zero);
            controller.ExecMove();

            SetHierarchyField(controller, "_movement", null);
            InvokeHierarchy(controller, "MoveTo", Vector2.one);
            controller.ApplyKnockback(Vector2.right);
            SetHierarchyField(controller, "_damageHandler", null);
            controller.setGameOver();
            controller.TakeMeleeDamage(1f);
            controller.TakeMeleeDamage(1f, Vector2.left);
            SetHierarchyField(controller, "_movement", movement);
            SetHierarchyField(
                controller,
                "_damageHandler",
                stats.GetComponent<PlayerDamageHandler>()
            );
        }

        private static void ExerciseShuffleWalkVisual(PlayerStats stats)
        {
            ShuffleWalkVisual visual = stats.GetComponentInChildren<ShuffleWalkVisual>(true);
            PlayerController controller = stats.GetComponent<PlayerController>();
            PlayerInputHandler input = stats.GetComponent<PlayerInputHandler>();
            Assert.That(visual, Is.Not.Null);
            Assert.That(controller, Is.Not.Null);
            Assert.That(input, Is.Not.Null);
            Face2DMovementDirection facing = stats.GetComponentInChildren<Face2DMovementDirection>(
                true
            );
            if (facing != null)
            {
                SetHierarchyField(input, "_smoothedInput", Vector2.one * 2f);
                InvokeHierarchy(facing, "LateUpdate");
                SetHierarchyField(facing, "controller", null);
                InvokeHierarchy(facing, "LateUpdate");
                SetHierarchyField(facing, "controller", controller);
            }
            PitchFromInputRelativeToDownPose pitch =
                stats.GetComponentInChildren<PitchFromInputRelativeToDownPose>(true);
            if (pitch != null)
            {
                pitch.hopVisual = null;
                pitch.controller = controller;
                SetHierarchyField(input, "_smoothedInput", Vector2.one * 2f);
                InvokeHierarchy(pitch, "LateUpdate");
                pitch.controller = null;
                InvokeHierarchy(pitch, "LateUpdate");
            }

            visual.ApplyStumble(0.5f);
            InvokeHierarchy(visual, "GetScaledJumpTime");
            InvokeHierarchy(visual, "GetScaledMinJumpHeight");
            InvokeHierarchy(visual, "GetScaledMaxJumpHeight");
            SetHierarchyField(visual, "_playerStats", null);
            InvokeHierarchy(visual, "GetSpeedMultiplier");
            SetHierarchyField(visual, "controller", null);
            SetHierarchyField(visual, "_playerStats", null);
            InvokeHierarchy(visual, "GetSpeedMultiplier");
            InvokeHierarchy(visual, "Update");
            SetHierarchyField(visual, "controller", controller);
            SetHierarchyField(visual, "_playerStats", stats);

            Collider playerCollider = stats.GetComponent<Collider>();
            int wallLayer = LayerMask.NameToLayer("Wall");
            Assert.That(playerCollider, Is.Not.Null);
            Assert.That(wallLayer, Is.GreaterThanOrEqualTo(0));
            SetHierarchyField(visual, "playerCollider", playerCollider);
            SetHierarchyField(visual, "wallLayerMask", 1 << wallLayer);
            // The hop displaces along screen-up, so the wall that is supposed to
            // block it has to stand across that direction, not across ground north.
            Vector2 screenUpGround = CameraController.ScreenUpGround;
            Vector3 hopDirection = screenUpGround.ToWorld();
            playerCollider.enabled = false;
            InvokeHierarchy(visual, "ClampHopOffsetAgainstWalls", hopDirection, 0.25f);
            InvokeHierarchy(visual, "GetWallPoseFactor", screenUpGround);
            playerCollider.enabled = true;

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Coverage Hop Wall";
            wall.layer = wallLayer;
            Bounds playerBounds = playerCollider.bounds;
            wall.transform.position =
                playerBounds.center + hopDirection * (playerBounds.extents.z + 0.1f);
            wall.transform.rotation = Quaternion.LookRotation(hopDirection, Vector3.up);
            wall.transform.localScale = new Vector3(4f, 4f, 0.1f);
            Physics.SyncTransforms();
            InvokeHierarchy(visual, "ClampHopOffsetAgainstWalls", hopDirection, 0.5f);
            InvokeHierarchy(visual, "GetWallPoseFactor", screenUpGround);
            Object.Destroy(wall);

            DriveHopState(visual, input, ShuffleWalkVisual.HopState.Idle, 0f, Vector2.zero);
            DriveHopState(visual, input, ShuffleWalkVisual.HopState.Idle, 0f, Vector2.one * 2f);
            DriveHopState(visual, input, ShuffleWalkVisual.HopState.Charging, 0f, Vector2.one);
            DriveHopState(visual, input, ShuffleWalkVisual.HopState.Charging, 1f, Vector2.zero);

            SetHierarchyField(visual, "currentJumpTime", 1f);
            DriveHopState(visual, input, ShuffleWalkVisual.HopState.Airborne, 0f, Vector2.one);
            DriveHopState(visual, input, ShuffleWalkVisual.HopState.Airborne, 2f, Vector2.one);
            DriveHopState(visual, input, ShuffleWalkVisual.HopState.Airborne, 2f, Vector2.zero);

            SetHierarchyField(visual, "currentBounceTime", 1f);
            DriveHopState(visual, input, ShuffleWalkVisual.HopState.BhopBounce, 0f, Vector2.one);
            DriveHopState(visual, input, ShuffleWalkVisual.HopState.BhopBounce, 2f, Vector2.one);
            DriveHopState(visual, input, ShuffleWalkVisual.HopState.BhopBounce, 2f, Vector2.zero);

            DriveHopState(visual, input, ShuffleWalkVisual.HopState.Landing, 1f, Vector2.one);
            DriveHopState(visual, input, ShuffleWalkVisual.HopState.Landing, 1f, Vector2.zero);
            DriveHopState(visual, input, ShuffleWalkVisual.HopState.Stopping, 0.01f, Vector2.zero);
            DriveHopState(visual, input, ShuffleWalkVisual.HopState.Stopping, 0.16f, Vector2.zero);
            DriveHopState(visual, input, ShuffleWalkVisual.HopState.Stopping, 0.3f, Vector2.zero);
            DriveHopState(visual, input, ShuffleWalkVisual.HopState.Stopping, 1f, Vector2.one);
            DriveHopState(visual, input, ShuffleWalkVisual.HopState.Stopping, 1f, Vector2.zero);
            DriveHopState(visual, input, ShuffleWalkVisual.HopState.Stopping, 0f, Vector2.one);
            input.ResetInput();
        }

        private static void DriveHopState(
            ShuffleWalkVisual visual,
            PlayerInputHandler input,
            ShuffleWalkVisual.HopState state,
            float timer,
            Vector2 movement
        )
        {
            SetHierarchyField(input, "_smoothedInput", movement);
            SetHierarchyField(visual, "<State>k__BackingField", state);
            SetHierarchyField(visual, "stateTimer", timer);
            InvokeHierarchy(visual, "Update");
        }
    }
}
