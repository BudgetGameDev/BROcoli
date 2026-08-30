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

            Vector2 direction = UnityEngine.Random.insideUnitCircle;
            if (direction.sqrMagnitude < 0.001f)
                direction = Vector2.right;
            Vector2 landingOffset = direction.normalized * UnityEngine.Random.Range(0.2f, 0.5f);
            Vector2 spawnGround = transform.position.ToGround();
            Vector3 spawnPosition = spawnGround.ToWorld(0.5f);
            Vector3 landingPosition = (spawnGround + landingOffset).ToWorld(0.5f);

            ExpGain expGain = PoolManager.Instance?.GetExpGain(spawnPosition);
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
