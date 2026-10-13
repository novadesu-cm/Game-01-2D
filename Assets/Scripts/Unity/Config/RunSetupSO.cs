using System;
using System.Collections.Generic;
using UnityEngine;

namespace GotA.Client
{
    [Serializable]
    public sealed class EnemySpawnSetup
    {
        public EnemyConfigSO enemy;
        public Vector2 position;
    }

    [Serializable]
    public sealed class WaveSetup
    {
        [Min(0)] public int potReward;
        public List<EnemySpawnSetup> enemies = new List<EnemySpawnSetup>();
    }

    /// <summary>
    /// Everything about a run except the player's build: arena/physics numbers, Gladiator stats,
    /// waves (spec sections 1, 2, 6). The player's Weapon + Trait are passed to
    /// DtoConverter.BuildRunConfig separately.
    /// </summary>
    [CreateAssetMenu(fileName = "RunSetup", menuName = "GotA/Run Setup")]
    public sealed class RunSetupSO : ScriptableObject
    {
        [Header("Arena / physics")]
        [Min(0.1f)] public float arenaRadius = 6.0f;
        [Min(0.001f)] public float fixedDt = 0.02f;
        [Min(0f)] public float drag = 4.0f;

        [Header("Run flow")]
        [Min(1f)] public float waveTimeLimitSeconds = 60f;
        [Range(0f, 1f)] public float timeoutPotMultiplier = 0.5f;

        [Header("Fighter")]
        public UnitStatsData fighterStats = new UnitStatsData();
        public Vector2 fighterSpawn = new Vector2(-3.0f, 0f);

        [Header("Waves (play order)")]
        public List<WaveSetup> waves = new List<WaveSetup>();

        [Header("Spawn jitter (D-03)")]
        [Min(0f)] public float spawnJitter = 0.3f;
    }
}
