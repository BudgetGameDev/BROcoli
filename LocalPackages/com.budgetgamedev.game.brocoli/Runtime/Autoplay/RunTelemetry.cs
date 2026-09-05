using System;
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
    /// deterministic mode equals the fixed capture step -- so runs are reproducible.
    /// </summary>
    public partial class RunTelemetry : MonoBehaviour
    {
        private AutoplayConfig _cfg;
        private string _jsonlPath;

        private PlayerStats _stats;
        private PlayerDamageHandler _damage;
        private DungeonManager _dungeon;

        /// <summary>
        /// Game-seconds of no level, no experience, and no new room before a run is
        /// called stalled. An agent pinned on something it cannot reach survives and
        /// reaches its features, so nothing else here would ever fail it.
        /// </summary>
        private const float StallSeconds = 120f;

        private float _elapsed; // game-seconds since start
        private float _sampleAcc;
        private float _startedRealtime;
        private float _lastProgressTime;
        private float _progressLevel;
        private float _progressExperience;
        private int _progressRooms;
        private bool _ended;
        private bool _awaitingRestart;

        /// <summary>The one thing this does to the world, named so a test can watch it.</summary>
        internal Action<GameOverOverlay> PressRestart = overlay => overlay.RestartGame();

        private readonly RunProgression _progression = new();

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
            _startedRealtime = Time.realtimeSinceStartup;
            ResolvePlayer();
        }

        private void Update()
        {
            if (_ended)
                return;

            float dt = AutoplayTimeControl.GameDelta;
            _elapsed += dt;
            _sampleAcc += dt;

            if (_stats == null)
                ResolvePlayer();

            if (_sampleAcc >= _cfg.Interval)
            {
                _sampleAcc = 0f;
                WriteSample();
            }

            if (_awaitingRestart && TryRestart())
                return;

            TrackProgress();
            if (_elapsed - _lastProgressTime >= StallSeconds)
                EndRun("stalled");
            else if (_elapsed >= _cfg.Duration)
                EndRun("duration");
            else if (JourneyIsOver)
                EndRun("journey");
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
            Vector2 pos = _stats != null ? _stats.transform.position.ToGround() : Vector2.zero;
            float nearest = NearestEnemyDistance(pos, out int enemies);
            if (enemies > _maxEnemies)
                _maxEnemies = enemies;
            float fps = Time.unscaledDeltaTime > 0f ? 1f / Time.unscaledDeltaTime : 0f;

            float health = _stats != null ? _stats.CurrentHealth : 0f;
            float maxHealth = _stats != null ? _stats.CurrentMaxHealth : 0f;
            float level = _stats != null ? _stats.CurrentLevel : 0f;
            _progression.Sample(
                _elapsed,
                level,
                health,
                maxHealth,
                AutoplayFeatureLog.Count(AutoplayFeatures.EnemyKilled),
                PlayerRing
            );

            var sb = new StringBuilder(220);
            sb.Append('{');
            Num(sb, "t", _elapsed);
            sb.Append(',');
            Num(sb, "x", pos.x);
            sb.Append(',');
            Num(sb, "z", pos.y);
            sb.Append(',');
            Num(sb, "hp", health);
            sb.Append(',');
            Num(sb, "maxHp", maxHealth);
            sb.Append(',');
            Num(sb, "level", level);
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
            sb.Append("\"ring\":").Append(PlayerRing);
            sb.Append(',');
            sb.Append("\"botReplans\":").Append(BotDriver.ReplanCount);
            sb.Append(',');
            sb.Append("\"stuckRecoveries\":").Append(BotDriver.StuckRecoveryCount);
            sb.Append("}\n");

            File.AppendAllText(_jsonlPath, sb.ToString());
        }

        /// <summary>
        /// How far out of the dungeon the player currently is. Read from where they
        /// are standing rather than from what the dungeon has streamed in, because
        /// rooms are built ahead of arrival and depth is about where the run got to.
        /// </summary>
        private int PlayerRing =>
            _dungeon != null && _dungeon.HasCurrentRoom
                ? DungeonLayout.Ring(_dungeon.CurrentRoom)
                : 0;

        /// <summary>Remembers the last moment the run actually got somewhere.</summary>
        private void TrackProgress()
        {
            float level = _stats != null ? _stats.CurrentLevel : 0f;
            float experience = _stats != null ? _stats.CurrentExperience : 0f;
            int rooms = _dungeon != null ? _dungeon.RoomsVisited : 0;
            if (
                Mathf.Approximately(level, _progressLevel)
                && Mathf.Approximately(experience, _progressExperience)
                && rooms == _progressRooms
            )
                return;

            _progressLevel = level;
            _progressExperience = experience;
            _progressRooms = rooms;
            _lastProgressTime = _elapsed;
        }

        /// <summary>
        /// Whether a journey run has been everywhere it set out to go. It is graded
        /// on its steps rather than on its length, so the minutes after the last one
        /// would be nothing but the bot playing on -- and the run's own subject, a
        /// death, has already happened by then.
        /// </summary>
        private bool JourneyIsOver =>
            _cfg.Scenario == AutoplayFeatures.JourneyScenario
            && AutoplayFeatureLog.Missing(AutoplayFeatures.SaveJourney).Count == 0;

        /// <summary>
        /// Scenarios that read a whole session rather than one life. A coverage sweep
        /// that stopped at the first death would only ever test whatever that life
        /// stumbled into, and a difficulty verdict drawn from a single life is a
        /// verdict on that life's luck.
        /// </summary>
        private bool PlaysAnotherLife =>
            _cfg.Scenario == "coverage"
            || _cfg.Scenario == "balance"
            || _cfg.Scenario == AutoplayFeatures.JourneyScenario;
    }
}
