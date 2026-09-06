using System;
using System.Collections;
using System.IO;
using BudgetGameDev.Shared;
using BudgetGameDev.Shared.Rendering;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>A temporary dungeon run. The save barrier is released only after the original scene restores.</summary>
    internal sealed class SystemReadinessSession : MonoBehaviour
    {
        private const string DungeonScene = "Brocoli_Dungeon_Common";
        private static SystemReadinessSession instance;
        private SystemReadinessPage page;
        private bool busy, cancelled;
        private string originScene, settings, result;
        private float originalTimeScale;
        private bool originDungeon;
        private BrocoliRunSave checkpoint;
        private UnityEngine.Random.State originalRandom;
        private Transform player;
        private NavMeshPath path;
        private Vector3[] corners = Array.Empty<Vector3>();
        private int corner, waypoint;
        private float nextPath;
        private Vector3 previousPosition;
        private double travelled;
        internal static bool IsOpen => instance != null && instance.page != null && instance.page.gameObject.activeSelf;
        internal static bool IsBenchmarkScene { get; private set; }
        internal static Vector2 Movement { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            IsBenchmarkScene = false;
            Movement = Vector2.zero;
            BrocoliSaveSystem.EndReadOnlyRun();
        }

        internal static void Open(TMP_FontAsset font)
        {
            if (instance == null)
            {
                var host = new GameObject("System readiness session");
                DontDestroyOnLoad(host);
                instance = host.AddComponent<SystemReadinessSession>();
                var view = new GameObject("System readiness page", typeof(RectTransform));
                view.transform.SetParent(host.transform, false);
                instance.page = view.AddComponent<SystemReadinessPage>();
                instance.page.Build(font, instance.Begin, () => instance.cancelled = true, instance.Close);
                view.SetActive(false);
            }
            instance.page.Show(instance.result ??
                "<b>QUICK GAMEPLAY CHECK</b>\n\n"
                + "Runs a temporary dungeon with your current graphics settings. Your current run is restored from a temporary in-memory checkpoint afterward. Benchmark progress is never saved.\n\n"
                + "The player follows a short route automatically: 5 seconds to warm up, then 20 seconds of measurement. Keep the game focused; ESC or Cancel stops the test.\n\n"
                + "Results cover frame pacing, CPU/GPU load, RAM/VRAM pressure, disk activity and space, and available temperatures. Each result includes a status and relevant next steps.\n\n"
                + "The baseline is 60 rendered FPS. Missing sensors are marked Not measured. A short sample can identify pressure; it cannot certify hardware health.\n\n"
                + CaptureSettings(), false);
        }

        private static string CaptureSettings()
        {
            var n = NvidiaRendering.Capture();
            int q = QualitySettings.GetQualityLevel();
            return $"Quality: {QualitySettings.names[q]} · {Screen.width} × {Screen.height}\n"
                + $"{GraphicsSettings.currentRenderPipeline?.GetType().Name ?? "Built-in"} · {SystemInfo.graphicsDeviceName}\n"
                + $"DLSS {(n.DlssRequested ? "On" : "Off")} · FG {n.GeneratedFrames + 1}× · Reflex {(n.Reflex == 2 ? "On + Boost" : n.Reflex == 1 ? "On" : "Off")}\n"
                + $"Display {Screen.currentResolution.refreshRateRatio.value:F0} Hz · VSync {QualitySettings.vSyncCount} · FPS cap {(Application.targetFrameRate < 0 ? "None" : Application.targetFrameRate.ToString())}";
        }

        private void Begin()
        {
            if (busy || BrocoliSaveSystem.ReadOnlyRun)
                return;
            originScene = SceneManager.GetActiveScene().name;
            originDungeon = originScene == DungeonScene;
            checkpoint = null;
            if (originDungeon && !BrocoliAutosaveController.TryCapture(out checkpoint))
            {
                page.Show("The current run cannot be captured safely. Return to the main menu to run this test. No scene or save was changed.", false);
                return;
            }
            originalTimeScale = Time.timeScale;
            originalRandom = UnityEngine.Random.state;
            settings = CaptureSettings();
            cancelled = false;
            busy = true;
            result = null;
            // Arm before unloading the current scene: its OnDestroy autosave must also be blocked.
            BrocoliSaveSystem.BeginReadOnlyRun();
            StartCoroutine(Execute());
        }

        private IEnumerator Execute()
        {
            yield return Guarded(Measure(), false);
            yield return Guarded(Restore(), true);
            busy = false;
            page.Show(result, false, !BrocoliSaveSystem.ReadOnlyRun);
            Debug.Log("[SystemReadiness] " + result);
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-readinessReport");
            if (index >= 0 && index + 1 < args.Length)
            {
                try { File.WriteAllText(args[index + 1], result); }
                catch (Exception e) { Debug.LogWarning("[SystemReadiness] Report export: " + e.Message); }
            }
        }

        private IEnumerator Guarded(IEnumerator work, bool restoring)
        {
            using (work as IDisposable)
            {
                while (true)
                {
                    bool next;
                    try { next = work.MoveNext(); }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                        result = restoring
                            ? "RESTORATION NEEDS ATTENTION\nYour original run is still held in memory. Saves remain disabled. Press BACK to retry restoring it.\n" + e.Message
                            : "BENCHMARK FAILED\nNo component assessment was made.\n" + e.Message;
                        yield break;
                    }
                    if (!next) yield break;
                    yield return work.Current;
                }
            }
        }

        private IEnumerator Measure()
        {
            IsBenchmarkScene = true;
            Movement = Vector2.zero;
            Time.timeScale = 1;
            GameAudioSettings.SetPauseMenuOpen(false);
            page.Show("Loading temporary dungeon…\nSaving is disabled.", true);
            yield return SceneManager.LoadSceneAsync(DungeonScene);
            yield return null;
            yield return null;
            player = PlayerStats.Resolve()?.transform.root;
            var dungeon = FindAnyObjectByType<DungeonManager>();
            if (player == null || dungeon == null || !dungeon.HasCurrentRoom)
                throw new InvalidOperationException("The benchmark dungeon did not initialize.");
            path = new NavMeshPath();
            corners = Array.Empty<Vector3>();
            corner = waypoint = 0;
            nextPath = 0;
            travelled = 0;
            previousPosition = player.position;
            using var measurements = new SystemReadinessMeasurements(SystemInfo.graphicsDeviceName, Application.dataPath);
            double began = Time.realtimeSinceStartupAsDouble;
            double previous = began, lastUi = -1;
            while (Time.realtimeSinceStartupAsDouble - began < 25)
            {
                yield return null;
                double now = Time.realtimeSinceStartupAsDouble;
                double elapsed = now - began;
                if (cancelled || !Application.isFocused || Time.timeScale != 1)
                {
                    result = "TEST CANCELLED\nNo component assessment was made. Your original run is restored. Keep the game focused and unpaused throughout the test, then try again.";
                    yield break;
                }
                if (elapsed >= 5 && previous - began >= 5)
                    measurements.AddFrame(now - previous);
                previous = now;
                Navigate();
                if (elapsed - lastUi >= .25)
                {
                    lastUi = elapsed;
                    if (CaptureSettings() != settings)
                    {
                        result = "SETTINGS CHANGED\nThe test was stopped to avoid mixing different settings. No assessment was made. Run again with the new settings.";
                        yield break;
                    }
                    page.Show(elapsed < 5 ? $"Warming up · {Math.Ceiling(5 - elapsed)} s\nSaving disabled · ESC to cancel"
                        : $"Measuring · {Math.Ceiling(25 - elapsed)} s left · {measurements.FrameCount} frames\nSaving disabled · ESC to cancel", true);
                }
            }
            result = travelled < 2
                ? "INCOMPLETE ROUTE\nThe player could not follow the benchmark route. No component assessment was made. Try the test again."
                : measurements.BuildReport(settings, travelled);
        }

        private void Navigate()
        {
            Vector3 position = player.position;
            travelled += Vector3.Distance(position, previousPosition);
            previousPosition = position;
            if (Time.unscaledTime >= nextPath)
            {
                nextPath = Time.unscaledTime + .4f;
                Vector3[] route = { new(5, 0, 0), new(5, 0, 5), new(-5, 0, 5), new(-5, 0, -5), new(0, 0, 0) };
                if (NavMesh.SamplePosition(route[waypoint], out var target, 4, NavMesh.AllAreas)
                    && NavMesh.SamplePosition(position, out var from, 3, NavMesh.AllAreas)
                    && NavMesh.CalculatePath(from.position, target.position, NavMesh.AllAreas, path)
                    && path.status == NavMeshPathStatus.PathComplete)
                {
                    if (Vector3.Distance(from.position, target.position) < 1)
                        waypoint = (waypoint + 1) % route.Length;
                    corners = path.corners;
                    corner = corners.Length > 1 ? 1 : 0;
                }
                else { waypoint = (waypoint + 1) % route.Length; corners = Array.Empty<Vector3>(); }
            }
            Movement = Vector2.zero;
            if (corner >= corners.Length) return;
            Vector2 delta = new(corners[corner].x - position.x, corners[corner].z - position.z);
            if (delta.magnitude < .5f) corner++;
            else Movement = delta.normalized;
        }

        private IEnumerator Restore()
        {
            page.Show("Restoring your original session…\nSaving remains disabled during restoration.", true);
            Movement = Vector2.zero;
            IsBenchmarkScene = false;
            Time.timeScale = 0;
            BrocoliSaveSystem.RestoreReadOnlyCheckpoint(checkpoint);
            SceneManager.sceneLoaded += FreezeRestoredScene;
            try { yield return SceneManager.LoadSceneAsync(originScene); }
            finally { SceneManager.sceneLoaded -= FreezeRestoredScene; }
            yield return null;
            yield return null;
            if (originDungeon)
            {
                if (PlayerStats.Resolve() == null)
                    throw new InvalidOperationException("Original player did not restore.");
                var pause = FindAnyObjectByType<PauseMenu>();
                if (pause == null) throw new InvalidOperationException("Original pause menu did not restore.");
                pause.Pause();
                pause.OpenSettings();
            }
            else
                FindAnyObjectByType<ResponsiveMainMenuLayout>()?.OpenSettings();
            Time.timeScale = originDungeon ? 0 : originalTimeScale;
            UnityEngine.Random.state = originalRandom;
            BrocoliSaveSystem.EndReadOnlyRun();
            checkpoint = null;
        }

        private void Close()
        {
            if (busy) return;
            if (BrocoliSaveSystem.ReadOnlyRun)
            {
                busy = true;
                StartCoroutine(RetryRestore());
                return;
            }
            page.gameObject.SetActive(false);
        }

        private void FreezeRestoredScene(Scene scene, LoadSceneMode mode) => Time.timeScale = 0;

        private IEnumerator RetryRestore()
        {
            yield return Guarded(Restore(), true);
            busy = false;
            page.Show(result, false, !BrocoliSaveSystem.ReadOnlyRun);
        }
    }
}
