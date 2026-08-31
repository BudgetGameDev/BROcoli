using System.Collections;
using System.Collections.Generic;
using BudgetGameDev.Shared;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class GamePreloader
    {
        private IEnumerator PrewarmPrefabs(float startProgress, float endProgress)
        {
            Vector3 warmupPos = new Vector3(-10000f, -10000f, 0f);
            List<GameObject> prefabsToWarm = CollectPoolPrefabs();

            // Instantiate each prefab offscreen
            for (int i = 0; i < prefabsToWarm.Count; i++)
            {
                float progress = Mathf.Lerp(
                    startProgress,
                    endProgress,
                    (float)i / prefabsToWarm.Count
                );
                _loadingScreen.SetProgress(progress);

                GameObject instance = Instantiate(prefabsToWarm[i], warmupPos, Quaternion.identity);
                instance.name = $"[WARMUP] {prefabsToWarm[i].name}";
                DisableWarmupBehaviors(instance);
                _warmupInstances.Add(instance);

                yield return WaitFramesRealtime(framesPerStep);
            }

            // Cleanup
            yield return null;
            foreach (GameObject go in _warmupInstances)
                if (go != null)
                    Destroy(go);
            _warmupInstances.Clear();
        }

        private void DisableWarmupBehaviors(GameObject go)
        {
            EnemyBase enemy = go.GetComponent<EnemyBase>();
            if (enemy != null)
                enemy.enabled = false;

            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb != null)
                rb.SetSimulated(false);

            foreach (Collider col in go.GetComponents<Collider>())
                col.enabled = false;

            ExpGain exp = go.GetComponent<ExpGain>();
            if (exp != null)
                exp.enabled = false;
        }

        private IEnumerator WaitFramesRealtime(int count)
        {
            // Use WaitForEndOfFrame since timeScale=0 would freeze WaitForSeconds
            for (int i = 0; i < count; i++)
                yield return new WaitForEndOfFrame();
        }

        public static void ResetPreloadFlag() => _hasPreloaded = false;
    }
}
