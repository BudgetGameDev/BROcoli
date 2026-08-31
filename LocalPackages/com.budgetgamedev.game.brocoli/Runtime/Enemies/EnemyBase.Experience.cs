using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public abstract partial class EnemyBase
    {
        /// <summary>Spawns an experience orb with a subtle hop from the death position.</summary>
        protected virtual void SpawnExpGain()
        {
            if (expGainPrefab == null)
                return;

            SpawnExpGain(
                UnityEngine.Random.insideUnitCircle,
                UnityEngine.Random.Range(0.2f, 0.5f),
                position => PoolManager.Instance?.GetExpGain(position)
            );
        }

        internal void SpawnExpGain(
            Vector2 direction,
            float landingDistance,
            System.Func<Vector3, ExpGain> getPooledExperience
        )
        {
            if (direction.sqrMagnitude < 0.001f)
                direction = Vector2.right;
            Vector2 landingOffset = direction.normalized * landingDistance;
            Vector2 spawnGround = transform.position.ToGround();
            Vector3 spawnPosition = spawnGround.ToWorld(0.5f);
            Vector3 landingPosition = (spawnGround + landingOffset).ToWorld(0.5f);

            ExpGain expGain = getPooledExperience?.Invoke(spawnPosition);
            if (expGain != null)
            {
                expGain.InitDropped(ScoreValue, landingPosition, ExpGain.DropStyle.Enemy);
                return;
            }

            GameObject expGainObject = Instantiate(
                expGainPrefab.gameObject,
                spawnPosition,
                Quaternion.identity
            );
            ExpGain expGainComponent = expGainObject.GetComponent<ExpGain>();
            if (expGainComponent != null)
                expGainComponent.InitDropped(ScoreValue, landingPosition, ExpGain.DropStyle.Enemy);
        }
    }
}
