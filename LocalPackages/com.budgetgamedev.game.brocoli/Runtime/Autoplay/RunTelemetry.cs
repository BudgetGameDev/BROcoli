using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Autoplay helper: samples gameplay state to a JSONL file, counts log
    /// warnings/errors/exceptions, evaluates the run against a scenario, and ends the
    /// run (writes <c>summary.json</c> manifest, then quits with a pass/fail exit code)
    /// on player death or when the configured game-time duration elapses.
    ///
    /// Sampling is driven by accumulated <see cref="Time.unscaledDeltaTime"/>, which in
    /// deterministic mode equals the fixed capture step — so runs are reproducible.
    /// </summary>
    public class RunTelemetry : MonoBehaviour
    {
        private AutoplayConfig _cfg;
        private string _jsonlPath;

        private PlayerStats _stats;
        private PlayerDamageHandler _damage;
        private DungeonManager _dungeon;

        private float _elapsed; // game-seconds since start
        private float _sampleAcc;
        private bool _ended;

        private int _warnings,
            _errors,
            _exceptions;
        private int _maxEnemies;
        private string _firstError = "";
        private readonly StringBuilder _logBuffer = new StringBuilder();

        public void Configure(AutoplayConfig cfg) => _cfg = cfg;

        private void OnEnable() => Application.logMessageReceived += OnLog;

        private void OnDisable()
        {
            Application.logMessageReceived -= OnLog;
            if (_damage != null)
                _damage.OnGameOver -= OnGameOver;
        }

        private void Start()
        {
            if (_cfg == null)
            {
                enabled = false;
                return;
            }
            Directory.CreateDirectory(_cfg.OutDir);
            _jsonlPath = Path.Combine(_cfg.OutDir, "telemetry.jsonl");
            File.WriteAllText(_jsonlPath, string.Empty);
            _sampleAcc = _cfg.Interval; // emit first sample immediately
            ResolvePlayer();
        }

        private void Update()
        {
            if (_ended)
                return;

            float dt = Time.unscaledDeltaTime;
            _elapsed += dt;
            _sampleAcc += dt;

            if (_stats == null)
                ResolvePlayer();

            if (_sampleAcc >= _cfg.Interval)
            {
                _sampleAcc = 0f;
                WriteSample();
            }

            if (_elapsed >= _cfg.Duration)
                EndRun("duration");
        }

        private void ResolvePlayer()
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go == null)
                return;
            _stats = go.GetComponent<PlayerStats>();
            _damage = go.GetComponent<PlayerDamageHandler>();
            _dungeon = FindAnyObjectByType<DungeonManager>();
            if (_damage != null)
                _damage.OnGameOver += OnGameOver;
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            switch (type)
            {
                case LogType.Warning:
                    _warnings++;
                    break;
                case LogType.Error:
                case LogType.Assert:
                    _errors++;
                    break;
                case LogType.Exception:
                    _exceptions++;
                    if (_firstError.Length == 0)
                        _firstError = condition;
                    break;
                default:
                    return;
            }
            if (_logBuffer.Length < 8000)
                _logBuffer.Append(type).Append(": ").Append(condition).Append('\n');
        }

        private float NearestEnemyDistance(Vector2 pos, out int count)
        {
            count = 0;
            var hash = EnemySpatialHash.Instance;
            if (hash == null)
                return -1f;
            count = hash.EnemyCount;
            float best = -1f;
            foreach (var e in hash.GetNearbyEnemies(pos, 40f))
            {
                float d = Vector2.Distance(pos, e.transform.position.ToGround());
                if (best < 0f || d < best)
                    best = d;
            }
            return best;
        }

        private void WriteSample()
        {
            Vector3 pos = _stats != null ? _stats.transform.position : Vector3.zero;
            float nearest = NearestEnemyDistance(pos, out int enemies);
            if (enemies > _maxEnemies)
                _maxEnemies = enemies;
            float fps = Time.unscaledDeltaTime > 0f ? 1f / Time.unscaledDeltaTime : 0f;

            var sb = new StringBuilder(220);
            sb.Append('{');
            Num(sb, "t", _elapsed);
            sb.Append(',');
            Num(sb, "x", pos.x);
            sb.Append(',');
            Num(sb, "y", pos.y);
            sb.Append(',');
            Num(sb, "hp", _stats != null ? _stats.CurrentHealth : 0f);
            sb.Append(',');
            Num(sb, "maxHp", _stats != null ? _stats.CurrentMaxHealth : 0f);
            sb.Append(',');
            Num(sb, "level", _stats != null ? _stats.CurrentLevel : 0f);
            sb.Append(',');
            Num(sb, "xp", _stats != null ? _stats.CurrentExperience : 0f);
            sb.Append(',');
            sb.Append("\"enemies\":").Append(enemies).Append(',');
            Num(sb, "nearestEnemy", nearest);
            sb.Append(',');
            Num(sb, "fps", fps);
            sb.Append(',');
            Num(sb, "timeScale", Time.timeScale);
            sb.Append(',');
            Str(sb, "botIntent", BotDriver.IntentName);
            sb.Append(',');
            sb.Append("\"roomsVisited\":").Append(_dungeon != null ? _dungeon.RoomsVisited : 0);
            sb.Append(',');
            sb.Append("\"botReplans\":").Append(BotDriver.ReplanCount);
            sb.Append(',');
            sb.Append("\"stuckRecoveries\":").Append(BotDriver.StuckRecoveryCount);
            sb.Append("}\n");

            File.AppendAllText(_jsonlPath, sb.ToString());
        }

        private void OnGameOver() => EndRun("gameover");

        private void EndRun(string reason)
        {
            if (_ended)
                return;
            _ended = true;
            if (_damage != null)
                _damage.OnGameOver -= OnGameOver;

            WriteSample();
            bool passed = EvaluateScenario(reason);
            WriteSummary(reason, passed);

            if (_logBuffer.Length > 0)
                File.WriteAllText(Path.Combine(_cfg.OutDir, "logs.txt"), _logBuffer.ToString());

            Debug.Log(
                $"[Autoplay] Run ended ({reason}). scenario={_cfg.Scenario} passed={passed}. Out: {_cfg.OutDir}"
            );
            Quit(passed ? 0 : 1);
        }

        private bool EvaluateScenario(string reason)
        {
            if (!LogsAreClean(_warnings, _errors, _exceptions))
                return false;
            float level = _stats != null ? _stats.CurrentLevel : 0f;
            switch (_cfg.Scenario)
            {
                case "survive":
                    return reason == "duration"; // lived the whole run
                case "progress":
                    return level >= _cfg.MinLevel; // leveled up enough
                case "smoke":
                default:
                    return true; // ran without exceptions
            }
        }

        internal static bool LogsAreClean(int warnings, int errors, int exceptions)
        {
            return warnings == 0 && errors == 0 && exceptions == 0;
        }

        private void WriteSummary(string reason, bool passed)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            Bool(sb, "passed", passed);
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
            sb.Append("\"exceptions\":").Append(_exceptions).Append(',');
            Str(sb, "firstError", _firstError);
            sb.Append('}');
            File.WriteAllText(Path.Combine(_cfg.OutDir, "summary.json"), sb.ToString());
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
