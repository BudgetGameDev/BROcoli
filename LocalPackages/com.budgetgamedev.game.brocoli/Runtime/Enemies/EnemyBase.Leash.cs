using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public abstract partial class EnemyBase
    {
        /// <summary>
        /// How far from where it woke an enemy will follow before giving up. A room is
        /// 28 by 20, so about one room: a group chases the player through the doorway
        /// and into the next room, and stops there.
        ///
        /// A wider leash looks harmless per room and is not: every room whose spawn
        /// lies inside the radius converges on the same spot, so at forty a run
        /// measured sixty-six enemies stacked on a player standing near the middle of
        /// half a dozen of them, wedged in place by their bodies.
        /// </summary>
        private const float LeashRadius = 26f;

        /// <summary>How near the player has to come before a returning enemy turns round.</summary>
        private const float ReAggroRadius = 14f;

        /// <summary>How near home counts as home.</summary>
        private const float LeashArrival = 2.5f;

        private Vector2 leashHome;
        private bool hasLeashHome;
        private bool pursuing = true;

        /// <summary>
        /// Whether this enemy is still chasing the player rather than walking back to
        /// the room it was spawned in. Attacks and shots are held while it is not.
        /// </summary>
        public bool IsPursuing => pursuing;

        /// <summary>
        /// Anchors an enemy to where it was placed. Without one, nothing in the game
        /// ever ended a chase: every enemy the run had woken followed the player for
        /// the rest of the session, so a balance run measured eighty-five of them
        /// alive at once and the tenth room was fought with the first nine still in
        /// tow. A room is meant to be a fight that can be won and left behind.
        /// </summary>
        public void SetLeashHome(Vector2 home)
        {
            leashHome = home;
            hasLeashHome = true;
            pursuing = true;
        }

        /// <summary>Where this enemy is heading: the player, or back where it started.</summary>
        protected Vector2 ChaseTarget
        {
            get
            {
                Vector2 playerGround = player != null ? player.position.ToGround() : leashHome;
                if (!hasLeashHome)
                    return playerGround;

                Vector2 here = rb != null ? rb.GroundPosition() : transform.position.ToGround();
                float fromHome = Vector2.Distance(here, leashHome);
                if (pursuing)
                    pursuing = fromHome <= LeashRadius;
                else if (Vector2.Distance(here, playerGround) <= ReAggroRadius)
                    pursuing = true;

                return pursuing ? playerGround : leashHome;
            }
        }

        /// <summary>Whether a returning enemy has got back to where it belongs.</summary>
        protected bool HasReachedLeashHome =>
            hasLeashHome
            && Vector2.Distance(
                rb != null ? rb.GroundPosition() : transform.position.ToGround(),
                leashHome
            ) <= LeashArrival;

        /// <summary>Forgets the anchor, so a pooled instance does not inherit the last one's.</summary>
        private void ResetLeash()
        {
            hasLeashHome = false;
            pursuing = true;
        }
    }
}
