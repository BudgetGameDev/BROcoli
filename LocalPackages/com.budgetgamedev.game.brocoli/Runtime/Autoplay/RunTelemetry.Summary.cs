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
            bool passed = EvaluateScenario(reason, missing);
            WriteSummary(reason, passed, missing);

            if (_logBuffer.Length > 0)
                File.WriteAllText(Path.Combine(_cfg.OutDir, "logs.txt"), _logBuffer.ToString());

            Debug.Log(
                $"[Autoplay] Run ended ({reason}). scenario={_cfg.Scenario} passed={passed}. "
                    + $"Unused features: {(missing.Count == 0 ? "none" : string.Join(", ", missing))}. "
                    + $"Out: {_cfg.OutDir}"
            );
            Quit(passed ? 0 : 1);
        }

        private bool EvaluateScenario(string reason, List<string> missing)
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
                case "smoke":
                default:
                    return true; // ran without exceptions
            }
        }

        internal static bool LogsAreClean(int warnings, int errors, int exceptions)
        {
            return warnings == 0 && errors == 0 && exceptions == 0;
        }

        private void WriteSummary(string reason, bool passed, List<string> missing)
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
            sb.Append("\"features\":").Append(AutoplayFeatureLog.ToJson()).Append(',');
            AppendMissing(sb, missing);
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

        private static void AppendMissing(StringBuilder sb, List<string> missing)
        {
            sb.Append("\"missingFeatures\":[");
            for (int index = 0; index < missing.Count; index++)
            {
                if (index > 0)
                    sb.Append(',');
                sb.Append('"').Append(missing[index]).Append('"');
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
