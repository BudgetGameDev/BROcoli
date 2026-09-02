using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class RunTelemetry
    {
        private void EndRun(string reason)
        {
            if (_ended)
                return;
            _ended = true;
            if (_damage != null)
                _damage.OnGameOver -= OnGameOver;

            WriteSample();
            List<string> missing = AutoplayFeatureLog.Missing();
            List<string> findings = ProgressionBalance.Evaluate(Progression, Scaling);
            bool passed = EvaluateScenario(reason, missing, findings);
            WriteSummary(reason, passed, missing, findings);

            if (_logBuffer.Length > 0)
                File.WriteAllText(Path.Combine(_cfg.OutDir, "logs.txt"), _logBuffer.ToString());

            Debug.Log(
                $"[Autoplay] Run ended ({reason}). scenario={_cfg.Scenario} passed={passed}. "
                    + $"Unused features: {(missing.Count == 0 ? "none" : string.Join(", ", missing))}. "
                    + $"Balance: {(findings.Count == 0 ? "in band" : string.Join("; ", findings))}. "
                    + $"{DescribeCaptures()}Out: {_cfg.OutDir}"
            );
            Quit(passed ? 0 : 1);
        }

        /// <summary>
        /// Names the triggers that never fired, because a run whose whole point was
        /// one screenshot should say so where the log is all a reader has.
        /// </summary>
        private static string DescribeCaptures()
        {
            if (!AutoplayCaptureTriggers.Any)
                return "";
            List<string> unfired = AutoplayCaptureTriggers.Unfired();
            return unfired.Count == 0
                ? "Every requested capture fired. "
                : $"Captures that never fired: {string.Join(", ", unfired)}. ";
        }

        private bool EvaluateScenario(string reason, List<string> missing, List<string> findings)
        {
            if (!LogsAreClean(_warnings, _errors, _exceptions))
                return false;
            if (reason == "stalled")
                return false;
            float level = _stats != null ? _stats.CurrentLevel : 0f;
            switch (_cfg.Scenario)
            {
                case "survive":
                    return reason == "duration"; // lived the whole run
                case "progress":
                    return level >= _cfg.MinLevel; // leveled up enough
                case "coverage":
                    // Surviving is not the point here: reaching every system is.
                    return missing.Count == 0;
                case "balance":
                    // Nor is surviving the point here: staying in the difficulty band is.
                    return findings.Count == 0;
                case "smoke":
                default:
                    return true; // ran without exceptions
            }
        }

        internal static bool LogsAreClean(int warnings, int errors, int exceptions)
        {
            return warnings == 0 && errors == 0 && exceptions == 0;
        }

        private void WriteSummary(
            string reason,
            bool passed,
            List<string> missing,
            List<string> findings
        )
        {
            float real = Time.realtimeSinceStartup - _startedRealtime;
            var sb = new StringBuilder();
            sb.Append('{');
            Bool(sb, "passed", passed);
            sb.Append(',');
            Str(sb, "tier", _cfg.Tier);
            sb.Append(',');
            Str(sb, "scenario", _cfg.Scenario);
            sb.Append(',');
            Str(sb, "reason", reason);
            sb.Append(',');
            sb.Append("\"seed\":").Append(_cfg.Seed).Append(',');
            Bool(sb, "deterministic", _cfg.Deterministic);
            sb.Append(',');
            Str(sb, "sha", _cfg.Sha);
            sb.Append(',');
            Num(sb, "durationSeconds", _elapsed);
            sb.Append(',');
            Num(sb, "realSeconds", real);
            sb.Append(',');
            Num(sb, "speedup", AutoplayTimeControl.Speedup(_elapsed, real));
            sb.Append(',');
            AppendRunCounters(sb);
            sb.Append(',');
            AppendProgression(sb, findings);
            sb.Append(',');
            sb.Append("\"features\":").Append(AutoplayFeatureLog.ToJson()).Append(',');
            AppendStrings(sb, "missingFeatures", missing);
            sb.Append(',');
            sb.Append("\"captures\":").Append(AutoplayCaptureTriggers.ToJson()).Append(',');
            AppendStrings(sb, "missingCaptures", AutoplayCaptureTriggers.Unfired());
            sb.Append(',');
            Str(sb, "firstError", _firstError);
            sb.Append('}');
            File.WriteAllText(Path.Combine(_cfg.OutDir, "summary.json"), sb.ToString());
        }

        private void AppendRunCounters(StringBuilder sb)
        {
            Num(sb, "finalLevel", _stats != null ? _stats.CurrentLevel : 0f);
            sb.Append(',');
            Num(sb, "finalHp", _stats != null ? _stats.CurrentHealth : 0f);
            sb.Append(',');
            sb.Append("\"roomsVisited\":")
                .Append(_dungeon != null ? _dungeon.RoomsVisited : 0)
                .Append(',');
            Num(sb, "distanceTravelled", BotDriver.DistanceTravelled);
            sb.Append(',');
            sb.Append("\"botReplans\":").Append(BotDriver.ReplanCount).Append(',');
            sb.Append("\"stuckRecoveries\":").Append(BotDriver.StuckRecoveryCount).Append(',');
            sb.Append("\"maxEnemies\":").Append(_maxEnemies).Append(',');
            sb.Append("\"warnings\":").Append(_warnings).Append(',');
            sb.Append("\"errors\":").Append(_errors).Append(',');
            sb.Append("\"exceptions\":").Append(_exceptions);
        }

        private static void AppendStrings(StringBuilder sb, string key, List<string> values)
        {
            sb.Append('"').Append(key).Append("\":[");
            for (int index = 0; index < values.Count; index++)
            {
                if (index > 0)
                    sb.Append(',');
                sb.Append('"').Append(Escape(values[index])).Append('"');
            }
            sb.Append(']');
        }

        private static void Num(StringBuilder sb, string key, float value) =>
            sb.Append('"')
                .Append(key)
                .Append("\":")
                .Append(value.ToString("0.###", CultureInfo.InvariantCulture));

        private static void Bool(StringBuilder sb, string key, bool value) =>
            sb.Append('"').Append(key).Append("\":").Append(value ? "true" : "false");

        private static void Str(StringBuilder sb, string key, string value) =>
            sb.Append('"').Append(key).Append("\":\"").Append(Escape(value)).Append('"');

        private static string Escape(string s) =>
            string.IsNullOrEmpty(s)
                ? ""
                : s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace('\n', ' ')
                    .Replace('\r', ' ');

        private void Quit(int code)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(code);
#endif
        }
    }
}
