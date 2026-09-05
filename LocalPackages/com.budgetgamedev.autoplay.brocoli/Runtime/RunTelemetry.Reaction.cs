using System.Globalization;
using System.Text;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class RunTelemetry
    {
        private void AppendReaction(StringBuilder sb)
        {
            sb.Append("\"reaction\":{");
            Str(sb, "profile", _cfg.ReactionProfile);
            sb.Append(',');
            sb.Append("\"observationIntervalSeconds\":")
                .Append(
                    _cfg.ObservationIntervalSeconds.ToString("R", CultureInfo.InvariantCulture)
                );
            sb.Append(',');
            sb.Append("\"reactionDelaySeconds\":")
                .Append(_cfg.ReactionDelaySeconds.ToString("R", CultureInfo.InvariantCulture));
            sb.Append(",\"observations\":").Append(BotDriver.ReactionObservationCount);
            sb.Append(",\"decisions\":").Append(BotDriver.ReactionDecisionCount);
            sb.Append('}');
        }
    }
}
