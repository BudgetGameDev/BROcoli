using System.Text;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class BotDriver
    {
        private Vector2 lastRequestedDirection;
        private float nextRecoveryTrace;

        private void TraceRecoveryCandidates()
        {
            if (
                !AutoplayController.IsActive
                || movement == null
                || player == null
                || Time.time < nextRecoveryTrace
                || GetComponent<RunTelemetry>() == null
            )
                return;
            nextRecoveryTrace = Time.time + 30f;
            Vector2 position = player.position.ToGround();
            float step = Mathf.Max(
                0.01f,
                (stats != null ? stats.CurrentMovementSpeed : 4f) * Time.fixedDeltaTime
            );
            Vector2 heading =
                lastRequestedDirection.sqrMagnitude > 0f ? lastRequestedDirection : Vector2.up;
            var trace = new StringBuilder("[Autoplay navigation] recovery t=");
            trace
                .Append(Time.time.ToString("F3"))
                .Append(" position=")
                .Append(position.ToString("F4"))
                .Append(" intent=")
                .Append(IntentName)
                .Append(" input=")
                .Append(Move.ToString("F4"));
            var visual = player.GetComponentInChildren<ShuffleWalkVisual>();
            if (visual != null)
                trace.Append(" animatedInput=").Append(visual.MovementDirection.ToString("F4"));
            string previousStatus = StepStatus;
            foreach (float angle in AvoidanceAngles)
            {
                Vector2 candidate = Quaternion.Euler(0f, 0f, angle) * heading;
                bool accepted = TryPhysicalStep(position, candidate * step, out Vector2 delta);
                trace
                    .Append(" | angle=")
                    .Append(angle)
                    .Append(" desired=")
                    .Append((candidate * step).ToString("F4"))
                    .Append(" actual=")
                    .Append(delta.ToString("F4"))
                    .Append(" accepted=")
                    .Append(accepted)
                    .Append(" reason=")
                    .Append(StepStatus);
            }
            StepStatus = previousStatus;
            int reported = 0;
            foreach (
                Collider nearby in Physics.OverlapSphere(
                    player.position,
                    2f,
                    ~0,
                    QueryTriggerInteraction.Ignore
                )
            )
            {
                if (nearby.transform.IsChildOf(player) || reported >= 16)
                    continue;
                reported++;
                trace
                    .Append(" | body=")
                    .Append(nearby.name)
                    .Append(" layer=")
                    .Append(LayerMask.LayerToName(nearby.gameObject.layer))
                    .Append(" center=")
                    .Append(nearby.bounds.center.ToString("F4"))
                    .Append(" size=")
                    .Append(nearby.bounds.size.ToString("F4"));
            }
            Debug.Log(trace.ToString());
        }
    }
}
