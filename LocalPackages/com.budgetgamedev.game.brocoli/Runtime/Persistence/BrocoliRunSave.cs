using System;
using System.Collections.Generic;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    [Serializable]
    internal sealed class BrocoliRunSave
    {
        public const int CurrentVersion = 2;

        public int version = CurrentVersion;

        /// <summary>Which of the player's save slots this run occupies.</summary>
        public int slot;

        /// <summary>UTC ticks of the last checkpoint, which orders the save list.</summary>
        public long savedAtTicks;
        public bool mobileControls;
        public Vector3 playerPosition;
        public BrocoliPlayerSave player = new();
        public BrocoliGameStateSave game = new();
        public BrocoliDungeonSave dungeon = new();
    }

    [Serializable]
    internal sealed class BrocoliPlayerSave
    {
        public float health;
        public float maxHealth;
        public float attackSpeed;
        public float damage;
        public float movementSpeed;
        public float experience;
        public float maxExperience;
        public float level;
        public float detectionRadius;
        public float sprayRange;
        public float sprayWidth;
        public float sprayDamageMultiplier;
        public float critChance;
        public float critDamage;
        public float dodgeChance;
        public float armor;
        public float healthRegen;
        public float lifeSteal;
        public bool levelUpChoicePending;
        public List<BrocoliTemporaryBoostSave> temporaryBoosts = new();
    }

    [Serializable]
    internal sealed class BrocoliTemporaryBoostSave
    {
        public TemporaryBoostType type;
        public float amount;
        public float remainingTime;
    }

    [Serializable]
    internal sealed class BrocoliGameStateSave
    {
        public int score;
        public float gameTime;
        public int enemiesKilled;
    }

    [Serializable]
    internal sealed class BrocoliDungeonSave
    {
        public int seed;
        public int roomsVisited;
        public List<BrocoliRoomSave> rooms = new();
    }

    [Serializable]
    internal sealed class BrocoliRoomSave
    {
        public int x;
        public int y;
        public bool visited;
        public List<int> openedChestSlots = new();
    }
}
