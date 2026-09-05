using System.Text;
using BudgetGameDev.Autoplay;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class RunTelemetry
    {
        private void AppendReadiness(StringBuilder sb)
        {
            SimulationReadinessGate gate = GetComponent<AutoplayReadiness>()?.Gate;
            sb.Append("\"readiness\":{");
            Bool(sb, "enabled", gate != null);
            sb.Append(',');
            Bool(sb, "waiting", gate?.Waiting ?? false);
            sb.Append(',');
            Bool(sb, "timedOut", gate?.TimedOut ?? false);
            sb.Append(",\"waitCount\":").Append(gate?.WaitCount ?? 0).Append(',');
            Num(sb, "realSeconds", (float)(gate?.TotalSeconds ?? 0));
            sb.Append(',');
            Num(sb, "maxWaitSeconds", (float)(gate?.MaximumWaitSeconds ?? 0));
            sb.Append(',');
            Num(sb, "timeoutSeconds", (float)(gate?.TimeoutSeconds ?? 0));
            sb.Append('}');
        }
    }
}
